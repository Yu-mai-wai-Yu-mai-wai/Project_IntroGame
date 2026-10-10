using System.IO;
using UnityEditor;
using UnityEngine;

namespace TawanOS.CardEngine
{
    /// <summary>
    /// Binds the finished full-card pictures in Assets/Art/Cards/FramedCard to the cards: s_xxxx.png goes to
    /// the cardImage of the card whose cardId is s_xxxx (frame, name and text are already in the picture; Unity
    /// only draws attack and khwan on top). back.png becomes the card back everywhere: the card prefab (hands,
    /// face-down cards), the reward / shop face-down cards (RewardFx) and the deck pile material on the combat
    /// table. Makes sure each picture imports as a Sprite. Other image slots are left alone. Safe to run again
    /// after a picture is replaced or added.
    /// </summary>
    public static class FramedCardBinder
    {
        private const string Folder = "Assets/Art/Cards/FramedCard";
        private const string BackPath = Folder + "/back.png";
        private const string CardPrefabPath = "Assets/CardEngineData/Prefabs/CardCube3DPrefab.prefab";
        private const string RewardFxPath = "Assets/Resources/RewardFx.asset";
        private const string PileMaterialPath = "Assets/ProjectAsset/CombatDemo/BackCardMat.mat";

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
            bool backOk = BindBack();
            AssetDatabase.SaveAssets();

            Debug.Log($"[FramedCardBinder] Bound {bound}, already bound {unchanged}." +
                      (missing.Count > 0 ? $" No picture for: {string.Join(", ", missing)}" : ""));
            return missing.Count == 0 && backOk;
        }

        private static bool BindBack()
        {
            var back = File.Exists(BackPath) ? EnsureSprite(BackPath) : null;
            if (back == null)
            {
                Debug.LogError($"[FramedCardBinder] {BackPath} not found; card back left as it was.");
                return false;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            var view = prefab != null ? prefab.GetComponent<CardView3D>() : null;
            if (view != null && view.cardBack != back)
            {
                Undo.RecordObject(view, "Bind card back");
                view.cardBack = back;
                EditorUtility.SetDirty(view);
                PrefabUtility.SavePrefabAsset(prefab);
            }

            var fx = AssetDatabase.LoadAssetAtPath<TawanOS.GameFlow.RewardFxConfigSO>(RewardFxPath);
            if (fx != null && fx.cardBack != back)
            {
                Undo.RecordObject(fx, "Bind card back");
                fx.cardBack = back;
                EditorUtility.SetDirty(fx);
            }

            // The deck pile on the combat table is a box textured with the back
            var pile = AssetDatabase.LoadAssetAtPath<Material>(PileMaterialPath);
            if (pile != null)
            {
                Undo.RecordObject(pile, "Bind card back");
                foreach (var prop in new[] { "_BaseMap", "_MainTex", "_EmissionMap" })
                    if (pile.HasProperty(prop) && pile.GetTexture(prop) != null) pile.SetTexture(prop, back.texture);
                EditorUtility.SetDirty(pile);
            }

            Debug.Log($"[FramedCardBinder] Card back: prefab={(view != null)} rewardFx={(fx != null)} pileMaterial={(pile != null)}");
            return view != null && fx != null && pile != null;
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
