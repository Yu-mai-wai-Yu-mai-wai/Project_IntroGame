#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Gives the boss (ผีตายโหง) a second phase: Enemies/PhiTaiHongBossPhase2Profile.asset, a fiercer form it comes
    // back as when its khwan first runs out (CombatManager.TryAdvanceEnemyPhase), linked from the boss profile's
    // phase2Profile. Only a missing phase-2 asset is created, so values tuned in the Inspector are kept; the link
    // is set either way.
    public static class BossPhaseSetupTool
    {
        private const string BossPath = "Assets/CardEngineData/Enemies/PhiTaiHongBossProfile.asset";
        private const string Phase2Path = "Assets/CardEngineData/Enemies/PhiTaiHongBossPhase2Profile.asset";

        [MenuItem("Tools/TawanOS/Card Engine/Setup Boss Phase 2")]
        private static void Setup_Menu()
        {
            Setup();
        }

        // Batch: -executeMethod TawanOS.CardEngine.BossPhaseSetupTool.Setup
        public static void Setup()
        {
            var boss = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(BossPath);
            if (boss == null)
            {
                Debug.LogError($"[BossPhaseSetupTool] {BossPath} not found.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var phase2 = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(Phase2Path);
            if (phase2 == null)
            {
                phase2 = ScriptableObject.CreateInstance<EnemyProfileSO>();
                phase2.enemyId = "enemy_boss_phase2";
                phase2.enemyName = "ผีตายโหง (ร่างคลั่ง)";
                phase2.portrait = boss.portrait;
                // Same khwan as the first form: the boss has to be beaten twice
                phase2.maxKhwan = boss.maxKhwan;
                phase2.deck = new List<CardDataSO>(boss.deck);
                phase2.startingHandSize = boss.startingHandSize;
                phase2.drawPerTurn = boss.drawPerTurn;
                phase2.maxHandSize = boss.maxHandSize;
                phase2.maxCardsPerTurn = boss.maxCardsPerTurn;
                phase2.isBoss = true;
                phase2.phase2Profile = null;
                // Harder hits, and the heavy blow comes as often as the normal one
                phase2.moves = new List<EnemyMove>
                {
                    new EnemyMove { intent = EnemyIntent.Attack, baseValue = 10, weight = 2, moveDescription = "กรีดร้องอาฆาต (โจมตี 10)" },
                    new EnemyMove { intent = EnemyIntent.HeavyAttack, baseValue = 18, weight = 2, moveDescription = "ฉีกร่างสังหาร (โจมตีหนัก 18)" },
                    new EnemyMove { intent = EnemyIntent.DebuffCurse, baseValue = 0, statusEffect = FirstDebuff(boss), weight = 1, moveDescription = "เลือดอาบพื้น (โดนของ)" },
                };
                AssetDatabase.CreateAsset(phase2, Phase2Path);
                Debug.Log($"<color=green>[BossPhaseSetupTool] {Phase2Path} created.</color>");
            }

            if (boss.phase2Profile != phase2)
            {
                boss.phase2Profile = phase2;
                EditorUtility.SetDirty(boss);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[BossPhaseSetupTool] {boss.enemyName} -> phase 2: {phase2.enemyName} (khwan {phase2.maxKhwan}).");
        }

        private const string NormalEnemyPath = "Assets/CardEngineData/Enemies/PraiGhostProfile.asset";

        [MenuItem("Tools/TawanOS/Card Engine/Give Boss A Card Deck")]
        private static void GiveCardDeck_Menu()
        {
            GiveCardDeck();
        }

        // Batch: -executeMethod TawanOS.CardEngine.BossPhaseSetupTool.GiveCardDeck
        // Both boss phases play cards like a normal enemy (ผีพรายน้ำนอง): the same deck, hand size and draws.
        // The second phase may play one more card a turn. Their moves stay as they are but are unused while a
        // deck is set.
        public static void GiveCardDeck()
        {
            var normal = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(NormalEnemyPath);
            var boss = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(BossPath);
            var phase2 = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(Phase2Path);
            if (normal == null || normal.deck == null || normal.deck.Count == 0 || boss == null || phase2 == null)
            {
                Debug.LogError("[BossPhaseSetupTool] Needs PraiGhostProfile with a deck, the boss profile and its phase 2 (run Setup Boss Phase 2 first).");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            CopyCardPlay(normal, boss, 0);
            CopyCardPlay(normal, phase2, 1);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[BossPhaseSetupTool] Boss phases play {normal.deck.Count} cards like {normal.enemyName}; " +
                      $"cards a turn: phase 1 {boss.maxCardsPerTurn}, phase 2 {phase2.maxCardsPerTurn}.</color>");
        }

        private const string EnemyFolder = "Assets/CardEngineData/Enemies";

        [MenuItem("Tools/TawanOS/Card Engine/Give Every Enemy A Card Deck")]
        private static void GiveEveryEnemyACardDeck_Menu()
        {
            GiveEveryEnemyACardDeck();
        }

        // Batch: -executeMethod TawanOS.CardEngine.BossPhaseSetupTool.GiveEveryEnemyACardDeck
        // Every enemy plays cards: one without a deck of its own (it only had moves) gets ผีพรายน้ำนอง's deck, hand
        // size, draws and cards a turn. Enemies that already have a deck, and scripted ones (the tutorial), are left as
        // they are.
        public static void GiveEveryEnemyACardDeck()
        {
            var normal = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(NormalEnemyPath);
            if (normal == null || normal.deck == null || normal.deck.Count == 0)
            {
                Debug.LogError($"[BossPhaseSetupTool] {NormalEnemyPath} with a deck is needed.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var given = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:EnemyProfileSO", new[] { EnemyFolder }))
            {
                var enemy = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (enemy == null || enemy == normal || enemy.scriptedDeck) continue;
                if (enemy.deck != null && enemy.deck.Count > 0) continue;
                CopyCardPlay(normal, enemy, 0);
                given.Add(enemy.enemyName);
            }
            AssetDatabase.SaveAssets();
            Debug.Log(given.Count > 0
                ? $"<color=green>[BossPhaseSetupTool] {given.Count} enemies now play {normal.enemyName}'s {normal.deck.Count} cards: {string.Join(", ", given)}</color>"
                : "[BossPhaseSetupTool] Every enemy already has a card deck.");
        }

        private static void CopyCardPlay(EnemyProfileSO from, EnemyProfileSO to, int extraCardsPerTurn)
        {
            to.deck = new List<CardDataSO>(from.deck);
            to.startingHandSize = from.startingHandSize;
            to.drawPerTurn = from.drawPerTurn;
            to.maxHandSize = from.maxHandSize;
            to.maxCardsPerTurn = from.maxCardsPerTurn + extraCardsPerTurn;
            to.scriptedDeck = false;
            EditorUtility.SetDirty(to);
        }

        private const string BossCardFolder = "Assets/CardEngineData/Cards/Boss";
        private const string DrainCardPath = BossCardFolder + "/Card_boss_drain.asset";

        [MenuItem("Tools/TawanOS/Card Engine/Setup Boss Phase 2 Rules")]
        private static void SetupPhase2Rules_Menu()
        {
            SetupPhase2Rules();
        }

        // Batch: -executeMethod TawanOS.CardEngine.BossPhaseSetupTool.SetupPhase2Rules
        // Phase 2 starts when the boss is down to half its khwan. It used to get a คาถาดูดขวัญ in its hand every turn;
        // that was taken out (14 Oct), so this clears cardEachTurn. The card asset is still made when missing.
        public static void SetupPhase2Rules()
        {
            var boss = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(BossPath);
            var phase2 = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(Phase2Path);
            if (boss == null || phase2 == null)
            {
                Debug.LogError("[BossPhaseSetupTool] Needs the boss profile and its phase 2 (run Setup Boss Phase 2 first).");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var drain = AssetDatabase.LoadAssetAtPath<CardDataSO>(DrainCardPath);
            if (drain == null)
            {
                if (!AssetDatabase.IsValidFolder(BossCardFolder)) AssetDatabase.CreateFolder("Assets/CardEngineData/Cards", "Boss");
                drain = ScriptableObject.CreateInstance<CardDataSO>();
                drain.cardId = "boss_drain";
                drain.cardNameThai = "คาถาดูดขวัญ";
                drain.cardNameEng = "Soul Drain";
                drain.descriptionFormat = "ดูดขวัญผู้เล่น 3 แล้วฟื้นขวัญตัวเองเท่าที่ดูดได้";
                drain.magicSchool = MagicSchool.BlackMagic;
                drain.cardType = CardType.Incantation;
                drain.targetType = TargetType.NoTarget;
                drain.customTypeText = "อาคม • มนต์ดำ";
                drain.meritCost = 0;
                drain.corruptionGain = 1;
                drain.abilities = new List<CardAbility>
                {
                    new CardAbility { trigger = AbilityTrigger.OnPlay, effect = AbilityEffect.DrainKhwan, target = AbilityTarget.Self, value = 3 },
                };
                AssetDatabase.CreateAsset(drain, DrainCardPath);
                Debug.Log($"<color=green>[BossPhaseSetupTool] {DrainCardPath} created.</color>");
            }

            if (!drain.enemyOnly)
            {
                drain.enemyOnly = true; // the boss's own card: never in the pit, rewards, shop or card book
                EditorUtility.SetDirty(drain);
            }
            boss.phase2AtKhwanRatio = 0.5f;
            phase2.cardEachTurn = null;
            EditorUtility.SetDirty(boss);
            EditorUtility.SetDirty(phase2);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BossPhaseSetupTool] Phase 2 at {boss.phase2AtKhwanRatio:P0} khwan; no card in hand each turn.");
        }

        // The status the first form's curse move inflicts, so the second form curses the same way
        private static StatusEffectType FirstDebuff(EnemyProfileSO boss)
        {
            foreach (var m in boss.moves)
            {
                if (m.intent == EnemyIntent.DebuffCurse) return m.statusEffect;
            }
            return StatusEffectType.KhwanPhawa;
        }
    }
}
#endif
