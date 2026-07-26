using UnityEngine;
using TMPro;

namespace TawanOS.MapEngine
{
    public class MapTooltipView : MonoBehaviour
    {
        [SerializeField] private GameObject tooltipPanel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Vector3 offset = new Vector3(0, 1.2f, 0);

        private Camera mainCamera;

        private void Awake()
        {
            mainCamera = Camera.main;
            HideTooltip();
        }

        public void ShowTooltip(string title, string description, Vector3 worldPosition)
        {
            if (tooltipPanel == null) return;

            if (titleText != null) titleText.text = title;
            if (descriptionText != null) descriptionText.text = description;

            transform.position = worldPosition + offset;
            tooltipPanel.SetActive(true);
        }

        public void HideTooltip()
        {
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
            }
        }
    }
}
