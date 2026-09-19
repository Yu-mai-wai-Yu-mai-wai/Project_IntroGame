using System;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Data-driven enemy brain. States and transitions come from EnemyProfileSO.aiStates.
    // Each time the enemy telegraphs its next move (SelectNextMove) the machine first checks the
    // current state's transitions (first true one wins, at most one switch per pick), then picks a
    // move from whichever state it ends up in.
    public class EnemyStateMachine
    {
        private EnemyProfileSO profile;
        private EnemyAIStateDef current;
        private int sequentialIndex;

        public string CurrentStateName => current != null ? current.stateName : "-";
        public EnemyCardPlayStyle CurrentPlayStyle => current != null ? current.playStyle : EnemyCardPlayStyle.Balanced;
        public int PicksInState { get; private set; }
        public bool HasStates => profile != null && profile.aiStates != null && profile.aiStates.Count > 0;

        public event Action<string, string> OnStateChanged; // from, to

        public void Init(EnemyProfileSO enemyProfile)
        {
            profile = enemyProfile;
            current = null;
            PicksInState = 0;
            sequentialIndex = 0;

            if (!HasStates) return;

            current = FindState(profile.initialAIState) ?? profile.aiStates[0];
        }

        public EnemyMove SelectNextMove(CombatStateData s, int turn)
        {
            if (current == null) return null;

            foreach (var t in current.transitions)
            {
                if (!Evaluate(t, s, turn)) continue;

                var next = FindState(t.targetState);
                if (next == null)
                {
                    Debug.LogWarning($"[EnemyAI] Transition target '{t.targetState}' not found in {profile.name}");
                    continue;
                }

                if (next != current) SwitchTo(next);
                break;
            }

            PicksInState++;
            return PickMove(current);
        }

        private void SwitchTo(EnemyAIStateDef next)
        {
            string from = current.stateName;
            current = next;
            PicksInState = 0;
            sequentialIndex = 0;
            Debug.Log($"[EnemyAI] {profile.enemyName}: {from} -> {next.stateName}");
            OnStateChanged?.Invoke(from, next.stateName);
        }

        private EnemyAIStateDef FindState(string stateName)
        {
            if (string.IsNullOrEmpty(stateName)) return null;
            return profile.aiStates.Find(x => x.stateName == stateName);
        }

        private bool Evaluate(EnemyAITransition t, CombatStateData s, int turn)
        {
            switch (t.condition)
            {
                case AITransitionCondition.EnemyKhwanBelowPercent:
                    return s.maxEnemyKhwan > 0 && (float)s.enemyKhwan / s.maxEnemyKhwan < t.threshold;
                case AITransitionCondition.PlayerKhwanBelowPercent:
                    return s.maxPlayerKhwan > 0 && (float)s.playerKhwan / s.maxPlayerKhwan < t.threshold;
                case AITransitionCondition.TurnAtLeast:
                    return turn >= Mathf.RoundToInt(t.threshold);
                case AITransitionCondition.TurnsInStateAtLeast:
                    return PicksInState >= Mathf.RoundToInt(t.threshold);
                case AITransitionCondition.EnemyHasShield:
                    return s.enemyShield > 0;
                case AITransitionCondition.PlayerHasShield:
                    return s.playerShield > 0;
                case AITransitionCondition.Always:
                    return true;
            }
            return false;
        }

        private EnemyMove PickMove(EnemyAIStateDef state)
        {
            var moves = state.moves;
            if (moves == null || moves.Count == 0) return null;

            if (state.selection == AIMoveSelection.Sequential)
            {
                var move = moves[sequentialIndex % moves.Count];
                sequentialIndex++;
                return move;
            }

            int total = 0;
            foreach (var m in moves) total += Mathf.Max(1, m.weight);
            int roll = UnityEngine.Random.Range(0, total);
            int acc = 0;
            foreach (var m in moves)
            {
                acc += Mathf.Max(1, m.weight);
                if (roll < acc) return m;
            }
            return moves[0];
        }
    }
}
