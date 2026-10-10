using System.IO;
using UnityEditor;
using UnityEngine;

namespace TawanOS.CardEngine
{
    /// <summary>
    /// Makes Assets/Art/Cards/FramedCard/back.png the card back everywhere: the card prefab (hands, face-down cards),
    /// the reward / shop face-down cards (RewardFx) and the deck pile material on the combat table. The card faces
    /// are bound by <see cref="CardArtBinder"/>. Safe to run again after the picture is replaced.
    /// </summary>
    public static class CardBackBinder
    {
        private const string BackPath = "Assets/Art/Cards/FramedCard/back.png";
        private const string CardPrefabPath = "Assets/CardEngineData/Prefabs/CardCube3DPrefab.prefab";
        private const string RewardFxPath = "Assets/Resources/RewardFx.asset";
        private const string PileMaterialPath = "Assets/ProjectAsset/CombatDemo/BackCardMat.mat";

        [MenuItem("Tools/TawanOS/Card Engine/Bind Card Back")]
        private static void BindMenu()
        {
            Bind();
        }

        /// <summary>-executeMethod TawanOS.CardEngine.CardBackBinder.BindBatch</summary>
        public static void BindBatch()
        {
            EditorApplication.Exit(Bind() ? 0 : 1);
        }

        public static bool Bind()
        {
            bool ok = BindBack();
            AssetDatabase.SaveAssets();
            return ok;
        }

        private static bool BindBack()
        {
            var back = File.Exists(BackPath) ? EnsureSprite(BackPath) : null;
            if (back == null)
            {
                Debug.LogError($"[CardBackBinder] {BackPath} not found; card back left as it was.");
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

            Debug.Log($"[CardBackBinder] Card back: prefab={(view != null)} rewardFx={(fx != null)} pileMaterial={(pile != null)}");
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
