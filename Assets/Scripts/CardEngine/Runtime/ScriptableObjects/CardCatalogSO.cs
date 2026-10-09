using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Every card in the game. Lets abilities look cards up (random summons). It is loaded from
    // Resources/CardCatalog by EffectResolver, so no scene wiring is needed.
    [CreateAssetMenu(fileName = "CardCatalog", menuName = "TawanOS/CardEngine/Card Catalog")]
    public class CardCatalogSO : ScriptableObject
    {
        public List<CardDataSO> cards = new List<CardDataSO>();

        [Header("Default Frames (cards without a Card Background)")]
        [Tooltip("For card screens in scenes with no card on the table (e.g. the deck viewer on the map). " +
                 "Tools > TawanOS > Card Engine > Fill Card Catalog Defaults fills them from CardCube3DPrefab.")]
        public Sprite defaultWhiteFrame;
        public Sprite defaultBlackFrame;

        [Tooltip("The deck a new run starts with (RunState seeds it on New Game, so the map's deck screen shows it before the first fight).")]
        public DeckConfigSO starterDeck;

        private static CardCatalogSO cached;

        // A card that is another card's upgraded version: only reached by upgrading at the เมรุ
        public bool IsUpgradedVersion(CardDataSO card)
        {
            return card != null && cards.Exists(c => c != null && c.upgradedCard == card);
        }

        // The cards random pools (rewards, shop, the pit) draw from: every card except upgraded versions
        public List<CardDataSO> BaseCards()
        {
            return cards.FindAll(c => c != null && !IsUpgradedVersion(c));
        }

        public static CardCatalogSO Load()
        {
            if (cached == null) cached = Resources.Load<CardCatalogSO>("CardCatalog");
            return cached;
        }
    }
}
