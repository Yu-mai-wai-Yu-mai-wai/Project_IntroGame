using System.IO;
using UnityEditor;
using UnityEngine;

namespace TawanOS.CardEngine
{
    /// <summary>
    /// Binds the finished full-card pictures in Assets/Art/Cards/FramedCard to the cards: s_xxxx.png goes to
    /// the cardImage of the card whose cardId is s_xxxx (frame, name and text are already in the picture; Unity
    /// only draws attack and khwan on top). Makes sure each picture imports as a Sprite. Other image slots are
    /// left alone. Safe to run again after a picture is replaced or added.
    /// </summary>
    public static class FramedCardBinder
    {
        private const string Folder = "Assets/Art/Cards/FramedCard";

        [MenuItem("Tools/TawanOS/Card Engine/Bind Framed Card Pictures")]
        private static void BindMenu()
        {
            Bind();
        }

        /// <summary>-executeMethod TawanOS.CardEngine.FramedCardBinder.BindBatch</summary>
        public static void BindBatch()
        {
            EditorApplication.Exit(Bind() ? 0 : 1);
        }

        public static bool Bind()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                Debug.LogError($"[FramedCardBinder] {Folder} not found.");
                return false;
            }

            int bound = 0, unchanged = 0;
            var missing = new System.Collections.Generic.List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:CardDataSO"))
            {
                var card = AssetDatabase.LoadAssetAtPath<CardDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (card == null || string.IsNullOrEmpty(card.cardId)) continue;

                string path = $"{Folder}/{card.cardId}.png";
                if (!File.Exists(path)) { missing.Add(card.cardId); continue; }

                var sprite = EnsureSprite(path);
                if (sprite == null) { missing.Add(card.cardId); continue; }
                if (card.cardImage == sprite) { unchanged++; continue; }

                Undo.RecordObject(card, "Bind framed card picture");
                card.cardImage = sprite;
                EditorUtility.SetDirty(card);
                bound++;
            }
            AssetDatabase.SaveAssets();

            Debug.Log($"[FramedCardBinder] Bound {bound}, already bound {unchanged}." +
                      (missing.Count > 0 ? $" No picture for: {string.Join(", ", missing)}" : ""));
            return missing.Count == 0;
        }

        private static Sprite EnsureSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
