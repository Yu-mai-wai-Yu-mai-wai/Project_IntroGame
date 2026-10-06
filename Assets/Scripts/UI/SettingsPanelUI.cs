using TawanOS.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TawanOS.UI
{
    /// <summary>
    /// Settings window: master / music / effects volume sliders and a fullscreen toggle. Built in code so the
    /// main menu and the pause menu share it (no prefab to keep in sync). Changes apply and save immediately.
    /// Everything is a standard Selectable, so it works with Tab / arrow keys; Esc closes it.
    /// </summary>
    public class SettingsPanelUI : MonoBehaviour
    {
        public Slider masterSlider, musicSlider, sfxSlider;
        public Toggle fullscreenToggle;
        public Button closeButton;

        private TextMeshProUGUI masterValue, musicValue, sfxValue;

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>Builds the window under <paramref name="parent"/> (a Canvas or a child of one), closed.</summary>
        public static SettingsPanelUI Create(Transform parent)
        {
            var theme = UIThemeSO.Current;

            var root = UiFactory.CreateRect("SettingsPanel", parent);
            UiFactory.Stretch(root, 0f);
            root.SetAsLastSibling();
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.75f);   // raycastTarget stays on: blocks clicks on the menu behind
            var panel = root.gameObject.AddComponent<SettingsPanelUI>();

            var window = UiFactory.CreateRect("Window", root);
            window.anchorMin = new Vector2(0.28f, 0.2f);
            window.anchorMax = new Vector2(0.72f, 0.8f);
            window.offsetMin = window.offsetMax = Vector2.zero;
            var border = window.gameObject.AddComponent<Image>();
            border.color = theme.crimson;
            var body = UiFactory.CreateImage("Body", window, theme.panel);
            UiFactory.Stretch(body.rectTransform, 3f);

            var title = Label(window, "Title", "ตั้งค่า", theme, theme.titleSize, 0.05f, 0.95f, 0.86f, 0.98f, TextAlignmentOptions.Center);
            if (theme.titleFont != null) title.font = theme.titleFont;

            panel.masterSlider = Row(window, theme, 0, "เสียงรวม", AudioChannel.Master, out panel.masterValue);
            panel.musicSlider = Row(window, theme, 1, "เพลง", AudioChannel.Bgm, out panel.musicValue);
            panel.sfxSlider = Row(window, theme, 2, "เสียงเอฟเฟกต์", AudioChannel.Sfx, out panel.sfxValue);

            Label(window, "FullscreenLabel", "เต็มจอ", theme, theme.bodySize, 0.06f, 0.36f, 0.30f, 0.43f, TextAlignmentOptions.MidlineLeft);
            panel.fullscreenToggle = MakeToggle(window, theme);
            panel.fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
            panel.fullscreenToggle.onValueChanged.AddListener(GameSettings.SetFullscreen);

            panel.closeButton = MakeButton(window, theme, "ปิด");
            panel.closeButton.onClick.AddListener(panel.Close);

            panel.masterSlider.onValueChanged.AddListener(v => panel.OnVolume(AudioChannel.Master, v));
            panel.musicSlider.onValueChanged.AddListener(v => panel.OnVolume(AudioChannel.Bgm, v));
            panel.sfxSlider.onValueChanged.AddListener(v => panel.OnVolume(AudioChannel.Sfx, v));

            root.gameObject.SetActive(false);
            return panel;
        }

        public void Open()
        {
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            masterSlider.SetValueWithoutNotify(AudioManager.LoadVolume(AudioChannel.Master));
            musicSlider.SetValueWithoutNotify(AudioManager.LoadVolume(AudioChannel.Bgm));
            sfxSlider.SetValueWithoutNotify(AudioManager.LoadVolume(AudioChannel.Sfx));
            fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
            ShowPercent();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(masterSlider.gameObject);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        private void OnVolume(AudioChannel channel, float value)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetVolume(channel, value);
            else AudioManager.SaveVolume(channel, value);
            ShowPercent();
        }

        private void ShowPercent()
        {
            if (masterValue != null) masterValue.text = Percent(masterSlider.value);
            if (musicValue != null) musicValue.text = Percent(musicSlider.value);
            if (sfxValue != null) sfxValue.text = Percent(sfxSlider.value);
        }

        private static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";

        // ---------------------------------------------------------------- building blocks

        private static TextMeshProUGUI Label(RectTransform parent, string name, string text, UIThemeSO theme, float size,
            float x0, float x1, float y0, float y1, TextAlignmentOptions align)
        {
            var t = UiFactory.CreateText(parent, name, text, size, theme.text, align, theme.bodyFont);
            t.rectTransform.anchorMin = new Vector2(x0, y0);
            t.rectTransform.anchorMax = new Vector2(x1, y1);
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            return t;
        }

        private static Slider Row(RectTransform window, UIThemeSO theme, int index, string label, AudioChannel channel, out TextMeshProUGUI valueText)
        {
            float top = 0.80f - index * 0.15f;
            Label(window, label + "Label", label, theme, theme.bodySize, 0.06f, 0.34f, top - 0.11f, top, TextAlignmentOptions.MidlineLeft);
            valueText = Label(window, label + "Value", "100%", theme, theme.bodySize, 0.82f, 0.95f, top - 0.11f, top, TextAlignmentOptions.MidlineRight);

            var slider = MakeSlider(window, theme, label + "Slider");
            var r = (RectTransform)slider.transform;
            r.anchorMin = new Vector2(0.36f, top - 0.11f);
            r.anchorMax = new Vector2(0.80f, top);
            r.offsetMin = r.offsetMax = Vector2.zero;
            slider.SetValueWithoutNotify(AudioManager.LoadVolume(channel));
            valueText.text = Percent(slider.value);
            return slider;
        }

        private static Slider MakeSlider(RectTransform parent, UIThemeSO theme, string name)
        {
            var root = UiFactory.CreateRect(name, parent);
            var slider = root.gameObject.AddComponent<Slider>();

            var bg = UiFactory.CreateImage("Background", root, theme.crimson);
            bg.rectTransform.anchorMin = new Vector2(0f, 0.35f);
            bg.rectTransform.anchorMax = new Vector2(1f, 0.65f);
            bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;

            var fillArea = UiFactory.CreateRect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0f, 0.35f);
            fillArea.anchorMax = new Vector2(1f, 0.65f);
            fillArea.offsetMin = new Vector2(12f, 0f);
            fillArea.offsetMax = new Vector2(-12f, 0f);
            var fill = UiFactory.CreateImage("Fill", fillArea, theme.accent);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

            var handleArea = UiFactory.CreateRect("Handle Slide Area", root);
            UiFactory.Stretch(handleArea, 0f);
            handleArea.offsetMin = new Vector2(12f, 0f);
            handleArea.offsetMax = new Vector2(-12f, 0f);
            var handle = UiFactory.CreateImage("Handle", handleArea, theme.accent);
            handle.raycastTarget = true;
            handle.rectTransform.anchorMin = new Vector2(0f, 0f);
            handle.rectTransform.anchorMax = new Vector2(0f, 1f);
            handle.rectTransform.sizeDelta = new Vector2(28f, 0f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            return slider;
        }

        private static Toggle MakeToggle(RectTransform parent, UIThemeSO theme)
        {
            var root = UiFactory.CreateRect("FullscreenToggle", parent);
            root.anchorMin = new Vector2(0.36f, 0.30f);
            root.anchorMax = new Vector2(0.36f, 0.43f);
            root.pivot = new Vector2(0f, 0.5f);
            root.sizeDelta = new Vector2(56f, 0f);
            root.anchoredPosition = Vector2.zero;
            var toggle = root.gameObject.AddComponent<Toggle>();

            var box = UiFactory.CreateImage("Box", root, theme.crimson);
            box.raycastTarget = true;
            UiFactory.Stretch(box.rectTransform, 0f);
            var mark = UiFactory.CreateImage("Check", box.rectTransform, theme.accent);
            UiFactory.Stretch(mark.rectTransform, 10f);

            toggle.targetGraphic = box;
            toggle.graphic = mark;
            return toggle;
        }

        private static Button MakeButton(RectTransform parent, UIThemeSO theme, string text)
        {
            var rect = UiFactory.CreateRect("CloseButton", parent);
            rect.anchorMin = new Vector2(0.32f, 0.05f);
            rect.anchorMax = new Vector2(0.68f, 0.19f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var img = rect.gameObject.AddComponent<Image>();
            img.color = theme.crimson;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var label = UiFactory.CreateText(rect, "Label", text, theme.bodySize, theme.text, TextAlignmentOptions.Center, theme.bodyFont);
            UiFactory.Stretch(label.rectTransform, 0f);
            return button;
        }
    }
}
