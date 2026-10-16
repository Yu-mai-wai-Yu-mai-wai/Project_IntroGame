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
        private float minHeight, labelMaxSize, hintMaxSize;

        private const float TextRightNoCost = -24f; // a choice with no cost lets its text run almost to the edge
        private const float HintSplit = 0.34f;      // the hint takes the bottom third of the button, the label the rest
        private const float LabelMinSize = 15f, HintMinSize = 13f;

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
            labelMaxSize = labelText.fontSize;
            hintMaxSize = hintText != null ? hintText.fontSize : 22f;
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

        // Every choice keeps the scene's height. A choice with no cost lets its text use the cost's room too, one with no
        // hint lets its label use the hint's; and a label that is still too long shrinks its letters (down to
        // LabelMinSize, over two lines) instead of spilling over the next choice.
        private void FitToText(bool hasCost, bool hasHint)
        {
            CacheLayout();
            if (labelText == null) return;

            float right = hasCost ? textRightWithCost : TextRightNoCost;
            Place(labelText.rectTransform, right, hasHint ? HintSplit : 0f, 1f, hasHint ? 12f : 7f);
            labelText.alignment = hasHint ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.MidlineLeft;
            ShrinkToFit(labelText, labelMaxSize, LabelMinSize);
            if (hintText != null)
            {
                Place(hintText.rectTransform, right, 0f, HintSplit, 3f);
                ShrinkToFit(hintText, hintMaxSize, HintMinSize);
            }

            if (layoutElement != null)
            {
                layoutElement.minHeight = minHeight;
                layoutElement.preferredHeight = minHeight;
            }
        }

        // Spans a text across the button between the left edge and `right`, from `bottom` to `top` of its height.
        // `topGap` keeps Thai vowels and tone marks inside the button: TextMeshPro's auto-size does not count them.
        private void Place(RectTransform rt, float right, float bottom, float top, float topGap)
        {
            rt.anchorMin = new Vector2(0f, bottom);
            rt.anchorMax = new Vector2(1f, top);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(textLeft, 3f);
            rt.offsetMax = new Vector2(right, -topGap);
        }

        private static void ShrinkToFit(TMP_Text text, float max, float min)
        {
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true;
            text.fontSizeMax = max;
            text.fontSizeMin = min;
            text.overflowMode = TextOverflowModes.Ellipsis;
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
