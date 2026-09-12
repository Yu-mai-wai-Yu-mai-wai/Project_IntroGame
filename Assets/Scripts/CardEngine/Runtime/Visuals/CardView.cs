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

        [Header("UI Components")]
        public Image artworkImage;
        public Image frameBorderImage;
        public Image schoolBadge;
        public TMP_Text nameThaiText;
        public TMP_Text nameEngText;
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

            if (nameThaiText != null) nameThaiText.text = card.cardNameThai;
            if (nameEngText != null) nameEngText.text = card.cardNameEng;

            if (descText != null)
            {
                descText.text = string.Format(card.descriptionFormat ?? "", card.baseValue);
            }

            if (costText != null)
            {
                costText.text = card.magicSchool == MagicSchool.WhiteMagic
                    ? $"{card.meritCost}"
                    : $"+{card.corruptionGain}";
                costText.color = card.magicSchool == MagicSchool.WhiteMagic
                    ? new Color(0.95f, 0.85f, 0.4f)
                    : new Color(0.85f, 0.2f, 0.2f);
            }

            if (artworkImage != null && card.artwork != null)
            {
                artworkImage.sprite = card.artwork;
                artworkImage.gameObject.SetActive(true);
            }

            if (frameBorderImage != null && card.frameBorder != null)
            {
                frameBorderImage.sprite = card.frameBorder;
            }

            if (familiarBadgeRoot != null)
            {
                bool isFamiliar = card.cardType == CardType.Familiar;
                familiarBadgeRoot.SetActive(isFamiliar);
                if (isFamiliar && hpAtkBadgeText != null)
                {
                    hpAtkBadgeText.text = $"{card.familiarHealth} / {card.familiarDamage}";
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
