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
        public int CurrentPlayerShield => state.playerShield;
        public int CurrentEnemyShield => state.enemyShield;

        public event Action<CombatPhase> OnPhaseChanged;
        public event Action<int, int> OnMeritChanged;
        public event Action<int, int> OnCorruptionChanged;
        public event Action<int, bool> OnShieldChanged;
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
            state.playerShield = 0;
            state.enemyShield = 0;
            state.playerStatuses.Clear();
            state.enemyStatuses.Clear();

            if (CardManager.Instance != null && CardManager.Instance.defaultDeckConfig != null)
            {
                CardManager.Instance.InitializeDeck(CardManager.Instance.defaultDeckConfig.startingCards);
            }

            PickNextEnemyMove();

            // Simplified Draw/Main/End test loop replaces the full turn flow when present
            if (TurnPhaseController.Instance != null && TurnPhaseController.Instance.isActiveAndEnabled)
            {
                TurnPhaseController.Instance.BeginTurns();
                return;
            }

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

            // Shield does not carry over into the player's own turn (StS-style block reset)
            state.playerShield = 0;
            OnShieldChanged?.Invoke(state.playerShield, true);

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
            if (TurnPhaseController.Instance != null && TurnPhaseController.Instance.isActiveAndEnabled)
            {
                TurnPhaseController.Instance.RequestEndTurn();
                return;
            }

            if (currentPhase != CombatPhase.PlayerTurn) return;

            SetPhase(CombatPhase.EnemyIntentExecution);
            StartCoroutine(EnemyTurnRoutine());
        }

        private IEnumerator EnemyTurnRoutine()
        {
            yield return new WaitForSeconds(0.5f);

            // Enemy shield does not carry over into the enemy's own turn
            state.enemyShield = 0;
            OnShieldChanged?.Invoke(state.enemyShield, false);

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

            // Tick duration-based statuses (Bleeding deals damage here) and amulet durability
            TickStatusDurations(onPlayer: true);
            TickStatusDurations(onPlayer: false);
            if (EffectResolver.Instance != null)
            {
                EffectResolver.Instance.TickAmuletDurability();
            }

            if (currentPhase == CombatPhase.Defeat || currentPhase == CombatPhase.Victory)
            {
                yield break;
            }

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
            TakeDamageInternal(amount, toPlayer, allowReflect: true);
        }

        private void TakeDamageInternal(int amount, bool toPlayer, bool allowReflect)
        {
            if (amount <= 0) return;

            // KhumPhai (คุ้มภัย): halves incoming damage on the defender
            if (HasStatus(StatusEffectType.KhumPhai, toPlayer))
            {
                amount = Mathf.Max(0, Mathf.RoundToInt(amount * 0.5f));
            }

            // MontSaThon (มนต์สะท้อน): reflects half of the (already reduced) damage back once
            if (allowReflect && amount > 0 && HasStatus(StatusEffectType.MontSaThon, toPlayer))
            {
                int reflected = Mathf.RoundToInt(amount * 0.5f);
                if (reflected > 0)
                {
                    TakeDamageInternal(reflected, !toPlayer, allowReflect: false);
                }
            }

            if (toPlayer)
            {
                int remaining = AbsorbShield(amount, ref state.playerShield);
                OnShieldChanged?.Invoke(state.playerShield, true);
                state.playerKhwan = Mathf.Max(0, state.playerKhwan - remaining);
                if (state.playerKhwan <= 0)
                {
                    EndCombat(false);
                }
            }
            else
            {
                int remaining = AbsorbShield(amount, ref state.enemyShield);
                OnShieldChanged?.Invoke(state.enemyShield, false);
                state.enemyKhwan = Mathf.Max(0, state.enemyKhwan - remaining);
                if (state.enemyKhwan <= 0)
                {
                    EndCombat(true);
                }
            }
        }

        private int AbsorbShield(int incomingDamage, ref int shield)
        {
            if (shield <= 0) return incomingDamage;
            int absorbed = Mathf.Min(shield, incomingDamage);
            shield -= absorbed;
            return incomingDamage - absorbed;
        }

        public void AddShield(int amount, bool toPlayer)
        {
            if (amount <= 0) return;

            if (toPlayer) state.playerShield += amount;
            else state.enemyShield += amount;

            OnShieldChanged?.Invoke(toPlayer ? state.playerShield : state.enemyShield, toPlayer);
        }

        public bool HasStatus(StatusEffectType type, bool onPlayer)
        {
            var list = onPlayer ? state.playerStatuses : state.enemyStatuses;
            return list.Exists(s => s.type == type);
        }

        public void ApplyStatus(StatusEffectType type, int duration, bool toPlayer)
        {
            if (duration <= 0) return;
            var list = toPlayer ? state.playerStatuses : state.enemyStatuses;
            var existing = list.Find(s => s.type == type);
            if (existing != null)
            {
                existing.duration = Mathf.Max(existing.duration, duration);
            }
            else
            {
                list.Add(new ActiveStatus(type, duration));
            }
        }

        private void TickStatusDurations(bool onPlayer)
        {
            var list = onPlayer ? state.playerStatuses : state.enemyStatuses;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].type == StatusEffectType.BleedingCurse)
                {
                    TakeDamageInternal(3, toPlayer: onPlayer, allowReflect: false);
                    if (currentPhase == CombatPhase.Defeat || currentPhase == CombatPhase.Victory)
                    {
                        return;
                    }
                }

                list[i].duration--;
                if (list[i].duration <= 0)
                {
                    list.RemoveAt(i);
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
