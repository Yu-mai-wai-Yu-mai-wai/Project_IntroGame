using System;
using System.Collections;
using UnityEngine;

namespace TawanOS.CardEngine
{
    public class CombatManager : MonoBehaviour, ICombatManager
    {
        public static CombatManager Instance { get; private set; }

        [Header("Runtime State")]
        [SerializeField] private CombatStateData state = new CombatStateData();
        [SerializeField] private CombatPhase currentPhase = CombatPhase.BattleInit;

        [Header("Active Enemy")]
        public EnemyProfileSO currentEnemyProfile;
        public EnemyMove nextEnemyMove;

        public CombatStateData State => state;
        public CombatPhase CurrentPhase => currentPhase;
        public int CurrentMerit => state.currentMerit;
        public int CurrentCorruption => state.currentCorruption;

        public event Action<CombatPhase> OnPhaseChanged;
        public event Action<int, int> OnMeritChanged;
        public event Action<int, int> OnCorruptionChanged;
        public event Action OnCurseBackfireTriggered;
        public event Action<bool> OnCombatEnded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (currentEnemyProfile != null)
            {
                StartCombat(currentEnemyProfile);
            }
        }

        public void StartCombat(EnemyProfileSO enemyProfile)
        {
            currentEnemyProfile = enemyProfile;
            state.playerKhwan = state.maxPlayerKhwan;
            state.enemyKhwan = enemyProfile != null ? enemyProfile.maxKhwan : 30;
            state.maxEnemyKhwan = state.enemyKhwan;
            state.currentMerit = 1;
            state.currentCorruption = 0;
            state.turnNumber = 1;

            if (CardManager.Instance != null && CardManager.Instance.defaultDeckConfig != null)
            {
                CardManager.Instance.InitializeDeck(CardManager.Instance.defaultDeckConfig.startingCards);
            }

            PickNextEnemyMove();
            SetPhase(CombatPhase.TurnStartDraw);
            StartCoroutine(TurnStartRoutine());
        }

        public void SetPhase(CombatPhase newPhase)
        {
            currentPhase = newPhase;
            state.currentPhase = newPhase;
            OnPhaseChanged?.Invoke(newPhase);
        }

        private IEnumerator TurnStartRoutine()
        {
            yield return new WaitForSeconds(0.3f);

            // Merit scaling: Turn 1 = 1, Turn 2 = 2, ... up to 6
            state.currentMerit = Mathf.Clamp(state.turnNumber, 1, state.maxMerit);
            OnMeritChanged?.Invoke(state.currentMerit, state.maxMerit);

            if (CardManager.Instance != null)
            {
                CardManager.Instance.DrawCards(CardManager.Instance.defaultDrawCount);
            }

            SetPhase(CombatPhase.PlayerTurn);
        }

        public void EndPlayerTurn()
        {
            if (currentPhase != CombatPhase.PlayerTurn) return;

            SetPhase(CombatPhase.EnemyIntentExecution);
            StartCoroutine(EnemyTurnRoutine());
        }

        private IEnumerator EnemyTurnRoutine()
        {
            yield return new WaitForSeconds(0.5f);

            if (nextEnemyMove != null && EffectResolver.Instance != null)
            {
                EffectResolver.Instance.ResolveEnemyIntent(nextEnemyMove.intent, nextEnemyMove.baseValue, nextEnemyMove.statusEffect);
            }

            yield return new WaitForSeconds(0.6f);

            if (state.playerKhwan <= 0)
            {
                EndCombat(false);
                yield break;
            }

            // Minion Lane Combat Attack (Friendly minions attack enemy)
            if (EffectResolver.Instance != null)
            {
                EffectResolver.Instance.ResolveMinionCombat();
            }

            if (state.enemyKhwan <= 0)
            {
                EndCombat(true);
                yield break;
            }

            SetPhase(CombatPhase.RoundEndStatusTick);
            yield return new WaitForSeconds(0.3f);

            // Discard unplayed cards
            if (CardManager.Instance != null)
            {
                CardManager.Instance.DiscardHand();
            }

            state.turnNumber++;
            PickNextEnemyMove();

            SetPhase(CombatPhase.TurnStartDraw);
            StartCoroutine(TurnStartRoutine());
        }

        public void PickNextEnemyMove()
        {
            if (currentEnemyProfile == null || currentEnemyProfile.moves.Count == 0)
            {
                nextEnemyMove = new EnemyMove { intent = EnemyIntent.Attack, baseValue = 5 };
                return;
            }

            // Weighted random selection
            int totalWeight = 0;
            foreach (var m in currentEnemyProfile.moves) totalWeight += Mathf.Max(1, m.weight);
            int roll = UnityEngine.Random.Range(0, totalWeight);
            int current = 0;
            foreach (var m in currentEnemyProfile.moves)
            {
                current += Mathf.Max(1, m.weight);
                if (roll < current)
                {
                    nextEnemyMove = m;
                    return;
                }
            }
            nextEnemyMove = currentEnemyProfile.moves[0];
        }

        public void TakeDamage(int amount, bool toPlayer)
        {
            if (toPlayer)
            {
                state.playerKhwan = Mathf.Max(0, state.playerKhwan - amount);
                if (state.playerKhwan <= 0)
                {
                    EndCombat(false);
                }
            }
            else
            {
                state.enemyKhwan = Mathf.Max(0, state.enemyKhwan - amount);
                if (state.enemyKhwan <= 0)
                {
                    EndCombat(true);
                }
            }
        }

        public void AddMerit(int amount)
        {
            state.currentMerit = Mathf.Clamp(state.currentMerit + amount, 0, state.maxMerit);
            OnMeritChanged?.Invoke(state.currentMerit, state.maxMerit);
        }

        public bool SpendMerit(int amount)
        {
            if (state.currentMerit >= amount)
            {
                state.currentMerit -= amount;
                OnMeritChanged?.Invoke(state.currentMerit, state.maxMerit);
                return true;
            }
            return false;
        }

        public void AddCorruption(int amount)
        {
            state.currentCorruption += amount;

            // Threshold curse backfire: at 9+, trigger curse and reset
            if (state.currentCorruption >= state.corruptionThreshold)
            {
                state.currentCorruption = 0;
                OnCorruptionChanged?.Invoke(state.currentCorruption, state.corruptionThreshold);
                OnCurseBackfireTriggered?.Invoke();

                if (EffectResolver.Instance != null)
                {
                    EffectResolver.Instance.TriggerCurseBackfire();
                }
                return;
            }

            OnCorruptionChanged?.Invoke(state.currentCorruption, state.corruptionThreshold);
        }

        public void EndCombat(bool isVictory)
        {
            SetPhase(isVictory ? CombatPhase.Victory : CombatPhase.Defeat);
            OnCombatEnded?.Invoke(isVictory);
            Debug.Log($"[CombatManager] Combat ended: {(isVictory ? "VICTORY" : "DEFEAT")}");
        }
    }
}
