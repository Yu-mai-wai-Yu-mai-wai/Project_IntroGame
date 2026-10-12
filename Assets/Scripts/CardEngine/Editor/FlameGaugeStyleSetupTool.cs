using UnityEditor;
using UnityEngine;

namespace TawanOS.CardEngine
{
    /// <summary>
    /// Creates Resources/FlameGaugeStyle.asset, the tunable look of the กุศล / มลทิน flame rings (FlameGaugeHUD).
    /// Safe to run again: an existing asset is left as the design team tuned it.
    /// Batch: -executeMethod TawanOS.CardEngine.FlameGaugeStyleSetupTool.CreateBatch
    /// </summary>
    public static class FlameGaugeStyleSetupTool
    {
        private const string AssetPath = "Assets/Resources/FlameGaugeStyle.asset";

        [MenuItem("Tools/TawanOS/Card Engine/Create Flame Gauge Style")]
        private static void CreateMenu()
        {
            Selection.activeObject = Create();
        }

        public static void CreateBatch()
        {
            EditorApplication.Exit(Create() != null ? 0 : 1);
        }

        public static FlameGaugeStyleSO Create()
        {
            var style = AssetDatabase.LoadAssetAtPath<FlameGaugeStyleSO>(AssetPath);
            if (style != null) return style;
            style = ScriptableObject.CreateInstance<FlameGaugeStyleSO>();
            AssetDatabase.CreateAsset(style, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[FlameGaugeStyleSetupTool] Created {AssetPath}.</color>");
            return style;
        }
    }
}
