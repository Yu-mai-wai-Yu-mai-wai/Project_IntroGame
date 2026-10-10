using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Lines the main-menu buttons up under the logo, evenly spaced top to bottom: each button (and the save line under Continue) is
    /// centred on the logo's centre, and the button labels are centred inside their buttons. Only anchors and
    /// label alignment change; sizes, fonts, colours and the rest of the scene are left alone. Safe to run again.
    /// </summary>
    public static class MainMenuButtonLayoutTool
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Tools/TawanOS/Main Menu/Centre Buttons Under Logo")]
        private static void RunMenu()
        {
            Run();
        }

        /// <summary>-executeMethod TawanOS.GameFlow.MainMenuButtonLayoutTool.RunBatch</summary>
        public static void RunBatch()
        {
            EditorApplication.Exit(Run() ? 0 : 1);
        }

        public static bool Run()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[MainMenuButtonLayout] Leave Play mode first.");
                return false;
            }

            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            MainMenuUI menu = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                menu = root.GetComponentInChildren<MainMenuUI>(true);
                if (menu != null) break;
            }
            // The logo sits next to the buttons, under the same parent
            var buttons = menu != null && menu.newGameButton != null ? menu.newGameButton.transform.parent : null;
            var logo = buttons != null ? buttons.Find("Logo") as RectTransform : null;
            if (logo == null)
            {
                Debug.LogError("[MainMenuButtonLayout] MainMenu has no Logo next to the New Game button.");
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
                return false;
            }

            float centre = (logo.anchorMin.x + logo.anchorMax.x) * 0.5f;
            var list = new System.Collections.Generic.List<Button>();
            foreach (var b in new[] { menu.newGameButton, menu.continueButton, menu.collectionButton, menu.settingsButton, menu.quitButton })
                if (b != null) list.Add(b);
            SpaceEvenly(list);
            foreach (var button in list)
            {
                CentreOn((RectTransform)button.transform, centre);
                var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label == null) continue;
                Undo.RecordObject(label, "Centre menu button label");
                label.alignment = TextAlignmentOptions.Center;
                label.margin = Vector4.zero;
                EditorUtility.SetDirty(label);
            }
            if (menu.continueInfoText != null)
            {
                CentreOn(menu.continueInfoText.rectTransform, centre);
                Undo.RecordObject(menu.continueInfoText, "Centre continue info");
                menu.continueInfoText.alignment = TextAlignmentOptions.Top;
                EditorUtility.SetDirty(menu.continueInfoText);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            Debug.Log($"[MainMenuButtonLayout] Buttons centred on x = {centre:0.###} (saved: {saved}).");
            return saved;
        }

        // Keeps the top of the first button and the bottom of the last, and spreads the ones between so every
        // gap is the same (buttons keep their height)
        private static void SpaceEvenly(System.Collections.Generic.List<Button> buttons)
        {
            if (buttons.Count < 3) return;
            var rects = buttons.ConvertAll(b => (RectTransform)b.transform);
            float top = rects[0].anchorMax.y;
            float bottom = rects[rects.Count - 1].anchorMin.y;
            float heights = 0f;
            foreach (var rt in rects) heights += rt.anchorMax.y - rt.anchorMin.y;
            float gap = (top - bottom - heights) / (rects.Count - 1);

            float y = top;
            foreach (var rt in rects)
            {
                Undo.RecordObject(rt, "Space menu buttons");
                float h = rt.anchorMax.y - rt.anchorMin.y;
                rt.anchorMax = new Vector2(rt.anchorMax.x, y);
                rt.anchorMin = new Vector2(rt.anchorMin.x, y - h);
                EditorUtility.SetDirty(rt);
                y -= h + gap;
            }
        }

        // Keeps the width, moves the anchors so their middle sits on x
        private static void CentreOn(RectTransform rt, float x)
        {
            Undo.RecordObject(rt, "Centre menu button");
            float half = (rt.anchorMax.x - rt.anchorMin.x) * 0.5f;
            rt.anchorMin = new Vector2(x - half, rt.anchorMin.y);
            rt.anchorMax = new Vector2(x + half, rt.anchorMax.y);
            EditorUtility.SetDirty(rt);
        }
    }
}
