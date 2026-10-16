using System;
using System.Collections.Generic;
using TawanOS.CardEngine;
using UnityEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// How likely each card type is when a card is drawn at random (a reward after a fight, กองของเซ่น, a หมอกดำ card
    /// reward, ศาลตายาย's stock). Each pick first rolls a type by these weights, then a card of that type; a type with
    /// no cards left drops out of the roll. The defaults are the Game Designer's 40 / 30 / 30 (บริวาร / เครื่องราง / อาคม).
    /// </summary>
    [Serializable]
    public class CardTypeOdds
    {
        [Min(0)] public float familiar = 40f;
        [Min(0)] public float amulet = 30f;
        [Min(0)] public float incantation = 30f;

        private float WeightOf(CardType type)
        {
            switch (type)
            {
                case CardType.Familiar: return familiar;
                case CardType.Amulet: return amulet;
                case CardType.Incantation: return incantation;
                default: return 0f;
            }
        }

        /// <summary>Up to `count` different cards from `source`, each one's type rolled by the weights.</summary>
        public List<CardDataSO> Pick(List<CardDataSO> source, int count)
        {
            var pool = source.FindAll(c => c != null);
            var picked = new List<CardDataSO>();
            var types = new[] { CardType.Familiar, CardType.Amulet, CardType.Incantation };
            while (picked.Count < count && pool.Count > 0)
            {
                // Only the types that still have cards (and a weight) take part
                float total = 0f;
                foreach (var t in types) if (pool.Exists(c => c.cardType == t)) total += WeightOf(t);

                List<CardDataSO> candidates = null;
                if (total > 0f)
                {
                    float roll = UnityEngine.Random.Range(0f, total);
                    foreach (var t in types)
                    {
                        if (!pool.Exists(c => c.cardType == t) || WeightOf(t) <= 0f) continue;
                        roll -= WeightOf(t);
                        if (roll < 0f) { candidates = pool.FindAll(c => c.cardType == t); break; }
                    }
                }
                if (candidates == null || candidates.Count == 0) candidates = pool; // all weights 0: any card

                var card = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                picked.Add(card);
                pool.Remove(card);
            }
            return picked;
        }
    }
}
