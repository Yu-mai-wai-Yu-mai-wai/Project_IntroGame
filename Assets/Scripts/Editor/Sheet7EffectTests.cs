using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using TawanOS.CardEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// The sheet 7 effects on the real card assets (run Tools/TawanOS/Cards/Apply Sheet 7 first): heals and what
    /// reacts to them, สาป on death, ตะกรุดดำ breaking, ผีปอบ's sacrifice, สายสิญจน์ stopping a flyer.
    /// Menu: Tools/TawanOS/Tests/Sheet 7 Effects. Batch: -executeMethod TawanOS.EditorTools.Sheet7EffectTests.Run
    /// </summary>
    public static class Sheet7EffectTests
    {
        private static Dictionary<string, CardDataSO> cards;
        private static CombatManager mgr;
        private static EffectResolver resolver;

        [MenuItem("Tools/TawanOS/Tests/Sheet 7 Effects")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            cards = new Dictionary<string, CardDataSO>();
            foreach (var guid in AssetDatabase.FindAssets("t:CardDataSO"))
            {
                var c = AssetDatabase.LoadAssetAtPath<CardDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (c != null && !string.IsNullOrEmpty(c.cardId)) cards[c.cardId] = c;
            }

            int failures = 0;
            failures += Case("คาถาเรียกขวัญ heals the player 2", () =>
            {
                mgr.State.playerKhwan = 10;
                Play("s_wi02");
                return mgr.State.playerKhwan == 12;
            });
            failures += Case("ลูกประคำ makes a heal of 2 restore 3", () =>
            {
                Board(true, "s_wa04", 0);
                mgr.State.playerKhwan = 10;
                Play("s_wi02");
                return mgr.State.playerKhwan == 13;
            });
            failures += Case("พระเครื่อง: two heals of 2 at full Khwan still cost the enemy its cap of 3", () =>
            {
                Board(true, "s_wa03", 0);
                mgr.State.enemyKhwan = 10;
                Play("s_wi02");
                Play("s_wi02");
                return mgr.State.playerKhwan == mgr.State.maxPlayerKhwan && mgr.State.enemyKhwan == 7;
            });
            failures += Case("A familiar that dies cursed takes its สาป off its owner's Khwan", () =>
            {
                var foe = Board(false, "s_bf01", 1);
                resolver.AddCardStatus(foe, CardStatusType.DoneKhong, 3, 99);
                mgr.State.enemyKhwan = 10;
                resolver.DamageCard(foe, 99, ignoreArmor: true, source: null, sourceIsPlayer: true);
                return mgr.State.enemyKhwan == 7;
            });
            failures += Case("ตะกรุดดำ: two ally deaths cost the enemy 1 each, then it breaks", () =>
            {
                var amulet = Board(true, "s_ba03", 0);
                var a = Board(true, "s_bf03", 1);
                var b = Board(true, "s_bf03", 2);
                mgr.State.enemyKhwan = 10;
                resolver.DamageCard(a, 99, true, null, false);
                resolver.DamageCard(b, 99, true, null, false);
                return mgr.State.enemyKhwan == 8 && amulet.pendingRemoval;
            });
            failures += Case("ผีปอบ sacrifices the ally the player picks and takes its attack", () =>
            {
                var ally = Board(true, "s_bf07", 1);
                int allyAtk = ally.familiarDamage;
                var popp = new CardInstance(cards["s_bf05"]) { boardSlot = 2 };
                int poppAtk = popp.familiarDamage;
                resolver.ResolveCardEffect(popp, ally, casterIsPlayer: true);
                return ally.IsDead && popp.familiarDamage == poppAtk + allyAtk;
            });
            failures += Case("สายสิญจน์: an enemy flyer must hit the card in front of it", () =>
            {
                Board(true, "s_wa05", 0);
                var wall = Board(true, "s_wf04", 1);
                var flyer = Board(false, "s_bf02", 1);
                return resolver.ChooseStrikeTarget(flyer, 1, fromPlayer: false) == wall;
            });

            if (failures == 0) Debug.Log("[Sheet7EffectTests] PASS");
            else Debug.LogError($"[Sheet7EffectTests] FAIL: {failures} case(s) failed.");
            if (exitOnFinish) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        // Each case gets a fresh CombatManager + EffectResolver
        private static int Case(string name, System.Func<bool> body)
        {
            var go = new GameObject("Sheet7EffectTests");
            var prevCombat = CombatManager.Instance;
            var prevResolver = EffectResolver.Instance;
            try
            {
                mgr = go.AddComponent<CombatManager>();
                resolver = go.AddComponent<EffectResolver>();
                SetInstance(typeof(CombatManager), mgr);
                SetInstance(typeof(EffectResolver), resolver);
                mgr.State.maxPlayerKhwan = 20;
                mgr.State.playerKhwan = 20;
                mgr.State.maxEnemyKhwan = 20;
                mgr.State.enemyKhwan = 20;
                resolver.ResetSheet7State();

                bool ok = body();
                if (ok) Debug.Log($"  PASS {name}");
                else Debug.LogError($"  FAIL {name}");
                return ok ? 0 : 1;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"  FAIL {name}: {e}");
                return 1;
            }
            finally
            {
                SetInstance(typeof(CombatManager), prevCombat);
                SetInstance(typeof(EffectResolver), prevResolver);
                Object.DestroyImmediate(go);
            }
        }

        private static void SetInstance(System.Type type, object value)
        {
            type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetSetMethod(true)?.Invoke(null, new[] { value });
        }

        private static CardInstance Board(bool player, string id, int slot)
        {
            var card = new CardInstance(cards[id]) { boardSlot = slot };
            (player ? mgr.State.activeBoardCards : mgr.State.enemyBoardCards).Add(card);
            resolver.RefreshAuras();
            return card;
        }

        private static void Play(string id) => resolver.ResolveCardEffect(new CardInstance(cards[id]), null, casterIsPlayer: true);
    }
}
