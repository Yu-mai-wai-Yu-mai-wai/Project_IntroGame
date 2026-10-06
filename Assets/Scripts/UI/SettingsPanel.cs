using System;
using System.Collections.Generic;
using TawanOS.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TawanOS.UI
{
    /// <summary>
    /// The settings window (plan task B2), built in code so every scene opens the same one: the main menu now,
    /// the Pause menu later. Three tabs (เสียง / ภาพ / เกมเพลย์); every change is saved and applied at once
    /// through <see cref="GameSettings"/>. A display-mode or resolution change asks to be kept and reverts after
    /// 10 seconds otherwise, so a screen the monitor cannot show fixes itself. Esc closes the window (or reverts
    /// a pending display change). Works while Time.timeScale is 0.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        public const int SortingOrder = 500;
        private const float RevertSeconds = 10f;

        public static event Action OnClosed;

        public static bool IsOpen => current != null;

        private static SettingsPanel current;

        private static readonly string[] TabNames = { "เสียง", "ภาพ", "เกมเพลย์" };
        private static readonly string[] VolumeLabels = { "เสียงรวม", "เพลง", "เสียงบรรยากาศ", "เสียงประกอบ" };
        private static readonly string[] DisplayModeNames = { "เต็มจอ", "เต็มจอแบบไร้ขอบ", "หน้าต่าง" };
        private static readonly string[] TextSpeedNames = { "ช้า", "ปกติ", "เร็ว", "ทันที" };
        private static readonly Dictionary<string, string> QualityNames = new Dictionary<string, string>
        {
            { "Mobile", "ต่ำ" }, { "Low", "ต่ำ" }, { "Medium", "กลาง" }, { "PC", "สูง" }, { "High", "สูง" }, { "Ultra", "สูงสุด" },
        };

        private UIThemeSO theme;
        private readonly List<GameObject> pages = new List<GameObject>();
        private readonly List<Image> tabImages = new List<Image>();
        private readonly List<Action> refreshers = new List<Action>();
        private int currentTab;

        // Pending display change, reverted unless confirmed
        private GameObject confirmDialog;
        private TextMeshProUGUI confirmText;
        private float revertTimer;
        private DisplayMode previousMode;
        private int previousResolution;

        /// <summary>Opens the window in the current scene, or returns the one already open.</summary>
        public static SettingsPanel Open()
        {
            if (current != null) return current;

            var canvas = UiFactory.CreateOverlayCanvas("SettingsPanel", SortingOrder);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var group = canvas.GetComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            group.interactable = true;

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(canvas.transform, false);
            }

            current = canvas.gameObject.AddComponent<SettingsPanel>();
            current.Build();
            return current;
        }

        public void Close()
        {
            if (confirmDialog != null && confirmDialog.activeSelf) RevertDisplay();
            GameSettings.Save();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (current != this) return;
            current = null;
            OnClosed?.Invoke();
        }

        private void Update()
        {
            bool confirming = confirmDialog != null && confirmDialog.activeSelf;
            if (confirming)
            {
                revertTimer -= Time.unscaledDeltaTime;
                confirmText.text = $"ใช้การตั้งค่าหน้าจอนี้ไหม?\n<size=75%>จะกลับเป็นค่าเดิมใน {Mathf.CeilToInt(Mathf.Max(0f, revertTimer))} วินาที</size>";
                if (revertTimer <= 0f || Input.GetKeyDown(KeyCode.Escape)) RevertDisplay();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        // ---------------------------------------------------------------- layout (1920x1080 reference)

        private void Build()
        {
            theme = UIThemeSO.Current;

            var dim = UiFactory.CreateImage("Dim", transform, new Color(0f, 0f, 0f, 0.75f));
            UiFactory.Stretch(dim.rectTransform, 0f);
            dim.raycastTarget = true; // blocks clicks on the scene behind

            var window = UiFactory.CreateImage("Window", transform, theme.panel);
            window.raycastTarget = true;
            var w = window.rectTransform;
            Place(w, Vector2.zero, new Vector2(1100f, 800f));
            window.gameObject.AddComponent<Outline>().effectColor = theme.crimson;

            var title = UiFactory.CreateText(w, "Title", "ตั้งค่า", theme.titleSize * 1.2f, theme.accent, TextAlignmentOptions.Center, theme.titleFont);
            Place(title.rectTransform, new Vector2(0f, 340f), new Vector2(1000f, 80f));

            for (int i = 0; i < TabNames.Length; i++)
            {
                int tab = i;
                var button = TextButton(w, "Tab_" + TabNames[i], TabNames[i], new Vector2(-260f + i * 260f, 265f), new Vector2(240f, 58f), () => ShowTab(tab));
                tabImages.Add((Image)button.targetGraphic);
                var page = UiFactory.CreateRect("Page_" + TabNames[i], w);
                UiFactory.Stretch(page, 0f);
                pages.Add(page.gameObject);
            }

            BuildAudioPage(pages[0].transform);
            BuildDisplayPage(pages[1].transform);
            BuildGameplayPage(pages[2].transform);

            TextButton(w, "ResetButton", "คืนค่าเริ่มต้น", new Vector2(-170f, -330f), new Vector2(280f, 60f), ResetCurrentTab);
            TextButton(w, "CloseButton", "ปิด", new Vector2(170f, -330f), new Vector2(280f, 60f), Close);

            BuildConfirmDialog(w);
            ShowTab(0);
        }

        private void BuildAudioPage(Transform page)
        {
            for (int i = 0; i < VolumeLabels.Length; i++)
            {
                VolumeRow(page, RowY(i), VolumeLabels[i], (VolumeChannel)i);
            }
            ToggleRow(page, RowY(4), "ปิดเสียงเมื่อสลับไปหน้าต่างอื่น", () => GameSettings.MuteInBackground, v => GameSettings.MuteInBackground = v);
        }

        private void BuildDisplayPage(Transform page)
        {
            SelectorRow(page, RowY(0), "โหมดหน้าจอ", () => DisplayModeNames[(int)GameSettings.Mode], step =>
            {
                RememberDisplay();
                GameSettings.Mode = (DisplayMode)Wrap((int)GameSettings.Mode + step, DisplayModeNames.Length);
                AskToKeepDisplay();
            });
            SelectorRow(page, RowY(1), "ความละเอียด", () =>
            {
                var size = GameSettings.Resolutions[GameSettings.ResolutionIndex];
                return $"{size.x} x {size.y}";
            }, step =>
            {
                RememberDisplay();
                GameSettings.ResolutionIndex = Wrap(GameSettings.ResolutionIndex + step, GameSettings.Resolutions.Count);
                AskToKeepDisplay();
            });
            ToggleRow(page, RowY(2), "V-Sync", () => GameSettings.VSync, v => GameSettings.VSync = v);
            var frameLimit = SelectorRow(page, RowY(3), "จำกัดเฟรมเรต", () =>
            {
                int limit = GameSettings.FrameLimits[GameSettings.FrameLimitIndex];
                return limit == 0 ? "ไม่จำกัด" : $"{limit} FPS";
            }, step => GameSettings.FrameLimitIndex = Wrap(GameSettings.FrameLimitIndex + step, GameSettings.FrameLimits.Length));
            // The cap does nothing while V-Sync holds the frame rate to the monitor
            refreshers.Add(() => frameLimit.SetInteractable(!GameSettings.VSync));
            SelectorRow(page, RowY(4), "คุณภาพกราฟิก", () => QualityName(GameSettings.QualityIndex),
                step => GameSettings.QualityIndex = Wrap(GameSettings.QualityIndex + step, QualitySettings.names.Length));
            ToggleRow(page, RowY(5), "แสดง FPS", () => GameSettings.ShowFps, v => GameSettings.ShowFps = v);

            if (Application.isEditor)
            {
                var hint = UiFactory.CreateText(page, "EditorHint", "ใน Unity Editor โหมดหน้าจอ ความละเอียด V-Sync และคุณภาพจะบันทึกไว้ แต่มีผลเมื่อเล่นจากไฟล์เกม (.exe)",
                    theme.labelSize, theme.text * 0.8f, TextAlignmentOptions.Center, theme.bodyFont);
                Place(hint.rectTransform, new Vector2(0f, -262f), new Vector2(1000f, 40f));
            }
        }

        private void BuildGameplayPage(Transform page)
        {
            SelectorRow(page, RowY(0), "ความเร็วข้อความ", () => TextSpeedNames[(int)GameSettings.TextSpeed],
                step => GameSettings.TextSpeed = (TextSpeed)Wrap((int)GameSettings.TextSpeed + step, TextSpeedNames.Length));
            ToggleRow(page, RowY(1), "ลดการสั่นของหน้าจอ", () => GameSettings.ReduceMotion, v => GameSettings.ReduceMotion = v);
        }

        private static float RowY(int row) => 180f - row * 78f;

        private static int Wrap(int value, int count) => ((value % count) + count) % count;

        private static string QualityName(int index)
        {
            string name = QualitySettings.names[index];
            return QualityNames.TryGetValue(name, out var thai) ? thai : name;
        }

        // ---------------------------------------------------------------- tabs, refresh, reset

        private void ShowTab(int tab)
        {
            currentTab = tab;
            for (int i = 0; i < pages.Count; i++)
            {
                pages[i].SetActive(i == tab);
                var button = tabImages[i].GetComponent<Button>();
                var colors = button.colors;
                colors.normalColor = i == tab ? Color.Lerp(theme.crimson, theme.accent, 0.35f) : theme.black;
                button.colors = colors;
                // Re-enabling makes the Selectable redraw its tint now instead of on the next hover
                button.enabled = false;
                button.enabled = true;
            }
            Refresh();
        }

        private void Refresh()
        {
            foreach (var refresh in refreshers) refresh();
        }

        private void ResetCurrentTab()
        {
            switch (currentTab)
            {
                case 0: GameSettings.ResetAudio(); break;
                case 1: GameSettings.ResetDisplay(); break;
                default: GameSettings.ResetGameplay(); break;
            }
            Refresh();
        }

        // ---------------------------------------------------------------- display confirmation

        private void RememberDisplay()
        {
            // A second change while the dialog is up keeps the first "before" values
            if (confirmDialog.activeSelf) return;
            previousMode = GameSettings.Mode;
            previousResolution = GameSettings.ResolutionIndex;
        }

        private void AskToKeepDisplay()
        {
            Refresh();
            if (Application.isEditor) return; // nothing changed on screen, nothing to confirm
            revertTimer = RevertSeconds;
            confirmDialog.SetActive(true);
        }

        private void KeepDisplay()
        {
            confirmDialog.SetActive(false);
        }

        private void RevertDisplay()
        {
            confirmDialog.SetActive(false);
            GameSettings.Mode = previousMode;
            GameSettings.ResolutionIndex = previousResolution;
            Refresh();
        }

        private void BuildConfirmDialog(RectTransform window)
        {
            var dim = UiFactory.CreateImage("ConfirmDisplay", window, new Color(0f, 0f, 0f, 0.8f));
            dim.raycastTarget = true;
            UiFactory.Stretch(dim.rectTransform, 0f);
            confirmDialog = dim.gameObject;

            var box = UiFactory.CreateImage("Box", dim.rectTransform, theme.panel);
            box.raycastTarget = true;
            Place(box.rectTransform, Vector2.zero, new Vector2(640f, 300f));
            box.gameObject.AddComponent<Outline>().effectColor = theme.accent;

            confirmText = UiFactory.CreateText(box.rectTransform, "Question", string.Empty, theme.bodySize * 1.25f, theme.text, TextAlignmentOptions.Center, theme.bodyFont);
            Place(confirmText.rectTransform, new Vector2(0f, 50f), new Vector2(600f, 140f));
            TextButton(box.rectTransform, "KeepButton", "เก็บไว้", new Vector2(-140f, -90f), new Vector2(240f, 60f), KeepDisplay);
            TextButton(box.rectTransform, "RevertButton", "ย้อนกลับ", new Vector2(140f, -90f), new Vector2(240f, 60f), RevertDisplay);
            confirmDialog.SetActive(false);
        }

        // ---------------------------------------------------------------- rows

        private void RowLabel(Transform parent, float y, string text)
        {
            var label = UiFactory.CreateText(parent, "Label_" + text, text, theme.bodySize * 1.15f, theme.text, TextAlignmentOptions.MidlineLeft, theme.bodyFont);
            Place(label.rectTransform, new Vector2(-290f, y), new Vector2(480f, 64f));
        }

        private void VolumeRow(Transform parent, float y, string label, VolumeChannel channel)
        {
            RowLabel(parent, y, label);

            var root = UiFactory.CreateRect("Slider_" + channel, parent);
            Place(root, new Vector2(180f, y), new Vector2(380f, 44f));
            var slider = root.gameObject.AddComponent<Slider>();

            var track = UiFactory.CreateImage("Track", root, theme.black);
            track.raycastTarget = true;
            Band(track.rectTransform, 0.35f, 0.65f, 0f);

            var fillArea = UiFactory.CreateRect("FillArea", root);
            Band(fillArea, 0.35f, 0.65f, 0f);
            var fill = UiFactory.CreateImage("Fill", fillArea, theme.crimson);
            UiFactory.Stretch(fill.rectTransform, 0f);
            slider.fillRect = fill.rectTransform;

            var handleArea = UiFactory.CreateRect("HandleArea", root);
            Band(handleArea, 0f, 1f, 12f);
            var handle = UiFactory.CreateImage("Handle", handleArea, theme.accent);
            handle.raycastTarget = true;
            handle.rectTransform.sizeDelta = new Vector2(24f, 0f);
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;

            var value = UiFactory.CreateText(parent, "Value_" + channel, string.Empty, theme.bodySize, theme.accent, TextAlignmentOptions.MidlineRight, theme.bodyFont);
            Place(value.rectTransform, new Vector2(430f, y), new Vector2(90f, 64f));

            slider.onValueChanged.AddListener(v =>
            {
                GameSettings.SetVolume(channel, v);
                value.text = Percent(v);
            });
            refreshers.Add(() =>
            {
                float v = GameSettings.GetVolume(channel);
                slider.SetValueWithoutNotify(v);
                value.text = Percent(v);
            });
        }

        private void ToggleRow(Transform parent, float y, string label, Func<bool> get, Action<bool> set)
        {
            RowLabel(parent, y, label);

            var box = UiFactory.CreateImage("Toggle_" + label, parent, theme.black);
            box.raycastTarget = true;
            Place(box.rectTransform, new Vector2(14f, y), new Vector2(48f, 48f));
            box.gameObject.AddComponent<Outline>().effectColor = theme.accent;
            var check = UiFactory.CreateImage("Check", box.rectTransform, theme.accent);
            UiFactory.Stretch(check.rectTransform, 10f);

            var toggle = box.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.onValueChanged.AddListener(v =>
            {
                set(v);
                Refresh();
            });
            refreshers.Add(() => toggle.SetIsOnWithoutNotify(get()));
        }

        /// <summary>"&lt; value &gt;" picker for a list of choices.</summary>
        private Selector SelectorRow(Transform parent, float y, string label, Func<string> get, Action<int> step)
        {
            RowLabel(parent, y, label);

            var selector = new Selector
            {
                previous = TextButton(parent, "Previous_" + label, "<", new Vector2(18f, y), new Vector2(56f, 56f), () => { step(-1); Refresh(); }),
                value = UiFactory.CreateText(parent, "Value_" + label, string.Empty, theme.bodySize * 1.1f, theme.text, TextAlignmentOptions.Center, theme.bodyFont),
                next = TextButton(parent, "Next_" + label, ">", new Vector2(400f, y), new Vector2(56f, 56f), () => { step(1); Refresh(); }),
            };
            Place(selector.value.rectTransform, new Vector2(209f, y), new Vector2(300f, 64f));
            refreshers.Add(() => selector.value.text = get());
            return selector;
        }

        private class Selector
        {
            public Button previous;
            public Button next;
            public TextMeshProUGUI value;

            public void SetInteractable(bool on)
            {
                previous.interactable = on;
                next.interactable = on;
                value.alpha = on ? 1f : 0.4f;
            }
        }

        private Button TextButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Action onClick)
        {
            var image = UiFactory.CreateImage(name, parent, Color.white);
            image.raycastTarget = true;
            Place(image.rectTransform, position, size);
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = theme.crimson;
            colors.highlightedColor = Color.Lerp(theme.crimson, theme.accent, 0.35f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Color.Lerp(theme.crimson, theme.accent, 0.6f);
            colors.disabledColor = new Color(theme.crimson.r, theme.crimson.g, theme.crimson.b, 0.35f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());

            var text = UiFactory.CreateText(image.rectTransform, "Label", label, theme.bodySize * 1.15f, theme.text, TextAlignmentOptions.Center, theme.bodyFont);
            UiFactory.Stretch(text.rectTransform, 0f);
            return button;
        }

        private static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";

        // Centered in the parent at a pixel offset
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        // Full width, a horizontal band of the parent's height
        private static void Band(RectTransform rect, float yMin, float yMax, float inset)
        {
            rect.anchorMin = new Vector2(0f, yMin);
            rect.anchorMax = new Vector2(1f, yMax);
            rect.offsetMin = new Vector2(inset, 0f);
            rect.offsetMax = new Vector2(-inset, 0f);
        }
    }
}
