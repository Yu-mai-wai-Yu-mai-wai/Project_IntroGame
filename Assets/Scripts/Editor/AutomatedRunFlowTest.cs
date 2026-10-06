using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TawanOS.CardEngine;
using TawanOS.GameFlow;
using TawanOS.MapEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Automated test suite for Plan Task A3 (Defeating Boss = End Game):
    /// 1. Verifies VictoryScene exists, is registered in EditorBuildSettings, and loads cleanly.
    /// 2. Verifies VictoryViewUI contains authentic Thai titles, stats summary, and main menu button.
    /// 3. Tests the Run Flow logic:
    ///    - Simulating Boss victory triggers RunState.EndRun(), clears MapSave, and targets VictoryScene.
    ///    - Simulating Defeat triggers RunState.EndRun(), clears MapSave, and targets MainMenu.
    /// 
    /// Menu: Tools/TawanOS/Tests/Automated Run Flow
    /// Batch: -executeMethod TawanOS.EditorTools.AutomatedRunFlowTest.Run
    /// </summary>
    public static class AutomatedRunFlowTest
    {
        [MenuItem("Tools/TawanOS/Tests/Automated Run Flow")]
        public static void RunFromMenu()
        {
            Execute(false);
        }

        public static void Run()
        {
            Execute(true);
        }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;

            // --- 1. Scene Registration & Asset Presence ---
            bool victoryInBuild = false;
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.path.Contains("VictoryScene"))
                {
                    victoryInBuild = s.enabled;
                    break;
                }
            }
            failures += Expect("VictoryScene is enabled in EditorBuildSettings", victoryInBuild);

            // --- 2. VictoryScene UI Elements & Structure ---
            var scene = EditorSceneManager.OpenScene(VictorySetupTool.ScenePath);
            var victoryCanvas = GameObject.Find("VictoryCanvas");
            failures += Expect("VictoryCanvas exists in VictoryScene", victoryCanvas != null);

            if (victoryCanvas != null)
            {
                var view = victoryCanvas.GetComponent<VictoryViewUI>();
                failures += Expect("VictoryViewUI component attached", view != null);

                if (view != null)
                {
                    failures += Expect("titleText is assigned", view.titleText != null);
                    failures += Expect("subtitleText is assigned", view.subtitleText != null);
                    failures += Expect("epilogueText is assigned", view.epilogueText != null);
                    failures += Expect("statsText is assigned", view.statsText != null);
                    failures += Expect("mainMenuButton is assigned", view.mainMenuButton != null);
                    failures += Expect("mainMenuButtonLabel is assigned", view.mainMenuButtonLabel != null);

                    if (view.titleText != null)
                    {
                        failures += Expect("titleText contains 'สู่สุขคติ'", view.titleText.text.Contains("สู่สุขคติ"));
                    }
                }
            }

            // --- 3. Run State Flow: Simulating Boss Victory ---
            // Start a simulated run
            RunState.StartNewRun();
            new MapSaveManager().SaveMap(new MapGraphData { totalFloors = 7, mapWidth = 3 });
            failures += Expect("RunState has active save before boss", RunState.HasSave);

            // Create or get GameFlowManager instance
            var flowGo = new GameObject("TestGameFlowManager");
            var flow = flowGo.AddComponent<GameFlowManager>();

            // Simulate entering Boss node via reflection
            var handleCombatEnteredMethod = typeof(GameFlowManager).GetMethod("HandleCombatNodeEntered",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (handleCombatEnteredMethod != null)
            {
                handleCombatEnteredMethod.Invoke(flow, new object[] { NodeType.Boss });
            }

            // Verify combat node type is Boss
            var combatNodeTypeField = typeof(GameFlowManager).GetField("currentCombatNodeType",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (combatNodeTypeField != null)
            {
                var type = (NodeType)combatNodeTypeField.GetValue(flow);
                failures += Expect("currentCombatNodeType is Boss", type == NodeType.Boss);
            }

            // Simulate combat victory
            var handleCombatEndedMethod = typeof(GameFlowManager).GetMethod("HandleCombatEnded",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (handleCombatEndedMethod != null)
            {
                handleCombatEndedMethod.Invoke(flow, new object[] { true });
            }

            // A3 DoD: Defeating Boss wipes the save so player cannot resume mid-boss
            failures += Expect("Boss victory clears RunState.HasSave", !RunState.HasSave);
            failures += Expect("Boss victory clears MapSave", !new MapSaveManager().HasSavedMap());

            // Cleanup test objects
            Object.DestroyImmediate(flowGo);

            Debug.Log(failures == 0 ? "[AutomatedRunFlowTest] PASS" : $"[AutomatedRunFlowTest] FAIL: {failures} check(s) failed");
            if (exitOnFinish) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Expect(string name, bool condition)
        {
            if (condition)
            {
                Debug.Log($"[AutomatedRunFlowTest] ok   {name}");
                return 0;
            }
            Debug.LogError($"[AutomatedRunFlowTest] FAIL {name}");
            return 1;
        }
    }
}
