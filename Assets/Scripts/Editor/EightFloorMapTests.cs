using System.Collections.Generic;
using System.Linq;
using TawanOS.GameFlow;
using TawanOS.MapEngine;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// The 8-floor map (Game Designer, 11 Oct): start, 8 floors of 3,2,3,1,3,3,2,2 nodes, boss on floor 8.
    /// Floors 1, 4, 7 (counted from 1) are fights only, the others never fights; the other five node types all
    /// show up; every path to the boss passes a วัด (Game Designer, 15 Oct); every node has a way in and out; every node has a board position and the floors run from the start
    /// end of the board to the boss end. A new run uses 8 floors.
    /// Menu: Tools/TawanOS/Tests/Eight Floor Map. Batch: -executeMethod TawanOS.EditorTools.EightFloorMapTests.Run
    /// </summary>
    public static class EightFloorMapTests
    {
        private const string MapConfigPath = "Assets/MapEngineData/Profiles/DefaultMapConfig.asset";
        private const int Seeds = 300;
        private static readonly int[] Widths = { 3, 2, 3, 1, 3, 3, 2, 2 };
        private static readonly int[] CombatFloors = { 0, 3, 6 };

        [MenuItem("Tools/TawanOS/Tests/Eight Floor Map")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;
            failures += Expect("New runs use 8 floors", RunState.DefaultTotalFloors == 8);

            var config = AssetDatabase.LoadAssetAtPath<MapConfigSO>(MapConfigPath);
            failures += Expect("DefaultMapConfig loads with 8 floors", config != null && config.totalFloors == 8);
            if (config == null) { Finish(failures, exitOnFinish); return; }

            var generator = new MapGraphGenerator();
            var seen = new HashSet<NodeType>();
            int badShape = 0, badCombat = 0, badLinks = 0, badBoard = 0, consecutive = 0, noTempleBeforeBoss = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var graph = generator.GenerateMap(config, seed);
                if (graph == null || graph.floors.Count != 9) { badShape++; continue; }

                for (int y = 0; y < Widths.Length; y++)
                {
                    var floor = graph.floors[y];
                    if (floor.Count != Widths[y]) badShape++;
                    bool fight = CombatFloors.Contains(y);
                    foreach (var node in floor)
                    {
                        if (fight != (node.type == NodeType.MinorEnemy)) badCombat++;
                        if (node.type == NodeType.EliteEnemy || node.type == NodeType.Boss) badCombat++;
                        if (!fight) seen.Add(node.type);
                        if (node.incomingConnections.Count == 0 || node.outgoingConnections.Count == 0) badLinks++;
                        foreach (var next in node.outgoingConnections)
                        {
                            var child = graph.GetNode(next);
                            if (child == null || next.y != y + 1) badLinks++;
                            else if (child.type == node.type && (node.type == NodeType.RestSite || node.type == NodeType.Store || node.type == NodeType.Temple)) consecutive++;
                        }
                        if (!MapManager.TryGetTableNodeFbxName(node.gridPosition, config.totalFloors, out _)) badBoard++;
                    }
                }
                var boss = graph.floors[8];
                if (boss.Count != 1 || boss[0].type != NodeType.Boss || boss[0].incomingConnections.Count != 2) badShape++;
                if (!MapManager.TryGetTableNodeFbxName(boss[0].gridPosition, config.totalFloors, out _)) badBoard++;
                if (graph.startNode == null || graph.startNode.outgoingConnections.Count != 3) badLinks++;
                if (CanReachBossWithoutTemple(graph)) noTempleBeforeBoss++;
            }

            failures += Expect($"{Seeds} maps: floors 3,2,3,1,3,3,2,2 then one boss", badShape == 0);
            failures += Expect("Floors 1, 4, 7 are all fights and no other floor has a fight", badCombat == 0);
            failures += Expect("Every node has a way in and a way on to the next floor", badLinks == 0);
            failures += Expect("Every node has a place on the board", badBoard == 0);
            failures += Expect("เมรุ, ศาลตายาย, วัด never follow their own type", consecutive == 0);
            failures += Expect("Every path to the boss passes a วัด", noTempleBeforeBoss == 0);
            foreach (var type in new[] { NodeType.Event, NodeType.Treasure, NodeType.Store, NodeType.RestSite, NodeType.Temple })
                failures += Expect($"{type} shows up on the random floors", seen.Contains(type));

            // Floors run from the start end of the board to the boss end, evenly
            float last = float.MaxValue;
            bool ordered = true;
            for (int y = -1; y <= 8; y++)
            {
                float x = MapManager.TableFloorX(y, 8);
                if (x >= last) ordered = false;
                last = x;
            }
            failures += Expect("Board columns go from start to boss", ordered);
            failures += Expect("Start and boss stay at the board ends",
                Mathf.Approximately(MapManager.TableFloorX(-1, 8), MapManager.TableFloorX(-1, 7))
                && Mathf.Approximately(MapManager.TableFloorX(8, 8), MapManager.TableFloorX(7, 7)));

            Finish(failures, exitOnFinish);
        }

        // Walks from the start without stepping on a วัด; true if the boss can still be reached that way
        private static bool CanReachBossWithoutTemple(MapGraphData graph)
        {
            if (graph.startNode == null) return true;
            var seenPos = new HashSet<Vector2Int>();
            var open = new Stack<NodeBlueprint>();
            open.Push(graph.startNode);
            while (open.Count > 0)
            {
                var node = open.Pop();
                if (node.type == NodeType.Boss) return true;
                foreach (var next in node.outgoingConnections)
                {
                    var child = graph.GetNode(next);
                    if (child == null || child.type == NodeType.Temple || !seenPos.Add(next)) continue;
                    open.Push(child);
                }
            }
            return false;
        }

        private static void Finish(int failures, bool exitOnFinish)
        {
            Debug.Log(failures == 0 ? "[EightFloorMapTests] PASS" : $"[EightFloorMapTests] FAIL: {failures} check(s) failed");
            if (exitOnFinish) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Expect(string label, bool ok)
        {
            Debug.Log($"[EightFloorMapTests] {(ok ? "ok  " : "FAIL")} {label}");
            return ok ? 0 : 1;
        }
    }
}
