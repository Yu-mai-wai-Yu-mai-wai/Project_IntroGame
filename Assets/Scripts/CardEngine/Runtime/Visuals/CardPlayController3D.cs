using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    // Click-to-play for the 3D hand:
    //  1. Click a card in the hand: it moves to the right side of the screen and waits there, and the
    //     camera swings to a top-down view of the board.
    //  2. Familiar / amulet: click an empty slot (they are highlighted) to put it there.
    //     Incantation with a target: click the target card (CardTargeting3D).
    //     Incantation without a target: click anywhere to cast it.
    //  3. Right-click or Esc puts the card back in the hand. The camera returns after every play/cancel.
    // Created on demand by CardView3D.
    public class CardPlayController3D : MonoBehaviour
    {
        public static CardPlayController3D Instance { get; private set; }

        // A card is being held (or was released this frame), so other clicks should not act
        public static bool BlocksInput => Instance != null && (Instance.held != null || Time.frameCount <= Instance.endedFrame);

        [Header("Held Card")]
        [Tooltip("Where the held card waits, in viewport coordinates (x 0..1 left..right, y 0..1 bottom..top).")]
        public Vector2 heldViewportPosition = new Vector2(0.85f, 0.5f);
        public float heldScale = 1.4f;
        public float moveDuration = 0.25f;

        [Header("Slot Picking")]
        [Tooltip("How close (fraction of the screen height) a click must be to a slot to pick it.")]
        public float slotPickRadius = 0.09f;
        public Color slotColor = new Color(1f, 0.85f, 0.3f, 0.35f);
        public Color slotHoverColor = new Color(1f, 0.85f, 0.3f, 0.8f);

        private CardView3D held;
        private int selectedFrame = -1;
        private int endedFrame = -1;

        private Camera cam;
        private bool cameraRequested;
        private bool switching;

        private readonly Dictionary<BoardSlotView, Renderer> slotMarkers = new Dictionary<BoardSlotView, Renderer>();
        private BoardSlotView hoveredSlot;
        private Material markerMaterial;

        public static CardPlayController3D Ensure()
        {
            if (Instance == null) new GameObject("CardPlayController3D").AddComponent<CardPlayController3D>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            markerMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool IsHolding(CardView3D view) => held != null && held == view;

        // ---------------------------------------------------------------- select / release

        public void Select(CardView3D view)
        {
            if (view == null || view.CardData == null) return;
            if (held == view) return;

            // An unplayable card leaves the current selection as it is
            var card = view.CardData;
            if (!CanStartPlaying(card)) return;

            if (held != null)
            {
                switching = true;
                if (CardTargeting3D.Instance != null && CardTargeting3D.BlocksInput) CardTargeting3D.Instance.Cancel();
                Release(returnToHand: true, keepCamera: true);
                switching = false;
            }

            held = view;
            selectedFrame = Time.frameCount;
            view.SetHeld(true);
            MoveToHeldPosition(view);
            MoveCameraToTop();

            if (card.cardType == CardType.Familiar || card.cardType == CardType.Amulet)
            {
                ShowSlotMarkers();
                Debug.Log($"[CardPlay] {card.cardNameThai}: click an empty slot (right-click / Esc to cancel)");
            }
            else if (EffectResolver.GetTargetedAbility(card) != null)
            {
                var targets = EffectResolver.Instance.GetValidTargets(card, casterIsPlayer: true);
                CardTargeting3D.Ensure().Begin(view, targets, OnTargetChosen, OnTargetCancelled);
            }
            else
            {
                Debug.Log($"[CardPlay] {card.cardNameThai}: click anywhere to cast (right-click / Esc to cancel)");
            }
        }

        // Same checks PlayCard makes, done up front so an unplayable card never leaves the hand
        private static bool CanStartPlaying(CardInstance card)
        {
            var cards = CardManager.Instance;
            var resolver = EffectResolver.Instance;
            if (cards == null || resolver == null) return false;

            if (!cards.IsAllowedNow(card))
            {
                Debug.Log($"[CardPlay] {card.cardNameThai} cannot be played in this phase");
                return false;
            }
            if (!cards.CanAfford(card))
            {
                Debug.LogWarning($"[CardPlay] Not enough Merit to play {card.cardNameThai} (Needs {card.meritCost})");
                return false;
            }
            if (!resolver.CanResolve(card, casterIsPlayer: true))
            {
                Debug.Log(card.cardType == CardType.Incantation
                    ? $"[CardPlay] {card.cardNameThai} has no valid target right now"
                    : $"[CardPlay] The board is full; {card.cardNameThai} cannot be played");
                return false;
            }
            return true;
        }

        // Cancels whatever is held (used when a phase ends)
        public void Cancel()
        {
            if (held == null) return;
            if (CardTargeting3D.Instance != null && CardTargeting3D.BlocksInput) CardTargeting3D.Instance.Cancel();
            Release(returnToHand: true);
        }

        private void Release(bool returnToHand, bool keepCamera = false)
        {
            HideSlotMarkers();
            var view = held;
            held = null;
            endedFrame = Time.frameCount;

            if (view != null)
            {
                view.SetHeld(false);
                if (returnToHand) view.ReturnToHand();
            }
            if (!keepCamera) MoveCameraHome();
        }

        private void MoveToHeldPosition(CardView3D view)
        {
            if (cam == null) cam = Camera.main;
            var parent = view.transform.parent;
            if (cam == null || parent == null) return;

            // The hand follows the camera, so a point fixed in the hand's space stays put on screen
            float depth = Vector3.Dot(parent.position - cam.transform.position, cam.transform.forward);
            Vector3 world = cam.ViewportToWorldPoint(new Vector3(heldViewportPosition.x, heldViewportPosition.y, depth));
            Vector3 local = parent.InverseTransformPoint(world);

            var t = view.transform;
            t.DOKill();
            t.DOLocalMove(local, moveDuration).SetEase(Ease.OutQuad);
            t.DOLocalRotateQuaternion(Quaternion.identity, moveDuration);
            t.DOScale(view.BaseScale * heldScale, moveDuration);
        }

        // ---------------------------------------------------------------- playing

        private void Update()
        {
            if (held == null) return;

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                // A target choice handles its own cancel
                if (!CardTargeting3D.BlocksInput) Release(returnToHand: true);
                return;
            }

            var card = held.CardData;
            bool boardCard = card.cardType == CardType.Familiar || card.cardType == CardType.Amulet;

            if (boardCard) UpdateSlotHover();

            if (!Input.GetMouseButtonDown(0) || Time.frameCount == selectedFrame) return;
            if (ClickedHandCard()) return; // that card's own click selects it instead

            if (boardCard)
            {
                if (hoveredSlot != null) PlayOnSlot(hoveredSlot);
            }
            else if (EffectResolver.GetTargetedAbility(card) == null)
            {
                PlayHeld(null);
            }
        }

        private void OnTargetChosen(CardInstance target)
        {
            PlayHeld(target);
        }

        private void OnTargetCancelled()
        {
            Release(returnToHand: true, keepCamera: switching);
        }

        private void PlayOnSlot(BoardSlotView slot)
        {
            var view = held;
            view.CardData.boardSlot = slot.slotIndex;
            bool played = CardManager.Instance != null && CardManager.Instance.PlayCard(view.CardData);

            Release(returnToHand: !played);
            if (played) view.PlaceOnBoard();
        }

        private void PlayHeld(CardInstance target)
        {
            var view = held;
            bool played = CardManager.Instance != null && CardManager.Instance.PlayCard(view.CardData, target);

            Release(returnToHand: !played);
            if (played && view != null)
            {
                view.transform.DOKill();
                Destroy(view.gameObject);
            }
        }

        private bool ClickedHandCard()
        {
            if (cam == null) cam = Camera.main;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            foreach (var hit in Physics.RaycastAll(ray, 200f))
            {
                var view = hit.collider.GetComponentInParent<CardView3D>();
                if (view != null && view != held && view.IsInHand) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- slots

        private static IEnumerable<BoardSlotView> EmptyPlayerSlots()
        {
            var board = CombatManager.Instance != null ? CombatManager.Instance.State.activeBoardCards : null;
            foreach (var slot in FindObjectsByType<BoardSlotView>(FindObjectsSortMode.None))
            {
                if (slot.side != BoardSlotView.SlotSide.Player) continue;
                if (slot.GetComponent<DeckPileView3D>() != null || slot.name.Contains("Deck")) continue;
                if (board != null && EffectResolver.CardAt(board, slot.slotIndex) != null) continue;
                yield return slot;
            }
        }

        private void ShowSlotMarkers()
        {
            HideSlotMarkers();
            foreach (var slot in EmptyPlayerSlots())
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "SlotMarker";
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(slot.transform, false);
                go.transform.localPosition = Vector3.up * 0.03f;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = new Vector3(1.5f, 0.02f, 2.1f);

                var r = go.GetComponent<Renderer>();
                r.material = markerMaterial;
                r.material.color = slotColor;
                go.transform.DOScale(go.transform.localScale * 1.08f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);

                slotMarkers[slot] = r;
            }
        }

        private void HideSlotMarkers()
        {
            foreach (var r in slotMarkers.Values)
            {
                if (r == null) continue;
                r.transform.DOKill();
                Destroy(r.gameObject);
            }
            slotMarkers.Clear();
            hoveredSlot = null;
        }

        private void UpdateSlotHover()
        {
            if (cam == null) cam = Camera.main;
            BoardSlotView nearest = null;
            float best = slotPickRadius * Screen.height;
            best *= best;

            foreach (var slot in slotMarkers.Keys)
            {
                if (slot == null) continue;
                Vector2 p = cam.WorldToScreenPoint(slot.transform.position);
                float d = (p - (Vector2)Input.mousePosition).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = slot;
                }
            }

            if (nearest == hoveredSlot) return;
            if (hoveredSlot != null && slotMarkers.TryGetValue(hoveredSlot, out var oldR) && oldR != null) oldR.material.color = slotColor;
            hoveredSlot = nearest;
            if (hoveredSlot != null && slotMarkers.TryGetValue(hoveredSlot, out var newR) && newR != null) newR.material.color = slotHoverColor;
        }

        // ---------------------------------------------------------------- camera

        // The top-down view comes from CombatCameraRig3D; it returns to the player's chosen view afterwards
        private void MoveCameraToTop()
        {
            if (cameraRequested) return;
            cameraRequested = true;
            CombatCameraRig3D.Ensure().RequestTop();
        }

        private void MoveCameraHome()
        {
            if (!cameraRequested) return;
            cameraRequested = false;
            CombatCameraRig3D.Instance?.ReleaseTop();
        }
    }
}
