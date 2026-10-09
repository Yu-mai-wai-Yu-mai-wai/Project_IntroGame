#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Fills the defaults on Resources/CardCatalog that runtime code needs outside combat:
    // - the default card frames, from CardCube3DPrefab's CardView3D (the deck viewer on the map draws them)
    // - the starter deck, which a new run is seeded with as soon as it starts
    // Batch: -executeMethod TawanOS.CardEngine.CardCatalogDefaultsTool.FillDefaults
    public static class CardCatalogDefaultsTool
    {
        private const string CardPrefabPath = "Assets/CardEngineData/Prefabs/CardCube3DPrefab.prefab";
        private const string StarterDeckPath = "Assets/CardEngineData/Decks/StarterDeckConfig.asset";

        [MenuItem("Tools/TawanOS/Card Engine/Fill Card Catalog Defaults (Frames, Starter Deck)")]
        public static void FillDefaults()
        {
            if (!TryFill(out string error))
            {
                Debug.LogError("[CardCatalogDefaultsTool] " + error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static bool TryFill(out string error)
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

            var starterDeck = AssetDatabase.LoadAssetAtPath<DeckConfigSO>(StarterDeckPath);
            if (starterDeck == null) { error = $"No DeckConfigSO at {StarterDeckPath}."; return false; }

            Undo.RecordObject(catalog, "Fill Card Catalog Defaults");
            catalog.defaultWhiteFrame = view.defaultWhiteFrame;
            catalog.defaultBlackFrame = view.defaultBlackFrame;
            catalog.starterDeck = starterDeck;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CardCatalogDefaultsTool] CardCatalog defaults set: frames {catalog.defaultWhiteFrame.name}, " +
                      $"{catalog.defaultBlackFrame.name}; starter deck {starterDeck.name} ({starterDeck.startingCards.Count} cards).");
            error = null;
            return true;
        }
    }
}
#endif
