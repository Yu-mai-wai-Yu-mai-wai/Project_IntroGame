using System.IO;
using UnityEditor;
using UnityEngine;
using TawanOS.CardEngine;
using TawanOS.GameFlow;
using TawanOS.MapEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Checks plan task A1: enemies resolve through Resources (works in a player build) and GameFlowManager no
    /// longer touches AssetDatabase. Menu: Tools/TawanOS/Tests/Encounter Table. Batch:
    /// -executeMethod TawanOS.EditorTools.EncounterTableTests.Run (exit code 1 on failure).
    /// </summary>
    public static class EncounterTableTests
    {
        [MenuItem("Tools/TawanOS/Tests/Encounter Table")]
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

            var table = EncounterTableSO.Load();
            failures += Expect("EncounterTable loads through Resources.Load", table != null);

            if (table != null)
            {
                failures += Expect("EncounterTable has at least 2 minor enemies", table.minor.Count >= 2);
                failures += Expect("EncounterTable has at least 1 boss enemy", table.boss.Count >= 1);

                // Check all minor enemies
                foreach (var m in table.minor)
                {
                    failures += Expect($"Minor enemy '{m.enemyName}' is not null", m != null);
                    if (m != null)
                    {
                        failures += Expect($"Minor enemy '{m.enemyName}' has 5 Khwan base", m.maxKhwan == 5);
                    }
                }

                // Check boss
                var boss = table.Pick(NodeType.Boss);
                failures += Expect("Boss node resolves an enemy", boss != null);
                if (boss != null)
                {
                    failures += Expect("Boss is the Phi Tai Hong profile (enemy_boss)", boss.enemyId == "enemy_boss");
                    failures += Expect("Boss name contains ผีตายโหง", boss.enemyName != null && boss.enemyName.Contains("ผีตายโหง"));
                    failures += Expect("Boss has 20 Khwan", boss.maxKhwan == 20);
                }

                // A9: Ensure all enemyIds in roster are unique
                var allEnemies = new System.Collections.Generic.List<EnemyProfileSO>();
                allEnemies.AddRange(table.minor);
                if (table.elite != null) allEnemies.AddRange(table.elite);
                allEnemies.AddRange(table.boss);

                var seenIds = new System.Collections.Generic.HashSet<string>();
                bool allUnique = true;
                foreach (var enemy in allEnemies)
                {
                    if (enemy != null)
                    {
                        if (!seenIds.Add(enemy.enemyId))
                        {
                            allUnique = false;
                            Debug.LogError($"[EncounterTableTests] Duplicate enemyId found: '{enemy.enemyId}' in {enemy.name}");
                        }
                    }
                }
                failures += Expect("All enemy IDs in roster are unique (A9)", allUnique);

                // A9: Rolling minor 20 times encounters at least 2 distinct enemies
                var pickedMinorIds = new System.Collections.Generic.HashSet<string>();
                for (int i = 0; i < 20; i++)
                {
                    var picked = table.Pick(NodeType.MinorEnemy);
                    if (picked != null) pickedMinorIds.Add(picked.enemyId);
                }
                failures += Expect("Picking minor enemies returns at least 2 distinct types (A9)", pickedMinorIds.Count >= 2);

                // P0-7: Progression Khwan tests (base 5 + 2 per node passed, boss 20)
                var prog = EnemyDeckProgressionSO.Load();
                failures += Expect("EnemyDeckProgressionSO loads", prog != null);
                if (prog != null && table.minor.Count > 0)
                {
                    var sampleMinor = table.minor[0];
                    failures += Expect("Minor khwan on floor 0 is 5", prog.KhwanFor(0, sampleMinor) == 5);
                    failures += Expect("Minor khwan on floor 3 is 11", prog.KhwanFor(3, sampleMinor) == 11);
                    failures += Expect("Minor khwan on floor 6 is 17", prog.KhwanFor(6, sampleMinor) == 17);
                    if (boss != null)
                    {
                        failures += Expect("Boss khwan on floor 7 is 20", prog.KhwanFor(7, boss) == 20);
                    }

                    // P0-7: Progression Upgrade scaling tests
                    var deckFloor6 = prog.DeckFor(NodeType.MinorEnemy, 6, sampleMinor, 0);
                    int upCountF6 = CountUpgrades(deckFloor6);
                    failures += Expect("player upgraded 0 → enemy floor-6 deck has <=1 upgraded card", deckFloor6 != null && upCountF6 <= 1);

                    var deckFloor0 = prog.DeckFor(NodeType.MinorEnemy, 0, sampleMinor, 3);
                    int upCountF0 = CountUpgrades(deckFloor0);
                    failures += Expect("player upgraded 3 → floor-0 minor has 0 upgraded", deckFloor0 != null && upCountF0 == 0);
                }
            }

            // Runtime code must not depend on AssetDatabase to find enemies
            string managerSource = File.ReadAllText("Assets/Scripts/GameFlow/GameFlowManager.cs");
            failures += Expect("GameFlowManager has no AssetDatabase or UnityEditor reference",
                !managerSource.Contains("AssetDatabase") && !managerSource.Contains("UnityEditor"));

            Debug.Log(failures == 0 ? "[EncounterTableTests] PASS" : $"[EncounterTableTests] FAIL: {failures} check(s) failed");
            if (exitOnFinish) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Expect(string name, bool condition)
        {
            Debug.Log($"[EncounterTableTests] {(condition ? "ok  " : "FAIL")} {name}");
            return condition ? 0 : 1;
        }

        private static int CountUpgrades(System.Collections.Generic.List<CardDataSO> cards)
        {
            if (cards == null) return 0;
            int count = 0;
            foreach (var c in cards)
            {
                if (c == null) continue;
                if ((!string.IsNullOrEmpty(c.cardId) && c.cardId.EndsWith("_up")) ||
                    (!string.IsNullOrEmpty(c.name) && c.name.EndsWith("_up")))
                {
                    count++;
                }
            }
            return count;
        }
    }
}
