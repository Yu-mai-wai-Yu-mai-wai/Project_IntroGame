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
    /// the scripted tutorial bot are left as they are. Loaded from Resources/EnemyDeckProgression; create it with
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

        private static EnemyDeckProgressionSO cached;

        public static EnemyDeckProgressionSO Load()
        {
            if (cached == null) cached = Resources.Load<EnemyDeckProgressionSO>(ResourceName);
            return cached;
        }

        /// <summary>The cards the enemy plays in this fight, or null to keep its own deck.</summary>
        public List<CardDataSO> DeckFor(NodeType nodeType, int nodesPassed, EnemyProfileSO enemy)
        {
            if (!enabled || enemy == null || enemy.scriptedDeck || enemy.deck == null || enemy.deck.Count == 0) return null;

            var steps = nodeType == NodeType.Boss ? boss : minor;

            Step best = null;
            foreach (var step in steps)
            {
                if (step == null || step.preset == null || step.preset.cards.Count == 0) continue;
                if (step.fromNodesPassed > nodesPassed) continue;
                if (best == null || step.fromNodesPassed >= best.fromNodesPassed) best = step;
            }
            if (best == null) return null;

            Debug.Log($"[EnemyDeckProgression] {enemy.enemyName} at {nodesPassed} nodes passed plays \"{best.preset.presetName}\" ({best.preset.cards.Count} cards)");
            return new List<CardDataSO>(best.preset.cards);
        }
    }
}
