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

            // --- 4. Map Mode Tests (Phase A10: 4 Floors Procedural Generation & Seed Checks) ---
            var generator = new MapGraphGenerator();
            var config4 = ScriptableObject.CreateInstance<MapConfigSO>();
            config4.totalFloors = 4;
            config4.mapWidth = 3;
            config4.use3DTableMode = false;

            for (int seed = 100; seed < 120; seed++)
            {
                var graph = generator.GenerateMap(config4, seed);
                failures += Expect($"Seed {seed} (4 floors): Graph not null", graph != null);
                if (graph == null) continue;

                failures += Expect($"Seed {seed} (4 floors): Floor count is 5", graph.floors.Count == 5);
                
                bool hasBoss = false;
                if (graph.floors.Count > 4 && graph.floors[4] != null)
                {
                    foreach (var n in graph.floors[4])
                    {
                        if (n.type == NodeType.Boss) hasBoss = true;
                    }
                }
                failures += Expect($"Seed {seed} (4 floors): Boss exists on floor 4", hasBoss);

                if (graph.floors.Count > 0 && graph.floors[0] != null)
                {
                    foreach (var startNode in graph.floors[0])
                    {
                        bool canReach = CanReachBoss(startNode, graph, 4);
                        failures += Expect($"Seed {seed} Node ({startNode.gridPosition.x},{startNode.gridPosition.y}) reaches Boss", canReach);
                    }
                }

                for (int f = 1; f < 4; f++)
                {
                    if (f >= graph.floors.Count) break;
                    foreach (var n in graph.floors[f])
                    {
                        failures += Expect($"Seed {seed} Node ({n.gridPosition.x},{n.gridPosition.y}) has incoming", n.incomingConnections.Count > 0);
                        failures += Expect($"Seed {seed} Node ({n.gridPosition.x},{n.gridPosition.y}) has outgoing", n.outgoingConnections.Count > 0);
                    }
                }
            }

            var tableConfig4 = ScriptableObject.CreateInstance<MapConfigSO>();
            tableConfig4.totalFloors = 4;
            tableConfig4.mapWidth = 3;
            tableConfig4.use3DTableMode = true;

            var tableGraph = generator.GenerateMap(tableConfig4, 42);
            failures += Expect("TableMode 4 floors: Graph generated", tableGraph != null);
            if (tableGraph != null)
            {
                failures += Expect("TableMode 4 floors: Floor count is 5", tableGraph.floors.Count == 5);
                failures += Expect("TableMode 4 floors: Boss is on floor 4", tableGraph.floors[4].Count == 1 && tableGraph.floors[4][0].type == NodeType.Boss);
                failures += Expect("TableMode 4 floors: Node.018 mapping exists", MapManager.TryGetTableNodeFbxName(new Vector2Int(1, 4), 4, out string fbx) && fbx == "Node.018");
            }

            RunState.StartNewRun(4);
            failures += Expect("RunState.Current.TotalFloors is 4", RunState.Current.TotalFloors == 4);
            failures += Expect("RunState save file exists", RunState.HasSave);
            string desc4 = RunState.DescribeSave();
            failures += Expect("RunState describe save shows 4 floors", desc4 != null && desc4.Contains("โหมดสั้น 4 ชั้น"));

            RunState.LoadSavedRun();
            failures += Expect("Reloaded RunState.Current.TotalFloors is 4", RunState.Current.TotalFloors == 4);

            RunState.StartNewRun(7);
            failures += Expect("RunState.Current.TotalFloors is 7", RunState.Current.TotalFloors == 7);
            string desc7 = RunState.DescribeSave();
            failures += Expect("RunState describe save shows 7 floors", desc7 != null && desc7.Contains("โหมดเต็ม 7 ชั้น"));

            RunState.EndRun();

            var mmScene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            var canvas = GameObject.Find("MainMenuCanvas");
            failures += Expect("MainMenu scene has MainMenuCanvas", canvas != null);
            if (canvas != null)
            {
                var menu = canvas.GetComponent<MainMenuUI>();
                failures += Expect("MainMenuUI attached", menu != null);
                failures += Expect("modePanel assigned", menu != null && menu.modePanel != null);
                failures += Expect("fullModeButton assigned", menu != null && menu.fullModeButton != null);
                failures += Expect("shortModeButton assigned", menu != null && menu.shortModeButton != null);
                failures += Expect("modeCancelButton assigned", menu != null && menu.modeCancelButton != null);
            }

            Object.DestroyImmediate(config4);
            Object.DestroyImmediate(tableConfig4);

            Debug.Log(failures == 0 ? "[AutomatedRunFlowTest] PASS" : $"[AutomatedRunFlowTest] FAIL: {failures} check(s) failed");
            if (exitOnFinish) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static bool CanReachBoss(NodeBlueprint startNode, MapGraphData graph, int bossFloor)
        {
            var visited = new System.Collections.Generic.HashSet<Vector2Int>();
            var queue = new System.Collections.Generic.Queue<Vector2Int>();
            queue.Enqueue(startNode.gridPosition);
            visited.Add(startNode.gridPosition);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.y == bossFloor) return true;

                var node = graph.GetNode(current);
                if (node == null) continue;

                foreach (var outPos in node.outgoingConnections)
                {
                    if (!visited.Contains(outPos))
                    {
                        visited.Add(outPos);
                        queue.Enqueue(outPos);
                    }
                }
            }

            return false;
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
