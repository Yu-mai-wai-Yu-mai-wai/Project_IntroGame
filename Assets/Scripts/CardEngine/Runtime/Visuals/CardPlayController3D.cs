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
        [Tooltip("Light lying on each slot the card can go to (TawanOS/SlotGlow). HDR: above 1 lights up Bloom.")]
        [ColorUsage(true, true)] public Color slotColor = new Color(0.9f, 0.62f, 0.22f, 0.35f);
        [ColorUsage(true, true)] public Color slotHoverColor = new Color(1.3f, 0.9f, 0.32f, 0.6f);
        [Tooltip("The light's size against the card that would be placed there (1 = exactly the card).")]
        public float slotGlowScale = 1f;

        private CardView3D held;
        private int selectedFrame = -1;
        private int endedFrame = -1;

        private Camera cam;
        private bool cameraRequested;
        private bool switching;

        private readonly Dictionary<BoardSlotView, Renderer> slotMarkers = new Dictionary<BoardSlotView, Renderer>();
        private BoardSlotView hoveredSlot;
        private BoardSlotView pendingSlot; // an amulet's slot, waiting for the card its ability acts on
        private Material markerMaterial;
        private const string SlotGlowShader = "Shaders/SlotGlow";
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");

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
            var shader = Resources.Load<Shader>(SlotGlowShader);
            markerMaterial = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool IsHolding(CardView3D view) => held != null && held == view;
        public bool IsHoldingAny => held != null;

        // ---------------------------------------------------------------- select / release

        public void Select(CardView3D view)
        {
            if (view == null || view.CardData == null) return;
            if (CombatManager.Instance != null && CombatManager.Instance.IsCombatOver) return;
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

            if (card.cardType == CardType.Familiar || card.cardType == CardType.Amulet || EffectResolver.PicksSlot(card))
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
            if (CombatManager.Instance != null && CombatManager.Instance.IsCombatOver) return false;
            var cards = CardManager.Instance;
            var resolver = EffectResolver.Instance;
            if (cards == null || resolver == null) return false;

            // One place decides why a card cannot be played; the player sees the reason on screen, not only in the Console
            var reason = cards.GetPlayBlockReason(card);
            if (reason != null)
            {
                Debug.Log($"[CardPlay] {card.cardNameThai} cannot be played: {reason}");
                TawanOS.UI.PlayerNotice.Show(reason);
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
            pendingSlot = null;
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

        // Where a card held from the hand under `handParent` waits, in world space (also used to show
        // the enemy's incantations in the same spot)
        public bool TryGetHeldWorldPoint(Transform handParent, out Vector3 world)
        {
            world = Vector3.zero;
            if (cam == null) cam = Camera.main;
            if (cam == null || handParent == null) return false;

            float depth = Vector3.Dot(handParent.position - cam.transform.position, cam.transform.forward);
            world = cam.ViewportToWorldPoint(new Vector3(heldViewportPosition.x, heldViewportPosition.y, depth));
            return true;
        }

        private void MoveToHeldPosition(CardView3D view)
        {
            // The hand follows the camera, so a point fixed in the hand's space stays put on screen
            var parent = view.transform.parent;
            if (!TryGetHeldWorldPoint(parent, out var world)) return;
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
            if (CombatManager.Instance != null && CombatManager.Instance.IsCombatOver)
            {
                if (held != null) Cancel();
                return;
            }
            if (held == null || TawanOS.UI.PauseMenu.IsPaused) return;

            if (Input.GetMouseButtonDown(1) || TawanOS.UI.EscapeKey.Use())
            {
                // A target choice handles its own cancel
                if (!CardTargeting3D.BlocksInput) Release(returnToHand: true);
                return;
            }

            var card = held.CardData;
            bool boardCard = card.cardType == CardType.Familiar || card.cardType == CardType.Amulet || EffectResolver.PicksSlot(card);

            if (boardCard && CardTargeting3D.BlocksInput) return; // picking the card an amulet acts on
            if (boardCard) UpdateSlotHover();
            if (boardCard && hoveredSlot != null) TawanOS.UI.PointerCursor.Hover();

            if (!Input.GetMouseButtonDown(0) || Time.frameCount == selectedFrame) return;
            if (TawanOS.UI.TutorialCoach.Blocking) return; // the click that closes a tutorial hint, not a cast
            if (TawanOS.VFX.BossPhaseCutscene.Playing) return;
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
            if (pendingSlot != null) PlaceHeld(target);
            else PlayHeld(target);
        }

        private void OnTargetCancelled()
        {
            Release(returnToHand: true, keepCamera: switching);
        }

        // A card whose ability acts on a card the player picks (ผ้ายันต์กลับด้าน) asks for it once the slot is chosen,
        // unless only one card fits
        private void PlayOnSlot(BoardSlotView slot)
        {
            var card = held.CardData;
            // ด้ายแดงผูกจิต: the column clicked is its lane; the amulet itself goes to the amulet slot
            if (EffectResolver.PicksLane(card)) { card.chosenLane = slot.slotIndex; card.boardSlot = EffectResolver.AmuletSlot; }
            else card.boardSlot = slot.slotIndex;
            pendingSlot = slot;

            if (EffectResolver.GetTargetedAbility(card) != null && EffectResolver.Instance != null)
            {
                var targets = EffectResolver.Instance.GetValidTargets(card, casterIsPlayer: true);
                if (targets.Count > 1)
                {
                    HideSlotMarkers();
                    CardTargeting3D.Ensure().Begin(held, targets, OnTargetChosen, OnTargetCancelled);
                    return;
                }
                if (targets.Count == 1)
                {
                    PlaceHeld(targets[0]);
                    return;
                }
            }
            PlaceHeld(null);
        }

        private void PlaceHeld(CardInstance target)
        {
            var view = held;
            bool played = CardManager.Instance != null && CardManager.Instance.PlayCard(view.CardData, target);

            Release(returnToHand: !played);
            if (!played) return;
            if (view.CardData.cardType != CardType.Incantation) { view.PlaceOnBoard(); return; }
            view.transform.DOKill(); // a summoning spell (คาถาเรียกผี): the spell is spent, what it calls up takes the slot
            Destroy(view.gameObject);
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

        // The slots a held card may go to: an amulet only to the amulet slot (also when one is there: it is replaced),
        // a familiar to the empty familiar columns
        private static IEnumerable<BoardSlotView> EmptyPlayerSlots(CardInstance card)
        {
            var type = card != null ? card.cardType : CardType.Familiar;
            bool anyLane = EffectResolver.PicksLane(card); // ด้ายแดงผูกจิต: every familiar column, taken or not
            var board = CombatManager.Instance != null ? CombatManager.Instance.State.activeBoardCards : null;
            foreach (var slot in FindObjectsByType<BoardSlotView>(FindObjectsSortMode.None))
            {
                if (slot.side != BoardSlotView.SlotSide.Player) continue;
                if (slot.GetComponent<DeckPileView3D>() != null || slot.name.Contains("Deck")) continue;
                if (anyLane)
                {
                    if (EffectResolver.IsFamiliarSlot(slot.slotIndex)) yield return slot;
                    continue;
                }
                if (type == CardType.Amulet)
                {
                    if (slot.slotIndex == EffectResolver.AmuletSlot) yield return slot;
                    continue;
                }
                if (!EffectResolver.IsFamiliarSlot(slot.slotIndex)) continue;
                if (board != null && EffectResolver.CardAt(board, slot.slotIndex) != null) continue;
                yield return slot;
            }
        }

        // A sheet of light lying flat on every empty slot, the size of the slot. It sits at the scene root: the
        // slots are rotated and scaled unevenly (1.8 x 3), which would skew a child.
        private void ShowSlotMarkers()
        {
            HideSlotMarkers();
            foreach (var slot in EmptyPlayerSlots(held != null ? held.CardData : null))
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "SlotMarker";
                Destroy(go.GetComponent<Collider>());
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, slot.gameObject.scene);

                Vector2 size = PlacedCardSize(slot);
                Vector3 along = Vector3.ProjectOnPlane(slot.transform.up, Vector3.up);
                if (along.sqrMagnitude < 0.0001f) along = Vector3.forward;
                go.transform.position = slot.transform.position + Vector3.up * 0.02f;
                // A quad's face looks down its -Z: face up, its length along the slot's
                go.transform.rotation = Quaternion.LookRotation(Vector3.down, along.normalized);
                go.transform.localScale = new Vector3(size.x, size.y, 1f) * slotGlowScale;

                var r = go.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.material = markerMaterial;
                r.material.SetFloat(AspectId, size.y > 0f ? size.x / size.y : 0.6f);
                SetMarkerColor(r, slotColor);

                // Fades in, then gently swells and shrinks while it waits
                var c = slotColor;
                var mat = r.material;
                DOTween.To(() => 0f, a => { c.a = a; mat.SetColor(ColorId, c); }, slotColor.a, 0.2f).SetTarget(go);
                go.transform.DOScale(go.transform.localScale * 1.08f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);

                slotMarkers[slot] = r;
            }
        }

        // The width and length in world units of the held card once placed in this slot (BoardSlotView.FitCardScale):
        // as wide as the slot's mesh times its cardFill, with the card's own shape
        private Vector2 PlacedCardSize(BoardSlotView slot)
        {
            var mf = slot.GetComponent<MeshFilter>();
            Vector3 mesh = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size : Vector3.one;
            Vector3 scale = slot.transform.lossyScale;
            float width = Mathf.Abs(mesh.x * scale.x) * slot.cardFill;
            Vector3 card = held != null ? held.BaseScale : Vector3.zero;
            float length = card.x > 0f && card.y > 0f ? width * card.y / card.x : Mathf.Abs(mesh.y * scale.y);
            return new Vector2(width, length);
        }

        private static void SetMarkerColor(Renderer r, Color color)
        {
            if (r != null) r.material.SetColor(ColorId, color);
        }

        private void HideSlotMarkers()
        {
            foreach (var r in slotMarkers.Values)
            {
                if (r == null) continue;
                r.transform.DOKill();
                DOTween.Kill(r.gameObject);
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
            if (hoveredSlot != null && slotMarkers.TryGetValue(hoveredSlot, out var oldR) && oldR != null)
            {
                DOTween.Kill(oldR.gameObject); // the fade-in, if still running
                SetMarkerColor(oldR, slotColor);
            }
            hoveredSlot = nearest;
            if (hoveredSlot != null && slotMarkers.TryGetValue(hoveredSlot, out var newR) && newR != null)
            {
                DOTween.Kill(newR.gameObject);
                SetMarkerColor(newR, slotHoverColor);
            }
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
