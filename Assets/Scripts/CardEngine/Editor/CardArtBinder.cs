using System.IO;
using UnityEditor;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Binds Assets/Art/Cards/FramedCard/<cardId>.png to CardDataSO.artwork (H8).
    // The picture is the finished card face with its own frame, name and type line; the game draws no frame over
    // it and writes only cost, attack, khwan and the ability text (CardFaceLayout.ArtCarriesFrame).
    // A card that still has an old printed `cardImage` has it cleared, since `cardImage` would hide the artwork.
    // Idempotent: running twice changes nothing. Batch: -executeMethod TawanOS.CardEngine.CardArtBinder.BindFromCli
    public static class CardArtBinder
    {
        const string ArtFolder = "Assets/Art/Cards/FramedCard";

        [MenuItem("Tools/TawanOS/Card Engine/Bind Card Art")]
        public static void BindFromMenu() => Bind();

        public static void BindFromCli()
        {
            var result = Bind();
            EditorApplication.Exit(result.missing == 0 ? 0 : 1);
        }

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
                    Debug.LogWarning($"[CardArtBinder] no art for {card.cardId} ({card.cardNameThai}) in {ArtFolder}");
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

        // File names are "<cardId>.png" (any case of the extension); imported as a Sprite if they are not yet
        static Sprite FindSprite(string cardId)
        {
            if (string.IsNullOrEmpty(cardId) || !AssetDatabase.IsValidFolder(ArtFolder)) return null;
            foreach (var guid in AssetDatabase.FindAssets(cardId, new[] { ArtFolder }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(Path.GetFileNameWithoutExtension(p), cardId, System.StringComparison.OrdinalIgnoreCase)) continue;
                if (AssetImporter.GetAtPath(p) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (sprite != null) return sprite;
            }
            return null;
        }
    }
}
