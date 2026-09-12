using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    [CreateAssetMenu(fileName = "NewDeckConfig", menuName = "TawanOS/CardEngine/Deck Config")]
    public class DeckConfigSO : ScriptableObject
    {
        public string deckId = "deck_starter";
        public string deckName = "สำรับพื้นฐาน (หมอธรรม)";
        public int defaultDrawCount = 5;
        public int maxHandSize = 10;
        public List<CardDataSO> startingCards = new List<CardDataSO>();
    }
}
