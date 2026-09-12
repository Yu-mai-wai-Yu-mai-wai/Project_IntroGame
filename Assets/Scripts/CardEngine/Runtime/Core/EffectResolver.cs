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
                    PlaceAmulet(card);
                    break;
                case CardType.Familiar:
                    SummonFamiliar(card);
                    break;
            }
        }

        private void ExecuteIncantation(CardInstance card, object target)
        {
            if (card.targetType == TargetType.SingleEnemy || card.targetType == TargetType.AllEnemies)
            {
                // Deal damage to enemy
                int damage = card.baseValue;
                if (CombatManager.Instance != null)
                {
                    CombatManager.Instance.TakeDamage(damage, toPlayer: false);
                }
                OnDamageDealt?.Invoke(damage, false);
            }
            else if (card.targetType == TargetType.Self)
            {
                // Grant shield or restore Khwan
                int shield = card.baseValue;
                OnShieldGranted?.Invoke(shield, true);
            }
        }

        private void PlaceAmulet(CardInstance card)
        {
            if (CombatManager.Instance == null) return;

            var list = CombatManager.Instance.State.activeAmulets;
            if (list.Count >= 3)
            {
                // Replace oldest slot (Ponytail: FIFO slot replacement)
                list.RemoveAt(0);
            }
            list.Add(card);
            OnSlotOccupied?.Invoke(card, list.Count - 1);
        }

        private void SummonFamiliar(CardInstance card)
        {
            if (CombatManager.Instance == null) return;

            var list = CombatManager.Instance.State.activeFamiliars;
            if (list.Count >= 3)
            {
                // Inscryption Sacrifice: replace first slot
                list.RemoveAt(0);
            }
            list.Add(card);
            OnSlotOccupied?.Invoke(card, list.Count - 1);
        }

        public void ResolveEnemyIntent(EnemyIntent intent, int value, StatusEffectType status = StatusEffectType.KhwanPhawa)
        {
            switch (intent)
            {
                case EnemyIntent.Attack:
                case EnemyIntent.HeavyAttack:
                    if (CombatManager.Instance != null)
                    {
                        CombatManager.Instance.TakeDamage(value, toPlayer: true);
                    }
                    OnDamageDealt?.Invoke(value, true);
                    break;

                case EnemyIntent.Defend:
                    OnShieldGranted?.Invoke(value, false);
                    break;

                case EnemyIntent.DebuffCurse:
                    ApplyStatusEffect(status, 2, toPlayer: true);
                    break;

                default:
                    if (CombatManager.Instance != null)
                    {
                        CombatManager.Instance.TakeDamage(value, toPlayer: true);
                    }
                    OnDamageDealt?.Invoke(value, true);
                    break;
            }
        }

        public void ResolveMinionCombat()
        {
            if (CombatManager.Instance == null) return;

            var familiars = CombatManager.Instance.State.activeFamiliars;
            for (int i = familiars.Count - 1; i >= 0; i--)
            {
                var fam = familiars[i];
                if (fam.familiarDamage > 0)
                {
                    CombatManager.Instance.TakeDamage(fam.familiarDamage, toPlayer: false);
                    OnDamageDealt?.Invoke(fam.familiarDamage, false);
                }
            }
        }

        public void ApplyStatusEffect(StatusEffectType status, int duration, bool toPlayer)
        {
            OnStatusApplied?.Invoke(status, duration, toPlayer);
        }

        public void TriggerCurseBackfire()
        {
            int backfireDamage = 10;
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.TakeDamage(backfireDamage, toPlayer: true);
            }
            OnDamageDealt?.Invoke(backfireDamage, true);
            Debug.LogWarning("[EffectResolver] Curse backfire triggered! Player took 10 damage.");
        }
    }
}
