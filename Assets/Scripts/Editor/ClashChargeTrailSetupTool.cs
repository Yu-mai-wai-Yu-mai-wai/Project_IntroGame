#if UNITY_EDITOR
using System.IO;
using TawanOS.VFX;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    // Builds the tails behind familiars lunging in the board clash (ClashChargeTrail): a soft streak texture and
    // a material per school in Art/VFX/ClashTrail, and a TrailRenderer prefab per school in Resources/ClashTrail.
    // Only what is missing is built: existing textures, materials and prefabs are tuned by hand and kept. To
    // rebuild one from the defaults, delete it first.
    public static class ClashChargeTrailSetupTool
    {
        private const string ArtFolder = "Assets/Art/VFX/ClashTrail";
        private const string PrefabFolder = "Assets/Resources/ClashTrail";
        private const string TexturePath = ArtFolder + "/ChargeTrailStreak.png";

        [MenuItem("Tools/TawanOS/VFX/Create Clash Charge Trail")]
        private static void Create_Menu()
        {
            Create();
        }

        // Batch: -executeMethod TawanOS.EditorTools.ClashChargeTrailSetupTool.Create
        public static void Create()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art/VFX")) AssetDatabase.CreateFolder("Assets/Art", "VFX");
            if (!AssetDatabase.IsValidFolder(ArtFolder)) AssetDatabase.CreateFolder("Assets/Art/VFX", "ClashTrail");
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Resources", "ClashTrail");

            var texture = MakeTexture();
            if (texture == null)
            {
                Debug.LogError("[ClashChargeTrailSetupTool] The streak texture could not be made.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            // มนต์ดำ: blood red at the card fading through dark red to nothing
            MakePrefab("ChargeTrailBlack", MakeMaterial("ChargeTrailBlack", texture), new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(new Color(1f, 0.2f, 0.12f), 0f),
                    new GradientColorKey(new Color(0.55f, 0.02f, 0.02f), 0.45f),
                    new GradientColorKey(new Color(0.08f, 0f, 0f), 1f),
                },
                alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) },
            });
            // มนต์ขาว: white-gold light fading to warm gold
            MakePrefab("ChargeTrailWhite", MakeMaterial("ChargeTrailWhite", texture), new Gradient
            {
                colorKeys = new[]
                {
                    new GradientColorKey(new Color(1f, 0.98f, 0.9f), 0f),
                    new GradientColorKey(new Color(1f, 0.85f, 0.5f), 0.45f),
                    new GradientColorKey(new Color(0.9f, 0.6f, 0.2f), 1f),
                },
                alphaKeys = new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.5f, 0.4f), new GradientAlphaKey(0f, 1f) },
            });
            AssetDatabase.SaveAssets();
        }

        // Soft across the tail's width (v), solid along its length (u): the gradient does the fade along it
        private static Texture2D MakeTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (existing != null) return existing;

            const int w = 64, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h * 2f - 1f;
                float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v)), 1.6f);
                for (int x = 0; x < w; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            File.WriteAllBytes(TexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TexturePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        private static Material MakeMaterial(string name, Texture2D texture)
        {
            string path = $"{ArtFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            // Vertex colour x texture, alpha blended; drawn by URP as an unlit pass
            var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void MakePrefab(string name, Material material, Gradient colour)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return; // keeps tuning on the prefab

            var root = new GameObject(name);
            var trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = 0.22f;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = 1.1f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            trail.colorGradient = colour;
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.numCapVertices = 2;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            var charge = root.AddComponent<ClashChargeTrail>();
            charge.trail = trail;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"<color=green>[ClashChargeTrailSetupTool] {path} built.</color>");
        }
    }
}
#endif
