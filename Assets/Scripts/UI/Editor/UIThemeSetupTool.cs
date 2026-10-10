using TMPro;
using UnityEditor;
using UnityEngine;

namespace TawanOS.UI
{
    /// <summary>
    /// Creates or refills Assets/Resources/UITheme.asset (plan task G1) with the Thai fonts the project already has.
    /// Existing values are kept; only empty font slots are filled. Batch:
    /// Unity.exe -batchmode -nographics -quit -projectPath ... -executeMethod TawanOS.UI.UIThemeSetupTool.CreateTheme
    /// </summary>
    public static class UIThemeSetupTool
    {
        private const string AssetPath = "Assets/Resources/" + UIThemeSO.ResourceName + ".asset";
        private const string BodyFontPath = "Assets/Fonts/EkkamaiVibe SDF.asset";
        private const string FallbackBodyFontPath = "Assets/Fonts/Sarabun-Regular SDF.asset";
        private const string TitleFontPath = "Assets/Fonts/MN-RueangLao SDF.asset";

        [MenuItem("Tools/TawanOS/UI/Create UI Theme")]
        public static void CreateThemeFromMenu()
        {
            CreateTheme();
        }

        public static void CreateTheme()
        {
            bool ok = TryCreate();
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool TryCreate()
        {
            var body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
            if (body == null) body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackBodyFontPath);
            var title = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
            if (body == null)
            {
                Debug.LogError($"[UIThemeSetupTool] Body font not found at {BodyFontPath} or fallback");
                return false;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

            var theme = AssetDatabase.LoadAssetAtPath<UIThemeSO>(AssetPath);
            bool created = theme == null;
            if (created)
            {
                theme = ScriptableObject.CreateInstance<UIThemeSO>();
                AssetDatabase.CreateAsset(theme, AssetPath);
            }

            theme.bodyFont = body;
            theme.titleFont = title != null ? title : body;

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            UIThemeSO.ResetCache();
            Debug.Log($"[UIThemeSetupTool] {(created ? "Created" : "Updated")} {AssetPath} (body={theme.bodyFont.name}, title={theme.titleFont.name})");
            return true;
        }
    }
}
