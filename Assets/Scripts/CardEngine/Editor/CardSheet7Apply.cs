using System.Collections.Generic;
using System.IO;
using System.Text;
using TawanOS.GameFlow;
using UnityEditor;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Phase 1 of the sheet 7 port (Docs/Plan/HANDOFF_sheet7_port.md, decisions by Narathorn, 21 Oct 2026):
    // reads Docs/Design/card_skills_th_en.csv and brings the data in line with it.
    //  - The 42 cards (by card_id) and their _up versions: cost (Merit or Corruption), ขวัญ / สะเทือนขวัญ on
    //    familiars, Thai card text. Upgrades keep the base stats except the ones the sheet changes
    //    (s_bf02 / s_wf06 start with 2 ATK, s_wi04 costs 2). Abilities: CardSheet7Abilities.
    //  - Every amulet lasts 3 turns.
    //  - Normal enemies play 2 cards a turn and have +2 Khwan (4 -> 6, growth cap 10 -> 12); the boss plays 1 in both phases.
    // Idempotent. Batch: -executeMethod TawanOS.CardEngine.CardSheet7Apply.ApplyFromCli
    public static class CardSheet7Apply
    {
        private const string SheetPath = "Docs/Design/card_skills_th_en.csv";
        private const int AmuletTurns = 3;
        private const int NormalEnemyKhwan = 6;   // 4 + 2 (sheet 7)
        private const int NormalEnemyKhwanCap = 12; // 10 + 2, so the +2 holds on every floor

        [MenuItem("Tools/TawanOS/Cards/Apply Sheet 7")]
        public static void ApplyFromMenu() => Apply();

        public static void ApplyFromCli()
        {
            if (!Apply()) EditorApplication.Exit(1);
        }

        public static bool Apply()
        {
            if (!File.Exists(SheetPath))
            {
                Debug.LogError($"[CardSheet7Apply] {SheetPath} not found.");
                return false;
            }

            var cards = new Dictionary<string, CardDataSO>();
            foreach (var guid in AssetDatabase.FindAssets("t:CardDataSO"))
            {
                var card = AssetDatabase.LoadAssetAtPath<CardDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (card != null && !string.IsNullOrEmpty(card.cardId)) cards[card.cardId] = card;
            }

            bool ok = true;
            int updated = 0;
            foreach (var row in ReadCsv(File.ReadAllText(SheetPath, Encoding.UTF8)))
            {
                string id = row["card_id"];
                if (!cards.TryGetValue(id, out var card))
                {
                    Debug.LogError($"[CardSheet7Apply] No CardDataSO with cardId {id}");
                    ok = false;
                    continue;
                }

                int cost = Int(row["cost"]);
                bool merit = row["cost_resource"].Trim() == "merit";
                int hp = Int(row["hp"]), atk = Int(row["atk"]);

                string school = row["school_th"].Trim();
                Write(card, c => { SetStats(c, merit, cost, hp, atk, row["skill_th"]); c.school = school; });
                updated++;

                var up = card.upgradedCard != null ? card.upgradedCard : (cards.TryGetValue(id + "_up", out var u) ? u : null);
                if (up == null) continue;
                int upCost = id == "s_wi04" ? 2 : cost;
                int upAtk = id == "s_bf02" || id == "s_wf06" ? 2 : atk;
                int upHp = id == "s_bf02" ? 3 : hp; // กระสือ+ is 2/3: the redesign sheet (23 Oct) starts กระสือ at 2 ATK already
                string upText = CardSheet7UpgradeText.ById.TryGetValue(id, out var full) ? full : row["upgraded_th"];
                Write(up, c => { SetStats(c, merit, upCost, upHp, upAtk, upText); c.school = school; });
                updated++;
            }

            ok &= CardSheet7Abilities.Apply(cards); // Phase 2: what every card does
            ok &= ApplyEnemies();
            // The keyword glossary in Resources takes the sheet 7 names (บิน, ดักบิน, คุ้มกัน, สาป, ฮีล, สังเวย)
            var glossary = Resources.Load<KeywordGlossarySO>("KeywordGlossary");
            if (glossary != null) { Undo.RecordObject(glossary, "Apply sheet 7"); glossary.PopulateDefaults(); EditorUtility.SetDirty(glossary); }
            AssetDatabase.SaveAssets();
            Debug.Log(ok ? $"<color=green>[CardSheet7Apply] {updated} cards and the enemy profiles updated from sheet 7.</color>"
                         : "[CardSheet7Apply] Finished with errors (see above).");
            return ok;
        }

        private static void SetStats(CardDataSO c, bool merit, int cost, int hp, int atk, string text)
        {
            c.meritCost = merit ? cost : 0;
            c.corruptionGain = merit ? 0 : cost;
            if (c.cardType == CardType.Familiar)
            {
                c.familiarHealth = hp;
                c.familiarDamage = atk;
            }
            if (c.cardType == CardType.Amulet)
            {
                // Sheet 7 amulets have no ขวัญ or attack: they last their turns or uses, and only พิธีสะเดาะเคราะห์ destroys one
                c.durability = AmuletTurns;
                c.familiarHealth = 0;
                c.familiarDamage = 0;
            }
            c.descriptionFormat = text.Trim().Replace("ไฟต์", "การต่อสู้"); // "จนจบไฟต์" reads "จนจบการต่อสู้" (Game Designer, 22 Oct)
        }

        private static bool ApplyEnemies()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyProfileSO"))
            {
                var enemy = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (enemy == null || enemy.scriptedDeck) continue; // the tutorial enemy plays a fixed script
                Undo.RecordObject(enemy, "Apply sheet 7");
                enemy.maxCardsPerTurn = enemy.isBoss ? 1 : 2;
                if (!enemy.isBoss) enemy.maxKhwan = NormalEnemyKhwan;
                EditorUtility.SetDirty(enemy);
            }

            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDeckProgressionSO"))
            {
                var progression = AssetDatabase.LoadAssetAtPath<EnemyDeckProgressionSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (progression == null) continue;
                Undo.RecordObject(progression, "Apply sheet 7");
                progression.khwanCap = NormalEnemyKhwanCap;
                EditorUtility.SetDirty(progression);
            }
            return true;
        }

        private static void Write(CardDataSO card, System.Action<CardDataSO> apply)
        {
            Undo.RecordObject(card, "Apply sheet 7");
            apply(card);
            EditorUtility.SetDirty(card);
        }

        private static int Int(string s) => int.TryParse(s.Trim(), out int v) ? v : 0;

        // CSV with a header row; fields may be quoted and contain commas or line breaks
        private static List<Dictionary<string, string>> ReadCsv(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (quoted)
                {
                    if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else if (ch == '"') quoted = false;
                    else field.Append(ch);
                }
                else if (ch == '"') quoted = true;
                else if (ch == ',') { row.Add(field.ToString()); field.Clear(); }
                else if (ch == '\n')
                {
                    row.Add(field.ToString().TrimEnd('\r')); field.Clear();
                    rows.Add(row); row = new List<string>();
                }
                else field.Append(ch);
            }
            if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }

            var result = new List<Dictionary<string, string>>();
            if (rows.Count == 0) return result;
            var header = rows[0];
            header[0] = header[0].TrimStart('﻿');
            for (int r = 1; r < rows.Count; r++)
            {
                if (rows[r].Count < header.Count) continue;
                var dict = new Dictionary<string, string>();
                for (int c = 0; c < header.Count; c++) dict[header[c]] = rows[r][c];
                result.Add(dict);
            }
            return result;
        }
    }
}
