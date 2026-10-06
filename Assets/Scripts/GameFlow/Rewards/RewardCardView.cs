using System;
using DG.Tweening;
using TawanOS.CardEngine;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// A card offered as a reward, drawn like the card in play: frame, artwork and the face text laid out
    /// by <see cref="CardFaceLayout"/> (a finished card PNG shows as it is, with only attack / Khwan on it).
    /// Click to take it.
    /// </summary>
    public class RewardCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
    {
        public Button button;
        public Image frame;
        public Image artwork;
        public TextMeshProUGUI costText;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI typeText;
        public TextMeshProUGUI attackText;
        public TextMeshProUGUI khwanText;
        public TextMeshProUGUI descriptionText;
        public GameObject pickedHighlight;

        [Header("Default Frames (cards without a Card Background)")]
        public Sprite defaultWhiteFrame;
        public Sprite defaultBlackFrame;

        private Action onPick;
        private CardDataSO shownCard;
        private bool ignoreClick; // this click closed the detail screen, it must not also take the card

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() =>
            {
                if (!ignoreClick) onPick?.Invoke();
                ignoreClick = false;
            });
        }

        public void Setup(CardDataSO card, Action pick)
        {
            onPick = pick;
            shownCard = card;
            transform.localScale = Vector3.one;

            // A finished card PNG has everything but attack / Khwan printed on it
            bool printed = card.cardImage != null;
            Sprite face = printed ? card.cardImage
                : card.cardBackground != null ? card.cardBackground
                : CardFaceLayout.DefaultFrame(defaultWhiteFrame, defaultBlackFrame, card.magicSchool);

            if (frame != null)
            {
                frame.sprite = face;
                frame.color = face != null ? Color.white : SchoolColor(card.magicSchool);
            }
            if (artwork != null)
            {
                artwork.sprite = card.artwork;
                artwork.enabled = !printed && card.artwork != null;
                artwork.preserveAspect = true;
            }

            bool familiar = card.cardType == CardType.Familiar;
            bool hasKhwan = familiar || (card.cardType == CardType.Amulet && card.familiarHealth > 0);

            SetText(costText, printed ? "" : card.magicSchool == MagicSchool.WhiteMagic ? card.meritCost.ToString() : card.corruptionGain.ToString(),
                CardFaceLayout.Cost, card, CardFaceLayout.Text.Cost);
            SetText(nameText, printed ? "" : card.cardNameThai, CardFaceLayout.Name, card, CardFaceLayout.Text.Name);
            SetText(typeText, printed ? "" : card.GetFormattedTypeText().Replace(" • ", "  "), CardFaceLayout.Type, card, CardFaceLayout.Text.Type);
            SetText(attackText, familiar ? card.familiarDamage.ToString() : "", CardFaceLayout.Attack, card, CardFaceLayout.Text.Stat);
            SetText(khwanText, hasKhwan ? card.familiarHealth.ToString() : "", CardFaceLayout.Khwan, card, CardFaceLayout.Text.Stat);
            if (descriptionText != null) descriptionText.text = printed ? "" : FormatDescription(card);

            if (pickedHighlight != null) pickedHighlight.SetActive(false);
            if (button != null) button.interactable = true;
        }

        // Single-line face text: the Card Data's size for it grows its box (same centre), as on the 3D card
        private static void SetText(TextMeshProUGUI text, string value, CardFaceLayout.Box box, CardDataSO card, CardFaceLayout.Text part)
        {
            if (text == null) return;
            text.text = value;
            PlaceOnFace(text.rectTransform, box.Scaled(CardFaceLayout.FontScale(card, part)));
        }

        // Face boxes are fractions of the card (CardFaceLayout: x -0.5..0.5, y 0.5 top .. -0.5 bottom)
        public static void PlaceOnFace(RectTransform rt, CardFaceLayout.Box box)
        {
            Vector2 c = box.center + new Vector2(0.5f, 0.5f);
            rt.anchorMin = c - box.size * 0.5f;
            rt.anchorMax = c + box.size * 0.5f;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public void SetResult(bool picked)
        {
            if (button != null) button.interactable = false;
            if (pickedHighlight != null) pickedHighlight.SetActive(picked);
            transform.DOKill();
            transform.DOScale(picked ? 1.12f : 0.9f, 0.25f).SetEase(Ease.OutBack);
            var group = GetComponent<CanvasGroup>();
            if (group != null) group.DOFade(picked ? 1f : 0.35f, 0.25f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            ignoreClick = CardDetailPanelUI.BlocksInput;
        }

        // Right-click: the same card detail screen as in combat
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right || shownCard == null || ignoreClick) return;
            CardDetailPanelUI.Ensure().Show(new CardInstance(shownCard), defaultWhiteFrame, defaultBlackFrame, nameText != null ? nameText.font : null);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button == null || !button.interactable) return;
            transform.DOKill();
            transform.DOScale(1.06f, 0.15f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (button == null || !button.interactable) return;
            transform.DOKill();
            transform.DOScale(1f, 0.15f);
        }

        private static Color SchoolColor(MagicSchool school)
        {
            return school == MagicSchool.WhiteMagic ? new Color(0.85f, 0.8f, 0.55f) : new Color(0.35f, 0.1f, 0.15f);
        }

        private static string FormatDescription(CardDataSO card)
        {
            try { return string.Format(card.descriptionFormat ?? string.Empty, card.baseValue); }
            catch (FormatException) { return card.descriptionFormat; }
        }
    }
}
