using System;

namespace TawanOS.CardEngine
{
    public interface ICombatManager
    {
        CombatStateData State { get; }
        CombatPhase CurrentPhase { get; }
        int CurrentMerit { get; }
        int CurrentCorruption { get; }
        int CurrentPlayerShield { get; }
        int CurrentEnemyShield { get; }

        void StartCombat(EnemyProfileSO enemyProfile);
        void EndPlayerTurn();
        void TakeDamage(int amount, bool toPlayer);
        void AddMerit(int amount);
        bool SpendMerit(int amount);
        void AddCorruption(int amount);
        void AddShield(int amount, bool toPlayer);
        bool HasStatus(StatusEffectType type, bool onPlayer);
        void ApplyStatus(StatusEffectType type, int duration, bool toPlayer);
        void EndCombat(bool isVictory);

        event Action<CombatPhase> OnPhaseChanged;
        event Action<int, int> OnMeritChanged; // current, max
        event Action<int, int> OnCorruptionChanged; // current, max
        event Action<int, bool> OnShieldChanged; // currentShield, toPlayer
        event Action OnCurseBackfireTriggered;
        event Action<bool> OnCombatEnded; // isVictory
    }
}
