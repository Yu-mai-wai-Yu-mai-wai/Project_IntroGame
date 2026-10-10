using System.IO;
using UnityEditor;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Binds Assets/Art/Cards/<cardId>_<name>.png to CardDataSO.artwork by cardId prefix (H8).
    // The final art set is complete (42/42), so a card that still has the old printed `cardImage`
    // has it cleared: CardDetailPanelUI hides `artwork` whenever `cardImage` is set.
    // Idempotent: running twice changes nothing. Batch: -executeMethod TawanOS.CardEngine.CardArtBinder.BindFromCli
    public static class CardArtBinder
    {
        const string ArtFolder = "Assets/Art/Cards";

        [MenuItem("Tools/TawanOS/Card Engine/Bind Card Art")]
        public static void BindFromMenu() => Bind();

        public static void BindFromCli() => Bind();

        public static (int bound, int missing, int clearedPrinted, int changed) Bind()
        {
            int bound = 0, missing = 0, cleared = 0, changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:CardDataSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardDataSO>(path);
                var sprite = FindSprite(card.cardId);
                if (sprite == null)
                {
                    missing++;
                    Debug.LogWarning($"[CardArtBinder] no art for {card.cardId} ({card.cardNameThai})");
                    continue;
                }
                bound++;
                bool dirty = false;
                if (card.artwork != sprite) { card.artwork = sprite; dirty = true; }
                if (card.cardImage != null) { card.cardImage = null; cleared++; dirty = true; }
                if (dirty) { EditorUtility.SetDirty(card); changed++; }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[CardArtBinder] bound={bound} missing={missing} clearedPrinted={cleared} changed={changed}");
            return (bound, missing, cleared, changed);
        }

        // File names are "<cardId>_<thai name>.png"; the id is matched as a prefix so odd names still bind.
        static Sprite FindSprite(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { ArtFolder }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var file = Path.GetFileNameWithoutExtension(p);
                if (file.StartsWith(cardId + "_", System.StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<Sprite>(p);
            }
            return null;
        }
    }
}
