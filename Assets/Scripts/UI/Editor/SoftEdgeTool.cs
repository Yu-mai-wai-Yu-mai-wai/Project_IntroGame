using TawanOS.EventEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.UI
{
    /// <summary>
    /// Adds <see cref="SoftEdgeImage"/> (picture edges fade into the background) to UI pictures.
    /// Soften Edge: the Images selected in the Hierarchy. Soften Event Illustration: the event picture in EventScene.
    /// Batch: -executeMethod TawanOS.UI.SoftEdgeTool.SoftenEventIllustration
    /// </summary>
    public static class SoftEdgeTool
    {
        private const string EventScenePath = "Assets/Scenes/EventScene.unity";

        [MenuItem("Tools/TawanOS/UI/Soften Edge")]
        public static void SoftenSelection()
        {
            int added = 0;
            foreach (var go in Selection.gameObjects)
            {
                if (go.GetComponent<Graphic>() == null || go.GetComponent<SoftEdgeImage>() != null) continue;
                Undo.AddComponent<SoftEdgeImage>(go);
                added++;
            }

            if (added == 0)
            {
                EditorUtility.DisplayDialog("Soften Edge",
                    "เลือก GameObject ที่มี Image (หรือ RawImage) ใน Hierarchy ก่อน แล้วกดเมนูนี้อีกครั้ง\n" +
                    "ถ้ามี Soft Edge อยู่แล้ว ปรับความนุ่มของแต่ละด้านได้ที่ Left / Right / Top / Bottom ใน Inspector", "OK");
                return;
            }
            Debug.Log($"[SoftEdgeTool] Added Soft Edge to {added} object(s). Adjust Left / Right / Top / Bottom in the Inspector, then save the scene.");
        }

        [MenuItem("Tools/TawanOS/UI/Soften Event Illustration")]
        public static void SoftenEventIllustrationFromMenu()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Setup in Play Mode", "Please exit Play Mode before running the Setup Tool.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SoftenEventIllustration();
        }

        /// <summary>Opens EventScene, gives the event picture a soft right edge, saves, and selects it for the Inspector.</summary>
        public static void SoftenEventIllustration()
        {
            Scene scene = EditorSceneManager.OpenScene(EventScenePath, OpenSceneMode.Single);
            var view = Object.FindFirstObjectByType<EventViewUI>();
            if (view == null || view.illustration == null)
            {
                Debug.LogError($"[SoftEdgeTool] {EventScenePath} has no EventViewUI with an illustration - run the Event Engine setup first.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var target = view.illustration.gameObject;
            var soft = target.GetComponent<SoftEdgeImage>();
            if (soft == null) soft = Undo.AddComponent<SoftEdgeImage>(target);
            Undo.RecordObject(soft, "Soften Event Illustration");
            soft.SetSides(right: EventViewUI.EventIllustrationFeather);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!Application.isBatchMode) Selection.activeGameObject = target;
            Debug.Log($"<color=green>[SoftEdgeTool] Event illustration in {EventScenePath} fades on the right. Adjust Left / Right / Top / Bottom in the Inspector.</color>");
        }
    }
}
