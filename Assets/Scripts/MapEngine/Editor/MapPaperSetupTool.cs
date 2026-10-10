using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TawanOS.MapEngine
{
    /// <summary>
    /// Puts the four-piece map paper (Assets/Art/MapPaper: หัว, เริ่มวนรอบแรก, ต่อไปเรื่อยๆ, ท้าย) into MapTestScene
    /// as a <see cref="MapPaperStrip"/>, in place of the single paper plane of the NavigateFinal model, which is only
    /// hidden (its renderer turned off), not removed. Each picture gets a material copied from that paper's material
    /// and cropped to the paper in the picture. Safe to run again.
    /// </summary>
    public static class MapPaperSetupTool
    {
        private const string ScenePath = "Assets/Scenes/MapTestScene.unity";
        private const string ArtFolder = "Assets/Art/MapPaper";
        private const string MaterialFolder = "Assets/Art/MapPaper/Materials";
        private const string SourceMaterialPath = "Assets/ProjectAsset/NavigateFinal/Material/PaperMapMat.mat";
        private const string OldPaperName = "PaperFloor";

        // Picture size and the paper inside each picture, in pixels from the top-left (measured from the alpha).
        // All four share one band of rows so their torn top and bottom edges line up.
        private const float PictureWidth = 1920f, PictureHeight = 1080f;
        private const float BandTop = 64f, BandBottom = 1046f;

        private struct PieceSpec
        {
            public string file; public float left, right;
            public PieceSpec(string file, float left, float right) { this.file = file; this.left = left; this.right = right; }
        }

        private static readonly PieceSpec Head = new PieceSpec("หัว", 40f, 655f);
        private static readonly PieceSpec FirstLoop = new PieceSpec("เริ่มวนรอบแรก", 29f, 1539f);
        private static readonly PieceSpec Repeat = new PieceSpec("ต่อไปเรื่อยๆ", 534f, 1539f);
        private static readonly PieceSpec Tail = new PieceSpec("ท้าย", 1145f, 1835f);

        [MenuItem("Tools/TawanOS/Map Engine/Setup Map Paper Strip")]
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

        /// <summary>-executeMethod TawanOS.MapEngine.MapPaperSetupTool.SetupBatch</summary>
        public static void SetupBatch()
        {
            EditorApplication.Exit(Setup() ? 0 : 1);
        }

        public static bool Setup()
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath);
            if (source == null)
            {
                Debug.LogError($"[MapPaperSetupTool] {SourceMaterialPath} not found.");
                return false;
            }
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder(ArtFolder, "Materials");

            var head = MakePiece(Head, source);
            var firstLoop = MakePiece(FirstLoop, source);
            var repeat = MakePiece(Repeat, source);
            var tail = MakePiece(Tail, source);
            if (head == null || firstLoop == null || repeat == null || tail == null) return false;

            Scene scene = SceneManager.GetActiveScene().path == ScenePath
                ? SceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var oldPaper = scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true))
                .FirstOrDefault(r => r.name == OldPaperName);

            var strip = Object.FindFirstObjectByType<MapPaperStrip>(FindObjectsInactive.Include);
            if (strip == null)
            {
                var go = new GameObject("MapPaperStrip");
                SceneManager.MoveGameObjectToScene(go, scene);
                strip = go.AddComponent<MapPaperStrip>();
            }
            strip.head = head;
            strip.firstLoop = firstLoop;
            strip.repeat = repeat;
            strip.tail = tail;
            strip.pixelHeight = BandBottom - BandTop;

            // Same place and size as the old paper: its depth covers the paper rows of the picture, its ends sit
            // the same distance before the start node and past the boss
            if (oldPaper != null)
            {
                var b = oldPaper.bounds;
                float oldPaperRows = 1036f - 72f; // the paper rows of เริ่มวนรอบแรก, which the old plane showed
                strip.worldDepth = b.size.z * strip.pixelHeight / oldPaperRows;
                strip.centerZ = b.center.z;
                strip.surfaceY = b.max.y;
                strip.margin = (b.max.x - MapManager.TableFloorX(-1, strip.editorFloors)
                                + MapManager.TableFloorX(strip.editorFloors, strip.editorFloors) - b.min.x) * 0.5f;
                oldPaper.enabled = false;
                EditorUtility.SetDirty(oldPaper);
            }
            else Debug.LogWarning($"[MapPaperSetupTool] No {OldPaperName} in {ScenePath}; kept the strip's own size.");

            strip.Layout(strip.editorFloors);
            EditorUtility.SetDirty(strip);
            int revertedTrees = RevertBillboardRotations(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MapPaperSetupTool] Map paper strip ready in {ScenePath}: {strip.transform.childCount} pieces, " +
                      $"depth {strip.worldDepth:F2}, margin {strip.margin:F2}; old paper hidden: {oldPaper != null}; " +
                      $"tree rotations reset: {revertedTrees}.");
            return true;
        }

        // TreeBillboardController turns the trees toward the camera even in the editor, so saving the scene would
        // store every tree's rotation as a change to the model. They are turned again when the game runs, so the
        // saved rotations are reset to the model's to keep the scene file free of them.
        private static int RevertBillboardRotations(Scene scene)
        {
            int reverted = 0;
            foreach (var controller in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TreeBillboardController>(true)))
            {
                var trees = new SerializedObject(controller).FindProperty("treeTransforms");
                for (int i = 0; trees != null && i < trees.arraySize; i++)
                {
                    var tree = trees.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                    if (tree == null || !PrefabUtility.IsPartOfPrefabInstance(tree)) continue;
                    var rotation = new SerializedObject(tree).FindProperty("m_LocalRotation");
                    if (rotation == null || !rotation.prefabOverride) continue;
                    PrefabUtility.RevertPropertyOverride(rotation, InteractionMode.AutomatedAction);
                    reverted++;
                }
            }
            return reverted;
        }

        private static MapPaperStrip.Piece MakePiece(PieceSpec spec, Material source)
        {
            string texturePath = $"{ArtFolder}/{spec.file}.png";
            if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer)
            {
                // Clamp so the crop does not bleed the far side of the picture; mipmaps because the paper is seen at an angle
                if (importer.wrapMode != TextureWrapMode.Clamp || !importer.alphaIsTransparency || !importer.mipmapEnabled)
                {
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = true;
                    importer.SaveAndReimport();
                }
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                Debug.LogError($"[MapPaperSetupTool] {texturePath} not found.");
                return null;
            }

            string materialPath = $"{MaterialFolder}/MapPaper_{spec.file}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(source);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", new Vector2((spec.right - spec.left) / PictureWidth, (BandBottom - BandTop) / PictureHeight));
            material.SetTextureOffset("_BaseMap", new Vector2(spec.left / PictureWidth, 1f - BandBottom / PictureHeight));
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            return new MapPaperStrip.Piece { material = material, pixelWidth = spec.right - spec.left };
        }
    }
}
