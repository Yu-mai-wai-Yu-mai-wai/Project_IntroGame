using System;
using System.Collections;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Minimal player-turn loop for testing the deck: Draw -> Main -> End -> (next turn) Draw.
    // Add it to any object in the combat scene. While it is enabled, CombatManager hands the turn
    // flow over to it (no enemy turn); the End Turn button and the Space key end the Main phase.
    public class TurnPhaseController : MonoBehaviour
    {
        public static TurnPhaseController Instance { get; private set; }

        [Header("Draw Phase")]
        [Tooltip("Cards drawn at the start of every turn, including turn 1. -1 = use CardManager.defaultDrawCount")]
        public int drawCount = 1;
        [Tooltip("Opening hand: drawn all at once when the game starts, before turn 1. -1 = CardManager.defaultDrawCount")]
        public int openingHandSize = 5;
        public float drawPhaseStartDelay = 0.3f;

        [Header("End Phase")]
        public bool discardHandAtEnd = false;
        public float endPhaseDelay = 0.6f;

        [Header("Enemy Phase")]
        [Tooltip("On = the enemy acts (state machine AI) after every End phase. Off = old player-only test loop.")]
        public bool enemyActsAfterEnd = true;

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

        public void RequestEndTurn()
        {
            if (CurrentPhase == TurnPhase.Main) endTurnRequested = true;
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

                // --- Main ---
                endTurnRequested = false;
                SetPhase(TurnPhase.Main);
                yield return new WaitUntil(() => endTurnRequested);

                // --- End ---
                SetPhase(TurnPhase.End);
                if (discardHandAtEnd && cards != null) cards.DiscardHand();
                yield return new WaitForSeconds(endPhaseDelay);

                // --- Enemy ---
                var combat = CombatManager.Instance;
                if (enemyActsAfterEnd && combat != null)
                {
                    SetPhase(TurnPhase.Enemy);
                    yield return combat.EnemyActionSequence();

                    if (combat.CurrentPhase == CombatPhase.Victory || combat.CurrentPhase == CombatPhase.Defeat)
                    {
                        loop = null;
                        yield break;
                    }

                    combat.PickNextEnemyMove(TurnNumber + 1);
                }
            }
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
                case TurnPhase.Main: combat.SetPhase(CombatPhase.PlayerTurn); break;
                case TurnPhase.End: combat.SetPhase(CombatPhase.RoundEndStatusTick); break;
                case TurnPhase.Enemy: combat.SetPhase(CombatPhase.EnemyIntentExecution); break;
            }
        }

        private void OnGUI()
        {
            if (!showDebugLabel) return;
            GUI.Label(new Rect(10, 10, 400, 24), $"Turn {TurnNumber}  |  Phase: {CurrentPhase}  |  [{endTurnKey}] end turn");
            if (CombatManager.Instance != null)
            {
                var move = CombatManager.Instance.nextEnemyMove;
                GUI.Label(new Rect(10, 32, 600, 24),
                    $"Enemy AI: {CombatManager.Instance.EnemyAIStateName}  |  Next: {(move != null ? $"{move.intent} {move.baseValue}" : "-")}");
            }
        }
    }
}
