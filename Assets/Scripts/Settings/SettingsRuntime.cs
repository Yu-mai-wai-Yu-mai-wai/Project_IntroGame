using TawanOS.UI;
using TMPro;
using UnityEngine;

namespace TawanOS.Settings
{
    /// <summary>
    /// The part of the settings that needs a live object: tells <see cref="GameSettings"/> when the window loses
    /// focus (mute in background) and draws the FPS counter. Created by GameSettings before the first scene and
    /// kept across scenes, like GameFlowManager.
    /// </summary>
    public class SettingsRuntime : MonoBehaviour
    {
        private const float FpsRefreshSeconds = 0.5f;

        private static SettingsRuntime instance;

        private Canvas fpsCanvas;
        private TextMeshProUGUI fpsText;
        private int frames;
        private float elapsed;

        public static void EnsureExists()
        {
            if (instance != null) return;
            var go = new GameObject("SettingsRuntime");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SettingsRuntime>();
        }

        private void OnEnable()
        {
            GameSettings.OnChanged += RefreshFpsVisibility;
        }

        private void OnDisable()
        {
            GameSettings.OnChanged -= RefreshFpsVisibility;
        }

        private void Start()
        {
            RefreshFpsVisibility();
        }

        private void OnApplicationFocus(bool focused)
        {
            GameSettings.SetApplicationFocus(focused);
        }

        private void Update()
        {
            if (fpsText == null || !fpsCanvas.gameObject.activeSelf) return;

            frames++;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < FpsRefreshSeconds) return;
            fpsText.text = $"FPS {Mathf.RoundToInt(frames / elapsed)}";
            frames = 0;
            elapsed = 0f;
        }

        private void RefreshFpsVisibility()
        {
            bool show = GameSettings.ShowFps;
            if (show && fpsCanvas == null) BuildFpsCounter();
            if (fpsCanvas != null) fpsCanvas.gameObject.SetActive(show);
        }

        // Top-left corner, above everything else, never blocks clicks
        private void BuildFpsCounter()
        {
            var theme = UIThemeSO.Current;
            fpsCanvas = UiFactory.CreateOverlayCanvas("FpsCounter", 1000, transform);
            fpsText = UiFactory.CreateText(fpsCanvas.transform, "Fps", "FPS", theme.labelSize, theme.accent, TextAlignmentOptions.TopLeft, theme.bodyFont);
            var rect = fpsText.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(12f, -8f);
            rect.sizeDelta = new Vector2(200f, 40f);
        }
    }
}
