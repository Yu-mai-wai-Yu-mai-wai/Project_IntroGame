#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Gives each enemy a deck built around one plan, so its cards work on each other (the planner,
    // EnemyCardPlayer.Planning.cs, plays them in the order that makes them work):
    // - ผีกระสือ: heals that hurt. พระเครื่อง / ลูกประคำ as the key amulet, heals (คาถาเรียกขวัญ, ผีเจ้าปู่,
    //   คาถาดูดขวัญ) and ผีแม่ย่านาง striking on every heal.
    // - ผีพรายน้ำนอง: curses. พรายน้ำ, คาถาสะกด, คาถาสาปแช่ง and ตุ๊กตาคุณไสย put them on, บทสวดส่งวิญญาณ cashes
    //   them in, ผีเจ้าที่ holds the line meanwhile. คาถาถอนคุณไสย in place of ผีตายโหง (22 Oct: Win Rate Sim Try Decks,
    //   strength 60% against 66%).
    // - ผีตายโหง (both phases): curses and deaths. คาถาคุณไสย curses the player's familiars for ผีตายโหง to hit harder
    //   and บทสวดส่งวิญญาณ to destroy; bodies (ผีหัวขาด, กุมารทอง) die for ตะกรุดดำ and ผีปอบ, เปรต grows on their
    //   deaths, ผ้ายันต์มหาอุด keeps them up; ผีบ้านผีเรือน and ผีเจ้าที่ hold the line, ควายธนู and บทแผ่เมตตา count them.
    // Merit is what holds an enemy back: Black Magic costs none, only Corruption, and the backfire (5 Khwan, never
    // below 1) is cheap for anyone with Khwan to spare. So minor enemies (6 Khwan) are mostly White Magic, and the boss
    // is half White (22 Oct: with no cap on plays, an almost all-Black boss filled the board every turn; Win Rate Sim).
    // Like the player, an enemy has no cap on the cards it plays a turn (22 Oct): only Merit, Corruption and what is
    // worth playing hold it back. Khwan and hand sizes stay as they are. Re-running sets these decks, the cap and the slip
    // chances again, over any change made in the Inspector.
    public static class EnemyDeckSetupTool
    {
        private const string EnemyFolder = "Assets/CardEngineData/Enemies/";
        private const string CardFolder = "Assets/CardEngineData/Cards/Sheet";
        // A turn can never use more: the hand starts at 3 and Merit caps at 6
        private const int NoPlayCap = 6;

        private static readonly (string profile, (string id, int count)[] deck)[] Decks =
        {
            ("NewEnemyProfile", new[] // ผีกระสือ, 12
            {
                ("s_wa03", 1), ("s_wa04", 1), ("s_wi02", 2), ("s_wf02", 2), ("s_wf07", 2),
                ("s_wf05", 1), ("s_bf02", 2), ("s_bi03", 1),
            }),
            ("PraiGhostProfile", new[] // ผีพรายน้ำนอง, 14
            {
                ("s_bf07", 3), ("s_bi05", 2), ("s_bi01", 1), ("s_ba01", 1), ("s_wi07", 2),
                ("s_wi05", 1), ("s_wf04", 2), ("s_wf05", 1), ("s_wa01", 1),
            }),
            ("PhiTaiHongBossProfile", BossDeck),
            ("PhiTaiHongBossPhase2Profile", BossDeck),
        };

        // How often a play is a slip (EnemyProfileSO.mistakeChance): minor enemies slip more than the boss (22 Oct)
        private static readonly Dictionary<string, float> MistakeChance = new Dictionary<string, float>
        {
            ["NewEnemyProfile"] = 0.3f,
            ["PraiGhostProfile"] = 0.3f,
            ["PhiTaiHongBossProfile"] = 0.15f,
            ["PhiTaiHongBossPhase2Profile"] = 0.15f,
        };

        // How often a pit pick is a slip (EnemyProfileSO.pitMistakeChance). 22 Oct, Win Rate Sim (Enemy Mistakes, Grown
        // Deck): the player beats the boss 3% of fights with no slips, 43-48% with 75%; floor 6 minor enemies 65-75%
        // with none, 83-86% with 25% (75% made them 97-99%)
        private static readonly Dictionary<string, float> PitMistakeChance = new Dictionary<string, float>
        {
            ["NewEnemyProfile"] = 0.25f,
            ["PraiGhostProfile"] = 0.25f,
            ["PhiTaiHongBossProfile"] = 0.75f,
            ["PhiTaiHongBossPhase2Profile"] = 0.75f,
        };

        // ผีตายโหง, 16
        private static (string, int)[] BossDeck => new[]
        {
            // Black Magic, 8
            ("s_bf01", 2), ("s_bi02", 2), ("s_ba03", 1), ("s_bf03", 1), ("s_bf05", 1), ("s_bf06", 1),
            // White Magic, 8
            ("s_wi07", 2), ("s_wf05", 1), ("s_wf01", 1), ("s_wf03", 1), ("s_wf04", 1), ("s_wi01", 1), ("s_wa07", 1),
        };

        [MenuItem("Tools/TawanOS/Card Engine/Setup Enemy Decks")]
        private static void Setup_Menu()
        {
            Setup();
        }

        // Batch: -executeMethod TawanOS.CardEngine.EnemyDeckSetupTool.Setup
        public static void Setup()
        {
            var cards = AssetDatabase.FindAssets("t:CardDataSO", new[] { CardFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<CardDataSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null && !string.IsNullOrEmpty(c.cardId))
                .GroupBy(c => c.cardId).ToDictionary(g => g.Key, g => g.First());

            var missing = new List<string>();
            foreach (var (profileName, deck) in Decks)
            {
                string path = EnemyFolder + profileName + ".asset";
                var profile = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(path);
                if (profile == null) { missing.Add(path); continue; }

                var list = new List<CardDataSO>();
                foreach (var (id, count) in deck)
                {
                    if (!cards.TryGetValue(id, out var card)) { missing.Add(id); continue; }
                    for (int i = 0; i < count; i++) list.Add(card);
                }
                profile.deck = list;
                profile.maxCardsPerTurn = NoPlayCap;
                profile.mistakeChance = MistakeChance.TryGetValue(profileName, out var chance) ? chance : 0f;
                profile.pitMistakeChance = PitMistakeChance.TryGetValue(profileName, out var pitChance) ? pitChance : 0f;
                EditorUtility.SetDirty(profile);
                Debug.Log($"[EnemyDeckSetupTool] {profile.enemyName}: {list.Count} cards, up to {NoPlayCap} a turn, "
                    + $"slips {profile.mistakeChance:0%}, pit slips {profile.pitMistakeChance:0%}, "
                    + string.Join(", ", deck.Select(d => $"{d.count}x {(cards.TryGetValue(d.id, out var c) ? c.cardNameThai : d.id)}")));
            }
            AssetDatabase.SaveAssets();

            if (missing.Count > 0)
            {
                Debug.LogError($"[EnemyDeckSetupTool] Not found: {string.Join(", ", missing)}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Debug.Log($"<color=green>[EnemyDeckSetupTool] {Decks.Length} enemy decks set.</color>");
        }
    }
}
#endif
