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
        [Tooltip("-1 = use CardManager.defaultDrawCount")]
        public int drawCount = 1;
        [Tooltip("Cards drawn on the first turn only (opening hand). -1 = same as drawCount")]
        public int firstTurnDrawCount = 5;
        public float drawPhaseStartDelay = 0.3f;

        [Header("End Phase")]
        public bool discardHandAtEnd = false;
        public float endPhaseDelay = 0.6f;

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
                    int perTurn = drawCount >= 0 ? drawCount : cards.defaultDrawCount;
                    int count = TurnNumber == 1 && firstTurnDrawCount >= 0 ? firstTurnDrawCount : perTurn;
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
            }
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
                case TurnPhase.Draw: combat.SetPhase(CombatPhase.TurnStartDraw); break;
                case TurnPhase.Main: combat.SetPhase(CombatPhase.PlayerTurn); break;
                case TurnPhase.End: combat.SetPhase(CombatPhase.RoundEndStatusTick); break;
            }
        }

        private void OnGUI()
        {
            if (!showDebugLabel) return;
            GUI.Label(new Rect(10, 10, 400, 24), $"Turn {TurnNumber}  |  Phase: {CurrentPhase}  |  [{endTurnKey}] end turn");
        }
    }
}
