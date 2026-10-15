using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TawanOS.CardEngine
{
    [Serializable]
    public struct KeywordEntry
    {
        public string term;
        [TextArea(2, 5)] public string definition;

        public KeywordEntry(string term, string definition)
        {
            this.term = term;
            this.definition = definition;
        }
    }

    /// <summary>
    /// Master glossary of card combat keywords and their Thai definitions (Plan task P1-1).
    /// Stored at Assets/Resources/KeywordGlossary.asset.
    /// </summary>
    [CreateAssetMenu(fileName = "KeywordGlossary", menuName = "TawanOS/Cards/Keyword Glossary")]
    public class KeywordGlossarySO : ScriptableObject
    {
        public const string ResourceName = "KeywordGlossary";

        public List<KeywordEntry> entries = new List<KeywordEntry>();

        private static KeywordGlossarySO cached;

        public static KeywordGlossarySO Current
        {
            get
            {
                if (cached == null) cached = Resources.Load<KeywordGlossarySO>(ResourceName);
                if (cached == null)
                {
                    cached = CreateInstance<KeywordGlossarySO>();
                    cached.PopulateDefaults();
                    cached.hideFlags = HideFlags.HideAndDontSave;
                }
                return cached;
            }
        }

        public static void ResetCache()
        {
            cached = null;
        }

        public void PopulateDefaults()
        {
            entries.Clear();
            entries.Add(new KeywordEntry("ผวา", "สะเทือนขวัญลดลง 1 หน่วยต่อ stack (ต่ำสุด 0) เป็นเวลา X เทิร์น"));
            entries.Add(new KeywordEntry("ผีบังตา", "ความสามารถของการ์ดใช้ไม่ได้ (ทริกเกอร์ คำสำคัญ ออร่า) แต่ยังโจมตีได้ตามปกติ"));
            entries.Add(new KeywordEntry("เกราะคุ้มภัย", "กันความเสียหายทั้งก้อนได้ 1 ครั้งต่อ stack รวมถึงการระเบิด"));
            entries.Add(new KeywordEntry("ชำระล้าง", "เมื่อได้รับมลทิน จะป้องกันมลทินนั้น 1 หน่วยต่อ 1 stack"));
            entries.Add(new KeywordEntry("ยั่วยุ", "[ชั่วคราว] ศัตรูต้องโจมตีการ์ดใบนี้ก่อน"));
            entries.Add(new KeywordEntry("ผีข้ามหัว", "[ชั่วคราว] โจมตีข้ามการ์ดบนกระดานเข้าใส่ผู้เล่นหรือศัตรูโดยตรง (ยกเว้นมีตัวดักจับ)"));
            entries.Add(new KeywordEntry("อัปมงคล", "[ชั่วคราว] สถานะทางลบของการ์ด (ผวา, ผีบังตา)"));
            entries.Add(new KeywordEntry("สิริมงคล", "[ชั่วคราว] สถานะทางบวกของการ์ด (เกราะคุ้มภัย, ยั่วยุ)"));
            entries.Add(new KeywordEntry("กุศล", "[ชั่วคราว] ทรัพยากรสำหรับใช้ร่ายการ์ดสายขาว"));
            entries.Add(new KeywordEntry("มลทิน", "[ชั่วคราว] ค่าสะสมจากการใช้การ์ดสายดำ หากสะสมจนเต็มเพดานจะเกิดคำสาปย้อน"));
            entries.Add(new KeywordEntry("คำสาปย้อน", "[ชั่วคราว] เมื่อมลทินถึงขีดจำกัด จะสูญเสียขวัญ 5 หน่วยทันที (ไม่ทะลุต่ำกว่า 1 เว้นแต่เหลือ 1 อยู่แล้ว)"));
        }

        public bool TryGetDefinition(string term, out string def)
        {
            foreach (var e in entries)
            {
                if (e.term == term)
                {
                    def = e.definition;
                    return true;
                }
            }
            def = null;
            return false;
        }

        public string GetDefinition(string term)
        {
            return TryGetDefinition(term, out var def) ? def : $"[ชั่วคราว] คำสำคัญ: {term}";
        }

        // Ordered list of keywords from longest to shortest to avoid partial matches
        private static readonly string[] KnownTerms =
        {
            "เกราะคุ้มภัย",
            "คำสาปย้อน",
            "ผีข้ามหัว",
            "สิริมงคล",
            "ชำระล้าง",
            "ผีบังตา",
            "อัปมงคล",
            "ยั่วยุ",
            "กุศล",
            "มลทิน",
            "ผวา",
        };

        private static Regex wrapRegex;

        /// <summary>
        /// Wraps known keywords in TMP link and color formatting: <link="kw:term"><u><color=#hex>term</color></u></link>.
        /// Existing tags (&lt;...&gt;) are preserved without corruption.
        /// </summary>
        public static string WrapKeywords(string text, string colorHex = null)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (string.IsNullOrEmpty(colorHex))
            {
                var theme = UI.UIThemeSO.Current;
                colorHex = theme != null ? ColorUtility.ToHtmlStringRGB(theme.accent) : "F0AD7B";
            }

            if (wrapRegex == null)
            {
                string alternation = string.Join("|", KnownTerms.Select(Regex.Escape));
                // Group 1: any XML/TMP tag (<...>) - preserved unchanged
                // Group 2: any known keyword outside tags
                wrapRegex = new Regex($"(<[^>]+>)|({alternation})", RegexOptions.Compiled);
            }

            return wrapRegex.Replace(text, match =>
            {
                if (match.Groups[1].Success) return match.Value;
                string term = match.Groups[2].Value;
                return $"<link=\"kw:{term}\"><u><color=#{colorHex}>{term}</color></u></link>";
            });
        }

        /// <summary>
        /// Finds which known keywords are mentioned on the card (description text or abilities).
        /// </summary>
        public static List<string> GetKeywordsOnCard(CardInstance card)
        {
            var found = new List<string>();
            if (card == null) return found;

            string desc = card.descriptionFormat ?? "";
            foreach (var term in KnownTerms)
            {
                if (desc.Contains(term) && !found.Contains(term))
                    found.Add(term);
            }

            if (card.abilities != null)
            {
                foreach (var a in card.abilities)
                {
                    if (a.effect == AbilityEffect.ApplyStatus)
                    {
                        string statusName = a.status switch
                        {
                            CardStatusType.Phawa => "ผวา",
                            CardStatusType.Blinded => "ผีบังตา",
                            CardStatusType.Protect => "เกราะคุ้มภัย",
                            CardStatusType.Taunt => "ยั่วยุ",
                            _ => null
                        };
                        if (statusName != null && !found.Contains(statusName))
                            found.Add(statusName);
                    }
                    if (a.effect == AbilityEffect.Taunt && !found.Contains("ยั่วยุ")) found.Add("ยั่วยุ");
                    if (a.effect == AbilityEffect.Overhead && !found.Contains("ผีข้ามหัว")) found.Add("ผีข้ามหัว");
                    if (a.effect == AbilityEffect.CleanseAll && !found.Contains("ชำระล้าง")) found.Add("ชำระล้าง");
                    if (a.effect == AbilityEffect.FlipOmens)
                    {
                        if (!found.Contains("อัปมงคล")) found.Add("อัปมงคล");
                        if (!found.Contains("สิริมงคล")) found.Add("สิริมงคล");
                    }
                }
            }

            if (card.armor > 0 && !found.Contains("เกราะคุ้มภัย")) found.Add("เกราะคุ้มภัย");

            return found;
        }
    }
}
