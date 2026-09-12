using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TawanOS.CardEngine
{
    public class BoardSlotView : MonoBehaviour
    {
        public enum SlotType
        {
            AmuletSlot,     // ช่องวางเครื่องราง (ซ้าย 3 ช่อง)
            FamiliarSlot    // ช่องวางบริวาร (หน้า 3 ช่อง)
        }

        [Header("Slot Identity")]
        public SlotType slotType = SlotType.FamiliarSlot;
        public int slotIndex = 0;

        [Header("UI Elements")]
        public Image slotFrameImage;
        public Image cardArtworkImage;
        public TMP_Text nameText;
        public TMP_Text statBadgeText;
        public GameObject occupiedRoot;
        public GameObject emptyRoot;

        private CardInstance currentCard;

        private void Start()
        {
            if (EffectResolver.Instance != null)
            {
                EffectResolver.Instance.OnSlotOccupied += HandleSlotOccupied;
            }
            ClearSlot();
        }

        private void OnDestroy()
        {
            if (EffectResolver.Instance != null)
            {
                EffectResolver.Instance.OnSlotOccupied -= HandleSlotOccupied;
            }
        }

        private void HandleSlotOccupied(CardInstance card, int targetIndex)
        {
            if (targetIndex != slotIndex) return;

            bool isMatchingType = (slotType == SlotType.AmuletSlot && card.cardType == CardType.Amulet) ||
                                  (slotType == SlotType.FamiliarSlot && card.cardType == CardType.Familiar);

            if (isMatchingType)
            {
                OccupySlot(card);
            }
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
                if (slotType == SlotType.AmuletSlot)
                {
                    statBadgeText.text = $"ความคงทน: {card.currentDurability}";
                }
                else
                {
                    statBadgeText.text = $"HP:{card.familiarHealth} | ATK:{card.familiarDamage}";
                }
            }
        }

        public void ClearSlot()
        {
            currentCard = null;
            if (occupiedRoot != null) occupiedRoot.SetActive(false);
            if (emptyRoot != null) emptyRoot.SetActive(true);
        }
    }
}
