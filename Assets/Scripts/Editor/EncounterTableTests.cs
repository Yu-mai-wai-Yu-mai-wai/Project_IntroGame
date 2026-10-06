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
                var minor = table.Pick(NodeType.MinorEnemy);
                var elite = table.Pick(NodeType.EliteEnemy);
                var boss = table.Pick(NodeType.Boss);

                failures += Expect("Minor node resolves an enemy", minor != null);
                failures += Expect("Elite node resolves an enemy", elite != null);
                failures += Expect("Boss node resolves an enemy", boss != null);
                failures += Expect("Boss is the Phi Tai Hong profile (enemy_boss)", boss != null && boss.enemyId == "enemy_boss");
                failures += Expect("Boss name contains ผีตายโหง", boss != null && boss.enemyName != null && boss.enemyName.Contains("ผีตายโหง"));
                failures += Expect("Minor enemy has 30 Khwan", minor != null && minor.maxKhwan == 30);
                failures += Expect("Boss has 60 Khwan", boss != null && boss.maxKhwan == 60);
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
