using System.Reflection;
using UnityEditor;
using UnityEngine;
using TawanOS.CardEngine;
using TawanOS.GameFlow;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Checks plan task A2: Khwan carries between combats. The run's HP is what a combat starts with, and the
    /// Khwan left at the end is written back. Menu: Tools/TawanOS/Tests/Run Khwan Sync. Batch:
    /// -executeMethod TawanOS.EditorTools.RunStateCombatSyncTests.Run (exit code 1 on failure).
    /// Runs on a non-persistent RunState so nothing is written to the save file.
    /// </summary>
    public static class RunStateCombatSyncTests
    {
        [MenuItem("Tools/TawanOS/Tests/Run Khwan Sync")]
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

            // 1. Run at 23/50 -> combat starts at 23/50
            var go = new GameObject("RunStateCombatSyncTests");
            try
            {
                var combat = go.AddComponent<CombatManager>();
                var run = RunState.Current;
                if (run.IsPersistent)
                {
                    Debug.LogError("[RunStateCombatSyncTests] RunState.Current is a persistent run; refusing to touch a real save");
                    Finish(1, exitOnFinish);
                    return;
                }

                run.SetCurrentHp(RunState.DefaultMaxHp);
                run.TakeDamage(RunState.DefaultMaxHp - 23);
                failures += Expect("Run HP is 23/50 before the fight", run.CurrentHp == 23 && run.MaxHp == 50);

                combat.SetStartingKhwan(run.CurrentHp, run.MaxHp);
                Apply(combat);
                failures += Expect("Combat starts at 23 Khwan (not full)", combat.State.playerKhwan == 23);
                failures += Expect("Combat max Khwan follows the run (50)", combat.State.maxPlayerKhwan == 50);

                // 2. Combat ends at 17 -> run HP is 17
                combat.State.playerKhwan = 17;
                run.SetCurrentHp(combat.State.playerKhwan);
                failures += Expect("Run HP becomes 17 after the fight", run.CurrentHp == 17);

                // 3. Healing then entering the next fight
                run.Heal(10);
                combat.SetStartingKhwan(run.CurrentHp, run.MaxHp);
                Apply(combat);
                failures += Expect("After +10 heal the next combat starts at 27", combat.State.playerKhwan == 27);

                // 4. Never below 1, never above max
                run.SetCurrentHp(0);
                failures += Expect("SetCurrentHp(0) is clamped to 1", run.CurrentHp == 1);
                run.SetCurrentHp(999);
                failures += Expect("SetCurrentHp(999) is clamped to max", run.CurrentHp == run.MaxHp);

                // 5. Without a run (scene opened alone) the combat starts at full Khwan
                var alone = go.AddComponent<CombatManager>();
                alone.State.maxPlayerKhwan = 50;
                alone.State.playerKhwan = 5;
                Apply(alone);
                failures += Expect("No run attached: combat starts at full Khwan", alone.State.playerKhwan == 50);

                run.SetCurrentHp(RunState.DefaultMaxHp);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            Debug.Log(failures == 0 ? "[RunStateCombatSyncTests] PASS" : $"[RunStateCombatSyncTests] FAIL: {failures} check(s) failed");
            Finish(failures, exitOnFinish);
        }

        // StartCombat itself needs Play mode (coroutines), so the Khwan step is called directly
        private static void Apply(CombatManager combat)
        {
            var method = typeof(CombatManager).GetMethod("ApplyPlayerStartingKhwan", BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null) throw new System.MissingMethodException("CombatManager.ApplyPlayerStartingKhwan");
            method.Invoke(combat, null);
        }

        private static void Finish(int failures, bool exitOnFinish)
        {
            if (exitOnFinish) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Expect(string name, bool condition)
        {
            Debug.Log($"[RunStateCombatSyncTests] {(condition ? "ok  " : "FAIL")} {name}");
            return condition ? 0 : 1;
        }
    }
}
