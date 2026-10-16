using System.Collections.Generic;
using TawanOS.CardEngine;
using UnityEngine;
using UnityEngine.Serialization;

namespace TawanOS.GameFlow
{
    public enum RewardTier
    {
        Minor,
        Elite,
        Boss,
        Offering, // กองของเซ่น on the map: no fight, the one card taken from the pile
        EventCard // a หมอกดำ event that hands out a card (appended: saves store the enum as a number)
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
        [Tooltip("กองของเซ่น (the map's offering-pile node): the one card taken from the pile (Game Designer, 12 Oct)")]
        [Min(1)] public int offeringChoices = 1;
        [Tooltip("A หมอกดำ event's card reward")]
        [Min(1)] public int eventChoices = 3;

        [Header("Card types")]
        [Tooltip("Every card reward (after a fight, กองของเซ่น, หมอกดำ) rolls each card's type by these weights.")]
        [FormerlySerializedAs("nonCombatOdds")] public CardTypeOdds cardTypeOdds = new CardTypeOdds();

        [Header("Skip")]
        [Tooltip("Off (Game Designer 10 Oct): the player must take one of the cards.")]
        public bool allowSkip = false;
        [Tooltip("Bonus incense for taking no card (keeps the deck lean).")]
        [Min(0)] public int skipIncense = 10;

        public int ChoicesFor(RewardTier tier)
        {
            return tier switch
            {
                RewardTier.Boss => bossChoices,
                RewardTier.Elite => eliteChoices,
                RewardTier.Offering => offeringChoices,
                RewardTier.EventCard => eventChoices,
                _ => minorChoices,
            };
        }

        // Boss-only cards (ผีปอบ) are never a reward; the player can still draw them from the pit
        public List<CardDataSO> GetPool()
        {
            if (cardPool != null && cardPool.Exists(c => c != null)) return cardPool.FindAll(c => c != null && !c.bossOnly);
            var catalog = CardCatalogSO.Load();
            return catalog != null ? catalog.BaseCards().FindAll(c => !c.bossOnly) : new List<CardDataSO>();
        }
    }
}
