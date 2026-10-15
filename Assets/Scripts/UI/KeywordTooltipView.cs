using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.UI
{
    /// <summary>
    /// Screen-space tooltip for keyword definitions (Task P1-1).
    /// Styled like MapTooltipView with paper/fog dark background, title in MN-RueangLao, and body in EkkamaiVibe.
    /// </summary>
    public class KeywordTooltipView : MonoBehaviour
    {
        private static KeywordTooltipView instance;

        public static KeywordTooltipView Instance
        {
            get
            {
                if (instance == null)
                {
                    var existing = FindFirstObjectByType<KeywordTooltipView>();
                    if (existing != null)
                    {
                        instance = existing;
                    }
                    else
                    {
                        var go = new GameObject("KeywordTooltipCanvas");
                        DontDestroyOnLoad(go);
                        instance = go.AddComponent<KeywordTooltipView>();
                        instance.BuildUI();
                    }
                }
                return instance;
            }
        }

        private Canvas canvas;
        private RectTransform panelRect;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI descriptionText;
        private Vector2 currentScreenPos;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                if (panelRect == null) BuildUI();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void BuildUI()
        {
            if (panelRect != null) return;

            var theme = UIThemeSO.Current;
            canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 999;

                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                var group = gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            var panelGo = new GameObject("TooltipPanel", typeof(RectTransform));
            panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.SetParent(transform, false);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.sizeDelta = new Vector2(360f, 120f);

            // Border
            var borderImg = panelGo.AddComponent<Image>();
            borderImg.color = theme != null ? theme.accent : new Color(0.94f, 0.68f, 0.48f);
            borderImg.raycastTarget = false;

            // Inner dark panel
            var innerGo = new GameObject("InnerPanel", typeof(RectTransform), typeof(Image));
            var innerRect = innerGo.GetComponent<RectTransform>();
            innerRect.SetParent(panelRect, false);
            UiFactory.Stretch(innerRect, 2f);
            var innerImg = innerGo.GetComponent<Image>();
            innerImg.color = theme != null ? theme.black : new Color(0.04f, 0.04f, 0.05f, 0.95f);
            innerImg.raycastTarget = false;

            // Layout
            var contentGo = new GameObject("Content", typeof(RectTransform));
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.SetParent(innerRect, false);
            UiFactory.Stretch(contentRect, 10f);

            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 6f;

            var fitter = panelGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            titleText = UiFactory.CreateText(contentRect, "Title", "", theme != null ? theme.titleSize * 0.75f : 28f,
                theme != null ? theme.accent : Color.yellow, TextAlignmentOptions.TopLeft, theme?.titleFont);
            titleText.textWrappingMode = TextWrappingModes.Normal;

            descriptionText = UiFactory.CreateText(contentRect, "Description", "", theme != null ? theme.bodySize * 0.85f : 20f,
                theme != null ? theme.text : Color.white, TextAlignmentOptions.TopLeft, theme?.bodyFont);
            descriptionText.textWrappingMode = TextWrappingModes.Normal;
            UiFactory.EnableThaiMarks(descriptionText);

            panelRect.gameObject.SetActive(false);
        }

        public void Show(string title, string description, Vector2 screenPosition)
        {
            if (panelRect == null) BuildUI();

            if (titleText != null) titleText.text = title;
            if (descriptionText != null) descriptionText.text = description;

            panelRect.gameObject.SetActive(true);
            UpdatePosition(screenPosition);
        }

        public void UpdatePosition(Vector2 screenPosition)
        {
            if (panelRect == null || !panelRect.gameObject.activeSelf) return;

            currentScreenPos = screenPosition;

            // Convert screen position to canvas space
            Vector2 pos = screenPosition;
            float margin = 20f;
            float w = panelRect.rect.width > 0 ? panelRect.rect.width : 360f;
            float h = panelRect.rect.height > 0 ? panelRect.rect.height : 120f;

            // Pivot adjustment so tooltip stays inside screen bounds
            float pivotX = 0f;
            float pivotY = 0f;
            float offsetX = 16f;
            float offsetY = 16f;

            if (pos.x + w + margin > Screen.width)
            {
                pivotX = 1f;
                offsetX = -16f;
            }
            if (pos.y + h + margin > Screen.height)
            {
                pivotY = 1f;
                offsetY = -16f;
            }

            panelRect.pivot = new Vector2(pivotX, pivotY);
            panelRect.position = pos + new Vector2(offsetX, offsetY);
        }

        public void Hide()
        {
            if (panelRect != null) panelRect.gameObject.SetActive(false);
        }
    }
}
