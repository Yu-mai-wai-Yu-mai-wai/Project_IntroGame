using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Turns TempleScene into the วัด as a 3D place (Game Designer, 15 Oct), built from models already in the project:
    /// the เมรุ building without its spire, furnace, door and cart becomes an old temple in the night forest; the
    /// doorway is walled up behind an empty dais (where a Buddha image will stand, lit gold for now) and an altar
    /// table holds candles from the combat table and the incense pot and vases from the ศาลตายาย. The camera kneels
    /// in front of the altar and a <see cref="TempleStageView"/> runs the visit; the old flat screen is removed.
    /// Safe to run again: the stage is rebuilt.
    /// </summary>
    public static class TempleStageSetupTool
    {
        private const string ScenePath = "Assets/Scenes/TempleScene.unity";
        private const string MeruModelPath = "Assets/ProjectAsset/Meru/Meru.fbx";
        private const string ShrineModelPath = "Assets/ProjectAsset/ShopFinal/ศาล.fbx";
        private const string CombatModelPath = "Assets/ProjectAsset/CombatFinal/Combat.fbx";
        private const string MaterialFolder = "Assets/ProjectAsset/Meru/Material/";
        private const string StageName = "TempleStage";
        private const string BuildingName = "TempleBuilding";

        // Where the เมรุ model stands in MeruScene, so the measurements below match it
        private static readonly Vector3 ModelPosition = new Vector3(-526.51f, 7.36f, 15.42f);
        private static readonly Quaternion ModelRotation = Quaternion.Euler(0f, -173.677f, 0f);
        private static readonly string[] RemovedParts = { "Furnace", "FurnaceLower", "InteriorFurnace", "Door", "DoorFrame", "Cart", "Lock" };

        [MenuItem("Tools/TawanOS/Temple/Setup Temple Stage")]
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

        /// <summary>-executeMethod TawanOS.GameFlow.TempleStageSetupTool.SetupBatch</summary>
        public static void SetupBatch()
        {
            EditorApplication.Exit(Setup() ? 0 : 1);
        }

        public static bool Setup()
        {
            var meruModel = AssetDatabase.LoadAssetAtPath<GameObject>(MeruModelPath);
            var shrineModel = AssetDatabase.LoadAssetAtPath<GameObject>(ShrineModelPath);
            var combatModel = AssetDatabase.LoadAssetAtPath<GameObject>(CombatModelPath);
            if (meruModel == null || shrineModel == null || combatModel == null)
            {
                Debug.LogError("[TempleStageSetupTool] The เมรุ, ศาล or combat model is missing.");
                return false;
            }

            Scene scene = SceneManager.GetActiveScene().path == ScenePath
                ? SceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var manager = Object.FindFirstObjectByType<TempleManager>(FindObjectsInactive.Include);
            var mainCamera = scene.GetRootGameObjects().Select(g => g.GetComponent<Camera>()).FirstOrDefault(c => c != null);
            if (manager == null || mainCamera == null)
            {
                Debug.LogError($"[TempleStageSetupTool] {ScenePath} needs its TempleManager and Main Camera - run Setup Temple Scene first.");
                return false;
            }

            foreach (var g in scene.GetRootGameObjects())
                if (g.name == StageName || g.name == BuildingName || g.GetComponent<TempleViewUI>() != null) Object.DestroyImmediate(g);
            manager.view = null;

            // The building
            var building = (GameObject)PrefabUtility.InstantiatePrefab(meruModel, scene);
            PrefabUtility.UnpackPrefabInstance(building, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            building.name = BuildingName;
            building.transform.SetPositionAndRotation(ModelPosition, ModelRotation);
            foreach (var part in RemovedParts)
            {
                var t = building.transform.Find(part);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            foreach (var cam in building.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(cam.gameObject);

            var stage = new GameObject(StageName).transform;
            SceneManager.MoveGameObjectToScene(stage.gameObject, scene);

            var wall = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "Wall.mat");
            var wood = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "TWood.mat");
            var gold = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "GoldEdge.mat");

            // Measured from the เมรุ model: the doorway's sill, its centre line and the wall it was in
            Vector3 o = ModelPosition;
            float floorY = o.y + 9.59f;
            float x = o.x + 38.96f;
            float backZ = o.z + 89.88f;

            Box("BackPanel", stage, new Vector3(x, floorY + 8f, backZ + 0.4f), new Vector3(11f, 16f, 0.6f), wall);
            Box("Dais", stage, new Vector3(x, floorY + 2.2f, backZ - 2f), new Vector3(9f, 4.4f, 3.4f), wall);
            Box("DaisTrim", stage, new Vector3(x, floorY + 4.5f, backZ - 2f), new Vector3(9.4f, 0.25f, 3.7f), gold);
            Box("AltarTable", stage, new Vector3(x, floorY + 1.6f, backZ - 6.2f), new Vector3(6.5f, 3.2f, 2.2f), wood);
            Box("AltarTop", stage, new Vector3(x, floorY + 3.25f, backZ - 6.2f), new Vector3(6.9f, 0.15f, 2.5f), gold);
            float top = floorY + 3.33f;

            var altar = new GameObject("Altar").transform;
            altar.SetParent(stage, false);
            Fit(Copy(shrineModel, "IncensePot", altar), 0.55f, new Vector3(x, top, backZ - 6.2f));
            Fit(Copy(shrineModel, "IncenseFull", altar), 1.5f, new Vector3(x, top + 0.35f, backZ - 6.2f));
            Fit(Copy(shrineModel, "Vase", altar), 1.1f, new Vector3(x - 2.6f, top, backZ - 6.0f));
            Fit(Copy(shrineModel, "Vase.001", altar), 1.1f, new Vector3(x + 2.6f, top, backZ - 6.0f));

            float[] across = { -1.7f, -0.9f, 0.9f, 1.7f };
            float[] heights = { 0.7f, 1.0f, 1.0f, 0.7f };
            var candleLights = new Light[across.Length];
            for (int i = 0; i < across.Length; i++)
            {
                var candle = Copy(combatModel, "Candle" + (i + 2), altar);
                Fit(candle, heights[i], new Vector3(x + across[i], top, backZ - 6.6f));
                var b = candle.GetComponent<Renderer>().bounds;
                Fit(Copy(combatModel, "CandleFlame" + (i + 2), altar), 0.12f, new Vector3(b.center.x, b.max.y, b.center.z));
                candleLights[i] = Point("CandleLight" + (i + 1), stage, new Vector3(b.center.x, b.max.y + 0.3f, b.center.z - 0.2f),
                    new Color(1f, 0.72f, 0.38f), 9f, 2.2f);
            }
            var daisGlow = Point("DaisGlow", stage, new Vector3(x, floorY + 9f, backZ - 4.5f), new Color(1f, 0.82f, 0.45f), 12f, 3f);

            // Kneeling in front of the altar
            // Far enough back that the altar sits above the button and the khwan bar at the bottom of the screen
            mainCamera.transform.position = new Vector3(x, floorY + 8.6f, backZ - 17f);
            mainCamera.transform.LookAt(new Vector3(x, floorY + 3.2f, backZ - 4f));
            mainCamera.fieldOfView = 50f;
            mainCamera.nearClipPlane = 0.1f;
            mainCamera.farClipPlane = 2000f;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.015f, 0.018f, 0.035f);
            EditorUtility.SetDirty(mainCamera);

            MakeNight(stage);

            var view = stage.gameObject.AddComponent<TempleStageView>();
            view.viewCamera = mainCamera;
            view.candleLights = candleLights;
            view.daisGlow = daisGlow;
            manager.stage = view;
            EditorUtility.SetDirty(manager);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[TempleStageSetupTool] Temple stage ready in {ScenePath}.");
            return true;
        }

        // Same night as the เมรุ
        private static void MakeNight(Transform stage)
        {
            var sky = new Color(0.015f, 0.018f, 0.035f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.11f, 0.12f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = sky;
            RenderSettings.fogDensity = 0.0028f;

            var moon = new GameObject("MoonLight").AddComponent<Light>();
            moon.transform.SetParent(stage, false);
            moon.transform.rotation = Quaternion.Euler(40f, 95f, 0f);
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.65f, 0.95f);
            moon.intensity = 0.35f;
            moon.shadows = LightShadows.Soft;
        }

        // A free copy of one part of a model, where it sits in the model
        private static Transform Copy(GameObject model, string part, Transform parent)
        {
            var whole = (GameObject)Object.Instantiate(model);
            var source = whole.GetComponentsInChildren<Transform>(true).First(t => t.name == part);
            var copy = Object.Instantiate(source.gameObject);
            copy.name = part;
            copy.transform.SetPositionAndRotation(source.position, source.rotation);
            copy.transform.localScale = source.lossyScale;
            Object.DestroyImmediate(whole);
            copy.transform.SetParent(parent, true);
            return copy.transform;
        }

        // Scales a part to `height` and stands its bottom middle on `foot`
        private static void Fit(Transform part, float height, Vector3 foot)
        {
            var r = part.GetComponentInChildren<Renderer>();
            part.localScale *= height / Mathf.Max(r.bounds.size.y, 0.0001f);
            var b = r.bounds;
            part.position += foot - new Vector3(b.center.x, b.min.y, b.center.z);
        }

        private static void Box(string name, Transform parent, Vector3 center, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        private static Light Point(string name, Transform parent, Vector3 at, Color colour, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = colour;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
            return light;
        }
    }
}
