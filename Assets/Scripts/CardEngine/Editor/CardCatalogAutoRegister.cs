#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Every Card Data asset belongs in Resources/CardCatalog (the pit and random summons draw from it).
    // A card created anywhere (Create > TawanOS > CardEngine > Card Data, the inspector's New Card
    // button, or a duplicate) is added automatically, and gets its file name as card id if it has none.
    public class CardCatalogAutoRegister : AssetPostprocessor
    {
        private const string CatalogPath = "Assets/CardEngineData/Resources/CardCatalog.asset";

        public static CardCatalogSO LoadCatalog()
        {
            return AssetDatabase.LoadAssetAtPath<CardCatalogSO>(CatalogPath);
        }

        public static void AddToCatalog(CardDataSO card)
        {
            var catalog = LoadCatalog();
            if (catalog == null || card == null || catalog.cards.Contains(card)) return;

            Undo.RecordObject(catalog, "Add card to catalog");
            catalog.cards.Add(card);
            EditorUtility.SetDirty(catalog);
            Debug.Log($"[CardCatalog] Added {card.name} ({card.cardNameThai})");
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
            {
                if (!path.EndsWith(".asset")) continue;
                string captured = path;

                // Changing assets during an import is unsafe; do it right after
                EditorApplication.delayCall += () =>
                {
                    var card = AssetDatabase.LoadAssetAtPath<CardDataSO>(captured);
                    if (card == null) return;

                    if (string.IsNullOrEmpty(card.cardId))
                    {
                        card.cardId = Path.GetFileNameWithoutExtension(captured);
                        EditorUtility.SetDirty(card);
                    }

                    var catalog = LoadCatalog();
                    if (catalog != null && !catalog.cards.Contains(card))
                    {
                        AddToCatalog(card);
                        AssetDatabase.SaveAssets();
                    }
                };
            }

            // Drop deleted cards from the catalog
            if (deleted.Length > 0)
            {
                EditorApplication.delayCall += () =>
                {
                    var catalog = LoadCatalog();
                    if (catalog == null) return;
                    if (catalog.cards.RemoveAll(c => c == null) > 0)
                    {
                        EditorUtility.SetDirty(catalog);
                        AssetDatabase.SaveAssets();
                    }
                };
            }
        }
    }
}
#endif
