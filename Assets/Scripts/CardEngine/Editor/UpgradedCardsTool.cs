#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Makes the upgraded version (the เมรุ's ชำระ) of every card that has none, and links it from the card's
    // upgradedCard. A first draft for the team to tune in the Inspector:
    //  - familiars: +1 attack and +1 khwan
    //  - amulets and incantations: cost -1 (merit for white magic, corruption for black magic, never below 0)
    // The copy keeps the card's art and abilities; its id is "<cardId>_up" and its names end in "+".
    // Cards that already have an upgraded version, upgraded versions themselves and enemy-only cards are left alone,
    // so running it again only fills in new cards.
    public static class UpgradedCardsTool
    {
        private const string SourceFolder = "Assets/CardEngineData/Cards/Sheet";
        private const string TargetFolder = "Assets/CardEngineData/Cards/Upgraded";
        public const string IdSuffix = "_up";

        [MenuItem("Tools/TawanOS/Card Engine/Create Upgraded Cards")]
        private static void Create_Menu()
        {
            Create();
        }

        // Batch: -executeMethod TawanOS.CardEngine.UpgradedCardsTool.Create
        public static void Create()
        {
            if (!AssetDatabase.IsValidFolder(TargetFolder)) AssetDatabase.CreateFolder("Assets/CardEngineData/Cards", "Upgraded");

            var cards = new List<CardDataSO>();
            foreach (var guid in AssetDatabase.FindAssets("t:CardDataSO", new[] { SourceFolder }))
            {
                var card = AssetDatabase.LoadAssetAtPath<CardDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (card != null) cards.Add(card);
            }

            int made = 0, linked = 0;
            foreach (var card in cards)
            {
                if (card.upgradedCard != null || card.enemyOnly || card.cardId.EndsWith(IdSuffix)) continue;

                string path = $"{TargetFolder}/Card_{card.cardId}{IdSuffix}.asset";
                var up = AssetDatabase.LoadAssetAtPath<CardDataSO>(path);
                if (up == null)
                {
                    up = Object.Instantiate(card);
                    up.name = $"Card_{card.cardId}{IdSuffix}";
                    up.cardId = card.cardId + IdSuffix;
                    up.cardNameThai = card.cardNameThai + "+";
                    up.cardNameEng = string.IsNullOrEmpty(card.cardNameEng) ? "" : card.cardNameEng + "+";
                    up.upgradedCard = null;
                    Improve(up);
                    AssetDatabase.CreateAsset(up, path);
                    made++;
                }

                card.upgradedCard = up;
                EditorUtility.SetDirty(card);
                linked++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[UpgradedCardsTool] {made} upgraded cards made, {linked} cards linked to theirs ({TargetFolder}).</color>");
        }

        private static void Improve(CardDataSO card)
        {
            if (card.cardType == CardType.Familiar)
            {
                card.familiarDamage += 1;
                card.familiarHealth += 1;
                return;
            }
            if (card.magicSchool == MagicSchool.WhiteMagic) card.meritCost = Mathf.Max(0, card.meritCost - 1);
            else card.corruptionGain = Mathf.Max(0, card.corruptionGain - 1);
        }
    }
}
#endif
