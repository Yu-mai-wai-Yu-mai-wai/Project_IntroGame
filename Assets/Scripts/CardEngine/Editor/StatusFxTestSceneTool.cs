using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Builds Assets/Scenes/StatusFxTestScene.unity: four cards facing the camera with เกราะคุ้มภัย, ผีบังตา, ผวา and all
    // three (StatusFxShowcase). Press Play to see the symbols and the shield's shimmer. Re-running rebuilds the scene.
    public static class StatusFxTestSceneTool
    {
        private const string ScenePath = "Assets/Scenes/StatusFxTestScene.unity";
        private const string CardPrefab = "Assets/CardEngineData/Prefabs/CardCube3DPrefab.prefab";
        private const string CardFolder = "Assets/CardEngineData/Cards/Sheet/";

        [MenuItem("Tools/TawanOS/Test/Status FX Test Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        // -executeMethod TawanOS.CardEngine.StatusFxTestSceneTool.BuildBatch
        public static void BuildBatch()
        {
            if (!Build()) EditorApplication.Exit(1);
        }

        public static bool Build()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefab);
            if (prefab == null)
            {
                Debug.LogError("[StatusFxTestScene] Card prefab not found: " + CardPrefab);
                return false;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.05f, 0.06f);
            cam.fieldOfView = 50f;
            camGo.transform.position = new Vector3(0f, -0.45f, -6.3f);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(30f, 0f, 0f);

            (string id, string title, CardStatusType[] statuses)[] rows =
            {
                ("s_bf01", "เกราะคุ้มภัย", new[] { CardStatusType.Protect }),
                ("s_wf03", "ผีบังตา", new[] { CardStatusType.Blinded }),
                ("s_bf03", "ผวา", new[] { CardStatusType.Phawa }),
                ("s_wf02", "ทั้งสามอย่าง", new[] { CardStatusType.Protect, CardStatusType.Blinded, CardStatusType.Phawa }),
            };

            var showcase = new GameObject("StatusFxShowcase").AddComponent<StatusFxShowcase>();
            showcase.slots = new StatusFxShowcase.Slot[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                var card = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                card.name = "Card_" + rows[i].id;
                card.transform.position = new Vector3((i - (rows.Length - 1) * 0.5f) * 1.75f, 0f, 0f);
                card.transform.rotation = Quaternion.identity; // the face (-Z) looks at the camera
                var view = card.GetComponent<CardView3D>();
                view.cardDataAsset = AssetDatabase.LoadAssetAtPath<CardDataSO>(CardFolder + "Card_" + rows[i].id + ".asset");
                if (view.cardDataAsset == null) Debug.LogWarning("[StatusFxTestScene] Card not found: " + rows[i].id);
                showcase.slots[i] = new StatusFxShowcase.Slot { card = view, title = rows[i].title, statuses = rows[i].statuses };
            }

            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[StatusFxTestScene] " + (saved ? "Saved " + ScenePath : "Could not save " + ScenePath));
            return saved;
        }
    }
}
