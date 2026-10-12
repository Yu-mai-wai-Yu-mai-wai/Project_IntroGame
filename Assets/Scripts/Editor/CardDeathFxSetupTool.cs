#if UNITY_EDITOR
using TawanOS.VFX;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    // Creates Resources/CardDeathFx.asset and its burn material (Art/Shaders/Mat_CardBurn.mat, TawanOS/CardBurnDissolve)
    // so dying board familiars burn away. Existing assets are kept; only an empty material slot is filled.
    public static class CardDeathFxSetupTool
    {
        private const string ShaderName = "TawanOS/CardBurnDissolve";
        private const string MaterialPath = "Assets/Art/Shaders/Mat_CardBurn.mat";
        private const string ConfigPath = "Assets/Resources/" + CardDeathFxConfigSO.ResourceName + ".asset";

        [MenuItem("Tools/TawanOS/VFX/Create Card Death FX")]
        private static void Create_Menu()
        {
            Create();
        }

        // Batch: -executeMethod TawanOS.EditorTools.CardDeathFxSetupTool.Create
        public static void Create()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[CardDeathFxSetupTool] Shader '{ShaderName}' not found (Assets/Art/Shaders/CardBurnDissolve.shader).");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Mat_CardBurn" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            var config = AssetDatabase.LoadAssetAtPath<CardDeathFxConfigSO>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<CardDeathFxConfigSO>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            if (config.burnMaterial == null)
            {
                config.burnMaterial = material;
                EditorUtility.SetDirty(config);
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            Debug.Log($"<color=green>[CardDeathFxSetupTool] {ConfigPath} uses {AssetDatabase.GetAssetPath(config.burnMaterial)}.</color>");
        }

        // Play Mode preview for tuning: burns one card on the board (the card is not killed, just hidden)
        [MenuItem("Tools/TawanOS/VFX/Preview Card Burn (Play Mode)")]
        private static void PreviewBurn()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[CardDeathFxSetupTool] Enter Play Mode and have a card on the board first.");
                return;
            }
            // A familiar on the board, else a card in the hand (always on screen)
            TawanOS.CardEngine.CardView3D pick = null;
            foreach (var view in Object.FindObjectsByType<TawanOS.CardEngine.CardView3D>(FindObjectsSortMode.None))
            {
                bool visible = false;
                foreach (var r in view.GetComponentsInChildren<SpriteRenderer>()) visible |= r.isVisible && r.enabled;
                if (!visible) continue;
                var slot = view.GetComponentInParent<TawanOS.CardEngine.BoardSlotView>();
                bool onBoard = slot != null && slot.GetComponent<TawanOS.CardEngine.DeckPileView3D>() == null && !slot.name.Contains("Deck");
                if (onBoard) { pick = view; break; }
                if (pick == null && view.GetComponentInParent<TawanOS.CardEngine.CameraAnchoredHand>() != null) pick = view;
            }
            if (pick != null)
            {
                float seconds = CardBurnEffect.Burn(pick.transform, CardDeathFxConfigSO.Load());
                Debug.Log($"[CardDeathFxSetupTool] Burning '{pick.name}' ({pick.CardData?.cardNameThai}) for {seconds:0.##} s.");
                return;
            }
            Debug.LogWarning("[CardDeathFxSetupTool] No card on the board or in the hand to burn.");
        }
    }
}
#endif
