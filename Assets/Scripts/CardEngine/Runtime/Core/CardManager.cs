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

        [Header("Debug/Testing")]
        [Tooltip("Off = cards can be played for free, ignoring Merit cost and Corruption gain, while other systems are being tested.")]
        public bool costSystemEnabled = false;

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

        // Whether the Merit cost can be paid right now (checked before asking the player for a target)
        public bool CanAfford(CardInstance card)
        {
            if (card == null) return false;
            if (!costSystemEnabled || CombatManager.Instance == null || card.magicSchool != MagicSchool.WhiteMagic) return true;
            return CombatManager.Instance.State.currentMerit >= card.meritCost;
        }

        // The turn order only lets familiars/amulets be played first, then incantations
        public bool IsAllowedNow(CardInstance card)
        {
            var turns = TurnPhaseController.Instance;
            return turns == null || !turns.isActiveAndEnabled || turns.CanPlayCard(card);
        }

        public bool PlayCard(CardInstance card, object target = null)
        {
            if (card == null || !handCards.Contains(card)) return false;

            if (!IsAllowedNow(card))
            {
                Debug.Log($"[CardManager] {card.cardNameThai} cannot be played in this phase");
                return false;
            }

            if (EffectResolver.Instance == null)
            {
                Debug.LogWarning($"[CardManager] No EffectResolver in scene; cannot play {card.cardNameThai}");
                return false;
            }

            // An incantation with nothing to act on stays in hand and costs nothing
            if (!EffectResolver.Instance.CanResolve(card, casterIsPlayer: true))
            {
                Debug.Log(card.cardType == CardType.Incantation
                    ? $"[CardManager] {card.cardNameThai} has no valid target right now"
                    : $"[CardManager] The board is full; {card.cardNameThai} cannot be played");
                return false;
            }

            // Validate Merit cost for White Magic before spending anything
            if (costSystemEnabled && CombatManager.Instance != null && card.magicSchool == MagicSchool.WhiteMagic
                && !CombatManager.Instance.SpendMerit(card.meritCost))
            {
                Debug.LogWarning($"[CardManager] Not enough Merit to play {card.cardNameThai} (Needs {card.meritCost})");
                return false;
            }

            handCards.Remove(card);
            EffectResolver.Instance.ResolveCardEffect(card, target);

            // Corruption is charged only once the Black Magic effect has actually resolved
            if (costSystemEnabled && CombatManager.Instance != null && card.magicSchool == MagicSchool.BlackMagic && card.corruptionGain > 0)
            {
                CombatManager.Instance.AddCorruption(card.corruptionGain);
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

        // หลุมจั่ว: the player clicks the pit to draw one random card out of every card in the game.
        // Each pull costs Corruption, whatever the debug cost switch says, since that is the pit's price.
        public const int PitCorruptionGain = 1;

        public bool DrawFromPit()
        {
            var combat = CombatManager.Instance;
            if (combat != null && combat.CurrentPhase != CombatPhase.PlayerTurn) return false;

            if (handCards.Count >= maxHandSize)
            {
                Debug.LogWarning("[CardManager] Hand is full! The pit gives nothing.");
                return false;
            }

            var template = PickPitCard();
            if (template == null) return false;

            var drawn = new CardInstance(template) { fromPit = true };
            AddToHand(drawn);
            DrawPitView3D.Instance?.PlayUseEffect();
            Debug.Log($"[CardManager] Pit drew {drawn.cardNameThai}");

            combat?.AddCorruption(PitCorruptionGain);
            return true;
        }

        // One random card out of every card in the game; the pit is shared by the player and the enemy
        public static CardDataSO PickPitCard()
        {
            var catalog = CardCatalogSO.Load();
            var pool = new List<CardDataSO>();
            if (catalog != null)
            {
                foreach (var template in catalog.cards) if (template != null) pool.Add(template);
            }
            if (pool.Count == 0)
            {
                Debug.LogWarning("[CardManager] No Resources/CardCatalog found; the pit has nothing to give.");
                return null;
            }
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        // A card created mid-combat (summoned, given, or brought back) goes straight to the hand
        public void AddToHand(CardInstance card)
        {
            if (card == null) return;

            if (handCards.Count >= maxHandSize)
            {
                discardPile.Add(card);
                OnCardDiscarded?.Invoke(card);
                return;
            }

            handCards.Add(card);
            OnCardDrawn?.Invoke(card);
        }

        // Board cards that died. Stored as a fresh copy so they come back at full strength.
        public void SendToGraveyard(CardInstance card)
        {
            if (card == null) return;
            discardPile.Add(card.source != null ? new CardInstance(card.source) : card);
        }

        public bool ReturnRandomFromDiscard()
        {
            if (discardPile.Count == 0) return false;

            int index = UnityEngine.Random.Range(0, discardPile.Count);
            var card = discardPile[index];
            discardPile.RemoveAt(index);
            AddToHand(card);
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
