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

        private void Awake()
        {
            mainCamera = Camera.main;
            baseLocalScale = transform.localScale;
        }

        public void Bind(CardInstance card)
        {
            CardData = card;
            if (card == null) return;

            if (nameLabel != null) nameLabel.text = card.cardNameThai;

            if (cardRenderer != null)
            {
                cardRenderer.material.color = card.magicSchool == MagicSchool.WhiteMagic
                    ? new Color(0.85f, 0.8f, 0.55f)
                    : new Color(0.35f, 0.1f, 0.15f);
            }
        }

        // applyImmediately=false records the resting pose only, so the caller can animate to it
        // (used for the draw fly-in from the deck).
        public void SetRestingTransform(Vector3 localPos, Quaternion localRot, bool applyImmediately = true)
        {
            if (isDragging || isPlacedOnBoard) return;
            originalLocalPosition = localPos;
            originalLocalRotation = localRot;
            if (!applyImmediately) return;
            transform.localPosition = localPos;
            transform.localRotation = localRot;
        }

        private void OnMouseEnter()
        {
            if (isDragging || isPlacedOnBoard) return;

            transform.DOKill();
            transform.DOLocalMove(originalLocalPosition + new Vector3(0, hoverLift, -hoverPullToCamera), 0.15f);
            transform.DOScale(baseLocalScale * hoverScale, 0.15f);
        }

        private void OnMouseExit()
        {
            if (isDragging || isPlacedOnBoard) return;

            transform.DOKill();
            transform.DOLocalMove(originalLocalPosition, 0.15f);
            transform.DOLocalRotateQuaternion(originalLocalRotation, 0.15f);
            transform.DOScale(baseLocalScale, 0.15f);
        }

        private void OnMouseDown()
        {
            if (isPlacedOnBoard) return;
            isDragging = true;
            if (mainCamera == null) mainCamera = Camera.main;
            transform.DOKill();
            dragStartWorld = GetMouseWorldAtCardDepth();
            dragStartLocalPosition = transform.localPosition;
        }

        private void OnMouseDrag()
        {
            if (isPlacedOnBoard) return;
            Vector3 mouseWorld = GetMouseWorldAtCardDepth();
            Vector3 worldDelta = mouseWorld - dragStartWorld;
            Vector3 localDelta = transform.parent != null
                ? transform.parent.InverseTransformVector(worldDelta)
                : worldDelta;
            transform.localPosition = dragStartLocalPosition + localDelta;
        }

        private void OnMouseUp()
        {
            if (isPlacedOnBoard) return;
            isDragging = false;

            if (transform.localPosition.y > originalLocalPosition.y + playDropThresholdY)
            {
                bool played = CardManager.Instance != null && CardManager.Instance.PlayCard(CardData);
                if (played)
                {
                    if (CardData.cardType == CardType.Amulet || CardData.cardType == CardType.Familiar)
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

            transform.DOLocalMove(originalLocalPosition, 0.25f).SetEase(Ease.OutQuad);
            transform.DOLocalRotateQuaternion(originalLocalRotation, 0.25f);
            transform.DOScale(baseLocalScale, 0.25f);
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

            BoardSlotView targetSlot = FindNearestEmptyPlayerSlot(Input.mousePosition);
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
