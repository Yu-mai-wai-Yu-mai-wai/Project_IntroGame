using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.ShopEngine
{
    /// <summary>One item on the shrine shelf: picture, name, detail and incense price.</summary>
    public class ShopItemView : MonoBehaviour
    {
        public Button button;
        public Image icon;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI subtitleText;
        public TextMeshProUGUI descriptionText;
        public TextMeshProUGUI priceText;
        public GameObject soldOverlay;

        private static readonly Color AffordableColor = new Color(0.98f, 0.84f, 0.5f);
        private static readonly Color TooExpensiveColor = new Color(0.9f, 0.4f, 0.35f);

        private Action onBuy;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(HandleClick);
        }

        public void Setup(Sprite sprite, string title, string subtitle, string description, string priceLabel,
            bool affordable, bool sold, Action buy)
        {
            onBuy = buy;

            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
                icon.preserveAspect = true;
            }
            SetText(titleText, title);
            SetText(subtitleText, subtitle);
            SetText(descriptionText, description);
            if (priceText != null)
            {
                priceText.text = priceLabel;
                priceText.color = affordable ? AffordableColor : TooExpensiveColor;
            }
            if (soldOverlay != null) soldOverlay.SetActive(sold);
            // Unaffordable items stay clickable so the guardian can tell the player off
            if (button != null) button.interactable = !sold && buy != null;
        }

        private void HandleClick()
        {
            if (onBuy == null) return;
            transform.DOKill(true);
            transform.DOPunchScale(Vector3.one * 0.06f, 0.25f, 8, 0.8f);
            onBuy.Invoke();
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text == null) return;
            text.text = value;
            text.gameObject.SetActive(!string.IsNullOrEmpty(value));
        }
    }
}
