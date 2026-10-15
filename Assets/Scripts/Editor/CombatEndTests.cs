using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using TawanOS.CardEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Checks the combat end rules of plan task A8 without entering Play mode:
    /// a combat ends exactly once, and when both sides reach 0 Khwan in the same resolution the player wins.
    /// Run from the menu (Tools/TawanOS/Tests/Combat End Rules) or in batch mode:
    /// Unity.exe -batchmode -nographics -quit -projectPath ... -executeMethod TawanOS.EditorTools.CombatEndTests.Run
    /// Exits with code 1 if any case fails.
    /// </summary>
    public static class CombatEndTests
    {
        private class Case
        {
            public string name;
            public int endedCount;
            public bool lastResult;
        }

        [MenuItem("Tools/TawanOS/Tests/Combat End Rules")]
        public static void RunFromMenu()
        {
            Execute(exitOnFinish: false);
        }

        public static void Run()
        {
            Execute(exitOnFinish: true);
        }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;

            failures += Check("EndCombat called twice fires once", 1, true, mgr =>
            {
                mgr.EndCombat(true);
                mgr.EndCombat(false);
            });

            failures += Check("Player-only lethal hit is a Defeat", 1, false, mgr =>
            {
                mgr.State.playerKhwan = 5;
                mgr.TakeDamage(10, toPlayer: true);
            });

            failures += Check("Enemy-only lethal hit is a Victory", 1, true, mgr =>
            {
                mgr.State.enemyKhwan = 5;
                mgr.TakeDamage(10, toPlayer: false);
            });

            // Enemy reflects half the hit onto the player (player 1 -> 0) and the hit itself kills the enemy (4 -> 0)
            failures += Check("Reflect kills both sides: Victory", 1, true, mgr =>
            {
                mgr.State.playerKhwan = 1;
                mgr.State.enemyKhwan = 4;
                mgr.ApplyStatus(StatusEffectType.MontSaThon, 3, toPlayer: false);
                mgr.TakeDamage(4, toPlayer: false);
            });

            failures += Check("Reflect kills only the player: Defeat", 1, false, mgr =>
            {
                mgr.State.playerKhwan = 1;
                mgr.State.enemyKhwan = 20;
                mgr.ApplyStatus(StatusEffectType.MontSaThon, 3, toPlayer: false);
                mgr.TakeDamage(4, toPlayer: false);
            });

            // Round-end bleeding on both sides, batched the same way ClashAndRoundEnd batches it
            failures += Check("Bleeding drops both to 0 in one batch: Victory", 1, true, mgr =>
            {
                mgr.State.playerKhwan = 3;
                mgr.State.enemyKhwan = 3;
                mgr.ApplyStatus(StatusEffectType.BleedingCurse, 2, toPlayer: true);
                mgr.ApplyStatus(StatusEffectType.BleedingCurse, 2, toPlayer: false);

                var tick = typeof(CombatManager).GetMethod("TickStatusDurations", BindingFlags.NonPublic | BindingFlags.Instance);
                if (tick == null) throw new System.MissingMethodException("CombatManager.TickStatusDurations");

                mgr.BeginOutcomeBatch();
                tick.Invoke(mgr, new object[] { true });
                tick.Invoke(mgr, new object[] { false });
                mgr.EndOutcomeBatch();
            });

            failures += Check("EndCombat(false) while the enemy is at 0 becomes Victory", 1, true, mgr =>
            {
                mgr.State.enemyKhwan = 0;
                mgr.EndCombat(false);
            });

            // P0-5: Detonate into a protected card: protect -1, card lives, detonating card dies
            failures += CheckDetonateProtectedCard();

            Debug.Log(failures == 0
                ? "[CombatEndTests] PASS (8 cases)"
                : $"[CombatEndTests] FAIL: {failures} case(s) failed");

            if (exitOnFinish) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int CheckDetonateProtectedCard()
        {
            const string name = "Detonate into a protected card: protect -1, card lives";
            var go = new GameObject("CombatEndTests_DetonateProtected");
            var prevCombat = CombatManager.Instance;
            var prevResolver = EffectResolver.Instance;
            try
            {
                var mgr = go.AddComponent<CombatManager>();
                var resolver = go.AddComponent<EffectResolver>();
                typeof(CombatManager).GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?.GetSetMethod(true)?.Invoke(null, new object[] { mgr });
                typeof(EffectResolver).GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?.GetSetMethod(true)?.Invoke(null, new object[] { resolver });

                var victim = new CardInstance
                {
                    cardId = "test_victim",
                    cardNameThai = "ผู้คุ้มภัย",
                    cardType = CardType.Familiar,
                    maxKhwan = 5,
                    familiarHealth = 5,
                    boardSlot = 0
                };
                victim.statuses.Add(new CardStatus(CardStatusType.Protect, 1, 99));
                mgr.State.enemyBoardCards.Add(victim);

                var bomb = new CardInstance
                {
                    cardId = "test_bomb",
                    cardNameThai = "ด้ายแดง",
                    cardType = CardType.Familiar,
                    maxKhwan = 2,
                    familiarHealth = 2,
                    boardSlot = 0,
                    abilities = new List<CardAbility>
                    {
                        new CardAbility { effect = AbilityEffect.DetonateWithOpposite, trigger = AbilityTrigger.OnPlay }
                    }
                };
                mgr.State.activeBoardCards.Add(bomb);

                resolver.TriggerAbilities(bomb, AbilityTrigger.OnPlay, ownerIsPlayer: true);

                bool protectConsumed = !victim.statuses.Exists(s => s.type == CardStatusType.Protect && s.stacks > 0);
                bool victimLives = !victim.IsDead && victim.familiarHealth == 5;
                bool bombDied = bomb.IsDead;

                bool ok = protectConsumed && victimLives && bombDied;
                Debug.Log($"[CombatEndTests] {(ok ? "ok  " : "FAIL")} {name} (protectConsumed={protectConsumed}, victimLives={victimLives}, bombDied={bombDied})");
                return ok ? 0 : 1;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CombatEndTests] FAIL {name}: {e}");
                return 1;
            }
            finally
            {
                typeof(CombatManager).GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?.GetSetMethod(true)?.Invoke(null, new object[] { prevCombat });
                typeof(EffectResolver).GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?.GetSetMethod(true)?.Invoke(null, new object[] { prevResolver });
                Object.DestroyImmediate(go);
            }
        }

        private static int Check(string name, int expectedCount, bool expectedResult, System.Action<CombatManager> arrange)
        {
            var go = new GameObject("CombatEndTests_" + name);
            try
            {
                var mgr = go.AddComponent<CombatManager>();
                var result = new Case { name = name };
                mgr.OnCombatEnded += win =>
                {
                    result.endedCount++;
                    result.lastResult = win;
                };

                arrange(mgr);

                bool ok = result.endedCount == expectedCount && (expectedCount == 0 || result.lastResult == expectedResult);
                Debug.Log($"[CombatEndTests] {(ok ? "ok  " : "FAIL")} {name} (ended {result.endedCount}x, last victory={result.lastResult})");
                return ok ? 0 : 1;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CombatEndTests] FAIL {name}: {e}");
                return 1;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
