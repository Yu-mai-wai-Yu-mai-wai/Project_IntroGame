using System;
using System.Collections;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // The turn loop. Every turn: Draw -> the player plays familiars/amulets -> the enemy plays
    // familiars/amulets -> the player casts incantations -> the enemy casts incantations -> the board
    // clash -> End. Add it to any object in the combat scene; while it is enabled, CombatManager hands
    // the turn flow over to it. The End Turn button and the Space key finish each of the player's phases.
    public class TurnPhaseController : MonoBehaviour
    {
        public static TurnPhaseController Instance { get; private set; }

        [Header("Draw Phase")]
        [Tooltip("Cards drawn at the start of every turn, including turn 1. -1 = use CardManager.defaultDrawCount")]
        public int drawCount = 1;
        [Tooltip("Opening hand: drawn all at once when the game starts, before turn 1. -1 = CardManager.defaultDrawCount")]
        public int openingHandSize = 3;
        public float drawPhaseStartDelay = 0.3f;

        [Header("End Phase")]
        public bool discardHandAtEnd = false;
        public float endPhaseDelay = 0.6f;

        [Header("Enemy Phases")]
        [Tooltip("On = the enemy plays its cards and the board clashes every turn. Off = player-only test loop.")]
        public bool enemyActsAfterEnd = true;
        public float enemyPhaseEndDelay = 0.4f;

        [Header("Testing")]
        public KeyCode endTurnKey = KeyCode.Space;
        public bool showDebugLabel = true;

        public TurnPhase CurrentPhase { get; private set; } = TurnPhase.None;
        public int TurnNumber { get; private set; }

        public event Action<TurnPhase> OnPhaseChanged;
        public event Action<int> OnTurnStarted;

        private bool endTurnRequested;
        private Coroutine loop;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void BeginTurns()
        {
            if (loop != null) StopCoroutine(loop);
            TurnNumber = 0;
            loop = StartCoroutine(TurnLoop());
        }

        // Finishes the player's current phase (familiars/amulets, then incantations)
        public void RequestEndTurn()
        {
            if (IsPlayerPhase(CurrentPhase)) endTurnRequested = true;
        }

        private static bool IsPlayerPhase(TurnPhase phase)
        {
            return phase == TurnPhase.PlayerBoard || phase == TurnPhase.PlayerSpell;
        }

        private static bool IsBoardCard(CardInstance card)
        {
            return card != null && (card.cardType == CardType.Familiar || card.cardType == CardType.Amulet);
        }

        private static bool IsIncantation(CardInstance card)
        {
            return card != null && card.cardType == CardType.Incantation;
        }

        // Which of the player's cards may be played right now
        public bool CanPlayCard(CardInstance card)
        {
            switch (CurrentPhase)
            {
                case TurnPhase.PlayerBoard: return IsBoardCard(card);
                case TurnPhase.PlayerSpell: return IsIncantation(card);
                default: return false;
            }
        }

        public static string PhaseLabel(TurnPhase phase)
        {
            switch (phase)
            {
                case TurnPhase.Draw: return "จั่วการ์ด";
                case TurnPhase.PlayerBoard: return "ลงบริวาร / เครื่องราง";
                case TurnPhase.EnemyBoard: return "ศัตรูลงบริวาร / เครื่องราง...";
                case TurnPhase.PlayerSpell: return "ร่ายอาคม";
                case TurnPhase.EnemySpell: return "ศัตรูร่ายอาคม...";
                case TurnPhase.Clash: return "การ์ดตีกัน...";
                case TurnPhase.End: return "จบเทิร์น";
                default: return "";
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(endTurnKey)) RequestEndTurn();
        }

        private IEnumerator TurnLoop()
        {
            // Opening hand: everything flies in together before the first turn starts
            yield return new WaitForSeconds(drawPhaseStartDelay);
            yield return DrawOpeningHands();

            while (true)
            {
                TurnNumber++;
                OnTurnStarted?.Invoke(TurnNumber);

                // --- Draw ---
                SetPhase(TurnPhase.Draw);
                yield return new WaitForSeconds(drawPhaseStartDelay);

                var cards = CardManager.Instance;
                if (cards != null)
                {
                    int count = drawCount >= 0 ? drawCount : cards.defaultDrawCount;
                    cards.DrawCards(count);

                    // Let the fly-in animation play out before the player can act
                    var hand = FindFirstObjectByType<HandLayoutController3D>();
                    if (hand != null && count > 0)
                    {
                        yield return new WaitForSeconds(hand.drawFlyDuration + (count - 1) * hand.drawStagger);
                    }
                }

                // Board cards with a start-of-turn ability (extra draw, Merit...) fire once the draw is done
                EffectResolver.Instance?.TriggerTurnStart(isPlayer: true);

                var combat = CombatManager.Instance;
                bool enemyActs = enemyActsAfterEnd && combat != null;

                // The enemy's turn starts at the same time: shield reset, start-of-turn abilities, its draw
                if (enemyActs) yield return combat.EnemyTurnStart();

                // --- 1. Player: familiars / amulets ---
                yield return PlayerPhase(TurnPhase.PlayerBoard);

                // --- 2. Enemy: familiars / amulets ---
                if (enemyActs)
                {
                    SetPhase(TurnPhase.EnemyBoard);
                    yield return combat.EnemyPlayCards(IsBoardCard);
                    yield return new WaitForSeconds(enemyPhaseEndDelay);
                    if (combat.IsCombatOver) break;
                }

                // --- 3. Player: incantations ---
                yield return PlayerPhase(TurnPhase.PlayerSpell);
                if (combat != null && combat.IsCombatOver) break;

                // --- 4. Enemy: incantations (an enemy without a deck uses its telegraphed move here) ---
                if (enemyActs)
                {
                    SetPhase(TurnPhase.EnemySpell);
                    yield return combat.EnemyPlayCards(IsIncantation);
                    combat.ResolveEnemyIntentMove();
                    yield return new WaitForSeconds(enemyPhaseEndDelay);
                    if (combat.IsCombatOver) break;

                    // --- 5. Board clash, then end-of-round ticks ---
                    SetPhase(TurnPhase.Clash);
                    yield return combat.ClashAndRoundEnd();
                    if (combat.IsCombatOver) break;
                }

                // --- End ---
                SetPhase(TurnPhase.End);
                if (discardHandAtEnd && cards != null) cards.DiscardHand();
                yield return new WaitForSeconds(endPhaseDelay);

                if (enemyActs) combat.PickNextEnemyMove(TurnNumber + 1);
            }

            loop = null;
        }

        // Waits for the player to finish one play phase (End Turn button / Space)
        private IEnumerator PlayerPhase(TurnPhase phase)
        {
            endTurnRequested = false;
            SetPhase(phase);
            yield return new WaitUntil(() => endTurnRequested || (CombatManager.Instance != null && CombatManager.Instance.IsCombatOver));

            // A half-finished target choice ends with the phase
            CardTargeting3D.Instance?.Cancel();
        }

        // Both sides draw their opening hand at the same moment, every card starting to fly at once
        private IEnumerator DrawOpeningHands()
        {
            float wait = 0f;

            var cards = CardManager.Instance;
            if (cards != null)
            {
                int count = openingHandSize >= 0 ? openingHandSize : cards.defaultDrawCount;
                var hand = FindFirstObjectByType<HandLayoutController3D>();
                if (hand != null) hand.BeginSimultaneousDraw();
                cards.DrawCards(count);
                if (hand != null && count > 0) wait = Mathf.Max(wait, hand.drawFlyDuration);
            }

            if (CombatManager.Instance != null && CombatManager.Instance.DrawEnemyOpeningHand())
            {
                wait = Mathf.Max(wait, 0.5f);
            }

            if (wait > 0f) yield return new WaitForSeconds(wait);
        }

        private void SetPhase(TurnPhase phase)
        {
            CurrentPhase = phase;
            SyncCombatManager(phase);
            OnPhaseChanged?.Invoke(phase);
            Debug.Log($"[TurnPhase] Turn {TurnNumber} - {phase}");
        }

        // Keeps the existing HUD (turn number, End Turn button, phase label) working
        private void SyncCombatManager(TurnPhase phase)
        {
            var combat = CombatManager.Instance;
            if (combat == null) return;

            combat.State.turnNumber = TurnNumber;
            switch (phase)
            {
                case TurnPhase.Draw:
                    combat.RefillMeritForTurn(TurnNumber);
                    combat.SetPhase(CombatPhase.TurnStartDraw);
                    break;
                case TurnPhase.PlayerBoard:
                case TurnPhase.PlayerSpell:
                    combat.SetPhase(CombatPhase.PlayerTurn);
                    break;
                case TurnPhase.EnemyBoard:
                case TurnPhase.EnemySpell:
                case TurnPhase.Clash:
                    combat.SetPhase(CombatPhase.EnemyIntentExecution);
                    break;
                case TurnPhase.End: combat.SetPhase(CombatPhase.RoundEndStatusTick); break;
            }
        }

        private void OnGUI()
        {
            if (!showDebugLabel) return;
            GUI.Label(new Rect(10, 10, 500, 24), $"Turn {TurnNumber}  |  Phase: {CurrentPhase}  |  [{endTurnKey}] next phase");
            if (CombatManager.Instance != null)
            {
                var move = CombatManager.Instance.nextEnemyMove;
                GUI.Label(new Rect(10, 32, 600, 24),
                    $"Enemy AI: {CombatManager.Instance.EnemyAIStateName}  |  Next: {(move != null ? $"{move.intent} {move.baseValue}" : "-")}");
            }
        }
    }
}
