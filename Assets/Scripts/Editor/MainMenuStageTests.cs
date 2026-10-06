using System.Collections.Generic;
using System.IO;
using System.Linq;
using TawanOS.GameFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Phase T3 Red Test: Verifies that MainMenu has a 3D stage with Scarecrow,
    /// >= 6 strings (สายสิญจน์), red flickering light (<= 3 Hz, WCAG 2.3.1), fog,
    /// and no solid background image blocking the 3D camera.
    /// Menu: Tools/TawanOS/Tests/Main Menu Stage
    /// </summary>
    public static class MainMenuStageTests
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Tools/TawanOS/Tests/Main Menu Stage")]
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
                // 1. Camera 3D
                var cam = Camera.main;
                failures += Expect("Main Camera exists in scene", cam != null);
                if (cam != null)
                {
                    failures += Expect("Camera is not orthographic (3D perspective)", !cam.orthographic);
                }

                // 2. Stage object and component
                var stage = Object.FindFirstObjectByType<MainMenuStage>();
                failures += Expect("MainMenuStage component exists in scene", stage != null);

                // 3. Scarecrow model
                bool hasScarecrow = false;
                var meshFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
                foreach (var mf in meshFilters)
                {
                    if (mf.sharedMesh != null && mf.sharedMesh.name.ToLowerInvariant().Contains("scarecrow"))
                    {
                        hasScarecrow = true;
                        break;
                    }
                    if (mf.gameObject.name.ToLowerInvariant().Contains("scarecrow"))
                    {
                        hasScarecrow = true;
                        break;
                    }
                }
                failures += Expect("Scarecrow 3D model exists in stage", hasScarecrow);

                // 4. At least 6 strings (สายสิญจน์)
                var lineRenderers = Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None);
                failures += Expect($"At least 6 strings in stage (found {lineRenderers.Length})", lineRenderers.Length >= 6);

                // 5. Red Light
                var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                bool hasRedLight = lights.Any(l => l.color.r > 0.6f && l.color.g < 0.45f && l.color.b < 0.45f);
                failures += Expect("Red horror light exists in stage", hasRedLight);

                // 6. Red light flicker frequency <= 3 Hz (WCAG 2.3.1)
                if (stage != null)
                {
                    failures += Expect($"Flicker frequency <= 3 Hz (is {stage.flickerFrequency:F1} Hz)", stage.flickerFrequency <= 3.0f);
                }

                // 7. Fog / mist particles
                var particles = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
                failures += Expect("Atmospheric fog/mist particle system exists", particles.Length > 0);
                foreach (var ps in particles)
                {
                    var psr = ps.GetComponent<ParticleSystemRenderer>();
                    failures += Expect($"{ps.name} has non-null particle material (no purple glitch)",
                        psr != null && psr.sharedMaterial != null && !psr.sharedMaterial.shader.name.Contains("InternalErrorShader"));
                }

                // 8. No solid background image blocking 3D camera
                var canvas = Object.FindFirstObjectByType<Canvas>();
                failures += Expect("Canvas exists", canvas != null);
                if (canvas != null)
                {
                    var images = canvas.GetComponentsInChildren<Image>(true);
                    bool hasBlockingBackground = false;
                    foreach (var img in images)
                    {
                        if (img.name == "Background" && img.color.a > 0.5f)
                        {
                            var rt = img.rectTransform;
                            if (rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one)
                            {
                                hasBlockingBackground = true;
                                break;
                            }
                        }
                    }
                    failures += Expect("No solid background image blocking 3D camera", !hasBlockingBackground);
                }
            }


            finally
            {
                if (needRestore && !string.IsNullOrEmpty(prevPath) && File.Exists(prevPath))
                {
                    EditorSceneManager.OpenScene(prevPath, OpenSceneMode.Single);
                }
            }

            if (failures == 0) Debug.Log("[MainMenuStageTests] PASS");
            else Debug.LogError($"[MainMenuStageTests] FAIL: {failures} check(s) failed.");
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
