using TawanOS.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.EventEngine
{
    /// <summary>
    /// EventScene (หมอกดำ and กองของเซ่น) split in two (Game Designer, 12 Oct): the illustration fills the left half
    /// edge to edge with no frame, and a black narration panel on the right meets it with a smoky edge down the
    /// middle, like the ศาลตายาย menu. An event without a picture leaves the left half dark (no frame, no "?").
    /// Rearranges the existing scene without rebuilding it; EventEngineSetupTool also runs it after a rebuild.
    /// Safe to run again. Batch: -executeMethod TawanOS.EventEngine.EventSmokeLayoutTool.ApplyBatch
    /// </summary>
    public static class EventSmokeLayoutTool
    {
        private const string ScenePath = "Assets/Scenes/EventScene.unity";
        private const string SmokePanelName = "SmokePanel";
        // The black panel starts a little left of the middle; its smoky border (SmokeEdgePanel.edge of its width)
        // rolls across the middle of the screen
        private const float PanelLeft = 0.44f;
        private const float SmokeEdge = 0.18f;
        private static readonly string[] OnTop = { "RunStatus", "OccurrenceTag" };

        [MenuItem("Tools/TawanOS/Event Engine/Split Layout With Smoke")]
        private static void ApplyMenu()
        {
            if (Application.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ApplyToScene();
        }

        public static void ApplyBatch()
        {
            EditorApplication.Exit(ApplyToScene() ? 0 : 1);
        }

        public static bool ApplyToScene()
        {
            Scene scene = SceneManager.GetActiveScene().path == ScenePath
                ? SceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var view = Object.FindFirstObjectByType<EventViewUI>(FindObjectsInactive.Include);
            if (view == null)
            {
                Debug.LogError($"[EventSmokeLayoutTool] {ScenePath} has no EventViewUI - run Setup Event Scene first.");
                return false;
            }
            if (!Apply(view)) return false;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[EventSmokeLayoutTool] {ScenePath}: picture on the left half, black smoke-edged panel on the right.");
            return true;
        }

        /// <summary>Lays out the event canvas that holds <paramref name="view"/>. Does not save the scene.</summary>
        public static bool Apply(EventViewUI view)
        {
            var root = view.transform;
            var illustration = view.illustration;
            if (illustration == null || illustration.transform.parent == null)
            {
                Debug.LogError("[EventSmokeLayoutTool] EventViewUI has no illustration under a holder.");
                return false;
            }

            // Left half, edge to edge; the holder masks what the cover-fit picture spills over
            var holder = (RectTransform)illustration.transform.parent;
            Fill(holder, Vector2.zero, new Vector2(0.5f, 1f));
            if (holder.TryGetComponent<Image>(out var holderImage)) { holderImage.enabled = false; EditorUtility.SetDirty(holderImage); }
            if (holder.TryGetComponent<Outline>(out var outline)) { outline.enabled = false; EditorUtility.SetDirty(outline); }
            if (holder.GetComponent<RectMask2D>() == null) holder.gameObject.AddComponent<RectMask2D>();
            var placeholder = holder.Find("Placeholder");
            if (placeholder != null) Object.DestroyImmediate(placeholder.gameObject);

            var pic = illustration.rectTransform;
            Fill(pic, Vector2.zero, Vector2.one);
            illustration.preserveAspect = false;
            var soft = illustration.GetComponent<SoftEdgeImage>();
            if (soft != null) Object.DestroyImmediate(soft);
            var fit = illustration.GetComponent<AspectRatioFitter>();
            if (fit == null) fit = illustration.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            EditorUtility.SetDirty(illustration);

            // Black panel on the right with a smoky left edge, behind the narration
            var smoke = root.Find(SmokePanelName) as RectTransform;
            if (smoke == null)
            {
                smoke = new GameObject(SmokePanelName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                smoke.SetParent(root, false);
            }
            Fill(smoke, new Vector2(PanelLeft, 0f), Vector2.one);
            var smokeImage = smoke.GetComponent<Image>();
            smokeImage.color = Color.black;
            smokeImage.raycastTarget = false;
            var edge = smoke.GetComponent<SmokeEdgePanel>();
            if (edge == null) edge = smoke.gameObject.AddComponent<SmokeEdgePanel>();
            edge.edge = SmokeEdge;
            smoke.SetSiblingIndex(holder.GetSiblingIndex() + 1);
            EditorUtility.SetDirty(smoke.gameObject);

            // The run status (HP, ธูป) and the "เหตุการณ์" tag were drawn before the picture and the panel: on top again
            foreach (string name in OnTop)
            {
                var t = root.Find(name);
                if (t != null) t.SetAsLastSibling();
            }

            EditorUtility.SetDirty(view);
            return true;
        }

        private static void Fill(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            EditorUtility.SetDirty(rt);
        }
    }
}
