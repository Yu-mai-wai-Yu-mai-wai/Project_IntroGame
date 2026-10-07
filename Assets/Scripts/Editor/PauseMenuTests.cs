using System.IO;
using System.Reflection;
using TawanOS.GameFlow;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Plan B3: Esc pauses in gameplay scenes only, time stops and comes back, the pause window has Resume /
    /// Settings / Main Menu, and a defeat shows a Game Over page with Restart and Main Menu instead of bouncing to
    /// the menu on a timer. Menu: Tools/TawanOS/Tests/Pause And Game Over.
    /// Batch: -executeMethod TawanOS.EditorTools.PauseMenuTests.Run
    /// </summary>
    public static class PauseMenuTests
    {
        [MenuItem("Tools/TawanOS/Tests/Pause And Game Over")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;
            float oldScale = Time.timeScale;
            GameObject pauseGo = null;
            var strayEventSystems = new System.Collections.Generic.List<GameObject>();
            try
            {
                // ---- which scenes can pause
                foreach (var s in new[] { "CombatTestScene", "MapTestScene", "ShopScene", "EventScene", "RewardScene", "MeruScene" })
                    failures += Expect($"pausable scene: {s}", PauseMenu.IsPausableScene(s));
                foreach (var s in new[] { "MainMenu", "VictoryScene", "IntroStoryScene", "" })
                    failures += Expect($"not pausable: '{s}'", !PauseMenu.IsPausableScene(s));

                // ---- Esc belongs to whoever is using it
                failures += Expect("Esc free when nothing is active", !PauseMenu.ShouldIgnoreEscape(false, false, false, false, false, false));
                failures += Expect("Esc ignored while a card detail is open", PauseMenu.ShouldIgnoreEscape(true, false, false, false, false, false));
                failures += Expect("Esc ignored while a card is held", PauseMenu.ShouldIgnoreEscape(false, true, false, false, false, false));
                failures += Expect("Esc ignored while choosing a target", PauseMenu.ShouldIgnoreEscape(false, false, true, false, false, false));
                failures += Expect("Esc ignored while the graveyard is open", PauseMenu.ShouldIgnoreEscape(false, false, false, true, false, false));
                failures += Expect("Esc ignored while Settings is open", PauseMenu.ShouldIgnoreEscape(false, false, false, false, true, false));
                failures += Expect("Esc ignored on the Game Over page", PauseMenu.ShouldIgnoreEscape(false, false, false, false, false, true));

                // ---- pause and resume
                pauseGo = new GameObject("PauseMenuTest");
                var pause = pauseGo.AddComponent<PauseMenu>();
                typeof(PauseMenu).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pause, null);

                Time.timeScale = 2f;
                pause.PauseGame();
                failures += Expect("PauseGame stops time", Time.timeScale == 0f);
                failures += Expect("IsPaused is true", PauseMenu.IsPaused);

                var canvas = pauseGo.transform.Find("PauseCanvas");
                failures += Expect("pause canvas exists", canvas != null);
                if (canvas != null)
                {
                    var window = (RectTransform)canvas.Find("Window");
                    foreach (string name in new[] { "ResumeButton", "SettingsButton", "MainMenuButton" })
                    {
                        var btn = window != null ? window.Find(name) as RectTransform : null;
                        failures += Expect($"pause window has {name}", btn != null && btn.GetComponent<Button>() != null);
                        if (btn != null && window != null)
                        {
                            float px = (window.anchorMax.y - window.anchorMin.y) * (btn.anchorMax.y - btn.anchorMin.y) * 1080f;
                            failures += Expect($"{name} is at least 44 px tall at 1080p ({px:0} px)", px >= 44f);
                        }
                    }
                }

                pause.ResumeGame();
                failures += Expect("ResumeGame restores the previous time scale", Time.timeScale == 2f);
                failures += Expect("IsPaused is false after resume", !PauseMenu.IsPaused);

                // a scene change while paused must never leave time frozen
                Time.timeScale = 1f;
                pause.PauseGame();
                typeof(PauseMenu).GetMethod("OnSceneLoaded", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(pause, new object[] { default(Scene), LoadSceneMode.Single });
                failures += Expect("scene load while paused unfreezes time", Time.timeScale == 1f && !PauseMenu.IsPaused);

                // ---- Game Over page
                var summary = new GameOverSummary { enemyName = "ผีป่าช้า", incense = 77, deckSize = 9, totalFloors = 7, maxHp = 50 };
                var screen = GameOverScreen.Show(summary);
                failures += Expect("Game Over page shows", GameOverScreen.IsShowing && screen != null);
                failures += Expect("has a Restart button", screen.restartButton != null);
                failures += Expect("has a Main Menu button", screen.menuButton != null);
                string text = screen.summaryText != null ? screen.summaryText.text : "";
                failures += Expect("summary names the enemy", text.Contains("ผีป่าช้า"));
                failures += Expect("summary shows incense", text.Contains("ธูปที่สะสม 77"));
                failures += Expect("summary shows the deck size", text.Contains("สำรับ 9 ใบ"));
                // a scene change must remove the page: it can never be left on top of the main menu
                typeof(GameOverScreen).GetMethod("OnActiveSceneChanged", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(screen, new object[] { default(Scene), default(Scene) });
                failures += Expect("active scene change asks the page to destroy itself", screen == null || screen.gameObject == null || screen.destroyRequested);
                if (screen != null && screen.gameObject != null) Object.DestroyImmediate(screen.gameObject);
                failures += Expect("Game Over page is gone after destroy", !GameOverScreen.IsShowing);

                // the delayed show only happens while the scene that was lost is still the active one
                var active = SceneManager.GetActiveScene();
                failures += Expect("Game Over may show while the same scene is active", GameOverScreen.ShouldShow(active, active));
                failures += Expect("Game Over must not show for an invalid scene", !GameOverScreen.ShouldShow(default(Scene), active));

                // ---- no timer bounces the player to the menu
                string flow = ReadSource("Assets/Scripts/GameFlow/GameFlowManager.cs");
                failures += Expect("GameFlowManager no longer auto-returns to the menu after a defeat", !flow.Contains("GoToMainMenuAfterDelay"));
                failures += Expect("GameFlowManager shows the Game Over page on defeat", flow.Contains("GameOverScreen.Show("));
                string over = ReadSource("Assets/Scripts/GameFlow/GameOver/GameOverScreen.cs");
                failures += Expect("Game Over page has no timer of its own", !over.Contains("WaitForSeconds") && !over.Contains("Invoke("));
            }
            finally
            {
                Time.timeScale = oldScale;
                if (pauseGo != null) Object.DestroyImmediate(pauseGo);
                foreach (var es in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                    if (es != null && es.gameObject.scene.IsValid() && es.gameObject.name == "EventSystem") strayEventSystems.Add(es.gameObject);
                foreach (var go in strayEventSystems) Object.DestroyImmediate(go);
            }

            if (failures == 0) Debug.Log("[PauseMenuTests] PASS");
            else Debug.LogError($"[PauseMenuTests] FAIL: {failures} check(s) failed.");
            if (exitOnFinish) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static string ReadSource(string path) => File.Exists(path) ? File.ReadAllText(path) : "";

        private static int Expect(string name, bool ok)
        {
            if (ok) { Debug.Log($"  PASS {name}"); return 0; }
            Debug.LogError($"  FAIL {name}");
            return 1;
        }
    }
}
