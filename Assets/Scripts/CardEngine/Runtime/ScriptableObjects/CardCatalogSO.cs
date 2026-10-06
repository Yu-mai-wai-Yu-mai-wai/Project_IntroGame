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
