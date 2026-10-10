using System.IO;
using TawanOS.GameFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Phase T4 Red Test: Verifies that MainMenu UI layout has:
    /// - Logo width >= 28% of the screen width
    /// - Menu buttons width <= 20% of the screen width and height >= 44 px (WCAG 2.5.5 touch/click target)
    /// - No solid background blocking the 3D scene
    /// Menu: Tools/TawanOS/Tests/Main Menu Layout
    /// </summary>
    public static class MainMenuLayoutTests
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Tools/TawanOS/Tests/Main Menu Layout")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;

            failures += Expect("MainMenu.unity exists", File.Exists(ScenePath));
            if (!File.Exists(ScenePath))
            {
                if (exitOnFinish) EditorApplication.Exit(1);
                return;
            }

            var currentScene = EditorSceneManager.GetActiveScene();
            bool needRestore = currentScene.path != ScenePath;
            string prevPath = currentScene.path;

            Scene scene;
            if (needRestore)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else
            {
                scene = currentScene;
            }

            try
            {
                var menuUI = Object.FindFirstObjectByType<MainMenuUI>();
                failures += Expect("MainMenuUI component exists", menuUI != null);

                // 1. Logo width >= 28% screen
                var logoTransform = menuUI != null ? menuUI.transform.Find("Menu/Logo") as RectTransform : null;
                failures += Expect("Logo RectTransform exists", logoTransform != null);
                if (logoTransform != null)
                {
                    float logoWidthFraction = logoTransform.anchorMax.x - logoTransform.anchorMin.x;
                    failures += Expect($"Logo width >= 28% screen (is {logoWidthFraction * 100f:F1}%)", logoWidthFraction >= 0.28f);
                }

                // 2. Buttons width <= 20% and height >= 44 px
                Button[] buttons = menuUI != null ? new[]
                {
                    menuUI.newGameButton,
                    menuUI.continueButton,
                    menuUI.settingsButton,
                    menuUI.quitButton
                } : new Button[0];

                foreach (var btn in buttons)
                {
                    if (btn == null)
                    {
                        failures += Expect("Menu button is not null", false);
                        continue;
                    }
                    var rt = btn.GetComponent<RectTransform>();
                    failures += Expect($"{btn.name} has RectTransform", rt != null);
                    if (rt != null)
                    {
                        float widthFraction = rt.anchorMax.x - rt.anchorMin.x;
                        float heightPx = (rt.anchorMax.y - rt.anchorMin.y) * 1080f;
                        failures += Expect($"{btn.name} width <= 20% screen (is {widthFraction * 100f:F1}%)", widthFraction <= 0.20f + 0.001f);
                        failures += Expect($"{btn.name} height >= 44 px (is {heightPx:F1} px)", heightPx >= 44f);
                    }
                }

                // 3. Background is transparent or removed so 3D stage is visible
                var bgTransform = menuUI != null ? menuUI.transform.Find("Background") : null;
                if (bgTransform != null)
                {
                    var bgImg = bgTransform.GetComponent<Image>();
                    failures += Expect("Background image is transparent or disabled", bgImg == null || !bgImg.enabled || bgImg.color.a < 0.2f);
                }
                else
                {
                    failures += Expect("Background image removed for 3D visibility", true);
                }
            }
            finally
            {
                if (needRestore && !string.IsNullOrEmpty(prevPath) && File.Exists(prevPath))
                {
                    EditorSceneManager.OpenScene(prevPath, OpenSceneMode.Single);
                }
            }

            if (failures == 0) Debug.Log("[MainMenuLayoutTests] PASS");
            else Debug.LogError($"[MainMenuLayoutTests] FAIL: {failures} check(s) failed.");
            if (exitOnFinish) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Expect(string name, bool ok)
        {
            if (ok) { Debug.Log($"  PASS {name}"); return 0; }
            Debug.LogError($"  FAIL {name}");
            return 1;
        }
    }
}
