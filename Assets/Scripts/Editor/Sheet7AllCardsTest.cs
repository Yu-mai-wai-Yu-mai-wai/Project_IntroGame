using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using TawanOS.CardEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Smoke test of every sheet 7 card and its upgrade: each one is played by the player into the same fight, then the
    /// same events follow (a heal, an ally dying, an enemy dying, the round ending, every player familiar striking).
    /// The result is compared with that fight played without the card: a card that throws, or changes nothing, is listed.
    /// Menu: Tools/TawanOS/Tests/Sheet 7 All Cards. Batch: -executeMethod TawanOS.EditorTools.Sheet7AllCardsTest.Run
    /// </summary>
    public static class Sheet7AllCardsTest
    {
        private static CombatManager mgr;
        private static EffectResolver res;
        private static CardManager hand;
        private static Dictionary<string, CardDataSO> cards;

        [MenuItem("Tools/TawanOS/Tests/Sheet 7 All Cards")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exit)
        {
            cards = AssetDatabase.FindAssets("t:CardDataSO").Select(g => AssetDatabase.LoadAssetAtPath<CardDataSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null && !string.IsNullOrEmpty(c.cardId)).GroupBy(c => c.cardId).ToDictionary(g => g.Key, g => g.First());
            var ids = cards.Keys.Where(k => k.StartsWith("s_")).OrderBy(k => k).ToList();

            string baseline = Fight(null, out _);
            var report = new StringBuilder();
            int bad = 0;
            var results = new Dictionary<string, string>();
            foreach (var id in ids)
            {
                string result = Fight(cards[id], out string error);
                results[id] = result;
                string verdict = error != null ? "ERROR " + error : result == baseline ? "NO CHANGE"
                    // an upgrade must play differently from its base card in this fight
                    : id.EndsWith("_up") && results.TryGetValue(id.Substring(0, id.Length - 3), out var baseResult) && baseResult == result
                        ? "UPGRADE SAME AS BASE" : "ok";
                if (verdict != "ok") bad++;
                report.AppendLine($"  {(verdict == "ok" ? "PASS" : "FAIL")} {id} {cards[id].cardNameThai}: {verdict}");
            }
            Debug.Log($"[Sheet7AllCardsTest] {ids.Count - bad}/{ids.Count} cards changed the fight without errors\n{report}");
            if (bad == 0) Debug.Log("[Sheet7AllCardsTest] PASS");
            else Debug.LogError($"[Sheet7AllCardsTest] FAIL: {bad} card(s) need a look (list above)");
            if (exit) EditorApplication.Exit(bad == 0 ? 0 : 1);
        }

        // One scripted fight; returns a snapshot of how it ended
        private static string Fight(CardDataSO played, out string error)
        {
            error = null;
            var go = new GameObject("Sheet7AllCardsTest");
            var prev = (CombatManager.Instance, EffectResolver.Instance, CardManager.Instance);
            try
            {
                mgr = go.AddComponent<CombatManager>();
                res = go.AddComponent<EffectResolver>();
                hand = go.AddComponent<CardManager>();
                Set(typeof(CombatManager), mgr); Set(typeof(EffectResolver), res); Set(typeof(CardManager), hand);
                var s = mgr.State;
                s.maxPlayerKhwan = s.maxEnemyKhwan = 20;
                s.playerKhwan = s.enemyKhwan = 15;
                s.currentCorruption = 6;
                s.turnNumber = 3;
                res.ResetSheet7State();
                hand.InitializeDeck(new List<CardDataSO> { cards["s_bf03"], cards["s_wf05"], cards["s_bi01"], cards["s_wi02"] });

                Board(true, "s_wf04", 1);
                Board(true, "s_bf07", 2);
                var cursed = Board(false, "s_bf01", 1);
                res.AddCardStatus(cursed, CardStatusType.DoneKhong, 2, 99);
                Board(false, "s_bf02", 2);
                Board(false, "s_wf01", 3);
                res.RemoveDeadFamiliars();

                if (played != null)
                {
                    var card = new CardInstance(played);
                    if (card.cardType == CardType.Amulet) { card.boardSlot = EffectResolver.AmuletSlot; card.chosenLane = 1; }
                    else if (card.cardType == CardType.Familiar) card.boardSlot = 3;
                    else if (EffectResolver.PicksSlot(card)) card.boardSlot = 3;
                    object target = EffectResolver.GetTargetedAbility(card) != null ? res.PickTarget(card, true) : null;
                    hand.AddToHand(card);
                    res.ResolveCardEffect(card, target, casterIsPlayer: true);
                }

                // The same events in every fight
                res.HealSide(true, 2);
                var ally = EffectResolver.CardAt(s.activeBoardCards, 1);
                if (ally != null) res.DamageCard(ally, 99, true, null, false);
                res.RemoveDeadFamiliars();
                var foe = EffectResolver.CardAt(s.enemyBoardCards, 3);
                if (foe != null) res.DamageCard(foe, 99, true, null, true);
                res.RemoveDeadFamiliars();
                res.TickCardStatuses();
                foreach (var c in s.activeBoardCards.ToList())
                {
                    if (c.IsDead || c.cardType != CardType.Familiar) continue;
                    res.ResolveFamiliarStrike(c, res.ChooseStrikeTarget(c, c.boardSlot, true), true);
                }
                res.RemoveDeadFamiliars();
                return Snapshot();
            }
            catch (System.Exception e)
            {
                error = e.GetType().Name + ": " + e.Message + " @ " + (e.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim();
                return "error";
            }
            finally
            {
                Set(typeof(CombatManager), prev.Item1); Set(typeof(EffectResolver), prev.Item2); Set(typeof(CardManager), prev.Item3);
                Object.DestroyImmediate(go);
            }
        }

        private static string Snapshot()
        {
            var s = mgr.State;
            string Side(List<CardInstance> b) => string.Join(" ", b.OrderBy(c => c.boardSlot).Select(c =>
                $"{c.boardSlot}:{c.cardId}:{c.familiarHealth}/{c.maxKhwan}/{res.EffectiveAttack(c)}[{string.Join(",", c.statuses.Select(x => x.type + "" + x.stacks))}]"));
            return $"P{s.playerKhwan} E{s.enemyKhwan} merit{s.currentMerit} corr{s.currentCorruption} hand{hand.Hand.Count}({string.Join(",", hand.Hand.Select(c => c.cardId + c.meritCost + c.corruptionGain))}) " +
                   $"grave{hand.DiscardPile.Count} | {Side(s.activeBoardCards)} || {Side(s.enemyBoardCards)}";
        }

        private static CardInstance Board(bool player, string id, int slot)
        {
            var c = new CardInstance(cards[id]) { boardSlot = slot };
            (player ? mgr.State.activeBoardCards : mgr.State.enemyBoardCards).Add(c);
            res.RefreshAuras();
            return c;
        }

        private static void Set(System.Type t, object v) =>
            t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetSetMethod(true)?.Invoke(null, new[] { v });
    }
}
