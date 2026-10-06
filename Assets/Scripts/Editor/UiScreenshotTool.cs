using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TawanOS.CardEngine;
using TawanOS.UI;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Plays CombatTestScene and saves game-view screenshots of the player-facing feedback (plan tasks H1, G3):
    /// the turn banner in the player's phase, the notice shown when a card cannot be played, and the top view
    /// with its "press C again" hint. Files go to Docs/Testing/ui_shots/ for the UI review.
    /// Menu: Tools/TawanOS/UI/Capture Combat Screenshots. Progress survives the domain reloads of Play mode
    /// through SessionState, like AutomatedCombatFlowTest.
    /// </summary>
    [InitializeOnLoad]
    public static class UiScreenshotTool
    {
        private const string KeyRun = "UST_Run";
        private const string KeyStep = "UST_Step";
        private const string KeyTime = "UST_Time";
        private const string OutDir = "Docs/Testing/ui_shots";
        private const string CombatScene = "Assets/Scenes/CombatTestScene.unity";
        private const string MapScene = "Assets/Scenes/MapTestScene.unity";

        private enum Step
        {
            WaitPlayerPhase,
            ShotBanner,
            TriggerNotice,
            ShotNotice,
            EnterTopView,
            ShotTopView,
            LeaveTopView,
            ExitPlay,
            RestoreScene,
            Done
        }

        static UiScreenshotTool()
        {
            if (SessionState.GetBool(KeyRun, false)) EditorApplication.update += Tick;
        }

        [MenuItem("Tools/TawanOS/UI/Capture Combat Screenshots")]
        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            SessionState.SetBool(KeyRun, true);
            SetStep(Step.WaitPlayerPhase);

            EditorSceneManager.OpenScene(CombatScene);
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        private static void SetStep(Step step)
        {
            SessionState.SetInt(KeyStep, (int)step);
            SessionState.SetFloat(KeyTime, (float)EditorApplication.timeSinceStartup);
        }

        private static float Elapsed()
        {
            return (float)EditorApplication.timeSinceStartup - SessionState.GetFloat(KeyTime, 0f);
        }

        private static void Shot(string name)
        {
            string path = Path.Combine(OutDir, name);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[UiScreenshotTool] saved {path}");
        }

        private static void Fail(string reason)
        {
            Debug.LogError($"[UiScreenshotTool] FAILED: {reason}");
            Finish();
        }

        private static void Finish()
        {
            SessionState.SetBool(KeyRun, false);
            EditorApplication.update -= Tick;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(KeyRun, false)) return;

            var step = (Step)SessionState.GetInt(KeyStep, 0);
            var turns = TurnPhaseController.Instance;

            switch (step)
            {
                case Step.WaitPlayerPhase:
                    if (EditorApplication.isPlaying && turns != null && turns.CurrentPhase == TurnPhase.PlayerBoard)
                    {
                        SetStep(Step.ShotBanner);
                    }
                    else if (Elapsed() > 90f)
                    {
                        Fail("the player's phase was not reached within 90 s");
                    }
                    break;

                case Step.ShotBanner:
                    if (Elapsed() > 1.2f)
                    {
                        Shot("1_banner_player_turn.png");
                        SetStep(Step.TriggerNotice);
                    }
                    break;

                case Step.TriggerNotice:
                    if (Elapsed() > 0.8f)
                    {
                        // An incantation cannot be played in the board phase: the real reason text comes from the game
                        var cards = CardManager.Instance;
                        var incantation = cards != null ? cards.Hand.FirstOrDefault(c => c.cardType == CardType.Incantation) : null;
                        string reason = incantation != null ? cards.GetPlayBlockReason(incantation) : null;
                        PlayerNotice.Show(reason ?? PlayBlockReasons.NotEnoughMerit(3, 1));
                        SetStep(Step.ShotNotice);
                    }
                    break;

                case Step.ShotNotice:
                    if (Elapsed() > 0.7f)
                    {
                        Shot("2_notice_card_blocked.png");
                        SetStep(Step.EnterTopView);
                    }
                    break;

                case Step.EnterTopView:
                    if (Elapsed() > 0.5f)
                    {
                        if (CombatCameraRig3D.Instance == null) { Fail("no CombatCameraRig3D"); break; }
                        CombatCameraRig3D.Instance.ToggleTopView();
                        SetStep(Step.ShotTopView);
                    }
                    break;

                case Step.ShotTopView:
                    if (Elapsed() > 1.8f)
                    {
                        var banner = Object.FindFirstObjectByType<TurnBannerView>();
                        var rig = CombatCameraRig3D.Instance;
                        Debug.Log($"[UiScreenshotTool] top view: rig.IsTopView={(rig != null && rig.IsTopView)} " +
                                  $"boardOnly={(rig != null && rig.IsBoardOnlyView)} banner={(banner != null)} " +
                                  $"hintVisible={(banner != null && banner.TopViewHintVisible)}");
                        Shot("3_top_view_hint.png");
                        SetStep(Step.LeaveTopView);
                    }
                    break;

                case Step.LeaveTopView:
                    if (Elapsed() > 0.6f)
                    {
                        CombatCameraRig3D.Instance?.ToggleTopView();
                        SetStep(Step.ExitPlay);
                    }
                    break;

                case Step.ExitPlay:
                    if (Elapsed() > 1.0f)
                    {
                        EditorApplication.isPlaying = false;
                        SetStep(Step.RestoreScene);
                    }
                    break;

                case Step.RestoreScene:
                    if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        EditorSceneManager.OpenScene(MapScene);
                        Debug.Log("[UiScreenshotTool] DONE");
                        SetStep(Step.Done);
                        SessionState.SetBool(KeyRun, false);
                        EditorApplication.update -= Tick;
                    }
                    break;
            }
        }
    }
}
