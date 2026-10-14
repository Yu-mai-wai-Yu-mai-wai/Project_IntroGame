using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Turns MeruScene into the เมรุ stage (Game Designer, 14 Oct) without touching the model's placement:
    /// the main camera starts at the model's outside camera; the building gets a click collider and a warm light that
    /// comes on under the pointer; an inside view stands at eye height looking straight at the furnace door; a fire
    /// burns inside the furnace and a second set of flames waits round the door to burst out; the screen
    /// (<see cref="MeruSetupTool.BuildCanvas"/>) replaces the old canvas; and a <see cref="MeruStageView"/> runs it all.
    /// Adds the model (Assets/ProjectAsset/Meru/Meru.fbx) if the scene has none. Safe to run again: the stage object is
    /// rebuilt, so tune it in the scene only after the last run (or change the numbers here).
    /// </summary>
    public static class MeruStageSetupTool
    {
        private const string ScenePath = "Assets/Scenes/MeruScene.unity";
        private const string ModelPath = "Assets/ProjectAsset/Meru/Meru.fbx";
        private const string StageName = "MeruStage";
        // Where the model stood when the Game Designer placed it (14 Oct); used only when the scene has no model yet
        private static readonly Vector3 DefaultModelPosition = new Vector3(-526.51f, 7.36f, 15.42f);

        // Inside view: eyes this far up the door's height above its sill, standing back so the door is this much
        // of the screen's height, with this field of view
        private const float EyeHeight = 0.72f;
        private const float DoorScreenHeight = 0.58f;
        private const float InsideFov = 45f;

        [MenuItem("Tools/TawanOS/Meru/Setup Meru Stage")]
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

        /// <summary>-executeMethod TawanOS.GameFlow.MeruStageSetupTool.SetupBatch</summary>
        public static void SetupBatch()
        {
            EditorApplication.Exit(Setup() ? 0 : 1);
        }

        public static bool Setup()
        {
            Scene scene = SceneManager.GetActiveScene().path == ScenePath
                ? SceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var manager = Object.FindFirstObjectByType<MeruManager>(FindObjectsInactive.Include);
            var mainCamera = scene.GetRootGameObjects().Select(g => g.GetComponent<Camera>()).FirstOrDefault(c => c != null);
            if (manager == null || mainCamera == null)
            {
                Debug.LogError($"[MeruStageSetupTool] {ScenePath} needs its MeruManager and Main Camera - run Setup Meru Scene first.");
                return false;
            }

            // The model, once
            var model = scene.GetRootGameObjects().FirstOrDefault(g => g.transform.Find("Door") != null && g.transform.Find("Camera.001") != null);
            if (model == null)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                if (asset == null)
                {
                    Debug.LogError($"[MeruStageSetupTool] {ModelPath} not found.");
                    return false;
                }
                model = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
                model.name = "Meru";
                model.transform.position = DefaultModelPosition;
            }
            var root = model.transform;
            var outsideCamera = root.Find("Camera")?.GetComponent<Camera>();
            var insideCamera = root.Find("Camera.001")?.GetComponent<Camera>();
            var door = root.Find("Door");
            var building = root.Find("Building");
            var interior = root.Find("InteriorFurnace")?.GetComponent<Renderer>();
            if (outsideCamera == null || insideCamera == null || door == null || building == null || interior == null)
            {
                Debug.LogError("[MeruStageSetupTool] The model needs Camera, Camera.001, Door, Building and InteriorFurnace.");
                return false;
            }

            // The model's cameras only mark views; the main camera does the looking
            foreach (var cam in new[] { outsideCamera, insideCamera })
            {
                cam.enabled = false;
                EditorUtility.SetDirty(cam);
            }
            mainCamera.transform.SetPositionAndRotation(outsideCamera.transform.position, outsideCamera.transform.rotation);
            mainCamera.fieldOfView = outsideCamera.fieldOfView;
            mainCamera.nearClipPlane = 0.1f;
            mainCamera.farClipPlane = 2000f;
            EditorUtility.SetDirty(mainCamera);
            EditorUtility.SetDirty(mainCamera.transform);

            // Click target outside: the whole building. The door is no longer clicked (a side is chosen instead).
            var meruClick = building.GetComponent<BoxCollider>();
            if (meruClick == null) meruClick = building.gameObject.AddComponent<BoxCollider>();
            EditorUtility.SetDirty(meruClick);
            var doorCollider = door.GetComponent<BoxCollider>();
            if (doorCollider != null) Object.DestroyImmediate(doorCollider);

            // The door: its middle, size, and the way it faces (toward where the model's inside camera stood)
            var doorBounds = door.GetComponent<Renderer>().bounds;
            var facing = DoorFacing(door, insideCamera.transform.position - doorBounds.center);
            var right = Vector3.Cross(Vector3.up, -facing).normalized; // the viewer's right, looking at the door
            var e = doorBounds.extents;
            float doorWidth = 2f * (Mathf.Abs(right.x) * e.x + Mathf.Abs(right.z) * e.z);
            float doorHeight = doorBounds.size.y;
            // The door turns about its edge on the viewer's left, where its hinges are
            var hinge = doorBounds.center - right * doorWidth * 0.5f;

            // The stage: lights, fire, inside view and the view that runs it all
            var stage = scene.GetRootGameObjects().FirstOrDefault(g => g.name == StageName);
            if (stage != null) Object.DestroyImmediate(stage);
            stage = new GameObject(StageName);
            SceneManager.MoveGameObjectToScene(stage, scene);

            var insideView = new GameObject("InsideView").transform;
            insideView.SetParent(stage.transform, false);
            float distance = doorHeight / (DoorScreenHeight * 2f * Mathf.Tan(InsideFov * 0.5f * Mathf.Deg2Rad));
            var eye = doorBounds.center + facing * distance;
            eye.y = doorBounds.min.y + doorHeight * EyeHeight;
            insideView.SetPositionAndRotation(eye, Quaternion.LookRotation(-facing, Vector3.up));

            MakeNight(mainCamera, stage.transform, outsideCamera.transform);
            var meruLight = BuildMeruLight(stage.transform, building.GetComponent<Renderer>().bounds, outsideCamera.transform.position);
            var fire = BuildFire(stage.transform, interior.bounds, doorBounds.center, facing, right, new Vector2(doorWidth, doorHeight), out var fireLight);

            // The screen replaces the old one
            foreach (var old in Object.FindObjectsByType<MeruViewUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(old.gameObject);
            var meruView = MeruSetupTool.BuildCanvas();
            SceneManager.MoveGameObjectToScene(meruView.gameObject, scene);
            manager.view = meruView;
            EditorUtility.SetDirty(manager);

            var stageView = stage.AddComponent<MeruStageView>();
            stageView.viewCamera = mainCamera;
            stageView.outsideView = outsideCamera.transform;
            stageView.outsideFov = outsideCamera.fieldOfView;
            stageView.insideView = insideView;
            stageView.insideFov = InsideFov;
            stageView.meruClick = meruClick;
            stageView.meruLight = meruLight;
            stageView.view = meruView;
            stageView.door = door;
            stageView.doorHinge = hinge;
            stageView.fire = fire;
            stageView.fireLight = fireLight;
            EditorUtility.SetDirty(stageView);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MeruStageSetupTool] Meru stage ready in {ScenePath}: inside view {distance:0.0} from the door, " +
                      $"hinge at {hinge}.");
            return true;
        }

        // The door is a thin slab: its thinnest axis is the way it faces, turned toward the given side
        private static Vector3 DoorFacing(Transform door, Vector3 towardViewer)
        {
            var size = door.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            var axis = size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;
            var facing = Vector3.ProjectOnPlane(door.TransformDirection(axis), Vector3.up).normalized;
            return Vector3.Dot(facing, towardViewer) < 0f ? -facing : facing;
        }

        // Night (Game Designer, 14 Oct): no daylight sky, a near-black blue background, dim blue ambient light,
        // thin dark fog that swallows the far trees, and a weak cold moon from behind the camera's left
        private static readonly Color NightSky = new Color(0.015f, 0.018f, 0.035f);
        private static readonly Color NightAmbient = new Color(0.11f, 0.12f, 0.18f);

        private static void MakeNight(Camera camera, Transform stage, Transform outsideView)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = NightSky;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = NightAmbient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = NightSky;
            RenderSettings.fogDensity = 0.0028f;

            var moonGo = new GameObject("MoonLight");
            moonGo.transform.SetParent(stage, false);
            var look = Vector3.ProjectOnPlane(outsideView.forward, Vector3.up).normalized;
            moonGo.transform.rotation = Quaternion.LookRotation(Quaternion.AngleAxis(35f, Vector3.up) * look, Vector3.up)
                                        * Quaternion.Euler(40f, 0f, 0f);
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.65f, 0.95f);
            moon.intensity = 0.35f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.6f;
        }

        // A warm light in front of the เมรุ, toward the outside camera, off until the pointer is on the building
        private static Light BuildMeruLight(Transform stage, Bounds building, Vector3 cameraPos)
        {
            var go = new GameObject("MeruLight");
            go.transform.SetParent(stage, false);
            var toCamera = Vector3.ProjectOnPlane(cameraPos - building.center, Vector3.up).normalized;
            float reach = building.extents.magnitude;
            go.transform.position = building.center + toCamera * reach * 0.65f + Vector3.up * building.extents.y * 0.4f;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.5f);
            light.range = reach * 2.2f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            return light;
        }

        // The fire (particles, built in play by MeruFireFx) on the furnace floor and round the door, and its light,
        // a little toward the door so it spills out when it opens
        private static MeruFireFx BuildFire(Transform stage, Bounds interior, Vector3 doorCenter, Vector3 facing, Vector3 right,
            Vector2 doorSize, out Light light)
        {
            var go = new GameObject("Fire");
            go.transform.SetParent(stage, false);
            var fire = go.AddComponent<MeruFireFx>();
            var forward = -facing; // into the furnace
            var e = interior.extents;
            float across = 2f * (Mathf.Abs(right.x) * e.x + Mathf.Abs(right.z) * e.z);
            float depth = 2f * (Mathf.Abs(forward.x) * e.x + Mathf.Abs(forward.z) * e.z);
            fire.furnaceFloor = new Vector3(interior.center.x, interior.min.y, interior.center.z) + forward * depth * 0.1f;
            fire.furnaceSize = new Vector3(across, interior.size.y, depth);
            fire.furnaceRight = right;
            fire.doorCenter = doorCenter;
            fire.doorSize = doorSize;
            fire.doorFacing = facing;

            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(stage, false);
            lightGo.transform.position = fire.furnaceFloor + Vector3.up * interior.size.y * 0.35f - forward * depth * 0.3f;
            light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.5f, 0.16f);
            light.range = Mathf.Max(interior.size.y, across) * 2.6f;
            light.intensity = 0.6f;
            light.shadows = LightShadows.None;
            return fire;
        }
    }
}
