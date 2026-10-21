using System;
using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // หลุมกลาง: every turn after the first, 4 cards are dealt face up between the two boards (2 familiars,
    // 1 incantation, 1 amulet). On top of its normal deck draw, each side takes one, the first pick
    // alternating (turn 2 the enemy, turn 3 the player, ...), and the cards left over are thrown away.
    // TurnPhaseController runs the picks; CenterPitView3D shows the cards in 4 slots and takes the player's click.
    public class CenterPit : MonoBehaviour
    {
        public static CenterPit Instance { get; private set; }

        [Tooltip("How many cards of each type are dealt every turn")]
        public int familiars = 2;
        public int incantations = 1;
        public int amulets = 1;

        [Tooltip("Highest card cost (Merit or Corruption) dealt on turns 2-3, turns 4-5, and from turn 6 on. " +
                 "A type with no card that cheap is dealt from all its cards.")]
        public int earlyCostCap = 1;
        public int midCostCap = 2;
        public int lateCostCap = 3;

        private readonly List<CardInstance> offer = new List<CardInstance>();
        public IReadOnlyList<CardInstance> Offer => offer;

        // True while the player has to click one of the cards
        public bool WaitingForPlayer { get; private set; }

        public event Action OnDealt;
        public event Action<CardInstance, bool> OnTaken; // card, taken by the player
        public event Action OnCleared;

        public static CenterPit Ensure(GameObject host)
        {
            if (Instance != null) return Instance;
            var pit = host.GetComponent<CenterPit>();
            return pit != null ? pit : host.AddComponent<CenterPit>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // The first pick alternates, starting with the enemy on turn 2 (the first turn with a pit)
        public static bool PlayerPicksFirst(int turn) => turn % 2 == 1;

        // The sim dealt each turn's pit at the end of the turn before, so turn N uses the cap of turn N - 1
        public int CostCap(int turn)
        {
            int dealtOn = turn - 1;
            return dealtOn <= 2 ? earlyCostCap : dealtOn <= 4 ? midCostCap : lateCostCap;
        }

        public static int Cost(CardDataSO card) => Mathf.Max(card.meritCost, card.corruptionGain);

        public void Deal(int turn)
        {
            Clear();
            var catalog = CardCatalogSO.Load();
            if (catalog == null)
            {
                Debug.LogWarning("[CenterPit] No Resources/CardCatalog found; the pit stays empty.");
                return;
            }

            var pool = catalog.BaseCards();
            int cap = CostCap(turn);
            DealType(pool, CardType.Familiar, familiars, cap);
            DealType(pool, CardType.Incantation, incantations, cap);
            DealType(pool, CardType.Amulet, amulets, cap);
            Debug.Log($"[CenterPit] Turn {turn} (cost {cap} or less): " + string.Join(", ", offer.ConvertAll(c => c.cardNameThai)));
            OnDealt?.Invoke();
        }

        private void DealType(List<CardDataSO> pool, CardType type, int count, int cap)
        {
            var kind = pool.FindAll(c => c.cardType == type);
            var cheap = kind.FindAll(c => Cost(c) <= cap);
            if (cheap.Count > 0) kind = cheap;
            if (kind.Count == 0) return;
            for (int i = 0; i < count; i++)
            {
                offer.Add(new CardInstance(kind[UnityEngine.Random.Range(0, kind.Count)]));
            }
        }

        public void BeginPlayerPick() => WaitingForPlayer = offer.Count > 0;

        // The player clicked a card in the pit
        public bool TakeForPlayer(CardInstance card)
        {
            if (!WaitingForPlayer || card == null || !offer.Contains(card)) return false;
            WaitingForPlayer = false;
            Take(card, true);
            card.fromPit = true;
            CardManager.Instance?.AddToHand(card);
            return true;
        }

        public bool TakeForEnemy(CardInstance card, EnemyCardPlayer enemy)
        {
            if (card == null || enemy == null || !offer.Contains(card)) return false;
            Take(card, false);
            card.fromPit = true;
            enemy.AddToHand(card);
            return true;
        }

        private void Take(CardInstance card, bool byPlayer)
        {
            offer.Remove(card);
            Debug.Log($"[CenterPit] {(byPlayer ? "Player" : "Enemy")} took {card.cardNameThai}");
            OnTaken?.Invoke(card, byPlayer);
        }

        // The cards nobody took are thrown away
        public void Clear()
        {
            WaitingForPlayer = false;
            if (offer.Count == 0) return;
            offer.Clear();
            OnCleared?.Invoke();
        }
    }
}
