using TawanOS.CardEngine;
using TawanOS.UI;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Checks plan task P1-2: Card book page layout.
    /// Right page shows:
    /// Line 1 = card name with cost beside it ("กุศล 2" or "มลทิน 2")
    /// Line 2 = "ขวัญ 2/2" and "สะเทือนขวัญ 2" on same line (only for cards that have them; hidden for spells)
    /// Then type line
    /// Then abilities
    /// Menu: Tools/TawanOS/Tests/Card Book Layout.
    /// </summary>
    public static class CardBookLayoutTests
    {
        [MenuItem("Tools/TawanOS/Tests/Card Book Layout")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;

            // 1. Familiar test card
            var famData = ScriptableObject.CreateInstance<CardDataSO>();
            famData.cardNameThai = "กุมารทอง";
            famData.magicSchool = MagicSchool.WhiteMagic;
            famData.cardType = CardType.Familiar;
            famData.meritCost = 2;
            famData.familiarDamage = 2;
            famData.familiarHealth = 2;
            var fam = new CardInstance(famData);

            string famHeader = CardDetailPanelUI.InfoHeader(fam, Color.white, "9A9090");
            string[] famLines = famHeader.Split('\n');
            failures += Expect("Familiar header has 3 lines", famLines.Length == 3);
            if (famLines.Length >= 3)
            {
                failures += Expect("Line 1 has card name 'กุมารทอง'", famLines[0].Contains("กุมารทอง"));
                failures += Expect("Line 1 has cost 'กุศล 2'", famLines[0].Contains("กุศล 2"));
                failures += Expect("Line 2 has 'ขวัญ 2/2'", famLines[1].Contains("ขวัญ") && famLines[1].Contains("2/2"));
                failures += Expect("Line 2 has 'สะเทือนขวัญ 2'", famLines[1].Contains("สะเทือนขวัญ") && famLines[1].Contains("2"));
                failures += Expect("Line 3 has type line", famLines[2].Length > 0 && !famLines[2].Contains("กุศล"));
            }

            // 2. Spell / Incantation test card
            var spellData = ScriptableObject.CreateInstance<CardDataSO>();
            spellData.cardNameThai = "สะกดวิญญาณ";
            spellData.magicSchool = MagicSchool.BlackMagic;
            spellData.cardType = CardType.Incantation;
            spellData.corruptionGain = 2;
            var spell = new CardInstance(spellData);

            string spellHeader = CardDetailPanelUI.InfoHeader(spell, Color.white, "9A9090");
            string[] spellLines = spellHeader.Split('\n');
            failures += Expect("Spell header has 2 lines (stats hidden)", spellLines.Length == 2);
            if (spellLines.Length >= 2)
            {
                failures += Expect("Spell Line 1 has card name 'สะกดวิญญาณ'", spellLines[0].Contains("สะกดวิญญาณ"));
                failures += Expect("Spell Line 1 has cost 'มลทิน 2'", spellLines[0].Contains("มลทิน 2"));
                failures += Expect("Spell header does NOT contain 'ขวัญ'", !spellHeader.Contains("ขวัญ"));
                failures += Expect("Spell header does NOT contain 'สะเทือนขวัญ'", !spellHeader.Contains("สะเทือนขวัญ"));
                failures += Expect("Spell Line 2 has type line", spellLines[1].Length > 0 && !spellLines[1].Contains("มลทิน"));
            }

            // 3. InfoBody test
            string famBody = CardDetailPanelUI.InfoBody(fam, Color.white, "9A9090", "C8C0C0", "80D090", "E07070");
            failures += Expect("InfoBody starts with 'ความสามารถ'", famBody.StartsWith("<size=") && famBody.Contains("ความสามารถ"));
            failures += Expect("InfoBody does not have duplicate cost", !famBody.Contains("กุศล 2"));

            Object.DestroyImmediate(famData);
            Object.DestroyImmediate(spellData);

            if (failures == 0)
                Debug.Log("[CardBookLayoutTests] PASS (all checks passed)");
            else
                Debug.LogError($"[CardBookLayoutTests] FAIL ({failures} checks failed)");

            if (exitOnFinish && Application.isBatchMode)
                EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Expect(string label, bool condition)
        {
            if (condition)
            {
                Debug.Log($"[CardBookLayoutTests] ok   {label}");
                return 0;
            }
            Debug.LogError($"[CardBookLayoutTests] FAIL {label}");
            return 1;
        }
    }
}
