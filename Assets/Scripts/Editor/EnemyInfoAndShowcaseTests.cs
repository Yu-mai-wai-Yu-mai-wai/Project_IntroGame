using System.Collections.Generic;
using System.Reflection;
using TawanOS.CardEngine;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Checks plan task P1-5:
    /// (a) The enemy name shows a compact "กุศล x/y · มลทิน a/b" (CombatStatusHUD).
    /// (b) Familiars and amulets with at least one ability are showcased like incantations; vanilla ones are not.
    /// Menu: Tools/TawanOS/Tests/Enemy Info And Showcase.
    /// Batch: -executeMethod TawanOS.EditorTools.EnemyInfoAndShowcaseTests.Run
    /// </summary>
    public static class EnemyInfoAndShowcaseTests
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [MenuItem("Tools/TawanOS/Tests/Enemy Info And Showcase")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;
            var testGo = new GameObject("EnemyInfoAndShowcaseTests");
            var prevCombat = CombatManager.Instance;
            EnemyProfileSO profile = null;

            try
            {
                // --- (b) Which enemy cards are showcased ---
                var incantation = new CardInstance { cardType = CardType.Incantation, cardNameThai = "อาคม" };
                var vanillaFamiliar = new CardInstance { cardType = CardType.Familiar, cardNameThai = "บริวาร", abilities = new List<CardAbility>() };
                var abilityFamiliar = new CardInstance
                {
                    cardType = CardType.Familiar, cardNameThai = "บริวารมีสกิล",
                    abilities = new List<CardAbility> { new CardAbility { trigger = AbilityTrigger.OnPlay, effect = AbilityEffect.DamageCard } }
                };
                var vanillaAmulet = new CardInstance { cardType = CardType.Amulet, cardNameThai = "เครื่องราง", abilities = new List<CardAbility>() };
                var abilityAmulet = new CardInstance
                {
                    cardType = CardType.Amulet, cardNameThai = "เครื่องรางมีสกิล",
                    abilities = new List<CardAbility> { new CardAbility { trigger = AbilityTrigger.Aura, effect = AbilityEffect.BuffKhwan } }
                };

                failures += Expect("Incantation is showcased", EnemyHandView3D.ShouldShowcase(incantation));
                failures += Expect("Vanilla familiar is not showcased", !EnemyHandView3D.ShouldShowcase(vanillaFamiliar));
                failures += Expect("Familiar with an ability is showcased", EnemyHandView3D.ShouldShowcase(abilityFamiliar));
                failures += Expect("Vanilla amulet is not showcased", !EnemyHandView3D.ShouldShowcase(vanillaAmulet));
                failures += Expect("Amulet with an ability is showcased", EnemyHandView3D.ShouldShowcase(abilityAmulet));

                var handGo = new GameObject("TestEnemyHand");
                handGo.transform.SetParent(testGo.transform);
                var hand = handGo.AddComponent<EnemyHandView3D>();
                var prefabGo = new GameObject("TestCardPrefab", typeof(BoxCollider), typeof(CardView3D));
                prefabGo.transform.SetParent(testGo.transform);
                hand.cardPrefab = prefabGo.GetComponent<CardView3D>();
                hand.showcaseInDuration = 0.3f;
                hand.showcaseHoldTime = 1.6f;
                hand.abilityCardHoldTime = 1.2f;

                // No CombatManager yet, so the enemy play speed is 1
                failures += Expect("Vanilla familiar is not held back", Mathf.Approximately(hand.RevealTime(vanillaFamiliar), 0f));
                failures += Expect("Familiar with an ability waits 0.3 + 1.2 s", Mathf.Approximately(hand.RevealTime(abilityFamiliar), 1.5f));
                failures += Expect("Incantation waits 0.3 + 1.6 s", Mathf.Approximately(hand.RevealTime(incantation), 1.9f));

                // --- (a) Compact merit and corruption next to the enemy name ---
                var combatGo = new GameObject("TestCombatManager");
                combatGo.transform.SetParent(testGo.transform);
                var combat = combatGo.AddComponent<CombatManager>();
                typeof(CombatManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                    ?.GetSetMethod(true)?.Invoke(null, new object[] { combat });

                var s = combat.State;
                s.enemyMerit = 3; s.maxMerit = 5;
                s.enemyCorruption = 2; s.enemyCorruptionThreshold = 7;
                s.enemyKhwan = 25; s.maxEnemyKhwan = 30;

                profile = ScriptableObject.CreateInstance<EnemyProfileSO>();
                profile.enemyName = "ผีพราย";
                combat.currentEnemyProfile = profile;

                var hudGo = new GameObject("TestCombatStatusHUD");
                hudGo.transform.SetParent(testGo.transform);
                var hud = hudGo.AddComponent<CombatStatusHUD>();
                typeof(CombatStatusHUD).GetMethod("Start", Private)?.Invoke(hud, null);
                typeof(CombatStatusHUD).GetMethod("Update", Private)?.Invoke(hud, null);

                var panel = typeof(CombatStatusHUD).GetField("enemy", Private)?.GetValue(hud);
                var title = panel?.GetType().GetField("title")?.GetValue(panel) as TMPro.TMP_Text;
                failures += Expect("Enemy title exists", title != null);
                string text = title != null ? title.text : "";
                failures += Expect("Enemy title has the enemy name", text.Contains("ผีพราย"));
                failures += Expect("Enemy title has 'กุศล 3/5'", text.Contains("กุศล 3/5"));
                failures += Expect("Enemy title has 'มลทิน 2/7'", text.Contains("มลทิน 2/7"));
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EnemyInfoAndShowcaseTests] Unexpected exception: {ex}");
                failures++;
            }
            finally
            {
                typeof(CombatManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                    ?.GetSetMethod(true)?.Invoke(null, new object[] { prevCombat });
                if (profile != null) Object.DestroyImmediate(profile);
                Object.DestroyImmediate(testGo); // the HUD's canvas is its child
            }

            if (failures == 0)
                Debug.Log("[EnemyInfoAndShowcaseTests] PASS (all checks passed)");
            else
                Debug.LogError($"[EnemyInfoAndShowcaseTests] FAIL ({failures} checks failed)");

            if (exitOnFinish && Application.isBatchMode)
                EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Expect(string label, bool condition)
        {
            if (condition)
            {
                Debug.Log($"[EnemyInfoAndShowcaseTests] ok   {label}");
                return 0;
            }
            Debug.LogError($"[EnemyInfoAndShowcaseTests] FAIL {label}");
            return 1;
        }
    }
}
