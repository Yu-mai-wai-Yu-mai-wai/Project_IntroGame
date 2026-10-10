using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.ShopEngine
{
    /// <summary>
    /// Puts the ศาลตายาย model (Assets/ProjectAsset/ShopFinal/ศาล.fbx) into ShopScene without rebuilding the scene:
    /// the main camera takes the model's camera view, the incense pot gets a click collider, and a
    /// <see cref="ShopShrineView"/> keeps the shop menu hidden until the pot is clicked. The menu background becomes
    /// solid black on the right of the screen only, and the shrine card on its left (frame, name, picture) is hidden,
    /// since the model now stands there. Safe to run again.
    /// </summary>
    public static class ShopShrineSetupTool
    {
        private const string ScenePath = "Assets/Scenes/ShopScene.unity";
        private const string ModelPath = "Assets/ProjectAsset/ShopFinal/ศาล.fbx";
        private const string StageName = "ShrineStage";
        // Left side of the black menu background, across the screen (the menu's first column starts at 0.33)
        private const float MenuLeft = 0.27f;

        [MenuItem("Tools/TawanOS/Shop Engine/Setup Shrine Stage")]
        private static void SetupMenu()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Setup in Play Mode", "Please exit Play Mode before running the Setup Tool.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Setup();
        }

        /// <summary>-executeMethod TawanOS.ShopEngine.ShopShrineSetupTool.SetupBatch</summary>
        public static void SetupBatch()
        {
            EditorApplication.Exit(Setup() ? 0 : 1);
        }

        public static bool Setup()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError($"[ShopShrineSetupTool] {ModelPath} not found.");
                return false;
            }

            Scene scene = SceneManager.GetActiveScene().path == ScenePath
                ? SceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var view = Object.FindFirstObjectByType<ShopViewUI>(FindObjectsInactive.Include);
            var mainCamera = scene.GetRootGameObjects().Select(g => g.GetComponent<Camera>()).FirstOrDefault(c => c != null);
            if (view == null || mainCamera == null)
            {
                Debug.LogError($"[ShopShrineSetupTool] {ScenePath} needs its ShopCanvas and Main Camera - run Setup Shop Scene first.");
                return false;
            }

            // The model, once
            var stage = scene.GetRootGameObjects().FirstOrDefault(g => g.name == StageName);
            if (stage == null)
            {
                stage = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                stage.name = StageName;
            }

            // The main camera looks through the model's camera; the model's own camera is switched off
            var modelCamera = stage.GetComponentsInChildren<Camera>(true).FirstOrDefault(c => c != mainCamera);
            if (modelCamera != null)
            {
                mainCamera.transform.SetPositionAndRotation(modelCamera.transform.position, modelCamera.transform.rotation);
                mainCamera.fieldOfView = modelCamera.fieldOfView;
                mainCamera.nearClipPlane = modelCamera.nearClipPlane;
                mainCamera.farClipPlane = Mathf.Max(modelCamera.farClipPlane, 300f);
                modelCamera.enabled = false;
                EditorUtility.SetDirty(modelCamera);
            }
            else Debug.LogWarning("[ShopShrineSetupTool] The model has no camera; the main camera keeps its place.");
            EditorUtility.SetDirty(mainCamera);

            // A box around the pot and the incense in it, so the whole thing can be clicked
            var pot = stage.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "IncensePot");
            if (pot == null)
            {
                Debug.LogError("[ShopShrineSetupTool] No IncensePot in the model.");
                return false;
            }
            var potParts = stage.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("Incense")).ToArray();
            var box = pot.GetComponent<BoxCollider>();
            if (box == null) box = pot.gameObject.AddComponent<BoxCollider>();
            var bounds = potParts[0].bounds;
            foreach (var r in potParts) bounds.Encapsulate(r.bounds);
            box.center = pot.InverseTransformPoint(bounds.center);
            var size = pot.InverseTransformVector(bounds.size);
            box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) * 1.1f;
            EditorUtility.SetDirty(box);

            // The view that opens the menu
            var shrineView = Object.FindFirstObjectByType<ShopShrineView>(FindObjectsInactive.Include);
            if (shrineView == null)
            {
                var go = new GameObject("ShrineView");
                SceneManager.MoveGameObjectToScene(go, scene);
                shrineView = go.AddComponent<ShopShrineView>();
            }
            shrineView.shopCanvas = view.GetComponent<Canvas>();
            shrineView.incensePot = box;
            shrineView.viewCamera = mainCamera;
            shrineView.potLights = stage.GetComponentsInChildren<Light>(true).ToArray();
            EditorUtility.SetDirty(shrineView);

            // The shrine model takes the place of the shrine card on the left of the menu, so the whole card goes:
            // frame, name, picture, and the guardian's name and words (Game Designer, 11 Oct)
            var panel = view.transform.Find("ShrinePanel");
            if (panel != null)
            {
                if (panel.TryGetComponent<Image>(out var panelImage)) { panelImage.enabled = false; EditorUtility.SetDirty(panelImage); }
                if (panel.TryGetComponent<Outline>(out var panelOutline)) { panelOutline.enabled = false; EditorUtility.SetDirty(panelOutline); }
            }
            foreach (var part in new Component[] { view.shrineNameText, view.shrineImage, view.guardianNameText, view.speechText, view.resultText })
            {
                if (part == null) continue;
                part.gameObject.SetActive(false);
                EditorUtility.SetDirty(part.gameObject);
            }

            // The menu is solid black on the right; its left border (drawn as smoke at runtime) starts a little
            // left of the menu's first column so the smoke rolls between the shrine and the menu
            var background = view.transform.Find("Background");
            if (background != null && background.TryGetComponent<Image>(out var image))
            {
                image.color = Color.black;
                var rt = image.rectTransform;
                rt.anchorMin = new Vector2(MenuLeft, 0f);
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                shrineView.menuBackground = image;
                EditorUtility.SetDirty(image);
                EditorUtility.SetDirty(rt);
            }
            shrineView.shrineRenderers = stage.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.name == "Housing" || r.name == "Base").ToArray();
            shrineView.shrineScreenWidth = 0.2f;
            EditorUtility.SetDirty(shrineView);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ShopShrineSetupTool] Shrine stage ready in {ScenePath}: camera from model {modelCamera != null}, " +
                      $"pot collider size {box.size}, {shrineView.potLights.Length} lights.");
            return true;
        }
    }
}
