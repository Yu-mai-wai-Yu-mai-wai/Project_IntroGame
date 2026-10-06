using UnityEditor;
using UnityEngine;
using TawanOS.CardEngine;
using TawanOS.UI;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Checks plan task H1 / G1 without Play mode: the reason texts and phase rules, the duplicate-notice rule,
    /// the theme's contrast and that Thai fonts are assigned. Menu: Tools/TawanOS/Tests/Player Feedback.
    /// Batch: -executeMethod TawanOS.EditorTools.PlayerFeedbackTests.Run (exit code 1 on failure).
    /// </summary>
    public static class PlayerFeedbackTests
    {
        [MenuItem("Tools/TawanOS/Tests/Player Feedback")]
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

            // --- which cards each player phase allows, and the reason otherwise
            failures += Expect("PlayerBoard allows a familiar", PlayBlockReasons.ForPhase(TurnPhase.PlayerBoard, CardType.Familiar) == null);
            failures += Expect("PlayerBoard allows an amulet", PlayBlockReasons.ForPhase(TurnPhase.PlayerBoard, CardType.Amulet) == null);
            failures += Expect("PlayerBoard blocks an incantation with a reason", !string.IsNullOrEmpty(PlayBlockReasons.ForPhase(TurnPhase.PlayerBoard, CardType.Incantation)));
            failures += Expect("PlayerSpell allows an incantation", PlayBlockReasons.ForPhase(TurnPhase.PlayerSpell, CardType.Incantation) == null);
            failures += Expect("PlayerSpell blocks a familiar with a reason", !string.IsNullOrEmpty(PlayBlockReasons.ForPhase(TurnPhase.PlayerSpell, CardType.Familiar)));
            foreach (var enemyPhase in new[] { TurnPhase.EnemyBoard, TurnPhase.EnemySpell })
            {
                string reason = PlayBlockReasons.ForPhase(enemyPhase, CardType.Familiar);
                failures += Expect($"{enemyPhase}: reason says it is the enemy's turn", reason != null && reason.Contains("ตาของศัตรู"));
            }
            failures += Expect("Clash blocks play with a reason", !string.IsNullOrEmpty(PlayBlockReasons.ForPhase(TurnPhase.Clash, CardType.Incantation)));
            failures += Expect("Merit reason shows cost and amount", PlayBlockReasons.NotEnoughMerit(3, 1) == "กุศลไม่พอ ต้องใช้ 3 แต่มี 1");
            failures += Expect("Owner label: player phases", PlayBlockReasons.OwnerLabel(TurnPhase.PlayerBoard) == "ตาของคุณ" && PlayBlockReasons.OwnerLabel(TurnPhase.PlayerSpell) == "ตาของคุณ");
            failures += Expect("Owner label: enemy phases", PlayBlockReasons.OwnerLabel(TurnPhase.EnemyBoard) == "ตาของศัตรู" && PlayBlockReasons.OwnerLabel(TurnPhase.EnemySpell) == "ตาของศัตรู");

            // --- duplicate notices
            PlayerNotice.ResetForTests();
            failures += Expect("First notice is shown", PlayerNotice.ShouldShow("ข้อความ", 10f));
            failures += Expect("Same notice within the window is dropped", !PlayerNotice.ShouldShow("ข้อความ", 10.5f));
            failures += Expect("A different notice is shown at once", PlayerNotice.ShouldShow("อีกข้อความ", 10.6f));
            failures += Expect("The same notice after the window is shown again", PlayerNotice.ShouldShow("อีกข้อความ", 10.6f + PlayerNotice.DuplicateWindowSeconds + 0.1f));
            failures += Expect("An empty notice is ignored", !PlayerNotice.ShouldShow("", 99f));
            PlayerNotice.ResetForTests();

            // --- theme: Thai fonts and WCAG contrast of every text/background pair it declares
            UIThemeSO.ResetCache();
            var theme = UIThemeSO.Current;
            failures += Expect("Theme asset has a body font (Thai)", theme.bodyFont != null);
            failures += Expect("Theme asset has a title font (Thai)", theme.titleFont != null);
            failures += Expect("No text size below 20", theme.labelSize >= 20f && theme.bodySize >= 20f && theme.titleSize >= 20f && theme.numberSize >= 20f);

            var textColors = new[] { ("text", theme.text), ("accent", theme.accent) };
            var backgrounds = new[] { ("black", theme.black), ("panel", theme.panel), ("crimson", theme.crimson) };
            foreach (var (tn, tc) in textColors)
            {
                foreach (var (bn, bc) in backgrounds)
                {
                    float ratio = UIThemeSO.Contrast(tc, bc);
                    failures += Expect($"Contrast {tn} on {bn} >= 4.5 ({ratio:F2})", ratio >= 4.5f);
                }
            }

            // Documented limit: crimson must not be used as text on dark backgrounds
            failures += Expect("Crimson on black stays below 4.5 (so it is a fill color only)", UIThemeSO.Contrast(theme.crimson, theme.black) < 4.5f);

            Debug.Log(failures == 0 ? "[PlayerFeedbackTests] PASS" : $"[PlayerFeedbackTests] FAIL: {failures} check(s) failed");
            if (exitOnFinish) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Expect(string name, bool condition)
        {
            Debug.Log($"[PlayerFeedbackTests] {(condition ? "ok  " : "FAIL")} {name}");
            return condition ? 0 : 1;
        }
    }
}
