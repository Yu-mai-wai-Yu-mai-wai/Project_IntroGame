using System.Collections.Generic;
using System.Linq;
using TawanOS.GameFlow;
using TawanOS.MapEngine;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// The 8-floor map (Game Designer, 11 Oct), in both shapes. Fixed (randomTableLinks off): start, 8 floors of
    /// 3,2,3,1,3,3,2,2 nodes, boss on floor 8. Random links (22 Oct, the default): 1-3 nodes per floor on rows 0-2,
    /// a link goes to the next floor or skips one on its row, links never cross or run through a node, the floor
    /// before the boss is never skipped, some path passes every floor and some maps have a shorter path too.
    /// In both shapes:
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

            var fixedConfig = Object.Instantiate(config);
            fixedConfig.randomTableLinks = false;
            fixedConfig.useRandomSeed = false;
            failures += CheckFixedMaps(fixedConfig);

            var randomConfig = Object.Instantiate(config);
            randomConfig.randomTableLinks = true;
            randomConfig.useRandomSeed = false;
            failures += CheckRandomLinkMaps(randomConfig);

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

        private static int CheckFixedMaps(MapConfigSO config)
        {
            int failures = 0;
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

            failures += Expect($"Fixed: {Seeds} maps: floors 3,2,3,1,3,3,2,2 then one boss", badShape == 0);
            failures += Expect("Fixed: floors 1, 4, 7 are all fights and no other floor has a fight", badCombat == 0);
            failures += Expect("Fixed: every node has a way in and a way on to the next floor", badLinks == 0);
            failures += Expect("Fixed: every node has a place on the board", badBoard == 0);
            failures += Expect("Fixed: เมรุ, ศาลตายาย, วัด never follow their own type", consecutive == 0);
            failures += Expect("Fixed: every path to the boss passes a วัด", noTempleBeforeBoss == 0);
            foreach (var type in new[] { NodeType.Event, NodeType.Treasure, NodeType.Store, NodeType.RestSite, NodeType.Temple })
                failures += Expect($"Fixed: {type} shows up on the random floors", seen.Contains(type));
            return failures;
        }

        private static int CheckRandomLinkMaps(MapConfigSO config)
        {
            int failures = 0;
            var generator = new MapGraphGenerator();
            var seen = new HashSet<NodeType>();
            int notRandom = 0, badShape = 0, badCombat = 0, badLinks = 0, badBoard = 0, crossings = 0, consecutive = 0;
            int noTempleBeforeBoss = 0, noFullPath = 0, withShortcut = 0, skipLinks = 0;
            var layouts = new HashSet<string>();
            for (int seed = 1; seed <= Seeds; seed++)
            {
                var graph = generator.GenerateMap(config, seed);
                if (graph == null || !graph.randomLinks) { notRandom++; continue; }
                if (graph.floors.Count != 9) { badShape++; continue; }

                var links = new List<(Vector2Int a, Vector2Int b)>();
                var layout = new System.Text.StringBuilder();
                for (int y = 0; y < 8; y++)
                {
                    var floor = graph.floors[y];
                    if (floor.Count < 1 || floor.Count > MapGraphGenerator.TableLanes || (y == 0 && floor.Count < 2)) badShape++;
                    foreach (var node in floor)
                    {
                        layout.Append(node.gridPosition).Append(node.outgoingConnections.Count);
                        if (node.gridPosition.x < 0 || node.gridPosition.x >= MapGraphGenerator.TableLanes) badShape++;
                        bool fight = y % 3 == 0 && y < 7;
                        if (fight != (node.type == NodeType.MinorEnemy)) badCombat++;
                        if (y == 7 && node.type != NodeType.Temple) badCombat++;
                        if (node.type == NodeType.EliteEnemy || node.type == NodeType.Boss) badCombat++;
                        if (!fight && y < 7) seen.Add(node.type);
                        if (node.incomingConnections.Count == 0 || node.outgoingConnections.Count == 0) badLinks++;
                        foreach (var next in node.outgoingConnections)
                        {
                            var child = graph.GetNode(next);
                            int dx = next.x - node.gridPosition.x, dy = next.y - y;
                            bool step = dy == 1 && Mathf.Abs(dx) <= 1 && next.y <= 7;
                            bool skip = dy == 2 && dx == 0 && next.y <= 7;
                            bool toBoss = y == 7 && child != null && child.type == NodeType.Boss;
                            if (child == null || !(step || skip || toBoss)) badLinks++;
                            else if (child.type == node.type && (node.type == NodeType.RestSite || node.type == NodeType.Store || node.type == NodeType.Temple)) consecutive++;
                            if (skip) skipLinks++;
                            if (step || skip) links.Add((node.gridPosition, next));
                        }
                        if (!MapManager.TryGetBoardPedestalName(node.gridPosition, config.totalFloors, true, out _)) badBoard++;
                    }
                }
                layouts.Add(layout.ToString());

                var boss = graph.floors[8];
                if (boss.Count != 1 || boss[0].type != NodeType.Boss || boss[0].incomingConnections.Count != graph.floors[7].Count) badShape++;
                if (boss.Count == 1 && !MapManager.TryGetBoardPedestalName(boss[0].gridPosition, config.totalFloors, true, out _)) badBoard++;
                if (graph.startNode == null || graph.startNode.outgoingConnections.Count != graph.floors[0].Count) badLinks++;
                if (CanReachBossWithoutTemple(graph)) noTempleBeforeBoss++;

                for (int i = 0; i < links.Count; i++)
                {
                    for (int j = i + 1; j < links.Count; j++)
                        if (Clash(links[i].a, links[i].b, links[j].a, links[j].b)) crossings++;
                    foreach (var node in graph.GetAllNodes())
                        if (OnInside(node.gridPosition, links[i].a, links[i].b)) crossings++;
                }

                PathLengths(graph, out int shortest, out int longest);
                if (longest != 8) noFullPath++;
                if (shortest < longest) withShortcut++;
            }

            failures += Expect($"Random: {Seeds} maps all come from the random-link generator", notRandom == 0);
            failures += Expect("Random: 1-3 nodes on every floor (2+ on the first), one boss fed by every floor-8 node", badShape == 0);
            failures += Expect("Random: floors 1, 4, 7 are all fights, floor 8 is all วัด, no other fights", badCombat == 0);
            failures += Expect("Random: every link goes to the next floor (one row over at most) or skips one on its row, never past floor 8", badLinks == 0);
            failures += Expect("Random: no two links cross and no link runs through a node", crossings == 0);
            failures += Expect("Random: every node has a place on the board", badBoard == 0);
            failures += Expect("Random: เมรุ, ศาลตายาย, วัด never follow their own type", consecutive == 0);
            failures += Expect("Random: every path to the boss passes a วัด", noTempleBeforeBoss == 0);
            failures += Expect("Random: every map has a path that passes all 8 floors", noFullPath == 0);
            failures += Expect($"Random: most maps also have a shorter path ({withShortcut}/{Seeds})", withShortcut > Seeds / 2);
            failures += Expect($"Random: maps differ from seed to seed ({layouts.Count} layouts)", layouts.Count > Seeds / 2);
            foreach (var type in new[] { NodeType.Event, NodeType.Treasure, NodeType.Store, NodeType.RestSite, NodeType.Temple })
                failures += Expect($"Random: {type} shows up on the random floors", seen.Contains(type));
            Debug.Log($"[EightFloorMapTests] Random: {skipLinks / (float)Seeds:0.0} skip links per map, {withShortcut}/{Seeds} maps with a shorter path");
            return failures;
        }

        // Fewest and most nodes passed from the start to the boss (start and boss left out)
        private static void PathLengths(MapGraphData graph, out int shortest, out int longest)
        {
            var lo = new Dictionary<Vector2Int, int>();
            var hi = new Dictionary<Vector2Int, int>();
            for (int y = graph.floors.Count - 1; y >= 0; y--)
            {
                foreach (var node in graph.floors[y])
                {
                    if (node.type == NodeType.Boss) { lo[node.gridPosition] = hi[node.gridPosition] = 0; continue; }
                    int a = int.MaxValue, b = 0;
                    foreach (var next in node.outgoingConnections)
                    {
                        if (!lo.ContainsKey(next)) continue;
                        a = Mathf.Min(a, lo[next]);
                        b = Mathf.Max(b, hi[next]);
                    }
                    lo[node.gridPosition] = a == int.MaxValue ? 0 : a + 1;
                    hi[node.gridPosition] = b + 1;
                }
            }
            shortest = int.MaxValue; longest = 0;
            foreach (var first in graph.floors[0])
            {
                shortest = Mathf.Min(shortest, lo[first.gridPosition]);
                longest = Mathf.Max(longest, hi[first.gridPosition]);
            }
        }

        // Same test as the generator's, written again so the check does not lean on the code it checks
        private static bool Clash(Vector2Int a, Vector2Int b, Vector2Int c, Vector2Int d)
        {
            if (a == c && b == d) return false;
            if (a == c || a == d || b == c || b == d)
                return OnInside(c, a, b) || OnInside(d, a, b) || OnInside(a, c, d) || OnInside(b, c, d);
            int o1 = Side(a, b, c), o2 = Side(a, b, d), o3 = Side(c, d, a), o4 = Side(c, d, b);
            if (o1 * o2 < 0 && o3 * o4 < 0) return true;
            return On(c, a, b) || On(d, a, b) || On(a, c, d) || On(b, c, d);
        }

        private static int Side(Vector2Int a, Vector2Int b, Vector2Int p)
        {
            return System.Math.Sign((b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x));
        }

        private static bool On(Vector2Int p, Vector2Int a, Vector2Int b)
        {
            return Side(a, b, p) == 0 && p.x >= Mathf.Min(a.x, b.x) && p.x <= Mathf.Max(a.x, b.x)
                && p.y >= Mathf.Min(a.y, b.y) && p.y <= Mathf.Max(a.y, b.y);
        }

        private static bool OnInside(Vector2Int p, Vector2Int a, Vector2Int b) => p != a && p != b && On(p, a, b);

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
