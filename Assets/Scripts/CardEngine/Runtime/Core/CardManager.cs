using System;
using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    public class CardManager : MonoBehaviour, ICardEngine
    {
        public static CardManager Instance { get; private set; }

        [Header("Configuration")]
        public DeckConfigSO defaultDeckConfig;
        public int defaultDrawCount = 5;
        public int maxHandSize = 10;

        private readonly List<CardInstance> drawPile = new List<CardInstance>();
        private readonly List<CardInstance> handCards = new List<CardInstance>();
        private readonly List<CardInstance> discardPile = new List<CardInstance>();

        public IReadOnlyList<CardInstance> Hand => handCards;
        public IReadOnlyList<CardInstance> DrawPile => drawPile;
        public IReadOnlyList<CardInstance> DiscardPile => discardPile;

        public event Action<CardInstance> OnCardDrawn;
        public event Action<CardInstance> OnCardPlayed;
        public event Action<CardInstance> OnCardDiscarded;
        public event Action OnDeckReshuffled;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void InitializeDeck(List<CardDataSO> startingDeck)
        {
            drawPile.Clear();
            handCards.Clear();
            discardPile.Clear();

            if (startingDeck != null)
            {
                foreach (var template in startingDeck)
                {
                    if (template != null)
                    {
                        drawPile.Add(new CardInstance(template));
                    }
                }
            }

            Shuffle(drawPile);
        }

        public void DrawCards(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (handCards.Count >= maxHandSize)
                {
                    Debug.LogWarning("[CardManager] Hand is full! Cannot draw more cards.");
                    break;
                }

                if (drawPile.Count == 0)
                {
                    if (discardPile.Count > 0)
                    {
                        ReshuffleDiscardIntoDraw();
                    }
                    else
                    {
                        Debug.Log("[CardManager] Both draw and discard piles are empty.");
                        break;
                    }
                }

                CardInstance drawnCard = drawPile[0];
                drawPile.RemoveAt(0);
                handCards.Add(drawnCard);

                OnCardDrawn?.Invoke(drawnCard);
            }
        }

        public bool PlayCard(CardInstance card, object target = null)
        {
            if (card == null || !handCards.Contains(card)) return false;

            if (CombatManager.Instance != null)
            {
                // Validate Merit cost for White Magic
                if (card.magicSchool == MagicSchool.WhiteMagic && !CombatManager.Instance.SpendMerit(card.meritCost))
                {
                    Debug.LogWarning($"[CardManager] Not enough Merit to play {card.cardNameThai} (Needs {card.meritCost})");
                    return false;
                }

                // Add Corruption for Black Magic
                if (card.magicSchool == MagicSchool.BlackMagic && card.corruptionGain > 0)
                {
                    CombatManager.Instance.AddCorruption(card.corruptionGain);
                }
            }

            handCards.Remove(card);

            if (EffectResolver.Instance != null)
            {
                EffectResolver.Instance.ResolveCardEffect(card, target);
            }

            // Amulet and Familiar remain on board if active, otherwise Incantation goes to discard
            if (card.cardType == CardType.Incantation)
            {
                discardPile.Add(card);
                OnCardDiscarded?.Invoke(card);
            }

            OnCardPlayed?.Invoke(card);
            return true;
        }

        public void DiscardCard(CardInstance card)
        {
            if (card == null) return;

            if (handCards.Remove(card))
            {
                discardPile.Add(card);
                OnCardDiscarded?.Invoke(card);
            }
        }

        public void DiscardHand()
        {
            while (handCards.Count > 0)
            {
                CardInstance card = handCards[0];
                handCards.RemoveAt(0);
                discardPile.Add(card);
                OnCardDiscarded?.Invoke(card);
            }
        }

        public void ReshuffleDiscardIntoDraw()
        {
            if (discardPile.Count == 0) return;

            drawPile.AddRange(discardPile);
            discardPile.Clear();
            Shuffle(drawPile);

            OnDeckReshuffled?.Invoke();
        }

        private void Shuffle(List<CardInstance> list)
        {
            var rng = new System.Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                CardInstance value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}
