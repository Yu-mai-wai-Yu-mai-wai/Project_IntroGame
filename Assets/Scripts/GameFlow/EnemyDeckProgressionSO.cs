using System;
using System.Collections.Generic;
using UnityEngine;
using TawanOS.CardEngine;
using TawanOS.MapEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Enemies get harder decks the further the player is into the map. For ordinary fights and the boss a list of steps:
    /// from N nodes passed (the fight's map floor, 0 = the first node) the enemy plays that preset's cards instead
    /// of its own deck. The last step whose number is reached wins. Enemies without a card deck (moveset only) and
    /// the scripted tutorial bot are left as they are. Their khwan grows the same way: khwanPerNode for every node
    /// passed, up to khwanCap (not the boss: its khwan is its profile's, per phase). Loaded from Resources/EnemyDeckProgression; create it with
    /// Tools/TawanOS/Game Flow/Create Enemy Deck Presets.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyDeckProgression", menuName = "TawanOS/Game Flow/Enemy Deck Progression")]
    public class EnemyDeckProgressionSO : ScriptableObject
    {
        public const string ResourceName = "EnemyDeckProgression";

        [Serializable]
        public class Step
        {
            [Tooltip("Used from this many nodes passed (the fight's map floor counted from 0).")]
            [Min(0)] public int fromNodesPassed;
            public EnemyDeckPresetSO preset;
        }

        [Tooltip("Off = every enemy plays its own deck, as before.")]
        public bool enabled = true;
        // The map has no elite fights any more: everything that is not the boss uses the minor steps
        public List<Step> minor = new List<Step>();
        public List<Step> boss = new List<Step>();

        [Header("Khwan")]
        [Tooltip("Off = every enemy keeps its profile's max khwan.")]
        public bool scaleKhwan = true;
        [Tooltip("Khwan an enemy gains for every node passed, on top of its profile's max khwan.")]
        [Min(0)] public int khwanPerNode = 2;
        [Tooltip("An enemy's khwan never grows past this (one whose profile already has more keeps its own).")]
        [Min(1)] public int khwanCap = 10;

        private static EnemyDeckProgressionSO cached;

        /// <summary>The enemy's max khwan for this fight, or null to keep its profile's.</summary>
        public int? KhwanFor(int nodesPassed, EnemyProfileSO enemy)
        {
            // A boss keeps its profile's khwan: each of its phases has a bar of that size (15 Oct)
            if (!enabled || !scaleKhwan || enemy == null || enemy.scriptedDeck || enemy.isBoss) return null;
            int grown = Mathf.Min(enemy.maxKhwan + khwanPerNode * Mathf.Max(0, nodesPassed), khwanCap);
            return Mathf.Max(enemy.maxKhwan, grown);
        }

        public static EnemyDeckProgressionSO Load()
        {
            if (cached == null) cached = Resources.Load<EnemyDeckProgressionSO>(ResourceName);
            return cached;
        }

        /// <summary>The cards the enemy plays in this fight, or null to keep its own deck.</summary>
        public List<CardDataSO> DeckFor(NodeType nodeType, int nodesPassed, EnemyProfileSO enemy, int? playerUpgradedOverride = null)
        {
            if (!enabled || enemy == null || enemy.scriptedDeck || enemy.deck == null || enemy.deck.Count == 0) return null;

            int deckSize = enemy.deck.Count;
            bool isBoss = nodeType == NodeType.Boss || enemy.isBoss;
            int ceiling = (!isBoss && nodesPassed < 6) ? 0 : Mathf.FloorToInt(deckSize / 3f);

            int playerUpgradedCount = playerUpgradedOverride ?? CountPlayerUpgradedCards();
            int targetUpgrades = Mathf.Min(ceiling, playerUpgradedCount + 1);

            var result = new List<CardDataSO>(enemy.deck);
            if (targetUpgrades <= 0) return result;

            int swapped = 0;
            for (int i = 0; i < result.Count && swapped < targetUpgrades; i++)
            {
                var card = result[i];
                if (card != null && card.upgradedCard != null)
                {
                    result[i] = card.upgradedCard;
                    swapped++;
                }
            }

            Debug.Log($"[EnemyDeckProgression] {enemy.enemyName} at {nodesPassed} nodes passed (playerUpgraded={playerUpgradedCount}, ceiling={ceiling}) upgraded {swapped} cards");
            return result;
        }

        public static int CountPlayerUpgradedCards()
        {
            var deck = RunState.Current != null && RunState.Current.HasDeck ? RunState.Current.Deck : null;
            if (deck == null) return 0;
            int count = 0;
            foreach (var card in deck)
            {
                if (IsCardUpgraded(card)) count++;
            }
            return count;
        }

        public static bool IsCardUpgraded(CardDataSO card)
        {
            if (card == null) return false;
            if (!string.IsNullOrEmpty(card.cardId) && card.cardId.EndsWith("_up")) return true;
            if (!string.IsNullOrEmpty(card.name) && card.name.EndsWith("_up")) return true;
            return false;
        }
    }
}
