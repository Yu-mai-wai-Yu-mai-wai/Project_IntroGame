using System.Collections.Generic;
using TawanOS.CardEngine;
using UnityEngine;

namespace TawanOS.GameFlow
{
    public enum RewardTier
    {
        Minor,
        Elite,
        Boss,
        Offering // กองของเซ่น on the map: no fight, just the cards
    }

    /// <summary>
    /// Card rewards after a won fight: how many cards are offered (pick one to add to the run deck),
    /// which cards can show up, and whether the player may skip.
    /// </summary>
    [CreateAssetMenu(fileName = "CardRewardConfig", menuName = "TawanOS/GameFlow/Card Reward Config")]
    public class CardRewardConfigSO : ScriptableObject
    {
        [Tooltip("Seeds the run deck if the reward screen is opened before any combat (testing).")]
        public DeckConfigSO starterDeck;

        [Tooltip("Cards that can be offered. Empty = every card in Resources/CardCatalog.")]
        public List<CardDataSO> cardPool = new List<CardDataSO>();

        [Header("Cards offered (pick 1)")]
        [Min(1)] public int minorChoices = 3;
        [Min(1)] public int eliteChoices = 3;
        [Min(1)] public int bossChoices = 4;
        [Tooltip("กองของเซ่น (the map's offering-pile node)")]
        [Min(1)] public int offeringChoices = 3;

        [Header("Skip")]
        public bool allowSkip = true;
        [Tooltip("Bonus incense for taking no card (keeps the deck lean).")]
        [Min(0)] public int skipIncense = 10;

        public int ChoicesFor(RewardTier tier)
        {
            return tier switch
            {
                RewardTier.Boss => bossChoices,
                RewardTier.Elite => eliteChoices,
                RewardTier.Offering => offeringChoices,
                _ => minorChoices,
            };
        }

        public List<CardDataSO> GetPool()
        {
            if (cardPool != null && cardPool.Exists(c => c != null)) return cardPool.FindAll(c => c != null);
            var catalog = CardCatalogSO.Load();
            return catalog != null ? catalog.BaseCards() : new List<CardDataSO>();
        }
    }
}
