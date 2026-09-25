using UnityEngine;
using TMPro;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    public class CardView3D : MonoBehaviour
    {
        [Header("Card Data Reference")]
        public CardInstance CardData;

        [Header("Visuals")]
        public Renderer cardRenderer;
        public TMP_Text nameLabel;

        [Header("Interaction Settings")]
        public float hoverLift = 0.4f;
        public float hoverPullToCamera = 0.3f;
        public float hoverScale = 1.15f;
        public float playDropThresholdY = 1.2f;

        private Camera mainCamera;
        private Vector3 baseLocalScale;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private Vector3 dragStartWorld;
        private Vector3 dragStartLocalPosition;
        private bool isDragging;
        private bool isPlacedOnBoard;
        private bool isAwaitingTarget;

        private void Awake()
        {
            mainCamera = Camera.main;
            baseLocalScale = transform.localScale;
        }

        public void Bind(CardInstance card)
        {
            CardData = card;
            if (card == null) return;

            RefreshLabel();

            if (cardRenderer != null)
            {
                cardRenderer.material.color = card.magicSchool == MagicSchool.WhiteMagic
                    ? new Color(0.85f, 0.8f, 0.55f)
                    : new Color(0.35f, 0.1f, 0.15f);
            }
        }

        // Familiars also show their live Khwan (HP) and attack under the name; amulets with Khwan show their Khwan
        public void RefreshLabel()
        {
            if (nameLabel == null || CardData == null) return;

            if (CardData.cardType == CardType.Familiar)
                nameLabel.text = $"{CardData.cardNameThai}\n{CardData.familiarHealth}/{CardData.familiarDamage}";
            else if (CardData.maxKhwan > 0)
                nameLabel.text = $"{CardData.cardNameThai}\n{CardData.familiarHealth}";
            else
                nameLabel.text = CardData.cardNameThai;
        }

        // applyImmediately=false records the resting pose only, so the caller can animate to it
        // (used for the draw fly-in from the deck).
        public void SetRestingTransform(Vector3 localPos, Quaternion localRot, bool applyImmediately = true)
        {
            if (isPlacedOnBoard) return;
            originalLocalPosition = localPos;
            originalLocalRotation = localRot;
            // A card held by the mouse (or waiting for a target) keeps its pose; it returns here afterwards
            if (!applyImmediately || isDragging || isAwaitingTarget) return;
            transform.localPosition = localPos;
            transform.localRotation = localRot;
        }

        private void OnMouseEnter()
        {
            if (isDragging || isPlacedOnBoard || isAwaitingTarget || CardTargeting3D.BlocksInput) return;

            transform.DOKill();
            transform.DOLocalMove(originalLocalPosition + new Vector3(0, hoverLift, -hoverPullToCamera), 0.15f);
            transform.DOScale(baseLocalScale * hoverScale, 0.15f);
        }

        private void OnMouseExit()
        {
            if (isDragging || isPlacedOnBoard || isAwaitingTarget || CardTargeting3D.BlocksInput) return;

            transform.DOKill();
            transform.DOLocalMove(originalLocalPosition, 0.15f);
            transform.DOLocalRotateQuaternion(originalLocalRotation, 0.15f);
            transform.DOScale(baseLocalScale, 0.15f);
        }

        private void OnMouseDown()
        {
            if (isPlacedOnBoard || isAwaitingTarget || CardTargeting3D.BlocksInput) return;
            isDragging = true;
            if (mainCamera == null) mainCamera = Camera.main;
            transform.DOKill();
            dragStartWorld = GetMouseWorldAtCardDepth();
            dragStartLocalPosition = transform.localPosition;
        }

        private void OnMouseDrag()
        {
            if (isPlacedOnBoard || !isDragging) return;
            Vector3 mouseWorld = GetMouseWorldAtCardDepth();
            Vector3 worldDelta = mouseWorld - dragStartWorld;
            Vector3 localDelta = transform.parent != null
                ? transform.parent.InverseTransformVector(worldDelta)
                : worldDelta;
            transform.localPosition = dragStartLocalPosition + localDelta;
        }

        private void OnMouseUp()
        {
            if (isPlacedOnBoard || !isDragging) return;
            isDragging = false;

            if (transform.localPosition.y > originalLocalPosition.y + playDropThresholdY)
            {
                // Incantations that act on one card wait here for the player to click that card
                if (EffectResolver.GetTargetedAbility(CardData) != null)
                {
                    if (TryBeginTargeting()) return;
                    ReturnToHand();
                    return;
                }

                // Board cards go to the column they were dropped on (the engine keeps that column)
                bool boardCard = CardData.cardType == CardType.Amulet || CardData.cardType == CardType.Familiar;
                if (boardCard)
                {
                    if (mainCamera == null) mainCamera = Camera.main;
                    var dropSlot = FindNearestEmptyPlayerSlot(Input.mousePosition);
                    CardData.boardSlot = dropSlot != null ? dropSlot.slotIndex : -1;
                }

                bool played = CardManager.Instance != null && CardManager.Instance.PlayCard(CardData);
                if (played)
                {
                    if (boardCard)
                    {
                        SnapToBoardSlot();
                    }
                    else
                    {
                        transform.DOKill();
                        Destroy(gameObject);
                    }
                    return;
                }
            }

            ReturnToHand();
        }

        private void ReturnToHand()
        {
            transform.DOKill();
            transform.DOLocalMove(originalLocalPosition, 0.25f).SetEase(Ease.OutQuad);
            transform.DOLocalRotateQuaternion(originalLocalRotation, 0.25f);
            transform.DOScale(baseLocalScale, 0.25f);
        }

        private bool TryBeginTargeting()
        {
            var cards = CardManager.Instance;
            var resolver = EffectResolver.Instance;
            if (cards == null || resolver == null) return false;

            if (!cards.IsAllowedNow(CardData))
            {
                Debug.Log($"[CardView3D] {CardData.cardNameThai} cannot be played in this phase");
                return false;
            }

            if (!cards.CanAfford(CardData))
            {
                Debug.LogWarning($"[CardView3D] Not enough Merit to play {CardData.cardNameThai} (Needs {CardData.meritCost})");
                return false;
            }

            var targets = resolver.GetValidTargets(CardData, casterIsPlayer: true);
            if (targets.Count == 0)
            {
                Debug.Log($"[CardView3D] {CardData.cardNameThai} has no valid target right now");
                return false;
            }

            isAwaitingTarget = true;
            CardTargeting3D.Ensure().Begin(this, targets, OnTargetChosen, OnTargetCancelled);
            return true;
        }

        private void OnTargetChosen(CardInstance target)
        {
            isAwaitingTarget = false;
            bool played = CardManager.Instance != null && CardManager.Instance.PlayCard(CardData, target);
            if (!played)
            {
                ReturnToHand();
                return;
            }
            transform.DOKill();
            Destroy(gameObject);
        }

        private void OnTargetCancelled()
        {
            isAwaitingTarget = false;
            if (this != null) ReturnToHand();
        }

        // Moves the dragged 3D card itself onto its assigned board slot, instead of just
        // spawning a separate placeholder there, so the physical card visually sits in the slot.
        //
        // The target slot is picked by SCREEN-space distance from the mouse, not 3D world
        // distance: the card being dragged floats close to the camera while the board slots
        // sit far away on the table, so with an angled (non-front-on) camera those two depths
        // have different parallax and a world-distance comparison picks the wrong slot even
        // when the card visually looks like it's over the right one. Comparing projected
        // screen positions matches what the player actually sees, independent of camera angle.
        private void SnapToBoardSlot()
        {
            if (mainCamera == null) mainCamera = Camera.main;

            // The engine has already put the card in a column; sit in that column's slot
            BoardSlotView targetSlot = FindPlayerSlot(CardData.boardSlot);
            if (targetSlot == null)
            {
                Debug.LogWarning($"[CardView3D] SnapToBoardSlot: no empty Player BoardSlotView available for {CardData.cardNameThai}.");
                transform.DOKill();
                Destroy(gameObject);
                return;
            }

            transform.SetParent(targetSlot.transform, worldPositionStays: true);
            transform.DOKill();
            transform.DOLocalMove(Vector3.zero, 0.3f).SetEase(Ease.OutQuad)
                .OnComplete(() => transform.localPosition = Vector3.zero);
            // Local rotation identity: the card inherits the slot's tilt and lies flat on the table.
            transform.DOLocalRotateQuaternion(Quaternion.identity, 0.3f)
                .OnComplete(() => transform.localRotation = Quaternion.identity);
            transform.DOScale(baseLocalScale, 0.3f);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            // The card has been committed to the board; it no longer drags/hovers like a hand card
            isPlacedOnBoard = true;
            enabled = false;
        }

        private static BoardSlotView FindPlayerSlot(int index)
        {
            if (index < 0) return null;
            foreach (BoardSlotView slot in Object.FindObjectsByType<BoardSlotView>(FindObjectsSortMode.None))
            {
                if (slot.side != BoardSlotView.SlotSide.Player) continue;
                if (slot.GetComponent<DeckPileView3D>() != null) continue;
                if (slot.slotIndex == index) return slot;
            }
            return null;
        }

        private BoardSlotView FindNearestEmptyPlayerSlot(Vector2 screenPosition)
        {
            if (mainCamera == null) return null;

            BoardSlotView[] slots = Object.FindObjectsByType<BoardSlotView>(FindObjectsSortMode.None);
            BoardSlotView nearest = null;
            float nearestSqrDist = float.MaxValue;

            foreach (BoardSlotView slot in slots)
            {
                if (slot.side != BoardSlotView.SlotSide.Player) continue;
                if (slot.GetComponent<DeckPileView3D>() != null) continue; // the deck is not a board slot
                if (slot.transform.childCount > 0) continue; // already occupied by a physical card

                Vector2 slotScreenPos = mainCamera.WorldToScreenPoint(slot.transform.position);
                float sqrDist = (slotScreenPos - screenPosition).sqrMagnitude;
                if (sqrDist < nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearest = slot;
                }
            }

            return nearest;
        }

        private Vector3 GetMouseWorldAtCardDepth()
        {
            Vector3 screenPoint = Input.mousePosition;
            screenPoint.z = mainCamera.WorldToScreenPoint(transform.position).z;
            return mainCamera.ScreenToWorldPoint(screenPoint);
        }
    }
}
