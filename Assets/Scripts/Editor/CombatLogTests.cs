using System;
using System.IO;
using TMPro;
using TawanOS.CardEngine;
using TawanOS.GameFlow;
using TawanOS.UI;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Tests for P1-6 Combat Log:
    /// - Deduplication of visible messages (extending display time instead of adding a new row)
    /// - Box fitting / TMP auto-sizing
    /// - Max 3 visible rows cap
    /// - Player khwan loss log lines with cause
    /// Menu: Tools/TawanOS/Tests/Combat Log
    /// </summary>
    public static class CombatLogTests
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "run_save.json");

        [MenuItem("Tools/TawanOS/Tests/Combat Log")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;
            string backupSave = File.Exists(SavePath) ? File.ReadAllText(SavePath) : null;

            GameObject testHostGo = null;
            GameObject combatGo = null;

            try
            {
                // 1. Deduplication and display time extension in NoticeHost
                var host = NoticeHost.CreateHostForTesting();
                testHostGo = host.gameObject;

                string msgA = "กุศลไม่พอ ต้องใช้ 2 แต่มี 1";
                host.Add(msgA, NoticeKind.Warning);
                failures += Expect("First message creates 1 visible toast", host.VisibleCount == 1);

                // Advance age to simulate time passing
                host.toasts[0].age = 2.0f;

                // Add same message again while visible
                host.Add(msgA, NoticeKind.Warning);
                failures += Expect("Duplicate message does not add a new row (still 1)", host.VisibleCount == 1);
                failures += Expect("Duplicate message resets age (extends display time)", host.toasts[0].age == 0f);

                // 2. Max 3 rows visible
                host.Add("ข้อความที่สอง", NoticeKind.Warning);
                host.Add("ข้อความที่สาม", NoticeKind.Warning);
                failures += Expect("3 distinct messages create 3 rows", host.VisibleCount == 3);

                host.Add("ข้อความที่สี่", NoticeKind.Warning);
                failures += Expect("4th message is capped at 3 rows max", host.VisibleCount == 3);
                failures += Expect("Oldest message was removed", host.toasts[0].message == "ข้อความที่สอง");

                // 3. Text box fitting & TMP auto-sizing
                var toastLabel = host.toasts[0].label;
                failures += Expect("Label has auto-sizing enabled", toastLabel != null && toastLabel.enableAutoSizing);
                failures += Expect("Label has Ellipsis overflow mode", toastLabel != null && toastLabel.overflowMode == TextOverflowModes.Ellipsis);

                // 4. Khwan loss causes in CombatManager
                combatGo = new GameObject("TestCombatManager");
                var combat = combatGo.AddComponent<CombatManager>();
                combat.State.playerKhwan = 20;
                combat.State.maxPlayerKhwan = 20;
                combat.State.playerShield = 0;

                string lastNoticeMsg = null;
                NoticeKind lastNoticeKind = NoticeKind.Info;
                Action<string, NoticeKind> onNotice = (m, k) =>
                {
                    lastNoticeMsg = m;
                    lastNoticeKind = k;
                };
                PlayerNotice.OnNotice += onNotice;

                // Direct damage with cause
                combat.TakeDamage(3, toPlayer: true, cause: "ถูก ผีเปรต โจมตี");
                failures += Expect("Damage log matches format '−3 ขวัญ ถูก ผีเปรต โจมตี'", lastNoticeMsg == "−3 ขวัญ ถูก ผีเปรต โจมตี");
                failures += Expect("Player khwan was reduced by 3", combat.State.playerKhwan == 17);

                // Damage absorbed by shield: only actual khwan lost is logged
                combat.AddShield(5, toPlayer: true);
                combat.TakeDamage(7, toPlayer: true, cause: "ถูก ผีกระสือ โจมตี");
                failures += Expect("Shield absorbed 5, 2 lost: '−2 ขวัญ ถูก ผีกระสือ โจมตี'", lastNoticeMsg == "−2 ขวัญ ถูก ผีกระสือ โจมตี");
                failures += Expect("Player khwan is 15", combat.State.playerKhwan == 15);

                // Full shield absorb: 0 lost, no notice
                lastNoticeMsg = null;
                combat.AddShield(10, toPlayer: true);
                combat.TakeDamage(4, toPlayer: true, cause: "ถูกโจมตี");
                failures += Expect("Full shield absorb produces no khwan loss notice", lastNoticeMsg == null);

                // Curse backfire khwan loss log
                var resolverGo = new GameObject("TestEffectResolver");
                var resolver = resolverGo.AddComponent<EffectResolver>();
                combat.State.playerKhwan = 10;
                resolver.TriggerCurseBackfire(toPlayer: true);
                failures += Expect("Curse backfire produces '−5 ขวัญ คำสาปย้อน'", lastNoticeMsg == "−5 ขวัญ คำสาปย้อน");
                failures += Expect("Player khwan dropped to 5", combat.State.playerKhwan == 5);
                UnityEngine.Object.DestroyImmediate(resolverGo);

                PlayerNotice.OnNotice -= onNotice;
            }
            finally
            {
                if (testHostGo != null) UnityEngine.Object.DestroyImmediate(testHostGo);
                if (combatGo != null) UnityEngine.Object.DestroyImmediate(combatGo);

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

            Debug.Log(failures == 0 ? "[CombatLogTests] PASS" : $"[CombatLogTests] FAIL: {failures} check(s) failed");
            if (exitOnFinish) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Expect(string label, bool ok)
        {
            Debug.Log($"[CombatLogTests] {(ok ? "ok  " : "FAIL")} {label}");
            return ok ? 0 : 1;
        }
    }
}
