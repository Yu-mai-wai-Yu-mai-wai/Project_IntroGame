#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    // Builds the direct-hit bursts (FaceHitVfx): Art/VFX/FaceHit/FaceHitBlack.vfx and FaceHitWhite.vfx
    // (DrawPitSmokeVfxBuilder.BuildFaceHit) and a prefab playing each in Resources/FaceHit.
    // Only what is missing is built: existing .vfx files and prefabs are tuned by hand and kept. To rebuild one
    // from the defaults, delete it first.
    public static class FaceHitVfxSetupTool
    {
        private const string PrefabFolder = "Assets/Resources/FaceHit";
        private const string VfxBuilderType = "TawanOS.EditorTools.Vfx.DrawPitSmokeVfxBuilder, Unity.Testing.VisualEffectGraph.Editor";

        [MenuItem("Tools/TawanOS/VFX/Create Face Hit VFX")]
        private static void Create_Menu()
        {
            Create();
        }

        // Batch: -executeMethod TawanOS.EditorTools.FaceHitVfxSetupTool.Create
        public static void Create()
        {
            var builder = System.Type.GetType(VfxBuilderType);
            if (builder == null)
            {
                Debug.LogError("[FaceHitVfxSetupTool] VFX Graph builder not found (Assets/Scripts/Editor/VfxGraphBuilder).");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            builder.GetMethod("BuildFaceHit").Invoke(null, null);

            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Resources", "FaceHit");
            MakePrefab("FaceHitBlack");
            MakePrefab("FaceHitWhite");
        }

        private static void MakePrefab(string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.VFX.VisualEffectAsset>($"Assets/Art/VFX/FaceHit/{name}.vfx");
            if (asset == null)
            {
                Debug.LogError($"[FaceHitVfxSetupTool] {name}.vfx was not built.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            string path = $"{PrefabFolder}/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return; // keeps tuning on the prefab

            var root = new GameObject(name);
            var vfx = root.AddComponent<UnityEngine.VFX.VisualEffect>();
            vfx.visualEffectAsset = asset;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"<color=green>[FaceHitVfxSetupTool] {path} built.</color>");
        }
    }
}
#endif
