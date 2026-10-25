using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace TawanOS.CardEngine
{
    /// <summary>
    /// Puts a unit after every number in a card's skill text and calls attack by the name on the card face
    /// (Game Designer, 25 Oct: "ติดสาป 2" / "โดน 1" / "ฮีล 2" / "1/1" read as bare numbers). Only words change, never a
    /// number. Safe to run twice. CardSheet7Apply runs it on every text it writes, so a re-import keeps the wording.
    /// </summary>
    public static class CardTextClarity
    {
        private const string N = @"(\d+|\{0\})";

        private static readonly (Regex find, string replace)[] Rules =
        {
            (new Regex(@"เรียกหุ่นพยนต์ " + N + "/" + N), "เรียกหุ่นพยนต์ (สะเทือนขวัญ $1 / ขวัญ $2)"),
            (new Regex(@"ตีเพิ่มเท่าตีของตัวนั้น"), "สะเทือนขวัญเพิ่มเท่ากับของตัวนั้น"),
            (new Regex(@"ได้ตีและขวัญเพิ่มเท่าตัวนั้น"), "ได้สะเทือนขวัญและขวัญเพิ่มเท่ากับตัวนั้น"),
            (new Regex(@"สูงสุด " + N + " ต่อเทิร์น"), "สูงสุด $1 หน่วยต่อเทิร์น"),
            (new Regex(@"การฮีลทุกครั้งของเรา \+"), "การฮีลขวัญเราทุกครั้ง +"),
            (new Regex(@"ตีบริวารที่ติดสาป เพิ่มขึ้น " + N + " หน่วย"), "ตีบริวารที่ติดสาป แรงขึ้น $1 ดาเมจ"),
            (new Regex(@"ตี \+" + N + " ต่อมลทินที่เรามี " + N + " หน่วย"), "สะเทือนขวัญ +$1 ทุกๆ มลทิน $2 หน่วยที่เรามี"),
            (new Regex(@"ตี \+"), "สะเทือนขวัญ +"),
            (new Regex(@"ติดสาป " + N + "(?! ชั้น)"), "ติดสาป $1 ชั้น"),
            (new Regex(@"โดน " + N), "โดนดาเมจ $1"),
            (new Regex(@"ฮีลตามขวัญของบริวาร(ตัว)?นั้น"), "ฮีลขวัญเราเท่ากับขวัญของบริวารตัวนั้น"),
            (new Regex(@"ฮีลตามขวัญของบริวาร"), "ฮีลขวัญเราเท่ากับขวัญของบริวารตัวนั้น"),
            (new Regex(@"ตัวนั้นนั้น"), "ตัวนั้น"),
            (new Regex(@"ฮีล " + N), "ฮีลขวัญเรา $1"),
            (new Regex(@"เสียขวัญ " + N + "(?! หน่วย)"), "เสียขวัญ $1 หน่วย"),
            // ฮีล reads ฟื้นฟูขวัญ (Game Designer, 25 Oct)
            (new Regex(@"ฮีลขวัญ"), "ฟื้นฟูขวัญ"),
            (new Regex(@"เท่าที่ฮีล"), "เท่าที่ฟื้นฟู"),
            (new Regex(@"ฮีล"), "ฟื้นฟูขวัญ"),
        };

        public static string Clarify(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            foreach (var (find, replace) in Rules) text = find.Replace(text, replace);
            return text;
        }

        [MenuItem("Tools/TawanOS/Card Engine/Clarify Card Text")]
        public static void ClarifyAllFromMenu() => ClarifyAll();

        // Batch: -executeMethod TawanOS.CardEngine.CardTextClarity.Run
        public static void Run()
        {
            ClarifyAll();
            EditorApplication.Exit(0);
        }

        public static int ClarifyAll()
        {
            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:CardDataSO"))
            {
                var card = AssetDatabase.LoadAssetAtPath<CardDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (card == null) continue;
                string clear = Clarify(card.descriptionFormat);
                if (clear == card.descriptionFormat) continue;
                card.descriptionFormat = clear;
                EditorUtility.SetDirty(card);
                changed++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[CardTextClarity] Clarified {changed} card texts.");
            return changed;
        }
    }
}
