using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Builds OfferingScene, the กองของเซ่น node as its own 3D place (Game Designer, 12 Oct): the forest of the
    /// ศาลตายาย model (Assets/ProjectAsset/ShopFinal/ศาล.fbx) with the shrine itself hidden, a pile of face-down
    /// cards on the open ground in front of the big tree, a light over it, an eye-level camera that sees the forest
    /// behind the pile, and OfferingManager + OfferingPileView. Adds the scene to Build Settings. Rebuilds the scene, so the menu is guarded.
    /// Batch: -executeMethod TawanOS.GameFlow.OfferingSetupTool.SetupBatch
    /// </summary>
    public static class OfferingSetupTool
    {
        private const string ScenePath = "Assets/Scenes/OfferingScene.unity";
        private const string ModelPath = "Assets/ProjectAsset/ShopFinal/ศาล.fbx";
        private const string CardBackPath = "Assets/Art/Cards/FramedCard/back.png";
        private const string MaterialFolder = "Assets/Art/Offering";
        private const string CardMaterialPath = MaterialFolder + "/Mat_OfferingCardBack.mat";

        // Parts of the model that are the shrine, not the forest
        private static readonly string[] ShrineParts = { "Base", "Housing", "Ladder", "IncenseFull", "IncensePot", "IncenseSmall", "Vase", "Vase.001" };

        // The pile
        private const int CardCount = 14;
        private const float CardWidth = 1.26f;   // the card face is 939 x 1312 px
        private const float CardHeight = 1.76f;
        private const float CardGap = 0.012f;    // height of each card in the pile
        private const float MaxTurn = 40f;       // degrees each card may be turned off straight
        private const float MaxSlide = 0.18f;    // how far each card may lie off the middle
        private const int PileSeed = 1210;       // same pile every time the scene is built

        // Where things stand, framed in Play mode (12 Oct): the pile sits in the clearing this far in front of where
        // the shrine stood, and the camera looks at it from a crouch so the forest and the big tree fill the back
        private const float PileForward = -6f;                                   // along Z from the shrine's base
        private static readonly Vector3 CameraFromPile = new Vector3(0f, 2.4f, -5.2f); // higher and farther back (Game Designer, 12 Oct)
        private const float CameraPitch = 17f;
        private const float CameraFov = 45f;
        private static readonly Vector3 LightFromPile = new Vector3(0f, 1.4f, -1.2f);

        [MenuItem("Tools/TawanOS/Offering/Setup Offering Scene")]
        private static void SetupOfferingScene_Menu()
        {
            if (TawanOS.EditorTools.SetupGuard.Confirm("Setup Offering Scene")) SetupOfferingScene();
        }

        public static void SetupBatch()
        {
            EditorApplication.Exit(SetupOfferingScene() ? 0 : 1);
        }

        public static bool SetupOfferingScene()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Setup in Play Mode", "Please exit Play Mode before running the Setup Tool.", "OK");
                return false;
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var cardBack = AssetDatabase.LoadAssetAtPath<Texture2D>(CardBackPath);
            if (model == null || cardBack == null)
            {
                Debug.LogError($"[OfferingSetupTool] Missing: model {model != null}, card back {cardBack != null}.");
                return false;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The forest: the shrine model with the shrine itself switched off
            var stage = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            stage.name = "ForestStage";
            foreach (var t in stage.GetComponentsInChildren<Transform>(true))
                if (ShrineParts.Contains(t.name)) t.gameObject.SetActive(false);

            // The model's own camera is off; ours stands low in front of the pile
            var modelCamera = stage.GetComponentsInChildren<Camera>(true).FirstOrDefault();
            if (modelCamera != null) modelCamera.gameObject.SetActive(false);

            // The pile stands on the open ground in front of where the shrine stood
            var basePart = stage.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Base");
            Vector3 spot = basePart != null ? basePart.GetComponent<Renderer>().bounds.center : Vector3.zero;
            spot.z += PileForward;
            spot.y = GroundHeight(stage, spot);

            var pile = BuildPile(spot, CardMaterial(cardBack), out Transform topCard);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.SetPositionAndRotation(spot + CameraFromPile, Quaternion.Euler(CameraPitch, 0f, 0f));
            cam.fieldOfView = CameraFov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;

            // A warm light over the pile, brightened on hover
            var lightGo = new GameObject("PileLight");
            lightGo.transform.position = spot + LightFromPile;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.42f);
            light.range = 6f;
            light.intensity = 3f;

            var managerGo = new GameObject("OfferingManager");
            var manager = managerGo.AddComponent<OfferingManager>();
            var view = managerGo.AddComponent<OfferingPileView>();
            view.manager = manager;
            view.pileCollider = pile.GetComponent<Collider>();
            view.topCard = topCard;
            view.pileLight = light;
            view.viewCamera = cam;

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[OfferingSetupTool] Built {ScenePath}: {CardCount} cards at {spot}.</color>");
            return true;
        }

        // Height of the ground under a point, found with a throwaway collider on the model's ground mesh
        private static float GroundHeight(GameObject stage, Vector3 at)
        {
            var ground = stage.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name == "Ground");
            if (ground == null) return at.y;
            var collider = ground.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = ground.sharedMesh;
            float y = at.y;
            if (collider.Raycast(new Ray(new Vector3(at.x, 200f, at.z), Vector3.down), out var hit, 500f)) y = hit.point.y;
            Object.DestroyImmediate(collider);
            return y;
        }

        private static GameObject BuildPile(Vector3 spot, Material material, out Transform topCard)
        {
            var pile = new GameObject("OfferingPile");
            pile.transform.position = spot;
            var random = new System.Random(PileSeed);
            float Range(float r) => (float)(random.NextDouble() * 2.0 - 1.0) * r;

            topCard = null;
            for (int i = 0; i < CardCount; i++)
            {
                var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
                card.name = $"Card{i + 1:00}";
                Object.DestroyImmediate(card.GetComponent<Collider>());
                card.transform.SetParent(pile.transform, false);
                // Lying flat, face down (the back faces up), each a little turned and slid
                card.transform.localPosition = new Vector3(Range(MaxSlide), CardGap * (i + 1), Range(MaxSlide));
                card.transform.localRotation = Quaternion.Euler(0f, Range(MaxTurn), 0f) * Quaternion.Euler(90f, 0f, 0f);
                card.transform.localScale = new Vector3(CardWidth, CardHeight, 1f);
                card.GetComponent<MeshRenderer>().sharedMaterial = material;
                topCard = card.transform;
            }

            var box = pile.AddComponent<BoxCollider>();
            float reach = CardHeight * 0.5f + MaxSlide;
            box.center = new Vector3(0f, CardGap * CardCount * 0.5f + 0.05f, 0f);
            box.size = new Vector3(reach * 2f, CardGap * CardCount + 0.3f, reach * 2f);
            return pile;
        }

        // The card back on a lit, cut-out material so the pile picks up the scene's lights and keeps rounded corners
        private static Material CardMaterial(Texture2D cardBack)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                Directory.CreateDirectory(MaterialFolder);
                AssetDatabase.Refresh();
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(CardMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, CardMaterialPath);
            }
            material.SetTexture("_BaseMap", cardBack);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", 0f); // both sides
            material.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
