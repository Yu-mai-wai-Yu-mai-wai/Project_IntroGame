using System.Linq;
using TawanOS.GameFlow;
using TawanOS.MapEngine;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// วัด node and the map without Elite (Game Designer, 11 Oct): the 7-floor map never rolls Elite, rolls วัด on
    /// some runs but never two in a row, the temple heals to full once per visit, the legend and the map config
    /// know the temple, and the shop no longer heals or removes cards.
    /// Menu: Tools/TawanOS/Tests/Temple. Batch: -executeMethod TawanOS.EditorTools.TempleTests.Run
    /// </summary>
    public static class TempleTests
    {
        private const string MapConfigPath = "Assets/MapEngineData/Profiles/DefaultMapConfig.asset";
        private const int Seeds = 300;

        [MenuItem("Tools/TawanOS/Tests/Temple")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;

            // 1. Map rolls
            var config = AssetDatabase.LoadAssetAtPath<MapConfigSO>(MapConfigPath);
            failures += Expect("DefaultMapConfig loads", config != null);
            if (config != null)
            {
                var generator = new MapGraphGenerator();
                int elites = 0, temples = 0, mapsWithTemple = 0, templeChains = 0;
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    var graph = generator.GenerateMap(config, seed);
                    if (graph == null) { failures += Expect($"Seed {seed}: map generated", false); continue; }
                    int here = 0;
                    foreach (var node in graph.GetAllNodes())
                    {
                        if (node.type == NodeType.EliteEnemy) elites++;
                        if (node.type != NodeType.Temple) continue;
                        here++;
                        foreach (var next in node.outgoingConnections)
                            if (graph.GetNode(next)?.type == NodeType.Temple) templeChains++;
                    }
                    temples += here;
                    if (here > 0) mapsWithTemple++;
                }
                Debug.Log($"[TempleTests] {Seeds} maps: {temples} temples, {mapsWithTemple} maps with a temple, {elites} elites");
                failures += Expect("No Elite node on any map", elites == 0);
                failures += Expect("Temples are rolled on some maps", mapsWithTemple > 0);
                failures += Expect("Not every map is forced to have a temple", mapsWithTemple < Seeds);
                failures += Expect("A temple never leads straight into another temple", templeChains == 0);

                var profile = config.GetProfileForType(NodeType.Temple);
                failures += Expect("DefaultMapConfig has a Temple profile", profile != null);
                failures += Expect("Temple profile is titled วัด", profile != null && profile.title == "วัด");
                failures += Expect("Temple profile uses the วัด 1.png icon", profile != null && profile.icon != null && profile.icon.name == "วัด 1");
            }

            // 2. Legend: วัด in, Elite out, still 7 rows
            failures += Expect("Legend lists วัด", MapLegendUI.AllNodeTypes.Contains(NodeType.Temple));
            failures += Expect("Legend no longer lists Elite", !MapLegendUI.AllNodeTypes.Contains(NodeType.EliteEnemy));
            failures += Expect("Legend has 7 rows", MapLegendUI.AllNodeTypes.Length == 7);

            // 3. The temple heals to full, once
            var run = RunState.Current;
            if (run.IsPersistent)
            {
                Debug.LogError("[TempleTests] RunState.Current is a persistent run; refusing to touch a real save");
                failures++;
            }
            else
            {
                var go = new GameObject("TempleTests");
                try
                {
                    var temple = go.AddComponent<TempleManager>();
                    run.SetCurrentHp(12);
                    failures += Expect("Hurt player can pray", temple.CanPray);
                    int healed = temple.Pray();
                    failures += Expect("Praying heals to full", run.CurrentHp == run.MaxHp);
                    failures += Expect("Pray reports the Khwan restored", healed == run.MaxHp - 12);
                    run.SetCurrentHp(5);
                    failures += Expect("Only one blessing per visit", temple.Pray() == 0 && run.CurrentHp == 5);

                    var fresh = go.AddComponent<TempleManager>();
                    run.SetCurrentHp(run.MaxHp);
                    failures += Expect("With full Khwan there is nothing to pray for", !fresh.CanPray && fresh.Pray() == 0);
                }
                finally
                {
                    run.SetCurrentHp(RunState.DefaultMaxHp);
                    Object.DestroyImmediate(go);
                }
            }

            // 4. Temple scene is in the build
            failures += Expect("TempleScene is in Build Settings",
                EditorBuildSettings.scenes.Any(s => s.enabled && s.path == "Assets/Scenes/TempleScene.unity"));

            // 5. The shop sells cards only: no heal (the วัด does it), no card removal (the เมรุ does it)
            var shopType = typeof(TawanOS.ShopEngine.ShopManager);
            failures += Expect("ShopManager has no blessing (heal) left", shopType.GetMethod("BuyBlessing") == null);
            failures += Expect("ShopManager has no card removal left", shopType.GetMethod("BeginRemoval") == null && shopType.GetMethod("RemoveCard") == null);

            Debug.Log(failures == 0 ? "[TempleTests] PASS" : $"[TempleTests] FAIL: {failures} check(s) failed");
            if (exitOnFinish) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Expect(string label, bool ok)
        {
            Debug.Log($"[TempleTests] {(ok ? "ok  " : "FAIL")} {label}");
            return ok ? 0 : 1;
        }
    }
}
