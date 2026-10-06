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

        public bool Interactable => button != null && button.interactable;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(Select);
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
