#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    // Builds the card landing bursts (CardLandVfx): Art/VFX/CardLand/CardLandBlack.vfx and CardLandWhite.vfx
    // (DrawPitSmokeVfxBuilder.BuildCardLand) and a prefab playing each in Resources/CardLand.
    // Only what is missing is built: existing .vfx files and prefabs are tuned by hand and kept. To rebuild one
    // from the defaults, delete it first.
    public static class CardLandVfxSetupTool
    {
        private const string PrefabFolder = "Assets/Resources/CardLand";
        private const string VfxBuilderType = "TawanOS.EditorTools.Vfx.DrawPitSmokeVfxBuilder, Unity.Testing.VisualEffectGraph.Editor";

        [MenuItem("Tools/TawanOS/VFX/Create Card Land VFX")]
        private static void Create_Menu()
        {
            Create();
        }

        // Batch: -executeMethod TawanOS.EditorTools.CardLandVfxSetupTool.Create
        public static void Create()
        {
            var builder = System.Type.GetType(VfxBuilderType);
            if (builder == null)
            {
                Debug.LogError("[CardLandVfxSetupTool] VFX Graph builder not found (Assets/Scripts/Editor/VfxGraphBuilder).");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            builder.GetMethod("BuildCardLand").Invoke(null, null);

            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Resources", "CardLand");
            MakePrefab("CardLandBlack");
            MakePrefab("CardLandWhite");
        }

        private static void MakePrefab(string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.VFX.VisualEffectAsset>($"Assets/Art/VFX/CardLand/{name}.vfx");
            if (asset == null)
            {
                Debug.LogError($"[CardLandVfxSetupTool] {name}.vfx was not built.");
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
            Debug.Log($"<color=green>[CardLandVfxSetupTool] {path} built.</color>");
        }
    }
}
#endif
