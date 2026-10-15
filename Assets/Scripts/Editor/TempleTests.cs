using System.IO;
using System.Linq;
using TawanOS.GameFlow;
using TawanOS.MapEngine;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// วัด node and the map without Elite (Game Designer, 11 Oct): the 7-floor map never rolls Elite, always has วัด
    /// on the floor before the boss (15 Oct) but never two in a row, the temple heals to full once per visit, the legend and the map config
    /// know the temple, and the shop no longer heals or removes cards.
    /// Menu: Tools/TawanOS/Tests/Temple. Batch: -executeMethod TawanOS.EditorTools.TempleTests.Run
    /// </summary>
    public static class TempleTests
    {
        private const string MapConfigPath = "Assets/MapEngineData/Profiles/DefaultMapConfig.asset";
        private const int Seeds = 300;
        private static string SavePath => Path.Combine(Application.persistentDataPath, "run_save.json");

        [MenuItem("Tools/TawanOS/Tests/Temple")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;
            string backupSave = File.Exists(SavePath) ? File.ReadAllText(SavePath) : null;

            try
            {
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
                    // Every map now has วัด on the floor before the boss (Game Designer, 15 Oct; was "not forced")
                    failures += Expect("Every map has a temple", mapsWithTemple == Seeds);
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

                // 3. One bow: heal to full when hurt; ขวัญชั่วคราว +5 when full, stacking; damage takes it first
                RunState.StartNewRun(RunState.DefaultTotalFloors);
                var run = RunState.Current;
                var go = new GameObject("TempleTests");
                try
                {
                    var temple = go.AddComponent<TempleManager>();
                    run.SetCurrentHp(12);
                    failures += Expect("Hurt player can pray", temple.CanPray);
                    int healed = temple.Pray();
                    failures += Expect("Hurt: praying heals to full, no extra", run.CurrentHp == run.MaxHp && run.TempKhwan == 0);
                    failures += Expect("Pray reports the Khwan restored", healed == run.MaxHp - 12);
                    run.SetCurrentHp(5);
                    failures += Expect("Only one blessing per visit", temple.Pray() == 0 && run.CurrentHp == 5);

                    var stageGo = new GameObject("StageTest");
                    stageGo.transform.SetParent(go.transform);
                    var stage = stageGo.AddComponent<TempleStageView>();
                    var full = stageGo.AddComponent<TempleManager>();
                    full.stage = stage;
                    run.SetCurrentHp(run.MaxHp);
                    stage.Show(full);
                    failures += Expect("Full: the one กราบไหว้ button is enabled", stage.PrayButton.interactable && stage.PrayText.text == "กราบไหว้");
                    int gained = full.Pray();
                    failures += Expect("Full: bowing gives ขวัญชั่วคราว +5 (20/20 -> 25/20)", gained == 5 && run.TempKhwan == 5 && run.CurrentHp == run.MaxHp + 5);

                    var again = go.AddComponent<TempleManager>();
                    run.SetCurrentHp(run.MaxHp + 3);
                    again.Pray();
                    failures += Expect("ขวัญชั่วคราว stacks (23/20 -> 28/20)", run.CurrentHp == run.MaxHp + 8);

                    run.Heal(10);
                    failures += Expect("Heals neither add to nor cut ขวัญชั่วคราว", run.CurrentHp == run.MaxHp + 8);
                    run.TakeDamage(8);
                    failures += Expect("Damage takes ขวัญชั่วคราว first, gone at max", run.CurrentHp == run.MaxHp && run.TempKhwan == 0);
                    run.AddTempKhwan(5);
                    run.ChangeMaxHp(3);
                    failures += Expect("Max Khwan +3 keeps ขวัญชั่วคราว on top", run.TempKhwan == 5);
                    run.SetCurrentHp(run.MaxHp + 50);
                    failures += Expect("SetCurrentHp never adds ขวัญชั่วคราว", run.CurrentHp == run.MaxHp + 5);
                    run.ChangeMaxHp(-3); // leave the run as the next test expects it
                    run.TakeDamage(run.TempKhwan);
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }

                // 4. Temple scene is in the build
                failures += Expect("TempleScene is in Build Settings",
                    EditorBuildSettings.scenes.Any(s => s.enabled && s.path == "Assets/Scenes/TempleScene.unity"));

                // 5. The shop sells cards only: no heal (the วัด does it), no card removal (the เมรุ does it)
                var shopType = typeof(TawanOS.ShopEngine.ShopManager);
                failures += Expect("Shop blessings raise max khwan/กุศล only, no heal", !System.Enum.GetNames(typeof(TawanOS.ShopEngine.ShopBlessingKind)).Any(n => n.Contains("Heal")));
                failures += Expect("ShopManager has no card removal left", shopType.GetMethod("BeginRemoval") == null && shopType.GetMethod("RemoveCard") == null);
            }
            finally
            {
                RunState.EndRun();
                if (backupSave != null)
                {
                    File.WriteAllText(SavePath, backupSave);
                }
                else
                {
                    RunState.DeleteSave();
                }
            }

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
