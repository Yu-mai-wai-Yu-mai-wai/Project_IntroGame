using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Gives the Windows build three graphics levels for the settings menu (ต่ำ / กลาง / สูง). The project had
    /// Mobile and PC, and Mobile was excluded from Standalone, so the build had only PC and the setting could not
    /// change. This lets Mobile run on Standalone (ต่ำ: 0.8 render scale, one shadow cascade, no SSAO), and adds
    /// Medium between them (กลาง: PC at full resolution with SSAO off, two cascades, smaller shadow maps, low soft
    /// shadows) using its own copies of the PC pipeline asset and renderer. Running it again changes nothing.
    /// Menu: Tools/TawanOS/Settings/Set Up Quality Levels
    /// Command line: -executeMethod TawanOS.EditorTools.QualityLevelsTool.Run
    /// </summary>
    public static class QualityLevelsTool
    {
        private const string QualityAsset = "ProjectSettings/QualitySettings.asset";
        private const string PcPipeline = "Assets/Settings/PC_RPAsset.asset";
        private const string PcRenderer = "Assets/Settings/PC_Renderer.asset";
        private const string MediumPipeline = "Assets/Settings/Medium_RPAsset.asset";
        private const string MediumRenderer = "Assets/Settings/Medium_Renderer.asset";
        private const string MediumName = "Medium";

        [MenuItem("Tools/TawanOS/Settings/Set Up Quality Levels")]
        public static void RunFromMenu() => SetUp();

        public static void Run()
        {
            if (!SetUp()) EditorApplication.Exit(1);
        }

        public static bool SetUp()
        {
            var pipeline = MakeMediumPipeline();
            if (pipeline == null) return false;

            var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(QualityAsset)[0]);
            var levels = quality.FindProperty("m_QualitySettings");
            int mobile = Find(levels, "Mobile"), pc = Find(levels, "PC");
            if (mobile < 0 || pc < 0)
            {
                Debug.LogError("[QualityLevelsTool] Expected the Mobile and PC quality levels");
                return false;
            }

            // ต่ำ: Mobile may run on Windows
            var excluded = levels.GetArrayElementAtIndex(mobile).FindPropertyRelative("excludedTargetPlatforms");
            for (int i = excluded.arraySize - 1; i >= 0; i--)
                if (excluded.GetArrayElementAtIndex(i).stringValue == "Standalone") excluded.DeleteArrayElementAtIndex(i);

            // กลาง: a copy of PC right before it, on the medium pipeline asset
            if (Find(levels, MediumName) < 0)
            {
                levels.InsertArrayElementAtIndex(pc); // a copy of PC at PC's place; PC moves one down
                var medium = levels.GetArrayElementAtIndex(pc);
                medium.FindPropertyRelative("name").stringValue = MediumName;
                medium.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
                pc++;
            }

            // Order ต่ำ, กลาง, สูง is what the settings menu steps through; Windows starts on สูง
            quality.FindProperty("m_CurrentQuality").intValue = pc;
            var defaults = quality.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < defaults.arraySize; i++)
            {
                var pair = defaults.GetArrayElementAtIndex(i);
                if (pair.FindPropertyRelative("first").stringValue == "Standalone") pair.FindPropertyRelative("second").intValue = pc;
            }
            quality.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log($"[QualityLevelsTool] Quality levels: {string.Join(", ", QualitySettings.names)} (Standalone default {pc})");
            return true;
        }

        private static int Find(SerializedProperty levels, string name)
        {
            for (int i = 0; i < levels.arraySize; i++)
                if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name) return i;
            return -1;
        }

        // Medium_RPAsset / Medium_Renderer: copies of PC's, made once and then left as they are (they can be tuned
        // by hand afterwards)
        private static Object MakeMediumPipeline()
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(MediumPipeline);
            if (existing != null) return existing;

            if (!AssetDatabase.CopyAsset(PcRenderer, MediumRenderer) || !AssetDatabase.CopyAsset(PcPipeline, MediumPipeline))
            {
                Debug.LogError("[QualityLevelsTool] Could not copy the PC pipeline asset or renderer");
                return null;
            }

            // The renderer without SSAO
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(MediumRenderer))
            {
                if (sub == null || sub.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                var feature = new SerializedObject(sub);
                feature.FindProperty("m_Active").boolValue = false;
                feature.ApplyModifiedPropertiesWithoutUndo();
            }

            var asset = AssetDatabase.LoadMainAssetAtPath(MediumPipeline);
            var so = new SerializedObject(asset);
            so.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue =
                AssetDatabase.LoadMainAssetAtPath(MediumRenderer);
            so.FindProperty("m_MainLightShadowmapResolution").intValue = 1024;
            so.FindProperty("m_AdditionalLightsShadowmapResolution").intValue = 1024;
            so.FindProperty("m_ShadowCascadeCount").intValue = 2;
            so.FindProperty("m_SoftShadowQuality").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
