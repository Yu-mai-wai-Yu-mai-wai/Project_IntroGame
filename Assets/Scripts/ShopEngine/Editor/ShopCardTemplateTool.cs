using TawanOS.GameFlow;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace TawanOS.ShopEngine
{
    /// <summary>
    /// Saves the real card face (built by <see cref="RewardSetupTool.BuildCardTemplate"/>, the same card as the
    /// reward screen) as a prefab and assigns it to ShopConfig.cardFaceTemplate, so the shop shows real cards
    /// on its shelf. ShopScene itself is not touched.
    /// </summary>
    public static class ShopCardTemplateTool
    {
        private const string PrefabPath = "Assets/ShopEngineData/ShopCardTemplate.prefab";
        private const string ConfigPath = "Assets/ShopEngineData/ShopConfig.asset";

        [MenuItem("Tools/TawanOS/Shop Engine/Create Shop Card Template")]
        private static void CreateTemplateMenu()
        {
            // Only a second run overwrites something, so only then ask
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null
                && !TawanOS.EditorTools.SetupGuard.Confirm("Create Shop Card Template")) return;
            if (CreateTemplate()) Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        /// <summary>-executeMethod TawanOS.ShopEngine.ShopCardTemplateTool.CreateTemplateBatch</summary>
        public static void CreateTemplateBatch()
        {
            EditorApplication.Exit(CreateTemplate() ? 0 : 1);
        }

        public static bool CreateTemplate()
        {
            var config = AssetDatabase.LoadAssetAtPath<ShopConfigSO>(ConfigPath);
            if (config == null)
            {
                Debug.LogError($"[ShopCardTemplateTool] {ConfigPath} not found.");
                return false;
            }

            // Same fonts as the reward screen's cards
            var titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MN-RueangLao SDF.asset")
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Charm-Bold SDF.asset");
            var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/EkkamaiVibe SDF.asset")
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Sarabun-Regular SDF.asset");
            var panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var holder = new GameObject("TemplateHolder", typeof(RectTransform));
            try
            {
                var card = RewardSetupTool.BuildCardTemplate(holder.transform, bodyFont, titleFont, panelSprite);
                card.gameObject.name = "ShopCardTemplate";
                card.transform.SetParent(null, false);

                var prefab = PrefabUtility.SaveAsPrefabAsset(card.gameObject, PrefabPath, out bool saved);
                Object.DestroyImmediate(card.gameObject);
                if (!saved || prefab == null)
                {
                    Debug.LogError($"[ShopCardTemplateTool] Could not save {PrefabPath}.");
                    return false;
                }

                config.cardFaceTemplate = prefab.GetComponent<RewardCardView>();
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=green>[ShopCardTemplateTool] Saved {PrefabPath} and assigned it to {ConfigPath}.</color>");
                return config.cardFaceTemplate != null;
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }
    }
}
