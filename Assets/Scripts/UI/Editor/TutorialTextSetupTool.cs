using UnityEditor;
using UnityEngine;

namespace TawanOS.UI
{
    /// <summary>
    /// Creates Assets/Resources/TutorialText.asset with the default tutorial words, then selects it so the words
    /// can be edited in the Inspector. An existing asset is left as it is. Batch:
    /// Unity.exe -batchmode -nographics -quit -projectPath ... -executeMethod TawanOS.UI.TutorialTextSetupTool.CreateTutorialText
    /// </summary>
    public static class TutorialTextSetupTool
    {
        private const string AssetPath = "Assets/Resources/" + TutorialTextSO.ResourceName + ".asset";

        [MenuItem("Tools/TawanOS/UI/Create Tutorial Text")]
        public static void CreateTutorialTextFromMenu()
        {
            var asset = TryCreate();
            if (asset != null) Selection.activeObject = asset;
        }

        public static void CreateTutorialText()
        {
            bool ok = TryCreate() != null;
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static TutorialTextSO TryCreate()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

            var asset = AssetDatabase.LoadAssetAtPath<TutorialTextSO>(AssetPath);
            if (asset != null)
            {
                Debug.Log($"[TutorialTextSetupTool] {AssetPath} already exists, kept as it is");
                return asset;
            }

            asset = ScriptableObject.CreateInstance<TutorialTextSO>();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            TutorialTextSO.ResetCache();
            Debug.Log($"[TutorialTextSetupTool] Created {AssetPath}");
            return asset;
        }
    }
}
