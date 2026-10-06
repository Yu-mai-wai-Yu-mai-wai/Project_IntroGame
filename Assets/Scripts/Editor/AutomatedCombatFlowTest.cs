using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TawanOS.CardEngine;
using TawanOS.MapEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Headless smoke test driven via `-executeMethod`: opens MapTestScene, enters Play mode,
    /// clicks the first attainable combat node, plays a card and ends a turn in CombatTestScene,
    /// then exits with a non-zero code if any runtime error/exception was logged.
    ///
    /// Entering/exiting Play mode triggers a full C# domain reload, which wipes normal static
    /// fields and event subscriptions. All progress state is therefore persisted in SessionState
    /// (survives a domain reload within the same Editor process) and re-subscribed to
    /// EditorApplication.update from a static constructor, which Unity re-runs after every reload.
    /// Not a permanent test suite - a one-off verification driver for this session's changes.
    /// </summary>
    [InitializeOnLoad]
    public static class AutomatedCombatFlowTest
    {
        private enum Step
        {
            EnterPlayMode,
            WaitForMap,
            ClickNode,
            WaitForCombat,
            WaitForPlayerTurn,
            PlayCard,
            EndTurn,
            WaitForRoundResolved,
            Done
        }

        private const string KeyRunning = "ACFT_Running";
        private const string KeyStep = "ACFT_Step";
        private const string KeyErrors = "ACFT_Errors";
        private const string KeyStepStart = "ACFT_StepStart";
        private const string KeyRunStart = "ACFT_RunStart";
        private const float GlobalBudgetSeconds = 240f;

        static AutomatedCombatFlowTest()
        {
            // Runs after every domain reload, including the ones triggered by entering/exiting Play mode.
            if (SessionState.GetBool(KeyRunning, false))
            {
                Debug.Log($"[AutomatedCombatFlowTest] Resumed after domain reload at step {(Step)SessionState.GetInt(KeyStep, 0)}");
                Application.logMessageReceived += HandleLog;
                EditorApplication.update += Tick;
            }
        }

        public static void Run()
        {
            // The test must start from a fresh run: a map save left by an earlier run (or by playing) makes the
            // first attainable node a later-floor Event/Shop node and the test times out waiting for combat.
            // This deletes the run save and the saved map of this machine's Editor profile.
            TawanOS.GameFlow.RunState.DeleteSave();
            new MapSaveManager().ClearSavedMap();

            SessionState.SetBool(KeyRunning, true);
            SessionState.SetInt(KeyStep, (int)Step.EnterPlayMode);
            SessionState.SetInt(KeyErrors, 0);
            SessionState.SetFloat(KeyRunStart, (float)EditorApplication.timeSinceStartup);
            SessionState.SetFloat(KeyStepStart, (float)EditorApplication.timeSinceStartup);

            Application.logMessageReceived += HandleLog;
            EditorSceneManager.OpenScene("Assets/Scenes/MapTestScene.unity");
            EditorApplication.update += Tick;
        }

        private static Step CurrentStep
        {
            get => (Step)SessionState.GetInt(KeyStep, 0);
            set => SessionState.SetInt(KeyStep, (int)value);
        }

        private static void HandleLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception)
            {
                SessionState.SetInt(KeyErrors, SessionState.GetInt(KeyErrors, 0) + 1);
                Debug.LogWarning($"[AutomatedCombatFlowTest] Captured {type}: {condition}");
            }
        }

        private static void Fail(string reason)
        {
            Debug.LogError($"[AutomatedCombatFlowTest] FAILED at step {CurrentStep}: {reason}");
            Finish(1);
        }

        private static void Finish(int code)
        {
            int errorCount = SessionState.GetInt(KeyErrors, 0);
            Debug.Log($"[AutomatedCombatFlowTest] RESULT: {(code == 0 ? "PASS" : "FAIL")}, errorsLogged={errorCount}");

            EditorApplication.update -= Tick;
            Application.logMessageReceived -= HandleLog;
            SessionState.SetBool(KeyRunning, false);

            EditorApplication.Exit(code == 0 && errorCount == 0 ? 0 : 1);
        }

        private static bool TimedOut(double seconds)
        {
            return EditorApplication.timeSinceStartup - SessionState.GetFloat(KeyStepStart, 0f) > seconds;
        }

        private static void AdvanceTo(Step next)
        {
            Debug.Log($"[AutomatedCombatFlowTest] {CurrentStep} -> {next}");
            CurrentStep = next;
            SessionState.SetFloat(KeyStepStart, (float)EditorApplication.timeSinceStartup);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(KeyRunning, false)) return;

            switch (CurrentStep)
            {
                case Step.EnterPlayMode:
                    if (!EditorApplication.isPlaying)
                    {
                        EditorApplication.isPlaying = true;
                    }
                    AdvanceTo(Step.WaitForMap);
                    break;

                case Step.WaitForMap:
                    if (!EditorApplication.isPlaying) break; // still transitioning into play mode
                    if (MapManager.Instance != null && MapManager.Instance.CurrentGraph != null)
                    {
                        AdvanceTo(Step.ClickNode);
                    }
                    else if (TimedOut(20))
                    {
                        Fail("MapManager.Instance / CurrentGraph never became ready");
                    }
                    break;

                case Step.ClickNode:
                    // The map is random each run, so only a combat node is a valid target: an Event/Shop/Treasure
                    // node never loads CombatTestScene and the step would time out (fixed 6 Oct, plan task A8 run)
                    var node = Object.FindObjectsByType<MapNodeView>(FindObjectsSortMode.None)
                        .FirstOrDefault(n => n.NodeData != null
                            && n.NodeData.status == NodeStatus.Attainable
                            && (n.NodeData.type == NodeType.MinorEnemy || n.NodeData.type == NodeType.EliteEnemy));
                    if (node == null)
                    {
                        if (TimedOut(5)) Fail("No Attainable combat MapNodeView (MinorEnemy/EliteEnemy) found to click");
                        break;
                    }
                    Debug.Log($"[AutomatedCombatFlowTest] Clicking node type={node.NodeData.type} at {node.NodeData.gridPosition}");
                    node.OnPointerClick(null);
                    AdvanceTo(Step.WaitForCombat);
                    break;

                case Step.WaitForCombat:
                    if (SceneManager.GetActiveScene().name == "CombatTestScene")
                    {
                        AdvanceTo(Step.WaitForPlayerTurn);
                    }
                    else if (TimedOut(20))
                    {
                        Fail("Scene never transitioned to CombatTestScene after node click");
                    }
                    break;

                case Step.WaitForPlayerTurn:
                    if (CombatManager.Instance != null && CombatManager.Instance.CurrentPhase == CombatPhase.PlayerTurn)
                    {
                        Debug.Log($"[AutomatedCombatFlowTest] PlayerTurn reached. Merit={CombatManager.Instance.CurrentMerit} " +
                                  $"PlayerKhwan={CombatManager.Instance.State.playerKhwan} EnemyKhwan={CombatManager.Instance.State.enemyKhwan} " +
                                  $"HandCount={CardManager.Instance?.Hand.Count}");
                        AdvanceTo(Step.PlayCard);
                    }
                    else if (TimedOut(20))
                    {
                        Fail("CombatManager never reached PlayerTurn phase");
                    }
                    break;

                case Step.PlayCard:
                    if (CardManager.Instance == null)
                    {
                        Fail("CardManager.Instance is null in combat scene");
                        break;
                    }
                    var card = CardManager.Instance.Hand.FirstOrDefault();
                    if (card == null)
                    {
                        Debug.LogWarning("[AutomatedCombatFlowTest] Hand is empty, skipping PlayCard step");
                        AdvanceTo(Step.EndTurn);
                        break;
                    }
                    bool played = CardManager.Instance.PlayCard(card);
                    Debug.Log($"[AutomatedCombatFlowTest] PlayCard '{card.cardNameThai}' school={card.magicSchool} type={card.cardType} " +
                              $"-> played={played} MeritAfter={CombatManager.Instance.CurrentMerit} " +
                              $"PlayerShield={CombatManager.Instance.CurrentPlayerShield} EnemyShield={CombatManager.Instance.CurrentEnemyShield} " +
                              $"Corruption={CombatManager.Instance.CurrentCorruption}");
                    AdvanceTo(Step.EndTurn);
                    break;

                case Step.EndTurn:
                    CombatManager.Instance.EndPlayerTurn();
                    Debug.Log("[AutomatedCombatFlowTest] EndPlayerTurn called");
                    AdvanceTo(Step.WaitForRoundResolved);
                    break;

                case Step.WaitForRoundResolved:
                    var phase = CombatManager.Instance.CurrentPhase;
                    if (phase == CombatPhase.PlayerTurn || phase == CombatPhase.Victory || phase == CombatPhase.Defeat)
                    {
                        Debug.Log($"[AutomatedCombatFlowTest] Round resolved. Phase={phase} " +
                                  $"PlayerKhwan={CombatManager.Instance.State.playerKhwan} EnemyKhwan={CombatManager.Instance.State.enemyKhwan} " +
                                  $"PlayerShield={CombatManager.Instance.CurrentPlayerShield} EnemyShield={CombatManager.Instance.CurrentEnemyShield}");
                        AdvanceTo(Step.Done);
                        Finish(0);
                    }
                    else if (TimedOut(90))
                    {
                        Fail($"Round never resolved back to PlayerTurn/Victory/Defeat, stuck at {phase}");
                    }
                    break;

                case Step.Done:
                    break;
            }

            // Wall-clock budget, not a frame count: in batch mode EditorApplication.update runs thousands of times
            // a second, so a frame budget ran out during a normal enemy spell animation (6 Oct)
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(KeyRunStart, 0f) > GlobalBudgetSeconds)
            {
                Fail($"Global time budget ({GlobalBudgetSeconds}s) exceeded, aborting to avoid hanging the process");
            }
        }
    }
}
