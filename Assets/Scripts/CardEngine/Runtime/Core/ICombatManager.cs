using System;

namespace TawanOS.CardEngine
{
    public interface ICombatManager
    {
        CombatStateData State { get; }
        CombatPhase CurrentPhase { get; }
        int CurrentMerit { get; }
        int CurrentCorruption { get; }

        void StartCombat(EnemyProfileSO enemyProfile);
        void EndPlayerTurn();
        void TakeDamage(int amount, bool toPlayer);
        void AddMerit(int amount);
        bool SpendMerit(int amount);
        void AddCorruption(int amount);
        void EndCombat(bool isVictory);

        event Action<CombatPhase> OnPhaseChanged;
        event Action<int, int> OnMeritChanged; // current, max
        event Action<int, int> OnCorruptionChanged; // current, max
        event Action OnCurseBackfireTriggered;
        event Action<bool> OnCombatEnded; // isVictory
    }
}
