using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TawanOS.CardEngine;
using TawanOS.UI;
using UnityEditor;
using UnityEngine;

namespace TawanOS.Tests
{
    public static class KeywordGlossaryTests
    {
        private const string AssetPath = "Assets/Resources/KeywordGlossary.asset";

        [MenuItem("Tools/TawanOS/Tests/Keyword Glossary Tests")]
        public static void RunFromMenu()
        {
            EnsureAssetExists();
            int passed = 0;
            int failed = 0;

            void Check(string name, bool condition)
            {
                if (condition)
                {
                    passed++;
                    Debug.Log($"<color=green>[PASS]</color> {name}");
                }
                else
                {
                    failed++;
                    Debug.LogError($"<color=red>[FAIL]</color> {name}");
                }
            }

            var glossary = Resources.Load<KeywordGlossarySO>("KeywordGlossary");
            Check("KeywordGlossary asset loads from Resources", glossary != null);

            string[] requiredTerms =
            {
                "ผวา", "ผีบังตา", "เกราะคุ้มภัย", "ชำระล้าง", "คุ้มกัน",
                "บิน", "ดักบิน", "สาป", "อัปมงคล", "สิริมงคล", "กุศล", "มลทิน", "คำสาปย้อน"
            };

            foreach (var term in requiredTerms)
            {
                bool has = glossary != null && glossary.TryGetDefinition(term, out string def) && !string.IsNullOrEmpty(def);
                Check($"Glossary contains term '{term}'", has);
            }

            // Test WrapKeywords
            string sample = "เมื่อโจมตี ติดสถานะ ผวา 2 stack และเพิ่ม เกราะคุ้มภัย ให้ตนเอง";
            string wrapped = KeywordGlossarySO.WrapKeywords(sample, "F2C14E");
            Check("WrapKeywords wraps 'ผวา' with link tag", wrapped.Contains("<link=\"kw:ผวา\"><u><color=#F2C14E>ผวา</color></u></link>"));
            Check("WrapKeywords wraps 'เกราะคุ้มภัย' with link tag", wrapped.Contains("<link=\"kw:เกราะคุ้มภัย\"><u><color=#F2C14E>เกราะคุ้มภัย</color></u></link>"));

            // Test tag preservation in WrapKeywords
            string withTags = "<color=red>ผวา</color>";
            string wrappedWithTags = KeywordGlossarySO.WrapKeywords(withTags, "F2C14E");
            Check("WrapKeywords preserves existing tags without corrupting them", wrappedWithTags.Contains("<color=red>") && wrappedWithTags.Contains("</color>"));

            // Test GetKeywordsOnCard
            var card = new CardInstance
            {
                cardId = "test_card",
                cardNameThai = "การ์ดทดสอบ",
                cardType = CardType.Familiar,
                magicSchool = MagicSchool.WhiteMagic,
                armor = 2,
                descriptionFormat = "มอบ คุ้มกัน และ ชำระล้าง 1 ครั้ง"
            };
            var foundKeywords = KeywordGlossarySO.GetKeywordsOnCard(card);
            Check("GetKeywordsOnCard finds 'เกราะคุ้มภัย' from armor", foundKeywords.Contains("เกราะคุ้มภัย"));
            Check("GetKeywordsOnCard finds 'คุ้มกัน' from description", foundKeywords.Contains("คุ้มกัน"));
            Check("GetKeywordsOnCard finds 'ชำระล้าง' from description", foundKeywords.Contains("ชำระล้าง"));

            // Test InfoBody includes คำสำคัญ block
            string body = CardDetailPanelUI.InfoBody(card, Color.white, "888888", "CCCCCC", "00FF00", "FF0000");
            Check("CardDetailPanelUI.InfoBody includes 'คำสำคัญ' header", body.Contains("คำสำคัญ"));
            Check("CardDetailPanelUI.InfoBody lists keyword and definition", body.Contains("คุ้มกัน") && body.Contains("ชำระล้าง"));

            // Test CardBookPanel spread structure
            var bookGo = new GameObject("TestBookPanel");
            var book = bookGo.AddComponent<CardBookPanel>();
            var prepareMethod = typeof(CardBookPanel).GetMethod("Prepare", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (prepareMethod != null)
            {
                bool ok = (bool)prepareMethod.Invoke(book, new object[] { null });
                Check("CardBookPanel.Prepare succeeds", ok);
                var spreadsField = typeof(CardBookPanel).GetField("spreads", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (spreadsField != null)
                {
                    var spreadsList = spreadsField.GetValue(book) as System.Collections.IList;
                    bool hasGlossarySpread = false;
                    if (spreadsList != null)
                    {
                        foreach (var s in spreadsList)
                        {
                            var secField = s.GetType().GetField("section");
                            if (secField != null && (int)secField.GetValue(s) == -2)
                            {
                                hasGlossarySpread = true;
                                break;
                            }
                        }
                    }
                    Check("CardBookPanel contains glossary spread (section -2)", hasGlossarySpread);
                }
            }
            Object.DestroyImmediate(bookGo);

            Debug.Log($"<b>[KeywordGlossaryTests Summary]</b> Passed: {passed}, Failed: {failed}");
        }

        [MenuItem("Tools/TawanOS/Keywords/Ensure Glossary Asset")]
        public static void EnsureAssetExists()
        {
            if (!Directory.Exists("Assets/Resources"))
            {
                Directory.CreateDirectory("Assets/Resources");
            }

            var asset = AssetDatabase.LoadAssetAtPath<KeywordGlossarySO>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<KeywordGlossarySO>();
                asset.PopulateDefaults();
                AssetDatabase.CreateAsset(asset, AssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"<color=green>[KeywordGlossary] Created {AssetPath}</color>");
            }
            else
            {
                // Ensure all default entries exist
                if (asset.entries == null || asset.entries.Count == 0)
                {
                    asset.PopulateDefaults();
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssets();
                }
            }
        }
    }
}
