using TawanOS.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.GameFlow
{
    /// <summary>What the Game Over page tells the player about the run that just ended.</summary>
    public struct GameOverSummary
    {
        public string enemyName;
        public int incense;
        public int deckSize;
        public int totalFloors;
        public int maxHp;

        public string ToText()
        {
            string by = string.IsNullOrEmpty(enemyName) ? "ศัตรู" : enemyName;
            return $"พ่ายแพ้ต่อ {by}\n\nธูปที่สะสม {incense}   •   สำรับ {deckSize} ใบ\nขวัญสูงสุด {maxHp}";
        }
    }

    /// <summary>
    /// Game Over page (plan B3): a message, a summary of the run and two buttons, Restart and Main Menu. It stays
    /// until the player chooses; nothing closes it on a timer.
    /// </summary>
    public class GameOverScreen : MonoBehaviour
    {
        public static GameOverScreen Instance { get; private set; }
        public static bool IsShowing => Instance != null;

        public Button restartButton;
        public Button menuButton;
        public TextMeshProUGUI summaryText;
        private int totalFloors;

        public static GameOverScreen Show(GameOverSummary summary)
        {
            if (Instance != null) Destroy(Instance.gameObject);

            var theme = UIThemeSO.Current;
            var canvas = UiFactory.CreateOverlayCanvas("GameOverCanvas", 600);
            var group = canvas.GetComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            group.interactable = true;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var screen = canvas.gameObject.AddComponent<GameOverScreen>();
            screen.totalFloors = summary.totalFloors;
            Instance = screen;

            var dim = UiFactory.CreateImage("Dim", canvas.transform, new Color(0.04f, 0f, 0f, 0.88f));
            dim.raycastTarget = true;
            UiFactory.Stretch(dim.rectTransform, 0f);

            var title = UiFactory.CreateText(canvas.transform, "Title", "ขวัญหลุด", theme.titleSize * 2f, theme.accent,
                TextAlignmentOptions.Center, theme.titleFont != null ? theme.titleFont : theme.bodyFont);
            Place(title.rectTransform, 0.2f, 0.8f, 0.68f, 0.86f);
            title.text = "ขวัญหลุด";

            screen.summaryText = UiFactory.CreateText(canvas.transform, "Summary", summary.ToText(), theme.bodySize * 1.2f, theme.text,
                TextAlignmentOptions.Center, theme.bodyFont);
            Place(screen.summaryText.rectTransform, 0.25f, 0.75f, 0.42f, 0.64f);

            screen.restartButton = UiFactory.CreateButton(canvas.transform, "RestartButton", "เริ่มใหม่", theme, new Vector2(0.30f, 0.24f), new Vector2(0.48f, 0.34f));
            screen.menuButton = UiFactory.CreateButton(canvas.transform, "MainMenuButton", "เมนูหลัก", theme, new Vector2(0.52f, 0.24f), new Vector2(0.70f, 0.34f));
            screen.restartButton.onClick.AddListener(screen.Restart);
            screen.menuButton.onClick.AddListener(screen.GoToMainMenu);

            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(screen.restartButton.gameObject);
            return screen;
        }

        private static void Place(RectTransform rect, float x0, float x1, float y0, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>True when a Game Over that was waiting on a timer should still appear: the scene where the
        /// fight was lost is loaded and still the active one.</summary>
        public static bool ShouldShow(Scene lostIn, Scene active)
        {
            return lostIn.IsValid() && lostIn.isLoaded && active.IsValid() && lostIn.handle == active.handle;
        }

        // Set when the page is told to go away; visible to tests because Destroy is deferred
        public bool destroyRequested;

        private void OnEnable() => SceneManager.activeSceneChanged += OnActiveSceneChanged;
        private void OnDisable() => SceneManager.activeSceneChanged -= OnActiveSceneChanged;

        // The page belongs to the scene it was shown in; it must never stay on top of another scene
        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            destroyRequested = true;
            Time.timeScale = 1f;
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);   // edit-mode tests
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Restart()
        {
            Time.timeScale = 1f;
            int floors = totalFloors > 0 ? totalFloors : RunState.DefaultTotalFloors;
            Destroy(gameObject);
            if (GameFlowManager.Instance != null) GameFlowManager.Instance.StartNewGame(floors);
        }

        private void GoToMainMenu()
        {
            Time.timeScale = 1f;
            Destroy(gameObject);
            if (Application.CanStreamedLevelBeLoaded(GameFlowManager.MainMenuSceneName))
                SceneTransition.Load(GameFlowManager.MainMenuSceneName);
        }
    }
}
