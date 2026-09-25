using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Card Data Reference")]
        public CardInstance CardData;

        [Header("UI Components (Art-Driven Card Layout)")]
        [Tooltip("ภาพพื้นหลังการ์ดเต็มแผ่นที่ทีม Art วาดวางได้เลย")]
        public Image cardBackgroundImage;
        public Image artworkImage;
        public Image frameBorderImage;
        public Image schoolBadge;
        public TMP_Text nameThaiText;
        public TMP_Text nameEngText;
        [Tooltip("ประเภท • รูปแบบ (แสดงบนแถบคั่นกลาง)")]
        public TMP_Text typeText;
        public TMP_Text costText;
        public TMP_Text descText;
        public GameObject familiarBadgeRoot;
        public TMP_Text hpAtkBadgeText;

        [Header("Interaction Settings")]
        public Canvas rootCanvas;
        public float hoverElevation = 35f;
        public float hoverScale = 1.15f;
        public float playDropThresholdY = 150f;

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private int originalSiblingIndex;
        private bool isDragging = false;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
        }

        public void Bind(CardInstance card)
        {
            CardData = card;
            if (card == null) return;

            // 1. ภาพพื้นหลังการ์ดเต็มใบจาก Art
            Sprite bg = card.cardBackground != null ? card.cardBackground : card.frameBorder;
            if (cardBackgroundImage != null && bg != null)
            {
                cardBackgroundImage.sprite = bg;
                cardBackgroundImage.gameObject.SetActive(true);
            }
            else if (frameBorderImage != null && bg != null)
            {
                frameBorderImage.sprite = bg;
            }

            // 2. ชื่อการ์ด
            if (nameThaiText != null) nameThaiText.text = card.cardNameThai;
            if (nameEngText != null) nameEngText.text = card.cardNameEng;

            // 3. ประเภทการ์ด • รูปแบบ (แถบคั่นกลาง)
            if (typeText != null)
            {
                typeText.text = card.GetFormattedTypeText();
            }

            // 4. รูปภาพการ์ดตรงกลาง
            if (artworkImage != null)
            {
                if (card.artwork != null)
                {
                    artworkImage.sprite = card.artwork;
                    artworkImage.gameObject.SetActive(true);
                }
                else
                {
                    artworkImage.gameObject.SetActive(false);
                }
            }

            // 5. คำอธิบาย
            if (descText != null)
            {
                try
                {
                    descText.text = string.Format(card.descriptionFormat ?? "", card.baseValue);
                }
                catch
                {
                    descText.text = card.descriptionFormat ?? "";
                }
            }

            // 6. ต้นทุน / ตัวเลขมุมบนขวา
            if (costText != null)
            {
                costText.text = card.magicSchool == MagicSchool.WhiteMagic
                    ? $"{card.meritCost}"
                    : $"+{card.corruptionGain}";
                costText.color = card.magicSchool == MagicSchool.WhiteMagic
                    ? new Color(0.95f, 0.85f, 0.4f)
                    : new Color(0.85f, 0.2f, 0.2f);
            }

            // 7. Familiar Badge (ถ้าเป็นการ์ดบริวาร)
            if (familiarBadgeRoot != null)
            {
                bool isFamiliar = card.cardType == CardType.Familiar;
                bool hasKhwan = isFamiliar || card.maxKhwan > 0;
                familiarBadgeRoot.SetActive(hasKhwan);
                if (hasKhwan && hpAtkBadgeText != null)
                {
                    hpAtkBadgeText.text = isFamiliar ? $"{card.familiarHealth} / {card.familiarDamage}" : $"{card.familiarHealth}";
                }
            }
        }

        public void SetRestingTransform(Vector3 pos, Quaternion rot)
        {
            if (isDragging) return;
            originalLocalPosition = pos;
            originalLocalRotation = rot;
            rectTransform.localPosition = pos;
            rectTransform.localRotation = rot;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isDragging) return;

            originalSiblingIndex = transform.GetSiblingIndex();
            transform.SetAsLastSibling();

            rectTransform.DOKill();
            rectTransform.DOLocalMove(originalLocalPosition + new Vector3(0, hoverElevation, 0), 0.15f);
            rectTransform.DOScale(hoverScale, 0.15f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (isDragging) return;

            rectTransform.DOKill();
            rectTransform.DOLocalMove(originalLocalPosition, 0.15f);
            rectTransform.DOLocalRotateQuaternion(originalLocalRotation, 0.15f);
            rectTransform.DOScale(1f, 0.15f);
            transform.SetSiblingIndex(originalSiblingIndex);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            isDragging = true;
            canvasGroup.blocksRaycasts = false;
            rectTransform.DOKill();
            rectTransform.DOScale(1.05f, 0.1f);
            rectTransform.localRotation = Quaternion.identity;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (rootCanvas == null) return;
            rectTransform.anchoredPosition += eventData.delta / rootCanvas.scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;
            canvasGroup.blocksRaycasts = true;

            // Check if dragged high enough to play
            if (rectTransform.localPosition.y > playDropThresholdY)
            {
                bool played = false;
                if (CardManager.Instance != null)
                {
                    played = CardManager.Instance.PlayCard(CardData);
                }

                if (played)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            // Return to hand position if not played
            rectTransform.DOLocalMove(originalLocalPosition, 0.25f).SetEase(Ease.OutQuad);
            rectTransform.DOLocalRotateQuaternion(originalLocalRotation, 0.25f);
            rectTransform.DOScale(1f, 0.25f);
        }
    }
}
