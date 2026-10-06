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
                failures += Expect("EncounterTable has at least 3 minor enemies", table.minor.Count >= 3);
                failures += Expect("EncounterTable has at least 1 elite enemy", table.elite.Count >= 1);
                failures += Expect("EncounterTable has at least 1 boss enemy", table.boss.Count >= 1);

                // A7: Elite profile is distinct from minor and has 45 Khwan
                var elite = table.Pick(NodeType.EliteEnemy);
                failures += Expect("Elite node resolves an enemy", elite != null);
                if (elite != null)
                {
                    failures += Expect("Elite enemy has 45 Khwan (A7)", elite.maxKhwan == 45);
                    failures += Expect("Elite enemy is not in minor list (A7)", !table.minor.Contains(elite));
                }

                // Check all minor enemies
                foreach (var m in table.minor)
                {
                    failures += Expect($"Minor enemy '{m.enemyName}' is not null", m != null);
                    if (m != null)
                    {
                        failures += Expect($"Minor enemy '{m.enemyName}' has 30 Khwan", m.maxKhwan == 30);
                    }
                }

                // Check boss
                var boss = table.Pick(NodeType.Boss);
                failures += Expect("Boss node resolves an enemy", boss != null);
                if (boss != null)
                {
                    failures += Expect("Boss is the Phi Tai Hong profile (enemy_boss)", boss.enemyId == "enemy_boss");
                    failures += Expect("Boss name contains ผีตายโหง", boss.enemyName != null && boss.enemyName.Contains("ผีตายโหง"));
                    failures += Expect("Boss has 60 Khwan", boss.maxKhwan == 60);
                }

                // A9: Ensure all enemyIds in roster are unique
                var allEnemies = new System.Collections.Generic.List<EnemyProfileSO>();
                allEnemies.AddRange(table.minor);
                allEnemies.AddRange(table.elite);
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
    }
}
