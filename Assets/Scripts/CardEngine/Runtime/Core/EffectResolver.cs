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
            if (card == null) return;

            switch (card.cardType)
            {
                case CardType.Incantation:
                    ExecuteIncantation(card, target);
                    break;
                case CardType.Amulet:
                case CardType.Familiar:
                    PlaceBoardCard(card);
                    break;
            }
        }

        private void ExecuteIncantation(CardInstance card, object target)
        {
            if (card.targetType == TargetType.SingleEnemy || card.targetType == TargetType.AllEnemies)
            {
                // Deal damage to enemy
                int damage = ApplyDamageStatusModifier(card.baseValue, attackerIsPlayer: true);
                if (CombatManager.Instance != null)
                {
                    CombatManager.Instance.TakeDamage(damage, toPlayer: false);
                }
                OnDamageDealt?.Invoke(damage, false);
            }
            else if (card.targetType == TargetType.Self)
            {
                // Grant shield (Khwan itself can only be restored at a temple, per design)
                int shield = card.baseValue;
                CombatManager.Instance?.AddShield(shield, toPlayer: true);
                OnShieldGranted?.Invoke(shield, true);
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
                var card = board[i];
                if (card.cardType == CardType.Familiar && card.familiarDamage > 0)
                {
                    int damage = ApplyDamageStatusModifier(card.familiarDamage, attackerIsPlayer: true);
                    CombatManager.Instance.TakeDamage(damage, toPlayer: false);
                    OnDamageDealt?.Invoke(damage, false);
                }
            }
        }

        public void ApplyStatusEffect(StatusEffectType status, int duration, bool toPlayer)
        {
            CombatManager.Instance?.ApplyStatus(status, duration, toPlayer);
            OnStatusApplied?.Invoke(status, duration, toPlayer);
        }

        public void TriggerCurseBackfire()
        {
            // Per design: crossing the Corruption threshold inflicts a random debuff, not fixed damage
            var values = (StatusEffectType[])Enum.GetValues(typeof(StatusEffectType));
            var chosen = values[UnityEngine.Random.Range(0, values.Length)];
            ApplyStatusEffect(chosen, StatusDebuffDefaultDuration, toPlayer: true);
            Debug.LogWarning($"[EffectResolver] Curse backfire triggered! Player afflicted with {chosen} for {StatusDebuffDefaultDuration} turns.");
        }
    }
}
