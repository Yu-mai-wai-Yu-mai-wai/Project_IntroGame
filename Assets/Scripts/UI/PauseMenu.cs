using System;
using System.Collections.Generic;
using TawanOS.CardEngine;
using TawanOS.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.UI
{
    /// <summary>
    /// Esc during play pauses the game (plan task B3 / F7): Time.timeScale = 0 and a menu with เล่นต่อ, ตั้งค่า,
    /// กลับเมนูหลัก and ออกจากเกม (the last two ask first). Created before the first scene and kept across scenes,
    /// like GameFlowManager; does nothing in the main menu and the intro story.
    /// Esc only pauses when nothing else used it (see <see cref="EscapeKey"/>). While paused, gameplay input
    /// checks <see cref="IsPaused"/>, and camera mouse events (OnMouseDown etc.) are switched off.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public const int SortingOrder = 400; // under SettingsPanel (500)

        private static readonly HashSet<string> ScenesWithoutPause = new HashSet<string> { GameFlowManager.MainMenuSceneName, "StoryScene" };

        public static bool IsPaused { get; private set; }

        public static event Action<bool> OnPauseChanged;

        private static PauseMenu instance;

        private float timeScaleBeforePause = 1f;
        private readonly Dictionary<Camera, int> cameraMasks = new Dictionary<Camera, int>();
        private UIThemeSO theme;
        private Canvas canvas;
        private GameObject confirmDialog;
        private TextMeshProUGUI confirmText;
        private Action confirmAction;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("PauseMenu");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PauseMenu>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        // Never carry a pause (or a frozen clock) into the next scene
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsPaused) Resume();
        }

        // LateUpdate: every Update has had its chance to use Esc first
        private void LateUpdate()
        {
            if (!EscapeKey.PressedAndUnhandled) return;

            if (IsPaused)
            {
                if (confirmDialog != null && confirmDialog.activeSelf) confirmDialog.SetActive(false);
                else Resume();
            }
            else if (CanPause())
            {
                Pause();
            }
        }

        private static bool CanPause()
        {
            return !ScenesWithoutPause.Contains(SceneManager.GetActiveScene().name) && !SettingsPanel.IsOpen;
        }

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;

            // OnMouseDown / OnMouseEnter come from cameras, not the UI, so the overlay alone would not block them
            cameraMasks.Clear();
            foreach (var cam in Camera.allCameras)
            {
                cameraMasks[cam] = cam.eventMask;
                cam.eventMask = 0;
            }

            Build();
            OnPauseChanged?.Invoke(true);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = timeScaleBeforePause;

            foreach (var pair in cameraMasks)
            {
                if (pair.Key != null) pair.Key.eventMask = pair.Value;
            }
            cameraMasks.Clear();

            if (canvas != null) Destroy(canvas.gameObject);
            canvas = null;
            OnPauseChanged?.Invoke(false);
        }

        private void GoToMainMenu()
        {
            Resume();
            GameFlowManager.Instance.ReturnToMainMenu();
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------------------------------------------------------- layout (1920x1080 reference)

        private void Build()
        {
            theme = UIThemeSO.Current;
            canvas = UiFactory.CreateOverlayCanvas("PauseCanvas", SortingOrder, transform);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var group = canvas.GetComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            group.interactable = true;

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(canvas.transform, false);
            }

            var dim = UiFactory.CreateImage("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.7f));
            UiFactory.Stretch(dim.rectTransform, 0f);
            dim.raycastTarget = true;

            var window = UiFactory.CreateImage("Window", canvas.transform, theme.panel);
            window.raycastTarget = true;
            Place(window.rectTransform, Vector2.zero, new Vector2(560f, 600f));
            window.gameObject.AddComponent<Outline>().effectColor = theme.crimson;
            var w = window.rectTransform;

            var title = UiFactory.CreateText(w, "Title", "หยุดเกม", theme.titleSize * 1.2f, theme.accent, TextAlignmentOptions.Center, theme.titleFont);
            Place(title.rectTransform, new Vector2(0f, 220f), new Vector2(500f, 80f));

            var resume = MenuButton(w, "ResumeButton", "เล่นต่อ", 110f, Resume);
            MenuButton(w, "SettingsButton", "ตั้งค่า", 20f, () => SettingsPanel.Open());
            MenuButton(w, "MainMenuButton", "กลับเมนูหลัก", -70f, () => Ask("กลับเมนูหลัก?", GoToMainMenu));
            MenuButton(w, "QuitButton", "ออกจากเกม", -160f, () => Ask("ออกจากเกม?", QuitGame));

            var hint = UiFactory.CreateText(w, "Hint", "กด Esc เพื่อเล่นต่อ", theme.labelSize, theme.text * 0.8f, TextAlignmentOptions.Center, theme.bodyFont);
            Place(hint.rectTransform, new Vector2(0f, -250f), new Vector2(500f, 40f));

            BuildConfirmDialog(canvas.transform);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(resume.gameObject);
        }

        private void Ask(string question, Action onYes)
        {
            // Run progress is saved on every change; a fight in progress is replayed from its start
            string note = CombatManager.Instance != null
                ? "การต่อสู้นี้จะเริ่มใหม่เมื่อกลับมาเล่นต่อ"
                : "ความคืบหน้าถูกบันทึกไว้แล้ว";
            confirmText.text = $"{question}\n<size=75%>{note}</size>";
            confirmAction = onYes;
            confirmDialog.SetActive(true);
        }

        private void BuildConfirmDialog(Transform parent)
        {
            var dim = UiFactory.CreateImage("Confirm", parent, new Color(0f, 0f, 0f, 0.6f));
            dim.raycastTarget = true;
            UiFactory.Stretch(dim.rectTransform, 0f);
            confirmDialog = dim.gameObject;

            var box = UiFactory.CreateImage("Box", dim.rectTransform, theme.panel);
            box.raycastTarget = true;
            Place(box.rectTransform, Vector2.zero, new Vector2(620f, 300f));
            box.gameObject.AddComponent<Outline>().effectColor = theme.accent;

            confirmText = UiFactory.CreateText(box.rectTransform, "Question", string.Empty, theme.bodySize * 1.25f, theme.text, TextAlignmentOptions.Center, theme.bodyFont);
            Place(confirmText.rectTransform, new Vector2(0f, 50f), new Vector2(580f, 140f));
            CreateButton(box.rectTransform, "YesButton", "ยืนยัน", new Vector2(-140f, -90f), new Vector2(240f, 60f), () => confirmAction?.Invoke());
            CreateButton(box.rectTransform, "NoButton", "ยกเลิก", new Vector2(140f, -90f), new Vector2(240f, 60f), () => confirmDialog.SetActive(false));
            confirmDialog.SetActive(false);
        }

        private Button MenuButton(RectTransform parent, string name, string label, float y, Action onClick)
        {
            return CreateButton(parent, name, label, new Vector2(0f, y), new Vector2(400f, 70f), onClick);
        }

        private Button CreateButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size, Action onClick)
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
            button.colors = colors;
            button.onClick.AddListener(() => onClick());

            var text = UiFactory.CreateText(image.rectTransform, "Label", label, theme.bodySize * 1.25f, theme.text, TextAlignmentOptions.Center, theme.bodyFont);
            UiFactory.Stretch(text.rectTransform, 0f);
            return button;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
    }
}
