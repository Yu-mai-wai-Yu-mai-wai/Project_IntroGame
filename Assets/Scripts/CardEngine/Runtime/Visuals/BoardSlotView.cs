using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    public class BoardSlotView : MonoBehaviour
    {
        public enum SlotSide
        {
            Player, // Functional: mirrors CombatManager.State.activeBoardCards
            Enemy   // Visual placeholder only - not wired to any game state yet
        }

        [Header("Slot Identity")]
        public SlotSide side = SlotSide.Player;
        public int slotIndex = 0;

        [Header("Card Fit")]
        [Tooltip("Card width as a fraction of the slot's width (1 = exactly as wide). The card keeps its own shape, so its height follows.")]
        [Range(0.5f, 1f)] public float cardFill = 1f;

        [Header("UI Elements")]
        public Image slotFrameImage;
        public Image cardArtworkImage;
        public TMP_Text nameText;
        public TMP_Text statBadgeText;
        public GameObject occupiedRoot;
        public GameObject emptyRoot;

        private CardInstance currentCard;

        // Local scale for a card parented to this slot: as wide as the slot's mesh (a quad lying in the
        // slot's local X/Y), with the card's own shape kept, so it may reach past the slot's ends.
        // cardScale is the card's normal scale; its thickness is kept. Worked out in world size and then
        // divided by the slot's own scale, so a slot scaled unevenly (e.g. 1.8 x 3) does not stretch the card.
        public Vector3 FitCardScale(Vector3 cardScale)
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null || cardScale.x <= 0f || cardScale.y <= 0f) return cardScale;

            Vector3 slotScale = transform.lossyScale;
            if (slotScale.x == 0f || slotScale.y == 0f || slotScale.z == 0f) return cardScale;

            float width = meshFilter.sharedMesh.bounds.size.x * Mathf.Abs(slotScale.x) * cardFill;
            float height = width * cardScale.y / cardScale.x;
            return new Vector3(width / Mathf.Abs(slotScale.x), height / Mathf.Abs(slotScale.y), cardScale.z / Mathf.Abs(slotScale.z));
        }

        private void Start()
        {
            if (side == SlotSide.Player && EffectResolver.Instance != null)
            {
                EffectResolver.Instance.OnSlotOccupied += HandleSlotOccupied;
                EffectResolver.Instance.OnSlotCleared += HandleSlotCleared;
            }
            ClearSlot();
        }

        private void OnDestroy()
        {
            if (side == SlotSide.Player && EffectResolver.Instance != null)
            {
                EffectResolver.Instance.OnSlotOccupied -= HandleSlotOccupied;
                EffectResolver.Instance.OnSlotCleared -= HandleSlotCleared;
            }
        }

        private void HandleSlotOccupied(CardInstance card, int targetIndex)
        {
            RemoveCardNoLongerOnBoard();
            if (targetIndex != slotIndex) return;
            OccupySlot(card);
        }

        private void HandleSlotCleared(int targetIndex)
        {
            RemoveCardNoLongerOnBoard();
            if (targetIndex != slotIndex) return;
            if (HasLiveBoardCard()) return; // see RemoveCardNoLongerOnBoard
            ClearSlot();
        }

        // A slot's physical card is removed only once it has left the board list (cards keep their
        // column, CardInstance.boardSlot, so this never removes a card that is still in play).
        private void RemoveCardNoLongerOnBoard()
        {
            if (side != SlotSide.Player) return;

            var card = GetComponentInChildren<CardView3D>();
            if (card == null || HasLiveBoardCard()) return;
            ClearSlot();
        }

        private bool HasLiveBoardCard()
        {
            var card = GetComponentInChildren<CardView3D>();
            if (card == null || CombatManager.Instance == null) return false;
            return CombatManager.Instance.State.activeBoardCards.Contains(card.CardData);
        }

        public void OccupySlot(CardInstance card)
        {
            currentCard = card;
            if (occupiedRoot != null) occupiedRoot.SetActive(true);
            if (emptyRoot != null) emptyRoot.SetActive(false);

            if (nameText != null) nameText.text = card.cardNameThai;
            if (cardArtworkImage != null && card.artwork != null)
            {
                cardArtworkImage.sprite = card.artwork;
                cardArtworkImage.gameObject.SetActive(true);
            }

            if (statBadgeText != null)
            {
                if (card.cardType == CardType.Familiar)
                    statBadgeText.text = $"HP:{card.familiarHealth} | ATK:{card.familiarDamage}";
                else if (card.maxKhwan > 0)
                    statBadgeText.text = $"HP:{card.familiarHealth}";
                else
                    statBadgeText.text = $"ความคงทน: {card.currentDurability}";
            }
        }

        public void ClearSlot()
        {
            currentCard = null;
            if (occupiedRoot != null) occupiedRoot.SetActive(false);
            if (emptyRoot != null) emptyRoot.SetActive(true);

            // Remove the physical 3D card that was dropped into this slot, if any
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.GetComponent<CardView3D>() != null)
                {
                    child.DOKill();
                    Destroy(child.gameObject);
                }
            }
        }
    }
}
