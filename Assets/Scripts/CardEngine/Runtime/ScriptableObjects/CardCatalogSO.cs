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

        public static CardCatalogSO Load()
        {
            if (cached == null) cached = Resources.Load<CardCatalogSO>("CardCatalog");
            return cached;
        }
    }
}
