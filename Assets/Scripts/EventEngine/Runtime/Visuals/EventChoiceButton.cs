using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.EventEngine
{
    /// <summary>One selectable option in the event choice list: label, outcome hint and cost.</summary>
    public class EventChoiceButton : MonoBehaviour
    {
        public Button button;
        public TextMeshProUGUI indexText;
        public TextMeshProUGUI labelText;
        public TextMeshProUGUI hintText;
        public TextMeshProUGUI costText;

        private static readonly Color EnabledLabel = new Color(0.96f, 0.92f, 0.82f);
        private static readonly Color DisabledLabel = new Color(0.5f, 0.47f, 0.43f);

        private Action onSelected;

        // The text's left and right edges as the scene lays them out (the right one leaves room for the cost)
        private float textLeft = float.NaN, textRightWithCost;
        private LayoutElement layoutElement;
        private float minHeight;

        private const float TextRightNoCost = -24f; // a choice with no cost lets its text run almost to the edge
        private const float PadY = 18f, HintGap = 2f;

        public bool Interactable => button != null && button.interactable;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(Select);
            CacheLayout();
        }

        private void CacheLayout()
        {
            if (!float.IsNaN(textLeft) || labelText == null) return;
            textLeft = labelText.rectTransform.offsetMin.x;
            textRightWithCost = labelText.rectTransform.offsetMax.x;
            layoutElement = GetComponent<LayoutElement>();
            minHeight = layoutElement != null ? layoutElement.minHeight : 92f;
        }

        public void Setup(int index, EventViewUI.ChoiceOption option)
        {
            onSelected = option.onSelected;

            if (indexText != null) indexText.text = (index + 1).ToString();
            if (labelText != null)
            {
                labelText.text = option.label;
                labelText.color = option.interactable ? EnabledLabel : DisabledLabel;
            }
            if (hintText != null)
            {
                hintText.text = option.hint;
                hintText.gameObject.SetActive(!string.IsNullOrEmpty(option.hint));
            }
            if (costText != null)
            {
                costText.text = option.costText;
                costText.gameObject.SetActive(!string.IsNullOrEmpty(option.costText));
            }
            if (button != null) button.interactable = option.interactable;
            FitToText(!string.IsNullOrEmpty(option.costText), !string.IsNullOrEmpty(option.hint));
        }

        // A long label wraps onto more lines: the button grows to hold them (and the hint under them) instead of the
        // text spilling over the next choice. A short one stays the scene's height, its text centred as before.
        private void FitToText(bool hasCost, bool hasHint)
        {
            CacheLayout();
            if (labelText == null) return;

            float right = hasCost ? textRightWithCost : TextRightNoCost;
            float width = ((RectTransform)transform).rect.width;
            if (width <= 0f && transform.parent is RectTransform parent) width = parent.rect.width;
            float textWidth = Mathf.Max(50f, width - textLeft + right);

            float labelH = labelText.GetPreferredValues(labelText.text, textWidth, 0f).y;
            float hintH = hasHint && hintText != null ? hintText.GetPreferredValues(hintText.text, textWidth, 0f).y : 0f;
            float content = labelH + (hintH > 0f ? HintGap + hintH : 0f);
            float height = Mathf.Max(minHeight, content + PadY * 2f);
            float top = (height - content) * 0.5f;

            Place(labelText.rectTransform, right, top, labelH);
            if (hintText != null && hintH > 0f) Place(hintText.rectTransform, right, top + labelH + HintGap, hintH);

            if (layoutElement != null)
            {
                layoutElement.minHeight = height;
                layoutElement.preferredHeight = height;
            }
        }

        // Pins a text to the top of the button, `fromTop` down and `height` tall, between the left edge and `right`
        private void Place(RectTransform rt, float right, float fromTop, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(textLeft, -fromTop - height);
            rt.offsetMax = new Vector2(right, -fromTop);
        }

        public void Select()
        {
            if (!Interactable) return;
            var callback = onSelected;
            onSelected = null; // Guard against double clicks while the next page builds
            callback?.Invoke();
        }
    }
}
