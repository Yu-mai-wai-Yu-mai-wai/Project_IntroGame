using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TawanOS.CardEngine;
using TawanOS.GameFlow;
using TawanOS.MapEngine;
using TawanOS.StoryEngine;
using TawanOS.UI;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Headless check of the intro tutorial fight, driven like <see cref="AutomatedCombatFlowTest"/>:
    /// New Game -> click through the intro until the dream page sends the player to CombatTestScene with the
    /// tutorial hints up -> lose the fight on purpose -> back in StoryScene on the page after the dream, with the
    /// run's Khwan, incense and save untouched. Exits non-zero on a failed check or any logged error.
    /// Run with raw Unity.exe (no -quit): -executeMethod TawanOS.EditorTools.AutomatedTutorialFlowTest.Run
    /// Deletes the run save and the saved map of this machine's Editor profile, like the combat smoke test.
    /// </summary>
    [InitializeOnLoad]
    public static class AutomatedTutorialFlowTest
    {
        private enum Step
        {
            EnterPlayMode,
            StartNewGame,
            ClickThroughStory,
            WaitForCombat,
            WaitForPlayerPhase,
            LoseFight,
            WaitForStory,
            Done
        }

        private const string Tag = "[AutomatedTutorialFlowTest]";
        private const string KeyRunning = "ATFT_Running";
        private const string KeyStep = "ATFT_Step";
        private const string KeyErrors = "ATFT_Errors";
        private const string KeyStepStart = "ATFT_StepStart";
        private const string KeyResumePage = "ATFT_ResumePage";
        private const string KeyHp = "ATFT_Hp";
        private const string KeyIncense = "ATFT_Incense";

        static AutomatedTutorialFlowTest()
        {
            if (SessionState.GetBool(KeyRunning, false))
            {
                Application.logMessageReceived += HandleLog;
                EditorApplication.update += Tick;
            }
        }

        [MenuItem("Tools/TawanOS/Tests/Automated Tutorial Flow")]
        public static void Run()
        {
            RunState.DeleteSave();
            new MapSaveManager().ClearSavedMap();

            var story = AssetDatabase.LoadAssetAtPath<StoryDataSO>("Assets/StoryEngineData/Story_Intro.asset");
            int fightPage = story != null ? story.pages.FindIndex(p => p.tutorialFightEnemy != null) : -1;
            if (fightPage < 0)
            {
                Debug.LogError($"{Tag} FAILED: Story_Intro has no page with a tutorial fight (run Tools > TawanOS > Story > Hook Up Intro Tutorial Fight).");
                EditorApplication.Exit(1);
                return;
            }

            SessionState.SetBool(KeyRunning, true);
            SessionState.SetInt(KeyStep, (int)Step.EnterPlayMode);
            SessionState.SetInt(KeyErrors, 0);
            SessionState.SetInt(KeyResumePage, fightPage + 1);
            SessionState.SetFloat(KeyStepStart, (float)EditorApplication.timeSinceStartup);

            Application.logMessageReceived += HandleLog;
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            EditorApplication.update += Tick;
        }

        private static Step CurrentStep
        {
            get => (Step)SessionState.GetInt(KeyStep, 0);
            set => SessionState.SetInt(KeyStep, (int)value);
        }

        private static void HandleLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception) return;
            SessionState.SetInt(KeyErrors, SessionState.GetInt(KeyErrors, 0) + 1);
            Debug.LogWarning($"{Tag} Captured {type}: {condition}");
        }

        private static void Fail(string reason)
        {
            Debug.LogError($"{Tag} FAILED at step {CurrentStep}: {reason}");
            Finish(1);
        }

        private static void Finish(int code)
        {
            int errors = SessionState.GetInt(KeyErrors, 0);
            Debug.Log($"{Tag} RESULT: {(code == 0 && errors == 0 ? "PASS" : "FAIL")}, errorsLogged={errors}");
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= HandleLog;
            SessionState.SetBool(KeyRunning, false);
            EditorApplication.Exit(code == 0 && errors == 0 ? 0 : 1);
        }

        private static bool TimedOut(double seconds)
        {
            return EditorApplication.timeSinceStartup - SessionState.GetFloat(KeyStepStart, 0f) > seconds;
        }

        private static void AdvanceTo(Step next)
        {
            Debug.Log($"{Tag} {CurrentStep} -> {next}");
            CurrentStep = next;
            SessionState.SetFloat(KeyStepStart, (float)EditorApplication.timeSinceStartup);
        }

        private static string ActiveScene => SceneManager.GetActiveScene().name;

        private static void Tick()
        {
            if (!SessionState.GetBool(KeyRunning, false)) return;

            switch (CurrentStep)
            {
                case Step.EnterPlayMode:
                    if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
                    else AdvanceTo(Step.StartNewGame);
                    break;

                case Step.StartNewGame:
                    if (GameFlowManager.Instance != null)
                    {
                        GameFlowManager.Instance.StartNewGame();
                        AdvanceTo(Step.ClickThroughStory);
                    }
                    else if (TimedOut(20)) Fail("GameFlowManager never bootstrapped");
                    break;

                case Step.ClickThroughStory:
                    if (ActiveScene == "CombatTestScene")
                    {
                        AdvanceTo(Step.WaitForCombat);
                    }
                    else if (ActiveScene == "MapTestScene")
                    {
                        Fail("The intro went straight to the map: the tutorial fight never started");
                    }
                    else
                    {
                        if (StoryPlayer.Instance != null) StoryPlayer.Instance.Advance();
                        if (TimedOut(60)) Fail($"Still in {ActiveScene} after clicking through the story");
                    }
                    break;

                case Step.WaitForCombat:
                    if (TutorialCoach.Instance != null && CombatManager.Instance != null)
                    {
                        var run = RunState.Current;
                        if (run.Resume != ResumeKind.None) { Fail($"The tutorial fight saved a resume point ({run.Resume})"); break; }
                        if (!run.HasDeck || run.Deck.Count == 0) { Fail("The run deck is empty in the tutorial fight"); break; }
                        SessionState.SetInt(KeyHp, run.CurrentHp);
                        SessionState.SetInt(KeyIncense, run.Incense);
                        AdvanceTo(Step.WaitForPlayerPhase);
                    }
                    else if (TimedOut(20)) Fail("CombatTestScene loaded without the tutorial hints (TutorialCoach)");
                    break;

                case Step.WaitForPlayerPhase:
                    var turns = TurnPhaseController.Instance;
                    if (turns != null && turns.CurrentPhase == TurnPhase.PlayerBoard)
                    {
                        AdvanceTo(Step.LoseFight);
                    }
                    else if (TimedOut(60)) Fail("The tutorial fight never reached the player's board phase");
                    break;

                case Step.LoseFight:
                    CombatManager.Instance.EndCombat(false);
                    AdvanceTo(Step.WaitForStory);
                    break;

                case Step.WaitForStory:
                    if (ActiveScene == "StoryScene" && StoryPlayer.Instance != null && StoryPlayer.Instance.PageIndex >= 0)
                    {
                        var run = RunState.Current;
                        int expectedPage = SessionState.GetInt(KeyResumePage, -1);
                        if (StoryPlayer.Instance.PageIndex != expectedPage) { Fail($"Story resumed on page {StoryPlayer.Instance.PageIndex}, expected {expectedPage}"); break; }
                        if (!RunState.HasSave || !run.IsPersistent) { Fail("Losing the tutorial fight ended the run"); break; }
                        if (run.CurrentHp != SessionState.GetInt(KeyHp, -1)) { Fail($"Khwan changed: {SessionState.GetInt(KeyHp, -1)} -> {run.CurrentHp}"); break; }
                        if (run.Incense != SessionState.GetInt(KeyIncense, -1)) { Fail($"Incense changed: {SessionState.GetInt(KeyIncense, -1)} -> {run.Incense}"); break; }
                        Debug.Log($"{Tag} ok   back in the story on page {expectedPage + 1}, Khwan {run.CurrentHp}, incense {run.Incense}, save kept");
                        AdvanceTo(Step.Done);
                    }
                    else if (ActiveScene == "MainMenu" && TimedOut(1)) Fail("Losing the tutorial fight went to the main menu");
                    else if (TimedOut(20)) Fail($"Never returned to StoryScene (still in {ActiveScene})");
                    break;

                case Step.Done:
                    Finish(0);
                    break;
            }
        }
    }
}
