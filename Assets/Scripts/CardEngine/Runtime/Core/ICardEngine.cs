using System;
using System.Collections.Generic;

namespace TawanOS.CardEngine
{
    public interface ICardEngine
    {
        IReadOnlyList<CardInstance> Hand { get; }
        IReadOnlyList<CardInstance> DrawPile { get; }
        IReadOnlyList<CardInstance> DiscardPile { get; }

        void InitializeDeck(List<CardDataSO> startingDeck);
        void DrawCards(int count);
        bool PlayCard(CardInstance card, object target = null);
        void DiscardCard(CardInstance card);
        void ReshuffleDiscardIntoDraw();

        event Action<CardInstance> OnCardDrawn;
        event Action<CardInstance> OnCardPlayed;
        event Action<CardInstance> OnCardDiscarded;
        event Action OnDeckReshuffled;
    }
}
