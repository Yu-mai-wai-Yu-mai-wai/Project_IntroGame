using System;
using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // หลุมกลาง: every turn after the first, 4 cards are dealt face up between the two boards (2 familiars,
    // 1 incantation, 1 amulet), half of them White Magic and half Black, which slot gets which at random (22 Oct).
    // On top of its normal deck draw, each side takes one, the first pick alternating (turn 2 the enemy, turn 3 the
    // player, ...), and the cards left over are thrown away.
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
        public event Action<CardInstance> OnReserved; // the enemy has claimed this card; it takes it after the player's pick

        // On a turn the enemy picks first, the card it claimed: shown to the player, who cannot take it
        // (Game Designer, 25 Oct: the enemy does not snatch a card before the player has read the pit)
        public CardInstance ReservedForEnemy { get; private set; }

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

            var slots = new List<CardType>();
            for (int i = 0; i < familiars; i++) slots.Add(CardType.Familiar);
            for (int i = 0; i < incantations; i++) slots.Add(CardType.Incantation);
            for (int i = 0; i < amulets; i++) slots.Add(CardType.Amulet);

            // Half the slots White, half Black (an odd one either), shuffled over the slots
            var schools = new List<MagicSchool>();
            for (int i = 0; i < slots.Count; i++)
                schools.Add(i < slots.Count / 2 ? MagicSchool.WhiteMagic
                    : i < slots.Count / 2 * 2 ? MagicSchool.BlackMagic
                    : UnityEngine.Random.value < 0.5f ? MagicSchool.WhiteMagic : MagicSchool.BlackMagic);
            for (int i = schools.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (schools[i], schools[j]) = (schools[j], schools[i]);
            }

            for (int i = 0; i < slots.Count; i++) DealOne(pool, slots[i], schools[i], cap);
            Debug.Log($"[CenterPit] Turn {turn} (cost {cap} or less): "
                + string.Join(", ", offer.ConvertAll(c => $"{c.cardNameThai} ({(c.magicSchool == MagicSchool.WhiteMagic ? "W" : "B")})")));
            OnDealt?.Invoke();
        }

        // One card of this type and school, the cheap ones (cost <= cap) when there are any. A type with no card of
        // that school at all is dealt from the other school, so the slot is never empty.
        private void DealOne(List<CardDataSO> pool, CardType type, MagicSchool school, int cap)
        {
            var kind = pool.FindAll(c => c.cardType == type && c.magicSchool == school);
            if (kind.Count == 0)
            {
                kind = pool.FindAll(c => c.cardType == type);
                Debug.LogWarning($"[CenterPit] No {school} {type} to deal; dealing either school");
            }
            var cheap = kind.FindAll(c => Cost(c) <= cap);
            if (cheap.Count > 0) kind = cheap;
            if (kind.Count == 0) return;
            offer.Add(new CardInstance(kind[UnityEngine.Random.Range(0, kind.Count)]));
        }

        public void BeginPlayerPick() => WaitingForPlayer = offer.Count > (ReservedForEnemy != null ? 1 : 0);

        public void ReserveForEnemy(CardInstance card)
        {
            if (card == null || !offer.Contains(card)) return;
            ReservedForEnemy = card;
            OnReserved?.Invoke(card);
        }

        // The player clicked a card in the pit
        public bool TakeForPlayer(CardInstance card)
        {
            if (!WaitingForPlayer || card == null || !offer.Contains(card) || card == ReservedForEnemy) return false;
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
            if (card == ReservedForEnemy) ReservedForEnemy = null;
            Debug.Log($"[CenterPit] {(byPlayer ? "Player" : "Enemy")} took {card.cardNameThai}");
            OnTaken?.Invoke(card, byPlayer);
        }

        // The cards nobody took are thrown away
        public void Clear()
        {
            WaitingForPlayer = false;
            ReservedForEnemy = null;
            if (offer.Count == 0) return;
            offer.Clear();
            OnCleared?.Invoke();
        }
    }
}
