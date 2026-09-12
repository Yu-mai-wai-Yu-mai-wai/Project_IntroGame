using System;

namespace TawanOS.CardEngine
{
    public interface IEffectResolver
    {
        void ResolveCardEffect(CardInstance card, object target = null);
        void ResolveEnemyIntent(EnemyIntent intent, int value, StatusEffectType status = StatusEffectType.KhwanPhawa);
        void ApplyStatusEffect(StatusEffectType status, int duration, bool toPlayer);
        void TriggerCurseBackfire();

        event Action<int, bool> OnDamageDealt; // amount, toPlayer
        event Action<int, bool> OnShieldGranted; // amount, toPlayer
        event Action<StatusEffectType, int, bool> OnStatusApplied; // status, duration, toPlayer
        event Action<CardInstance, int> OnSlotOccupied; // card, slotIndex
    }
}
