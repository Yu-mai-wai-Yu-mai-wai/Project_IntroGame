using System;
using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    [Serializable]
    public class EnemyMove
    {
        public EnemyIntent intent = EnemyIntent.Attack;
        public int baseValue = 6;
        public StatusEffectType statusEffect = StatusEffectType.KhwanPhawa;
        public int weight = 1;
        public string moveDescription = "โจมตีสะเทือนขวัญ";
    }

    public enum AITransitionCondition
    {
        EnemyKhwanBelowPercent,     // threshold = 0..1 of the enemy's max Khwan
        PlayerKhwanBelowPercent,    // threshold = 0..1 of the player's max Khwan
        TurnAtLeast,                // threshold = turn number about to be played
        TurnsInStateAtLeast,        // threshold = how many moves were already picked in this state
        EnemyHasShield,
        PlayerHasShield,
        Always
    }

    public enum AIMoveSelection
    {
        Weighted,   // random by EnemyMove.weight
        Sequential  // cycle through the list in order
    }

    [Serializable]
    public class EnemyAITransition
    {
        public AITransitionCondition condition = AITransitionCondition.EnemyKhwanBelowPercent;
        public float threshold = 0.5f;
        public string targetState;
    }

    [Serializable]
    public class EnemyAIStateDef
    {
        public string stateName = "State";
        public AIMoveSelection selection = AIMoveSelection.Weighted;
        [Tooltip("Only used when the enemy has a card deck: how it picks which card to play in this state.")]
        public EnemyCardPlayStyle playStyle = EnemyCardPlayStyle.Balanced;
        public List<EnemyMove> moves = new List<EnemyMove>();
        [Tooltip("Checked in order every time the enemy picks its next move; the first true one switches state.")]
        public List<EnemyAITransition> transitions = new List<EnemyAITransition>();
    }

    [CreateAssetMenu(fileName = "NewEnemyProfile", menuName = "TawanOS/CardEngine/Enemy Profile")]
    public class EnemyProfileSO : ScriptableObject
    {
        [Header("Identity")]
        public string enemyId = "enemy_prai";
        public string enemyName = "ผีพราย";
        public Sprite portrait;

        [Header("Stats")]
        public int maxKhwan = 30;

        [Header("Card Deck (enemy plays cards like the player)")]
        [Tooltip("If not empty, the enemy draws and plays these cards each turn instead of using the Moveset / state moves.")]
        public List<CardDataSO> deck = new List<CardDataSO>();
        public int startingHandSize = 3;
        public int drawPerTurn = 1;
        public int maxHandSize = 10;
        [Tooltip("Safety cap on cards played in one enemy turn.")]
        public int maxCardsPerTurn = 6;

        [Header("Moveset AI")]
        public List<EnemyMove> moves = new List<EnemyMove>();

        [Header("State Machine AI (leave empty to use the plain Moveset above)")]
        public List<EnemyAIStateDef> aiStates = new List<EnemyAIStateDef>();
        [Tooltip("Name of the state to start in. Empty = first state in the list.")]
        public string initialAIState;

        [Header("Boss Settings")]
        public bool isBoss = false;
        public EnemyProfileSO phase2Profile;

#if UNITY_EDITOR
        [ContextMenu("Fill Sample State Machine (Opening / Aggressive / Enraged)")]
        private void FillSampleStateMachine()
        {
            UnityEditor.Undo.RecordObject(this, "Fill Sample AI");
            initialAIState = "Opening";
            aiStates = new List<EnemyAIStateDef>
            {
                new EnemyAIStateDef
                {
                    stateName = "Opening",
                    selection = AIMoveSelection.Sequential,
                    playStyle = EnemyCardPlayStyle.Defensive,
                    moves = new List<EnemyMove>
                    {
                        new EnemyMove { intent = EnemyIntent.Defend, baseValue = 6, moveDescription = "ตั้งการ์ด" },
                        new EnemyMove { intent = EnemyIntent.DebuffCurse, statusEffect = StatusEffectType.KhwanPhawa, moveDescription = "สาปให้ขวัญผวา" }
                    },
                    transitions = new List<EnemyAITransition>
                    {
                        new EnemyAITransition { condition = AITransitionCondition.TurnsInStateAtLeast, threshold = 2, targetState = "Aggressive" }
                    }
                },
                new EnemyAIStateDef
                {
                    stateName = "Aggressive",
                    selection = AIMoveSelection.Weighted,
                    playStyle = EnemyCardPlayStyle.Aggressive,
                    moves = new List<EnemyMove>
                    {
                        new EnemyMove { intent = EnemyIntent.Attack, baseValue = 6, weight = 3, moveDescription = "โจมตีสะเทือนขวัญ" },
                        new EnemyMove { intent = EnemyIntent.Defend, baseValue = 5, weight = 1, moveDescription = "ตั้งการ์ด" }
                    },
                    transitions = new List<EnemyAITransition>
                    {
                        new EnemyAITransition { condition = AITransitionCondition.EnemyKhwanBelowPercent, threshold = 0.4f, targetState = "Enraged" }
                    }
                },
                new EnemyAIStateDef
                {
                    stateName = "Enraged",
                    selection = AIMoveSelection.Weighted,
                    playStyle = EnemyCardPlayStyle.Aggressive,
                    moves = new List<EnemyMove>
                    {
                        new EnemyMove { intent = EnemyIntent.HeavyAttack, baseValue = 12, weight = 2, moveDescription = "คลั่งโจมตีรุนแรง" },
                        new EnemyMove { intent = EnemyIntent.Attack, baseValue = 8, weight = 2, moveDescription = "โจมตีสะเทือนขวัญ" }
                    }
                }
            };
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
