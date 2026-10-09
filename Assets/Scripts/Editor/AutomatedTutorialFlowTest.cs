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
    /// tutorial hints up -> close each hint (checking it froze the fight and lit something on screen) up to the
    /// board-phase ones -> lose the fight on purpose -> back in StoryScene on the page after the dream, with the
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
        private const string KeySeenHints = "ATFT_SeenHints";

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
            SessionState.SetString(KeySeenHints, "");
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

        // Turn 1, player's board phase: the hand is the story page's fixed opening hand, every card in it can be
        // played this turn on the turn's Merit, and the scripted bot has put down exactly its turn-1 card
        private static string CheckTurnOne(TurnPhaseController turns)
        {
            var story = AssetDatabase.LoadAssetAtPath<StoryDataSO>("Assets/StoryEngineData/Story_Intro.asset");
            var page = story.pages.Find(p => p.tutorialFightEnemy != null);
            var cards = CardManager.Instance;
            var combat = CombatManager.Instance;
            if (turns.TurnNumber != 1) return $"Expected turn 1, got turn {turns.TurnNumber}";

            var handIds = new System.Collections.Generic.List<string>();
            foreach (var c in cards.Hand) handIds.Add(c.cardId);
            var wantIds = page.tutorialOpeningHand.ConvertAll(c => c.cardId);
            if (string.Join(",", handIds) != string.Join(",", wantIds))
                return $"Opening hand is [{string.Join(",", handIds)}], expected [{string.Join(",", wantIds)}]";

            int merit = 0;
            bool hasBoard = false, hasSpell = false;
            foreach (var c in cards.Hand)
            {
                if (c.magicSchool == MagicSchool.WhiteMagic) merit += c.meritCost;
                if (c.cardType == CardType.Incantation) hasSpell = true;
                else
                {
                    hasBoard = true;
                    if (!turns.CanPlayCard(c) || !cards.CanAfford(c)) return $"{c.cardId} cannot be played in the board phase of turn 1";
                }
            }
            if (!hasBoard || !hasSpell) return "The opening hand needs both a familiar/amulet and an incantation";
            if (merit > combat.CurrentMerit) return $"The hand costs {merit} Merit, turn 1 has {combat.CurrentMerit}";

            var bot = page.tutorialFightEnemy;
            if (!bot.scriptedDeck) return $"{bot.name} is not a scripted bot";
            int expectBoard = bot.cardsPerTurn.Count > 0 ? bot.cardsPerTurn[0] : 0;
            if (combat.State.enemyBoardCards.Count != expectBoard)
                return $"The bot has {combat.State.enemyBoardCards.Count} cards on the board after its turn-1 phase, expected {expectBoard}";

            Debug.Log($"{Tag} ok   turn 1 hand [{string.Join(",", handIds)}] costs {merit}/{combat.CurrentMerit} Merit; bot put down {expectBoard} card(s)");
            return null;
        }

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
                    // Each hint freezes the fight until it is closed: read it, check it lit its target, close it
                    var coach = TutorialCoach.Instance;
                    if (coach != null && coach.IsShowingHint)
                    {
                        string title = coach.ShowingTitle;
                        if (Time.timeScale != 0f) { Fail($"Hint '{title}' is up but the fight is not frozen"); break; }
                        Debug.Log($"{Tag} hint '{title}' lit={coach.ShowingLit}");
                        if (!coach.ShowingLit) { Fail($"Hint '{title}' found nothing on screen to light"); break; }
                        SessionState.SetString(KeySeenHints, SessionState.GetString(KeySeenHints, "") + "|" + title);
                        coach.Next();
                        break;
                    }

                    var turns = TurnPhaseController.Instance;
                    bool sawBoardHints = SessionState.GetString(KeySeenHints, "").Contains("|กุศล");
                    if (turns != null && turns.CurrentPhase == TurnPhase.PlayerBoard && sawBoardHints)
                    {
                        if (Time.timeScale != 1f) { Fail($"Time scale left at {Time.timeScale} after the hints closed"); break; }
                        string problem = CheckTurnOne(turns);
                        if (problem != null) { Fail(problem); break; }
                        AdvanceTo(Step.LoseFight);
                    }
                    else if (TimedOut(90)) Fail($"Never saw the board-phase hints (seen: {SessionState.GetString(KeySeenHints, "")})");
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
                        if (Time.timeScale != 1f) { Fail($"The story runs with time scale {Time.timeScale}"); break; }
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
