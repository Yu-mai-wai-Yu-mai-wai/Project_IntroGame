#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Copies the default card frames from CardCube3DPrefab's CardView3D into Resources/CardCatalog, so card
    // screens in scenes with no card on the table (the deck viewer on the map) draw the same frames.
    // Batch: -executeMethod TawanOS.CardEngine.CardCatalogFramesTool.CopyDefaultFrames
    public static class CardCatalogFramesTool
    {
        private const string CardPrefabPath = "Assets/CardEngineData/Prefabs/CardCube3DPrefab.prefab";

        [MenuItem("Tools/TawanOS/Card Engine/Copy Default Frames To Card Catalog")]
        public static void CopyDefaultFrames()
        {
            if (!TryCopy(out string error))
            {
                Debug.LogError("[CardCatalogFramesTool] " + error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static bool TryCopy(out string error)
        {
            var catalog = CardCatalogSO.Load();
            if (catalog == null) { error = "Resources/CardCatalog not found."; return false; }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            var view = prefab != null ? prefab.GetComponentInChildren<CardView3D>(true) : null;
            if (view == null) { error = $"No CardView3D on {CardPrefabPath}."; return false; }
            if (view.defaultWhiteFrame == null || view.defaultBlackFrame == null)
            {
                error = $"{CardPrefabPath} has no default white / black frame set.";
                return false;
            }

            Undo.RecordObject(catalog, "Copy Default Frames To Card Catalog");
            catalog.defaultWhiteFrame = view.defaultWhiteFrame;
            catalog.defaultBlackFrame = view.defaultBlackFrame;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CardCatalogFramesTool] CardCatalog frames set: {catalog.defaultWhiteFrame.name}, {catalog.defaultBlackFrame.name}.");
            error = null;
            return true;
        }
    }
}
#endif
