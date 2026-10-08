using UnityEditor;
using UnityEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Creates Resources/RewardFx.asset (card back, smoke, burn material and the reward screen's timings) and
    /// the burn material. Safe to run again: it only fills in missing art references and never resets the
    /// numbers the design team tuned. It does not touch RewardScene.
    /// </summary>
    public static class RewardFxSetupTool
    {
        private const string ConfigPath = "Assets/Resources/RewardFx.asset";
        private const string ShaderName = "TawanOS/UI/BurnDissolve";
        private const string MaterialPath = "Assets/Art/Shaders/Mat_UIBurn.mat";
        private const string CardBackPath = "Assets/ProjectAsset/CombatDemo/BackCard.png";
        private const string SmokePath = "Assets/Art/Particles/SoftSmoke.png";

        [MenuItem("Tools/TawanOS/Rewards/Create Reward FX Config")]
        public static void CreateConfigMenu()
        {
            if (CreateConfig()) Selection.activeObject = AssetDatabase.LoadAssetAtPath<RewardFxConfigSO>(ConfigPath);
        }

        /// <summary>-executeMethod TawanOS.GameFlow.RewardFxSetupTool.CreateConfigBatch</summary>
        public static void CreateConfigBatch()
        {
            EditorApplication.Exit(CreateConfig() ? 0 : 1);
        }

        public static bool CreateConfig()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[RewardFxSetupTool] Shader '{ShaderName}' not found (Assets/Art/Shaders/UIBurnDissolve.shader).");
                return false;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Mat_UIBurn" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            var config = AssetDatabase.LoadAssetAtPath<RewardFxConfigSO>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<RewardFxConfigSO>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            if (config.burnMaterial == null) config.burnMaterial = material;
            if (config.cardBack == null) config.cardBack = AssetDatabase.LoadAssetAtPath<Sprite>(CardBackPath);
            if (config.smokeTexture == null) config.smokeTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(SmokePath);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            bool ok = config.burnMaterial != null && config.cardBack != null && config.smokeTexture != null;
            if (ok) Debug.Log($"<color=green>[RewardFxSetupTool] {ConfigPath} is ready. Tune the reward screen's motion there.</color>");
            else Debug.LogError($"[RewardFxSetupTool] Missing art: cardBack={config.cardBack != null} ({CardBackPath}), smoke={config.smokeTexture != null} ({SmokePath}).");
            return ok;
        }
    }
}
