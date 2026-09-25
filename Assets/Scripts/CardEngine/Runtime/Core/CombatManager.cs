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
            state.playerPurify = 0;
            state.enemyPurify = 0;

            if (CardManager.Instance != null && CardManager.Instance.defaultDeckConfig != null)
            {
                CardManager.Instance.InitializeDeck(CardManager.Instance.defaultDeckConfig.startingCards);
            }

            state.enemyBoardCards.Clear();
            state.enemyCorruption = 0;

            // Auras from the previous fight are gone with its board
            state.corruptionThreshold -= state.playerAuraCorruptionCap;
            state.enemyCorruptionThreshold -= state.enemyAuraCorruptionCap;
            state.playerAuraCorruptionCap = 0;
            state.enemyAuraCorruptionCap = 0;
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

            EffectResolver.Instance?.TriggerTurnStart(isPlayer: true);

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
            yield return EnemyTurnStart();

            if (enemyCards.Active)
            {
                // Enemy plays cards from its own deck, same rules as the player
                yield return EnemyPlayCards(null);
            }
            else
            {
                ResolveEnemyIntentMove();
            }

            yield return new WaitForSeconds(0.6f);
            yield return ClashAndRoundEnd();
        }

        // Start of the enemy's turn: its shield resets, its start-of-turn abilities fire, it draws
        public IEnumerator EnemyTurnStart()
        {
            // Enemy shield does not carry over into the enemy's own turn
            state.enemyShield = 0;
            OnShieldChanged?.Invoke(state.enemyShield, false);

            EffectResolver.Instance?.TriggerTurnStart(isPlayer: false);

            yield return enemyCards.DrawForTurn();
        }

        // The enemy plays the cards `filter` allows (null = any)
        public IEnumerator EnemyPlayCards(Predicate<CardInstance> filter)
        {
            if (!enemyCards.Active) yield break;
            yield return enemyCards.PlayCards(this, enemyAI.CurrentPlayStyle, filter);
        }

        // An enemy without a card deck uses its telegraphed move instead
        public void ResolveEnemyIntentMove()
        {
            if (enemyCards.Active || nextEnemyMove == null || EffectResolver.Instance == null) return;
            ReportEnemyAction(nextEnemyMove.moveDescription);
            EffectResolver.Instance.ResolveEnemyIntent(nextEnemyMove.intent, nextEnemyMove.baseValue, nextEnemyMove.statusEffect);
        }

        // Board clash, then the end-of-round ticks. Callers must check for Victory/Defeat afterwards.
        public IEnumerator ClashAndRoundEnd()
        {
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
                EffectResolver.Instance.TickCardStatuses();
            }
        }

        // Familiars fight in columns, left to right (slot 0..4). In each column an overhead (ตีข้ามหัว)
        // familiar strikes first, then the normal one; otherwise the player's familiar strikes first, then
        // the enemy's (or both at once when they face each other). A familiar only
        // clashes with the familiar in the opposite slot of the same column; when that slot has no live
        // familiar, it hits the opposing player's Khwan.
        // Damage is applied through EffectResolver at the moment of impact; without a 3D board view it
        // is applied instantly.
        private IEnumerator BoardClashSequence()
        {
            var resolver = EffectResolver.Instance;
            if (resolver == null) yield break;

            var view = BoardClashView3D.Instance;
            Action<ClashStrike> apply = s => resolver.ResolveFamiliarStrike(s.attacker, s.target, s.fromPlayer, s.isCrit);

            for (int col = 0; col < EffectResolver.MaxBoardSlots; col++)
            {
                if (IsCombatOver) yield break;

                CardInstance playerCard = PlayerFamiliarAt(col);
                CardInstance enemyCard = EnemyFamiliarAt(col);
                bool playerReady = CanAttack(playerCard);
                bool enemyReady = CanAttack(enemyCard);
                if (!playerReady && !enemyReady) continue;

                bool playerOverhead = EffectResolver.HasKeyword(playerCard, AbilityEffect.Overhead);
                bool enemyOverhead = EffectResolver.HasKeyword(enemyCard, AbilityEffect.Overhead);

                // Two familiars trade blows at the same moment only when each one's target is the other
                // (never when one strikes overhead: overhead strikers always go first)
                if (playerReady && enemyReady && !playerOverhead && !enemyOverhead
                    && MakeStrike(col, playerCard, fromPlayer: true, view).target == enemyCard
                    && MakeStrike(col, enemyCard, fromPlayer: false, view).target == playerCard)
                {
                    var ps = MakeStrike(col, playerCard, fromPlayer: true, view);
                    var es = MakeStrike(col, enemyCard, fromPlayer: false, view);
                    if (view != null) yield return view.HeadOn(col, ps, es, apply);
                    else { apply(ps); apply(es); }
                }
                else
                {
                    // Overhead strikers hit first, then normal ones; on a tie the player goes first
                    bool enemyFirst = enemyOverhead && !playerOverhead;
                    for (int turn = 0; turn < 2; turn++)
                    {
                        bool fromPlayer = (turn == 0) != enemyFirst;
                        CardInstance attacker = fromPlayer ? playerCard : enemyCard;

                        // Re-check: the first strike may just have killed or disabled this column's other familiar
                        if (IsCombatOver || !CanAttack(attacker)) continue;

                        var strike = MakeStrike(col, attacker, fromPlayer, view);
                        if (view != null) yield return view.Strike(strike, apply);
                        else apply(strike);
                    }
                }

                if (state.enemyKhwan <= 0 || state.playerKhwan <= 0) break;
            }

            // Dead familiars leave the board only now, since removing them shifts the slots
            resolver.RemoveDeadFamiliars();
        }

        private static bool CanAttack(CardInstance card)
        {
            var resolver = EffectResolver.Instance;
            if (resolver != null) return resolver.CanFamiliarAttack(card);
            return EffectResolver.IsAliveFamiliar(card) && card.familiarDamage > 0;
        }

        private CardInstance PlayerFamiliarAt(int col)
        {
            var card = EffectResolver.CardAt(state.activeBoardCards, col);
            return EffectResolver.IsAliveFamiliar(card) ? card : null;
        }

        private CardInstance EnemyFamiliarAt(int col)
        {
            var card = EffectResolver.CardAt(state.enemyBoardCards, col);
            return EffectResolver.IsAliveFamiliar(card) ? card : null;
        }

        // Who a familiar hits is decided by EffectResolver.ChooseStrikeTarget: the familiar in the opposite
        // slot by default, a taunting card instead, or (overhead) the player. target == null = the player.
        private ClashStrike MakeStrike(int col, CardInstance attacker, bool fromPlayer, BoardClashView3D view)
        {
            var resolver = EffectResolver.Instance;
            CardInstance target = resolver != null ? resolver.ChooseStrikeTarget(attacker, col, fromPlayer) : null;
            int targetCol = target != null ? resolver.BoardIndexOf(target, onPlayerSide: !fromPlayer) : -1;

            return new ClashStrike
            {
                fromPlayer = fromPlayer,
                column = col,
                attacker = attacker,
                target = target,
                targetColumn = targetCol >= 0 ? targetCol : col,
                isCrit = resolver != null && resolver.RollCrit(attacker, fromPlayer)
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

        public void AddPurify(int charges, bool toPlayer)
        {
            if (charges <= 0) return;
            if (toPlayer) state.playerPurify += charges;
            else state.enemyPurify += charges;
        }

        private static bool IsDebuffStatus(StatusEffectType type)
        {
            return type == StatusEffectType.KhwanPhawa || type == StatusEffectType.BleedingCurse;
        }

        public void ApplyStatus(StatusEffectType type, int duration, bool toPlayer)
        {
            if (duration <= 0) return;

            // ชำระล้าง: a purify charge swallows the debuff
            if (IsDebuffStatus(type) && (toPlayer ? state.playerPurify : state.enemyPurify) > 0)
            {
                if (toPlayer) state.playerPurify--;
                else state.enemyPurify--;
                Debug.Log($"[CombatManager] Purify blocked {type} on {(toPlayer ? "player" : "enemy")}");
                return;
            }

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
            if (state.enemyCorruption < state.enemyCorruptionThreshold) return;

            state.enemyCorruption = 0;
            EffectResolver.Instance?.TriggerCurseBackfire(toPlayer: false);
        }

        // เบี้ยแก้: raises how much Corruption a side can hold before the curse backfires
        public void RaiseCorruptionThreshold(int amount, bool forPlayer)
        {
            if (forPlayer)
            {
                state.corruptionThreshold += amount;
                OnCorruptionChanged?.Invoke(state.currentCorruption, state.corruptionThreshold);
            }
            else
            {
                state.enemyCorruptionThreshold += amount;
            }
        }

        public void AddCorruption(int amount)
        {
            state.currentCorruption += amount;

            // Threshold curse backfire: at 7+ (the threshold), trigger curse and reset
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
