using System;
using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    public class EffectResolver : MonoBehaviour, IEffectResolver
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
                    break;
            }
        }

        private void ExecuteIncantation(CardInstance card, object target, bool casterIsPlayer)
        {
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
            if (list.Count >= MaxBoardSlots) list.RemoveAt(0);
            list.Add(card);
            ResyncEnemySlots(list);
        }

        private void ResyncEnemySlots(List<CardInstance> list)
        {
            for (int i = 0; i < MaxBoardSlots; i++)
            {
                if (i < list.Count) OnEnemySlotOccupied?.Invoke(list[i], i);
                else OnEnemySlotCleared?.Invoke(i);
            }
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
            if (list.Count >= MaxBoardSlots)
            {
                // Replace oldest slot (Ponytail: FIFO slot replacement)
                list.RemoveAt(0);
            }
            list.Add(card);
            ResyncSlots(list);
        }

        private void ResyncSlots(List<CardInstance> list)
        {
            for (int i = 0; i < MaxBoardSlots; i++)
            {
                if (i < list.Count)
                {
                    OnSlotOccupied?.Invoke(list[i], i);
                }
                else
                {
                    OnSlotCleared?.Invoke(i);
                }
            }
        }

        public void TickAmuletDurability()
        {
            if (CombatManager.Instance == null) return;

            var list = CombatManager.Instance.State.activeBoardCards;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].cardType != CardType.Amulet) continue;

                list[i].currentDurability--;
                if (list[i].currentDurability <= 0)
                {
                    list.RemoveAt(i);
                }
            }
            ResyncSlots(list);

            // Enemy amulets wear out too
            var enemyList = CombatManager.Instance.State.enemyBoardCards;
            for (int i = enemyList.Count - 1; i >= 0; i--)
            {
                if (enemyList[i].cardType != CardType.Amulet) continue;

                enemyList[i].currentDurability--;
                if (enemyList[i].currentDurability <= 0) enemyList.RemoveAt(i);
            }
            ResyncEnemySlots(enemyList);
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

        // A familiar strikes: a target familiar loses Khwan (familiarHealth); with no target familiar the
        // hit lands on the opposing player's Khwan. Attackers are not checked for being alive so that
        // two familiars can trade blows at the same moment.
        public void ResolveFamiliarStrike(CardInstance attacker, CardInstance targetFamiliar, bool fromPlayer)
        {
            if (attacker == null || attacker.familiarDamage <= 0) return;

            if (targetFamiliar == null)
            {
                ResolveFamiliarAttack(attacker, fromPlayer);
                return;
            }

            int damage = CombatManager.Instance != null
                ? ApplyDamageStatusModifier(attacker.familiarDamage, attackerIsPlayer: fromPlayer)
                : attacker.familiarDamage;
            Debug.Log($"[Clash] {attacker.cardNameThai} hits {targetFamiliar.cardNameThai} for {damage} (same column)");
            targetFamiliar.familiarHealth = Mathf.Max(0, targetFamiliar.familiarHealth - damage);
            OnFamiliarDamaged?.Invoke(targetFamiliar, damage);
            if (targetFamiliar.familiarHealth <= 0) OnFamiliarDied?.Invoke(targetFamiliar);
        }

        // Called once the whole clash is over, because removing cards shifts the board slots
        public void RemoveDeadFamiliars()
        {
            if (CombatManager.Instance == null) return;

            var playerList = CombatManager.Instance.State.activeBoardCards;
            if (playerList.RemoveAll(c => c.cardType == CardType.Familiar && c.familiarHealth <= 0) > 0)
            {
                ResyncSlots(playerList);
            }

            var enemyList = CombatManager.Instance.State.enemyBoardCards;
            if (enemyList.RemoveAll(c => c.cardType == CardType.Familiar && c.familiarHealth <= 0) > 0)
            {
                ResyncEnemySlots(enemyList);
            }
        }

        // One familiar attacks the opposing side's Khwan directly
        public void ResolveFamiliarAttack(CardInstance card, bool fromPlayer)
        {
            if (card == null || card.cardType != CardType.Familiar || card.familiarDamage <= 0) return;
            if (CombatManager.Instance == null) return;

            int damage = ApplyDamageStatusModifier(card.familiarDamage, attackerIsPlayer: fromPlayer);
            var combat = CombatManager.Instance;
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
