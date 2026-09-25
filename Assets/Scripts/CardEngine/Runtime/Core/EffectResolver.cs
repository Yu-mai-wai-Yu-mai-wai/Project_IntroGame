using System;
using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    public partial class EffectResolver : MonoBehaviour, IEffectResolver
    {
        public static EffectResolver Instance { get; private set; }

        public event Action<int, bool> OnDamageDealt;
        public event Action<int, bool> OnShieldGranted;
        public event Action<StatusEffectType, int, bool> OnStatusApplied;
        public event Action<CardInstance, int> OnSlotOccupied;
        public event Action<int> OnSlotCleared;
        public event Action<CardInstance, int> OnEnemySlotOccupied;
        public event Action<int> OnEnemySlotCleared;
        public event Action<CardInstance, int> OnFamiliarDamaged; // familiar, damage
        public event Action<CardInstance> OnFamiliarDied;

        public const int MaxBoardSlots = 5;
        private const int StatusDebuffDefaultDuration = 2;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            catalog = CardCatalogSO.Load();
        }

        public void ResolveCardEffect(CardInstance card, object target = null)
        {
            ResolveCardEffect(card, target, casterIsPlayer: true);
        }

        // casterIsPlayer = false: the enemy is playing the card, so "enemy" targets mean the player
        public void ResolveCardEffect(CardInstance card, object target, bool casterIsPlayer)
        {
            if (card == null) return;

            switch (card.cardType)
            {
                case CardType.Incantation:
                    ExecuteIncantation(card, target, casterIsPlayer);
                    break;
                case CardType.Amulet:
                case CardType.Familiar:
                    if (casterIsPlayer) PlaceBoardCard(card);
                    else PlaceEnemyBoardCard(card);
                    RunOnPlayAbilities(card, target, casterIsPlayer);
                    break;
            }
        }

        private void RunOnPlayAbilities(CardInstance card, object target, bool casterIsPlayer)
        {
            if (card.abilities == null || card.abilities.Count == 0) return;

            TriggerAbilities(card, AbilityTrigger.OnPlay, casterIsPlayer, null, target);
            RemoveDeadFamiliars();
        }

        private void ExecuteIncantation(CardInstance card, object target, bool casterIsPlayer)
        {
            // Cards with abilities are fully data-driven; the rest keep the older baseValue/targetType rules
            if (card.abilities != null && card.abilities.Count > 0)
            {
                RunOnPlayAbilities(card, target, casterIsPlayer);
                return;
            }

            if (card.targetType == TargetType.SingleEnemy || card.targetType == TargetType.AllEnemies)
            {
                // Deal damage to the caster's opponent
                int damage = ApplyDamageStatusModifier(card.baseValue, attackerIsPlayer: casterIsPlayer);
                if (CombatManager.Instance != null)
                {
                    CombatManager.Instance.TakeDamage(damage, toPlayer: !casterIsPlayer);
                }
                OnDamageDealt?.Invoke(damage, !casterIsPlayer);
            }
            else if (card.targetType == TargetType.Self)
            {
                // Grant shield (Khwan itself can only be restored at a temple, per design)
                int shield = card.baseValue;
                CombatManager.Instance?.AddShield(shield, toPlayer: casterIsPlayer);
                OnShieldGranted?.Invoke(shield, casterIsPlayer);
            }
        }

        private void PlaceEnemyBoardCard(CardInstance card)
        {
            if (CombatManager.Instance == null) return;

            var list = CombatManager.Instance.State.enemyBoardCards;
            PutOnBoard(list, card);
            RefreshAuras();
            ResyncEnemySlots(list);
        }

        private void ResyncEnemySlots(List<CardInstance> list)
        {
            for (int i = 0; i < MaxBoardSlots; i++)
            {
                var card = CardAt(list, i);
                if (card != null) OnEnemySlotOccupied?.Invoke(card, i);
                else OnEnemySlotCleared?.Invoke(i);
            }
        }

        // The card in board column `slot`, or null when that column is empty
        public static CardInstance CardAt(List<CardInstance> board, int slot)
        {
            if (board == null || slot < 0) return null;
            return board.Find(c => c != null && c.boardSlot == slot);
        }

        // The column a new card goes to: the requested one when it is free, else the leftmost free one (-1 = full)
        public static int FreeSlot(List<CardInstance> board, int preferred = -1)
        {
            if (preferred >= 0 && preferred < MaxBoardSlots && CardAt(board, preferred) == null) return preferred;
            for (int i = 0; i < MaxBoardSlots; i++)
            {
                if (CardAt(board, i) == null) return i;
            }
            return -1;
        }

        // Places a card in a fixed column. When the board is full the oldest card is replaced in its column.
        private static void PutOnBoard(List<CardInstance> list, CardInstance card)
        {
            int slot = FreeSlot(list, card.boardSlot);
            if (slot < 0)
            {
                var oldest = list[0];
                slot = oldest.boardSlot;
                oldest.boardSlot = -1;
                list.RemoveAt(0);
            }
            card.boardSlot = slot;
            list.Add(card);
        }

        private int ApplyDamageStatusModifier(int baseDamage, bool attackerIsPlayer)
        {
            if (CombatManager.Instance == null) return baseDamage;

            // KhwanPhawa (ขวัญผวา): reduces the afflicted attacker's outgoing damage by 25%
            if (CombatManager.Instance.HasStatus(StatusEffectType.KhwanPhawa, attackerIsPlayer))
            {
                baseDamage = Mathf.Max(0, Mathf.RoundToInt(baseDamage * 0.75f));
            }
            return baseDamage;
        }

        private void PlaceBoardCard(CardInstance card)
        {
            if (CombatManager.Instance == null) return;

            var list = CombatManager.Instance.State.activeBoardCards;
            PutOnBoard(list, card);
            RefreshAuras();
            ResyncSlots(list);
        }

        private void ResyncSlots(List<CardInstance> list)
        {
            for (int i = 0; i < MaxBoardSlots; i++)
            {
                var card = CardAt(list, i);
                if (card != null) OnSlotOccupied?.Invoke(card, i);
                else OnSlotCleared?.Invoke(i);
            }
        }

        public void TickAmuletDurability()
        {
            if (CombatManager.Instance == null) return;

            var list = CombatManager.Instance.State.activeBoardCards;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].cardType != CardType.Amulet || list[i].currentDurability <= 0) continue;

                list[i].currentDurability--;
                if (list[i].currentDurability <= 0)
                {
                    list[i].boardSlot = -1;
                    list.RemoveAt(i);
                }
            }
            ResyncSlots(list);

            // Enemy amulets wear out too
            var enemyList = CombatManager.Instance.State.enemyBoardCards;
            for (int i = enemyList.Count - 1; i >= 0; i--)
            {
                if (enemyList[i].cardType != CardType.Amulet || enemyList[i].currentDurability <= 0) continue;

                enemyList[i].currentDurability--;
                if (enemyList[i].currentDurability <= 0)
                {
                    enemyList[i].boardSlot = -1;
                    enemyList.RemoveAt(i);
                }
            }
            ResyncEnemySlots(enemyList);

            // Amulets that wore out take their auras with them
            RemoveDeadFamiliars();
        }

        public void ResolveEnemyIntent(EnemyIntent intent, int value, StatusEffectType status = StatusEffectType.KhwanPhawa)
        {
            switch (intent)
            {
                case EnemyIntent.Attack:
                case EnemyIntent.HeavyAttack:
                    int damage = ApplyDamageStatusModifier(value, attackerIsPlayer: false);
                    if (CombatManager.Instance != null)
                    {
                        CombatManager.Instance.TakeDamage(damage, toPlayer: true);
                    }
                    OnDamageDealt?.Invoke(damage, true);
                    break;

                case EnemyIntent.Defend:
                    CombatManager.Instance?.AddShield(value, toPlayer: false);
                    OnShieldGranted?.Invoke(value, false);
                    break;

                case EnemyIntent.DebuffCurse:
                    ApplyStatusEffect(status, 2, toPlayer: true);
                    break;

                default:
                    int fallbackDamage = ApplyDamageStatusModifier(value, attackerIsPlayer: false);
                    if (CombatManager.Instance != null)
                    {
                        CombatManager.Instance.TakeDamage(fallbackDamage, toPlayer: true);
                    }
                    OnDamageDealt?.Invoke(fallbackDamage, true);
                    break;
            }
        }

        public void ResolveMinionCombat()
        {
            if (CombatManager.Instance == null) return;

            var board = CombatManager.Instance.State.activeBoardCards;
            for (int i = board.Count - 1; i >= 0; i--)
            {
                ResolveFamiliarAttack(board[i], fromPlayer: true);
            }
        }

        public static bool IsAliveFamiliar(CardInstance card)
        {
            return card != null && card.cardType == CardType.Familiar && card.familiarHealth > 0;
        }

        // Cards a familiar can hit: live familiars, and amulets that have Khwan (they can be destroyed)
        public static bool IsStrikeable(CardInstance card)
        {
            if (card == null || card.IsDead) return false;
            return card.cardType == CardType.Familiar || card.maxKhwan > 0;
        }

        // A familiar strikes: the target card loses Khwan (armor first); with no target the hit lands on the
        // opposing player's Khwan. Attackers are not checked for being alive so that two familiars can
        // trade blows at the same moment. Multi-strikers also hit the cards beside the target.
        // isCrit (rolled beforehand with RollCrit) doubles the damage.
        public void ResolveFamiliarStrike(CardInstance attacker, CardInstance targetFamiliar, bool fromPlayer, bool isCrit = false)
        {
            if (!CanStrike(attacker)) return;

            if (HasCardStatus(attacker, CardStatusType.Blinded) && UnityEngine.Random.Range(0, 100) < BlindedMissPercent)
            {
                Debug.Log($"[Clash] {attacker.cardNameThai} is blinded and misses");
                return;
            }

            int attack = CombatManager.Instance != null
                ? ApplyDamageStatusModifier(EffectiveAttack(attacker), attackerIsPlayer: fromPlayer)
                : EffectiveAttack(attacker);
            if (attack <= 0) return;

            if (isCrit)
            {
                attack *= CritDamageMultiplier;
                Debug.Log($"[Clash] {attacker.cardNameThai} lands a critical hit ({attack})");
            }

            var targets = CollectStrikeTargets(attacker, targetFamiliar, fromPlayer);
            if (targets.Count == 0)
            {
                HitOpposingPlayer(attacker, attack, fromPlayer);
            }
            else
            {
                foreach (var t in targets)
                {
                    Debug.Log($"[Clash] {attacker.cardNameThai} hits {t.cardNameThai} for {attack}");
                    DamageCard(t, attack, ignoreArmor: false, attacker, fromPlayer);
                }
            }

            // กรรมตามสนอง: the damage dealt comes back on the attacker
            if (HasCardStatus(attacker, CardStatusType.Karma)) DamageCard(attacker, attack, ignoreArmor: true, source: null, sourceIsPlayer: !fromPlayer);
        }

        // Called once the whole clash is over. The other cards keep their columns.
        // Dead player cards go to the graveyard so they can be brought back.
        public void RemoveDeadFamiliars()
        {
            if (CombatManager.Instance == null) return;

            // Removing a card can end its aura, and losing an aura can (rarely) kill another card
            for (int pass = 0; pass < 5; pass++)
            {
                bool removed = RemoveDeadFrom(CombatManager.Instance.State.activeBoardCards, true);
                removed |= RemoveDeadFrom(CombatManager.Instance.State.enemyBoardCards, false);
                RefreshAuras();
                if (!removed) break;
            }
        }

        private bool RemoveDeadFrom(List<CardInstance> list, bool isPlayer)
        {
            var dead = list.FindAll(c => c.IsDead);
            if (dead.Count == 0) return false;

            list.RemoveAll(c => c.IsDead);
            foreach (var c in dead)
            {
                c.boardSlot = -1;
                SendToGraveyard(c, isPlayer);
            }
            if (isPlayer) ResyncSlots(list);
            else ResyncEnemySlots(list);
            return true;
        }

        // One familiar attacks the opposing side's Khwan directly
        public void ResolveFamiliarAttack(CardInstance card, bool fromPlayer)
        {
            if (card == null || card.cardType != CardType.Familiar) return;
            ResolveFamiliarStrike(card, null, fromPlayer, RollCrit(card, fromPlayer));
        }

        private void HitOpposingPlayer(CardInstance card, int damage, bool fromPlayer)
        {
            var combat = CombatManager.Instance;
            if (combat == null) return;

            int shield = fromPlayer ? combat.CurrentEnemyShield : combat.CurrentPlayerShield;
            Debug.Log($"[Clash] {card.cardNameThai} hits {(fromPlayer ? "enemy" : "player")} Khwan for {damage} (target shield {shield} absorbs first)");
            combat.TakeDamage(damage, toPlayer: !fromPlayer);
            OnDamageDealt?.Invoke(damage, !fromPlayer);
        }

        // Enemy familiars attack the player
        public void ResolveEnemyMinionCombat()
        {
            if (CombatManager.Instance == null) return;

            var board = CombatManager.Instance.State.enemyBoardCards;
            for (int i = board.Count - 1; i >= 0; i--)
            {
                ResolveFamiliarAttack(board[i], fromPlayer: false);
            }
        }

        public void ApplyStatusEffect(StatusEffectType status, int duration, bool toPlayer)
        {
            CombatManager.Instance?.ApplyStatus(status, duration, toPlayer);
            OnStatusApplied?.Invoke(status, duration, toPlayer);
        }

        public void TriggerCurseBackfire()
        {
            TriggerCurseBackfire(toPlayer: true);
        }

        public void TriggerCurseBackfire(bool toPlayer)
        {
            // Per design: crossing the Corruption threshold inflicts a random debuff, not fixed damage
            var values = (StatusEffectType[])Enum.GetValues(typeof(StatusEffectType));
            var chosen = values[UnityEngine.Random.Range(0, values.Length)];
            ApplyStatusEffect(chosen, StatusDebuffDefaultDuration, toPlayer);
            Debug.LogWarning($"[EffectResolver] Curse backfire triggered! {(toPlayer ? "Player" : "Enemy")} afflicted with {chosen} for {StatusDebuffDefaultDuration} turns.");
        }
    }
}
