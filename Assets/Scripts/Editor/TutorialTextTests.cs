using TawanOS.UI;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Checks plan task P0-13: Tutorial hints text.
    /// 1. Board hint clarifies cancel vs already placed card cannot be retrieved.
    /// 2. Typo "แต่ะเทิร์น" is fixed to "แต่ละเทิร์น".
    /// Menu: Tools/TawanOS/Tests/Tutorial Text. Batch: -executeMethod TawanOS.EditorTools.TutorialTextTests.Run
    /// </summary>
    public static class TutorialTextTests
    {
        [MenuItem("Tools/TawanOS/Tests/Tutorial Text")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;
            TutorialTextSO.ResetCache();
            var words = TutorialTextSO.Current;

            failures += Expect("TutorialTextSO.Current is not null", words != null);
            if (words != null)
            {
                failures += Expect("Board hint contains updated text",
                    words.board.body.Contains("คลิกขวาหรือกด Esc เพื่อยกเลิกการ์ดที่เลือก (ก่อนวางลงสนาม) การ์ดที่วางแล้วเอาคืนไม่ได้"));
                failures += Expect("Board hint no longer contains old text",
                    !words.board.body.Contains("เอาการ์ดกลับเข้ามือ"));

                failures += Expect("Hand hint contains 'แต่ละเทิร์น'",
                    words.hand.body.Contains("แต่ละเทิร์น"));
                failures += Expect("Hand hint does not contain typo 'แต่ะเทิร์น'",
                    !words.hand.body.Contains("แต่ะเทิร์น"));
            }

            var defaults = ScriptableObject.CreateInstance<TutorialTextSO>();
            failures += Expect("Defaults board hint contains updated text",
                defaults.board.body.Contains("คลิกขวาหรือกด Esc เพื่อยกเลิกการ์ดที่เลือก (ก่อนวางลงสนาม) การ์ดที่วางแล้วเอาคืนไม่ได้"));
            failures += Expect("Defaults board hint no longer contains old text",
                !defaults.board.body.Contains("เอาการ์ดกลับเข้ามือ"));
            failures += Expect("Defaults do not contain typo 'แต่ะเทิร์น'",
                !defaults.hand.body.Contains("แต่ะเทิร์น"));
            Object.DestroyImmediate(defaults);

            if (failures == 0)
                Debug.Log("[TutorialTextTests] PASS (all checks passed)");
            else
                Debug.LogError($"[TutorialTextTests] FAIL ({failures} checks failed)");

            if (exitOnFinish && Application.isBatchMode)
                EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Expect(string label, bool condition)
        {
            if (condition)
            {
                Debug.Log($"[TutorialTextTests] ok   {label}");
                return 0;
            }
            Debug.LogError($"[TutorialTextTests] FAIL {label}");
            return 1;
        }
    }
}
