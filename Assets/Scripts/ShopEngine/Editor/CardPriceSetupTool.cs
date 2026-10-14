using System.Text;
using TawanOS.CardEngine;
using UnityEditor;
using UnityEngine;

namespace TawanOS.ShopEngine
{
    /// <summary>
    /// Gives every card sold in the ศาลตายาย (the base cards of the catalog) a starting price in its Card Data
    /// (<see cref="CardDataSO.shopPrice"/>), worked out from its casting cost by <see cref="ShopConfigSO.PriceFromCost"/>
    /// (Game Designer, 15 Oct: 5-8 for the dear cards, 1-4 for the cheap and weak ones). Cards that already have a price
    /// keep it, so prices tuned by hand are never overwritten; run "Reset Card Prices From Cost" to start them over.
    /// </summary>
    public static class CardPriceSetupTool
    {
        [MenuItem("Tools/TawanOS/Shop Engine/Set Card Prices From Cost")]
        private static void SetMenu() => Apply(overwrite: false);

        [MenuItem("Tools/TawanOS/Shop Engine/Reset Card Prices From Cost")]
        private static void ResetMenu()
        {
            if (EditorUtility.DisplayDialog("Reset card prices", "Every card's shop price goes back to the one worked out from its cost. Prices set by hand are lost.", "Reset", "Cancel"))
                Apply(overwrite: true);
        }

        /// <summary>-executeMethod TawanOS.ShopEngine.CardPriceSetupTool.SetBatch</summary>
        public static void SetBatch()
        {
            EditorApplication.Exit(Apply(overwrite: false) ? 0 : 1);
        }

        public static bool Apply(bool overwrite)
        {
            var catalog = CardCatalogSO.Load();
            if (catalog == null)
            {
                Debug.LogError("[CardPriceSetupTool] No CardCatalog in Resources.");
                return false;
            }

            var log = new StringBuilder();
            int set = 0, kept = 0;
            foreach (var card in catalog.BaseCards())
            {
                if (card == null) continue;
                if (card.shopPrice > 0 && !overwrite) { kept++; continue; }
                card.shopPrice = ShopConfigSO.PriceFromCost(card);
                EditorUtility.SetDirty(card);
                set++;
                log.Append($"{card.cardId}={card.shopPrice} ");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[CardPriceSetupTool] Priced {set} card(s), kept {kept} already priced. {log}");
            return true;
        }
    }
}
