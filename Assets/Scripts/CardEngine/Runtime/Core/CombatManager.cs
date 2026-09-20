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

        [Header("Merit")]
        [Tooltip("Merit available on turn 1; it grows by 1 each following turn up to the max.")]
        [SerializeField] private int firstTurnMerit = 2;

        [Header("Active Enemy")]
        public EnemyProfileSO currentEnemyProfile;
        public EnemyMove nextEnemyMove;

        private readonly EnemyCardPlayer enemyCards = new EnemyCardPlayer();
        public EnemyCardPlayer EnemyCards => enemyCards;
        public int FirstTurnMerit => firstTurnMerit;
        public bool IsCombatOver => currentPhase == CombatPhase.Victory || currentPhase == CombatPhase.Defeat;
        public event Action<string> OnEnemyAction;

        private readonly EnemyStateMachine enemyAI = new EnemyStateMachine();
        public string EnemyAIStateName => enemyAI.HasStates ? enemyAI.CurrentStateName : "(plain moveset)";

        public CombatStateData State => state;
        public CombatPhase CurrentPhase => currentPhase;
        public int CurrentMerit => state.currentMerit;
        public int CurrentCorruption => state.currentCorruption;
        public int CurrentPlayerShield => state.playerShield;
        public int CurrentEnemyShield => state.enemyShield;

        public event Action<CombatPhase> OnPhaseChanged;
        public event Action<int, int> OnMeritChanged;
        public event Action<int, int> OnEnemyMeritChanged;
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
            state.currentCorruption = 0;
            state.turnNumber = 1;
            RefillMeritForTurn(state.turnNumber);
            state.playerShield = 0;
            state.enemyShield = 0;
            state.playerStatuses.Clear();
            state.enemyStatuses.Clear();

            if (CardManager.Instance != null && CardManager.Instance.defaultDeckConfig != null)
            {
                CardManager.Instance.InitializeDeck(CardManager.Instance.defaultDeckConfig.startingCards);
            }

            state.enemyBoardCards.Clear();
            state.enemyCorruption = 0;
            enemyAI.Init(enemyProfile);
            enemyCards.Init(enemyProfile);
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

        // Draws the enemy's opening hand (all at once). Returns false when the enemy has no card deck.
        public bool DrawEnemyOpeningHand()
        {
            return enemyCards.DrawOpeningHand();
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

            RefillMeritForTurn(state.turnNumber);

            if (state.turnNumber == 1) DrawEnemyOpeningHand();

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

            yield return EnemyActionSequence();
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

        // The enemy's whole action: shield reset, telegraphed move, minion combat, end-of-round ticks.
        // Callers must check for Victory/Defeat afterwards.
        public IEnumerator EnemyActionSequence()
        {
            // Enemy shield does not carry over into the enemy's own turn
            state.enemyShield = 0;
            OnShieldChanged?.Invoke(state.enemyShield, false);

            if (enemyCards.Active)
            {
                // Enemy plays cards from its own deck, same rules as the player
                yield return enemyCards.PlayTurn(this, state.turnNumber, enemyAI.CurrentPlayStyle);
            }
            else if (nextEnemyMove != null && EffectResolver.Instance != null)
            {
                ReportEnemyAction(nextEnemyMove.moveDescription);
                EffectResolver.Instance.ResolveEnemyIntent(nextEnemyMove.intent, nextEnemyMove.baseValue, nextEnemyMove.statusEffect);
            }

            yield return new WaitForSeconds(0.6f);

            if (state.playerKhwan <= 0)
            {
                if (!IsCombatOver) EndCombat(false);
                yield break;
            }

            // Board clash: familiars on both sides attack, column by column from left to right
            yield return BoardClashSequence();

            if (state.enemyKhwan <= 0)
            {
                if (!IsCombatOver) EndCombat(true);
                yield break;
            }

            if (state.playerKhwan <= 0)
            {
                if (!IsCombatOver) EndCombat(false);
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
        }

        // Familiars fight in columns, left to right (slot 0..4). In each column the player's familiar
        // strikes first, then the enemy's (or both at once when they face each other). A familiar only
        // clashes with the familiar in the opposite slot of the same column; when that slot has no live
        // familiar, it hits the opposing player's Khwan.
        // Damage is applied through EffectResolver at the moment of impact; without a 3D board view it
        // is applied instantly.
        private IEnumerator BoardClashSequence()
        {
            var resolver = EffectResolver.Instance;
            if (resolver == null) yield break;

            var view = BoardClashView3D.Instance;
            Action<ClashStrike> apply = s => resolver.ResolveFamiliarStrike(s.attacker, s.target, s.fromPlayer);

            for (int col = 0; col < EffectResolver.MaxBoardSlots; col++)
            {
                if (IsCombatOver) yield break;

                CardInstance playerCard = PlayerFamiliarAt(col, view);
                CardInstance enemyCard = EnemyFamiliarAt(col);
                bool playerReady = CanAttack(playerCard);
                bool enemyReady = CanAttack(enemyCard);
                if (!playerReady && !enemyReady) continue;

                if (playerReady && enemyReady
                    && TargetColumn(col, attackerIsPlayer: true, view) == col
                    && TargetColumn(col, attackerIsPlayer: false, view) == col)
                {
                    var ps = MakeStrike(col, playerCard, fromPlayer: true, view);
                    var es = MakeStrike(col, enemyCard, fromPlayer: false, view);
                    if (view != null) yield return view.HeadOn(col, ps, es, apply);
                    else { apply(ps); apply(es); }
                }
                else
                {
                    if (playerReady)
                    {
                        var ps = MakeStrike(col, playerCard, fromPlayer: true, view);
                        if (view != null) yield return view.Strike(ps, apply);
                        else apply(ps);
                    }

                    // Re-check: the player's strike may just have killed this column's enemy familiar
                    if (!IsCombatOver && CanAttack(enemyCard))
                    {
                        var es = MakeStrike(col, enemyCard, fromPlayer: false, view);
                        if (view != null) yield return view.Strike(es, apply);
                        else apply(es);
                    }
                }

                if (state.enemyKhwan <= 0 || state.playerKhwan <= 0) break;
            }

            // Dead familiars leave the board only now, since removing them shifts the slots
            resolver.RemoveDeadFamiliars();
        }

        private static bool CanAttack(CardInstance card)
        {
            return EffectResolver.IsAliveFamiliar(card) && card.familiarDamage > 0;
        }

        private CardInstance PlayerFamiliarAt(int col, BoardClashView3D view)
        {
            CardInstance card;
            if (view != null)
            {
                card = view.PlayerCardAt(col);
                if (card != null && !state.activeBoardCards.Contains(card)) card = null;
            }
            else
            {
                card = col < state.activeBoardCards.Count ? state.activeBoardCards[col] : null;
            }
            return EffectResolver.IsAliveFamiliar(card) ? card : null;
        }

        private CardInstance EnemyFamiliarAt(int col)
        {
            var card = col < state.enemyBoardCards.Count ? state.enemyBoardCards[col] : null;
            return EffectResolver.IsAliveFamiliar(card) ? card : null;
        }

        // A familiar only fights the familiar in the opposite slot of its own column. Returns that
        // column, or -1 when the opposite slot has no live familiar and the hit goes to the player.
        private int TargetColumn(int col, bool attackerIsPlayer, BoardClashView3D view)
        {
            var opposing = attackerIsPlayer ? EnemyFamiliarAt(col) : PlayerFamiliarAt(col, view);
            return opposing != null ? col : -1;
        }

        private ClashStrike MakeStrike(int col, CardInstance attacker, bool fromPlayer, BoardClashView3D view)
        {
            int targetCol = TargetColumn(col, fromPlayer, view);
            CardInstance target = null;
            if (targetCol >= 0) target = fromPlayer ? EnemyFamiliarAt(targetCol) : PlayerFamiliarAt(targetCol, view);

            return new ClashStrike
            {
                fromPlayer = fromPlayer,
                column = col,
                attacker = attacker,
                target = target,
                targetColumn = targetCol >= 0 ? targetCol : col
            };
        }

        // forTurn = the turn the telegraphed move will be played on (0 = current turn number)
        public void PickNextEnemyMove(int forTurn = 0)
        {
            if (enemyCards.Active)
            {
                // Card-playing enemy: no telegraphed move, but the state machine still advances
                if (enemyAI.HasStates) enemyAI.SelectNextMove(state, forTurn > 0 ? forTurn : state.turnNumber);
                nextEnemyMove = null;
                return;
            }

            if (enemyAI.HasStates)
            {
                var aiMove = enemyAI.SelectNextMove(state, forTurn > 0 ? forTurn : state.turnNumber);
                if (aiMove != null)
                {
                    nextEnemyMove = aiMove;
                    return;
                }
            }

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

        // Merit scaling: turn 1 = firstTurnMerit (2), then +1 every turn, capped at maxMerit.
        // Unspent Merit does not carry over; it is refilled to the turn's value.
        // The enemy follows exactly the same rule, and is refilled at the same moment as the player.
        public int MeritForTurn(int turn)
        {
            return Mathf.Clamp(firstTurnMerit + Mathf.Max(0, turn - 1), 0, state.maxMerit);
        }

        public void RefillMeritForTurn(int turn)
        {
            state.currentMerit = MeritForTurn(turn);
            OnMeritChanged?.Invoke(state.currentMerit, state.maxMerit);

            state.enemyMerit = MeritForTurn(turn);
            OnEnemyMeritChanged?.Invoke(state.enemyMerit, state.maxMerit);
        }

        public bool CanAffordEnemyMerit(int amount)
        {
            return state.enemyMerit >= amount;
        }

        public bool SpendEnemyMerit(int amount)
        {
            if (state.enemyMerit < amount) return false;

            state.enemyMerit -= amount;
            OnEnemyMeritChanged?.Invoke(state.enemyMerit, state.maxMerit);
            return true;
        }

        public void AddEnemyMerit(int amount)
        {
            state.enemyMerit = Mathf.Clamp(state.enemyMerit + amount, 0, state.maxMerit);
            OnEnemyMeritChanged?.Invoke(state.enemyMerit, state.maxMerit);
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

        public void ReportEnemyAction(string description)
        {
            Debug.Log($"[EnemyAI] {description}");
            OnEnemyAction?.Invoke(description);
        }

        // Enemy Black Magic builds Corruption too; crossing the threshold afflicts the enemy
        public void AddEnemyCorruption(int amount)
        {
            state.enemyCorruption += amount;
            if (state.enemyCorruption < state.corruptionThreshold) return;

            state.enemyCorruption = 0;
            EffectResolver.Instance?.TriggerCurseBackfire(toPlayer: false);
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
