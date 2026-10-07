using TawanOS.CardEngine;
using TawanOS.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Esc pauses the game in every gameplay scene (plan B3): time stops, with Resume, Settings and Back to Main
    /// Menu. Created automatically before the first scene, like GameFlowManager. The run autosaves on every change,
    /// so leaving for the menu keeps it for "Continue".
    /// Esc is also the cancel key of card dragging, targeting and the card / graveyard panels; while any of those
    /// is active (or closed during this very frame) Esc belongs to them and does not pause.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Instance { get; private set; }

        public static bool IsPaused => Instance != null && Instance.paused;

        private GameObject root;
        private Button resumeButton;
        private SettingsPanelUI settings;
        private bool paused;
        private float previousTimeScale = 1f;
        private bool settingsWasOpen;
        private int settingsClosedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("PauseMenu").AddComponent<PauseMenu>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);   // edit-mode tests cannot call it
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this)
            {
                if (paused) Time.timeScale = previousTimeScale;
                Instance = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // A scene change while paused (Back to Main Menu) must never leave time frozen
            if (paused) ResumeGame();
            if (root != null) Destroy(root);
            root = null;
            settings = null;
        }

        private void Update()
        {
            bool settingsOpen = settings != null && settings.IsOpen;
            if (settingsWasOpen && !settingsOpen) settingsClosedFrame = Time.frameCount;
            settingsWasOpen = settingsOpen;

            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (!IsPausableScene(SceneManager.GetActiveScene().name)) return;

            if (paused)
            {
                // Esc inside Settings only closes Settings (including the frame it just closed)
                if (!settingsOpen && Time.frameCount != settingsClosedFrame) ResumeGame();
                return;
            }

            if (ShouldIgnoreEscape(CardDetailPanelUI.BlocksInput, CardPlayController3D.BlocksInput,
                    CardTargeting3D.BlocksInput, GraveyardPanelUI.BlocksInput, false, GameOverScreen.IsShowing))
                return;

            PauseGame();
        }

        // ---------------------------------------------------------------- rules (pure, tested)

        /// <summary>Gameplay scenes only: not the menu, not the victory screen.</summary>
        public static bool IsPausableScene(string sceneName)
        {
            return !string.IsNullOrEmpty(sceneName)
                && sceneName != GameFlowManager.MainMenuSceneName
                && sceneName != GameFlowManager.VictorySceneName
                && sceneName != "IntroStoryScene";
        }

        /// <summary>True when something else owns the Esc key this frame.</summary>
        public static bool ShouldIgnoreEscape(bool cardDetail, bool cardHeld, bool targeting, bool graveyard, bool settingsOpen, bool gameOver)
        {
            return cardDetail || cardHeld || targeting || graveyard || settingsOpen || gameOver;
        }

        // ---------------------------------------------------------------- pause / resume

        public void PauseGame()
        {
            if (paused) return;
            paused = true;
            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;

            EnsureEventSystem();
            if (root == null) Build();
            root.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
        }

        public void ResumeGame()
        {
            if (!paused) return;
            paused = false;
            Time.timeScale = previousTimeScale;
            if (settings != null && settings.IsOpen) settings.Close();
            if (root != null) root.SetActive(false);
        }

        private void GoToMainMenu()
        {
            ResumeGame();
            if (Application.CanStreamedLevelBeLoaded(GameFlowManager.MainMenuSceneName))
                SceneManager.LoadScene(GameFlowManager.MainMenuSceneName, LoadSceneMode.Single);
        }

        // ---------------------------------------------------------------- UI

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.transform.SetParent(null);
        }

        private void Build()
        {
            var theme = UIThemeSO.Current;
            var canvas = UiFactory.CreateOverlayCanvas("PauseCanvas", 500, transform);
            var group = canvas.GetComponent<CanvasGroup>();
            group.blocksRaycasts = true;     // blocks clicks on the game behind
            group.interactable = true;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            root = canvas.gameObject;

            var dim = UiFactory.CreateImage("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.7f));
            dim.raycastTarget = true;
            UiFactory.Stretch(dim.rectTransform, 0f);

            var window = UiFactory.CreateRect("Window", canvas.transform);
            window.anchorMin = new Vector2(0.36f, 0.26f);
            window.anchorMax = new Vector2(0.64f, 0.74f);
            window.offsetMin = window.offsetMax = Vector2.zero;
            var border = window.gameObject.AddComponent<Image>();
            border.color = theme.crimson;
            var body = UiFactory.CreateImage("Body", window, theme.panel);
            UiFactory.Stretch(body.rectTransform, 3f);

            var title = UiFactory.CreateText(window, "Title", "หยุดเกม", theme.titleSize, theme.text,
                TextAlignmentOptions.Center, theme.titleFont != null ? theme.titleFont : theme.bodyFont);
            title.rectTransform.anchorMin = new Vector2(0.05f, 0.78f);
            title.rectTransform.anchorMax = new Vector2(0.95f, 0.96f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;

            resumeButton = UiFactory.CreateButton(window, "ResumeButton", "เล่นต่อ", theme, new Vector2(0.12f, 0.56f), new Vector2(0.88f, 0.72f));
            var settingsButton = UiFactory.CreateButton(window, "SettingsButton", "ตั้งค่า", theme, new Vector2(0.12f, 0.36f), new Vector2(0.88f, 0.52f));
            var menuButton = UiFactory.CreateButton(window, "MainMenuButton", "กลับเมนูหลัก", theme, new Vector2(0.12f, 0.16f), new Vector2(0.88f, 0.32f));

            resumeButton.onClick.AddListener(ResumeGame);
            settingsButton.onClick.AddListener(() =>
            {
                if (settings == null) settings = SettingsPanelUI.Create(canvas.transform);
                settings.Open();
            });
            menuButton.onClick.AddListener(GoToMainMenu);
        }
    }
}
