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
    /// Checks the enemy's turn planning (EnemyCardPlayer.Planning.cs) on small set-up boards: it spends its Merit over
    /// several cards instead of one big one, keeps a card that would do nothing, never sets off a backfire that kills
    /// it, and heals when hurt; cards that work on each other are played in the order that makes them work, and the
    /// planning stays fast. Then every sheet 7 card is planned alone on a busy board, to catch exceptions.
    /// Menu: Tools/TawanOS/Tests/Enemy AI Plan. Batch: -executeMethod TawanOS.EditorTools.EnemyAIPlanTest.Run
    /// </summary>
    public static class EnemyAIPlanTest
    {
        private static CombatManager mgr;
        private static EffectResolver res;
        private static Dictionary<string, CardDataSO> cards;
        private static double lastPlanMs; // the time of one PlanTurn in the last Plan
        private static float lastCardsWorth; // the last Plan's cards, each valued where it came in it (no blocking)
        // Half a frame at 60 fps: a plan is made once before each card the enemy plays, while it is seen thinking
        private const double MaxPlanMs = 8.0;
        private static readonly List<string> leftovers = new List<string>(); // plans that left the board changed
        private static bool timePlans = true;
        // An enemy deck has to give the planner something to play most turns
        private const float MinDeckPlayRate = 0.8f;

        [MenuItem("Tools/TawanOS/Tests/Enemy AI Plan")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exit)
        {
            cards = AssetDatabase.FindAssets("t:CardDataSO").Select(g => AssetDatabase.LoadAssetAtPath<CardDataSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null && !string.IsNullOrEmpty(c.cardId)).GroupBy(c => c.cardId).ToDictionary(g => g.Key, g => g.First());

            leftovers.Clear();
            var report = new StringBuilder();
            int bad = 0;
            void Check(string name, bool ok, string detail)
            {
                if (!ok) bad++;
                report.AppendLine($"  {(ok ? "PASS" : "FAIL")} {name}: {detail}");
            }

            // Two attackers coming at it, 4 Merit, two plays: two familiars that block both columns beat one big card
            var plan = Plan(s => { s.enemyMerit = 4; PlayerBoard("s_bf01", 1); PlayerBoard("s_wf03", 2); },
                2, "s_wf07", "s_wf05", "s_wf01");
            Check("spends the Merit over two cards", plan.Count == 2 && plan.Sum(c => c.meritCost) == 4, Names(plan));
            // ผีบ้านผีเรือน grows with every familiar played after it
            Check("ผีบ้านผีเรือน before the familiar it grows on", Before(plan, "s_wf01", "s_wf05"), Names(plan));

            // พิธีสะเดาะเคราะห์ with no amulet to break: not worth a play
            plan = Plan(s => { s.enemyMerit = 4; }, 2, "s_wi06");
            Check("keeps a card that would do nothing", plan.Count == 0, Names(plan));

            // At 1 Khwan, Black Magic that reaches the Corruption threshold would kill it
            plan = Plan(s => { s.enemyKhwan = 1; s.enemyCorruption = 5; s.enemyCorruptionThreshold = 7; }, 2, "s_bf02");
            Check("no backfire that kills it", plan.Count == 0, Names(plan));

            // Hurt, one play: the heal rather than weakening a familiar
            plan = Plan(s => { s.enemyKhwan = 3; s.maxEnemyKhwan = 10; s.enemyMerit = 3; PlayerBoard("s_bf07", 1); }, 1, "s_wi02", "s_wi03");
            Check("heals when hurt", plan.Count == 1 && plan[0].cardId == "s_wi02", Names(plan));

            // The amulet is the plan's key: ตุ๊กตาคุณไสย curses the player's familiar facing each familiar played under it
            plan = Plan(s => { s.enemyCorruptionThreshold = 20; PlayerBoard("s_bf02", 1); PlayerBoard("s_bf07", 2); },
                3, "s_ba01", "s_bf02", "s_bf03");
            Check("plays the amulet with the familiars it helps", plan.Any(c => c.cardId == "s_ba01")
                && plan.Count(c => c.cardType == CardType.Familiar) >= 1, Names(plan));

            // Cards that work on each other, in the order that makes them work
            plan = Plan(s => { s.enemyMerit = 4; s.enemyCorruptionThreshold = 20; PlayerBoard("s_bf07", 1); }, 2, "s_wi07", "s_bi05");
            Check("curses the familiar before บทสวดส่งวิญญาณ destroys it", plan.Count == 2 && Before(plan, "s_bi05", "s_wi07"), Names(plan));

            plan = Plan(s => { s.enemyMerit = 7; s.enemyKhwan = 5; s.maxEnemyKhwan = 10; PlayerBoard("s_bf02", 1); }, 2, "s_wi02", "s_wf07");
            Check("ผีแม่ย่านาง before the heal that makes it strike", plan.Count == 2 && Before(plan, "s_wf07", "s_wi02"), Names(plan));

            // A full hand of ten, no cap on plays (6, as for every enemy since 22 Oct), a busy board: the whole search
            // stays well under a frame
            plan = Plan(s =>
            {
                s.enemyMerit = 6; s.enemyKhwan = 6; s.maxEnemyKhwan = 10; s.enemyCorruptionThreshold = 12;
                PlayerBoard("s_bf02", 1); PlayerBoard("s_wf01", 2, curse: 1); PlayerBoard("s_bf07", 3);
            }, 6, "s_wf01", "s_wf05", "s_wf07", "s_wi02", "s_wi07", "s_bi05", "s_bf01", "s_bf03", "s_ba01", "s_wa04");
            Check($"plans fast (under {MaxPlanMs} ms)", lastPlanMs < MaxPlanMs, $"{lastPlanMs:0.00} ms a plan: {Names(plan)}");

            // Every sheet 7 card alone in hand on a busy board: planned without an exception
            int played = 0, errors = 0;
            var lines = new StringBuilder();
            foreach (var id in cards.Keys.Where(k => k.StartsWith("s_")).OrderBy(k => k))
            {
                try
                {
                    var p = Plan(s =>
                    {
                        s.enemyMerit = 6; s.enemyKhwan = 6; s.maxEnemyKhwan = 10; s.enemyCorruption = 2;
                        PlayerBoard("s_bf02", 1); PlayerBoard("s_wf01", 3, curse: 2); PlayerBoard("s_wa01", EffectResolver.AmuletSlot);
                        EnemyBoard("s_bf07", 1); EnemyBoard("s_wf04", 2, curse: 1);
                    }, 2, id);
                    if (p.Count > 0) played++;
                    lines.Append($"{id}{(p.Count > 0 ? "+" : "-")} ");
                }
                catch (System.Exception e)
                {
                    errors++;
                    Check($"{id} plans without an exception", false, e.GetType().Name + ": " + e.Message);
                }
            }
            Check("every sheet 7 card plans", errors == 0, $"{played} played alone (+), the rest kept: {lines}");
            Check("planning puts the board, hand and curses back", leftovers.Count == 0, string.Join("; ", leftovers));
            SurveyDecks(Check);

            Debug.Log($"[EnemyAIPlanTest]\n{report}");
            if (bad == 0) Debug.Log("[EnemyAIPlanTest] PASS");
            else Debug.LogError($"[EnemyAIPlanTest] FAIL: {bad} check(s) (list above)");
            if (exit) EditorApplication.Exit(bad == 0 ? 0 : 1);
        }

        // Every enemy deck in play (EnemyProfileSO, not the scripted tutorial): hands dealt from it at random over the
        // middle of a fight, planned against a player with two familiars out. How often it finds something worth
        // playing and how many cards, then what its cards add to each other, in the planner's points (each card valued
        // where it comes in the plan, Corruption costs counted, what familiars block left out):
        // - in a turn: a plan of two cards less the two played alone. An enemy that plays one card a turn (the boss)
        //   is measured as if it could play two. Merit caps at 6, so two White cards of 3-4 rarely fit one turn.
        // - over turns: the same hand planned with one more card of the deck already on our board (played on an
        //   earlier turn: the amulet the plan is built around, a familiar the heals or deaths work with), less
        //   planned on an empty board.
        private static void SurveyDecks(System.Action<string, bool, string> check)
        {
            const int hands = 60, handSize = 4;
            timePlans = false;
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:EnemyProfileSO", new[] { "Assets/CardEngineData/Enemies" }))
                {
                    var enemy = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(AssetDatabase.GUIDToAssetPath(guid));
                    if (enemy == null || enemy.scriptedDeck || enemy.deck == null || enemy.deck.Count == 0) continue;

                    var random = new System.Random(7);
                    int withPlay = 0, cards = 0, pairs = 0, seeded = 0;
                    float inTurn = 0f, overTurns = 0f;
                    for (int h = 0; h < hands; h++)
                    {
                        var dealt = enemy.deck.OrderBy(_ => random.Next()).ToList();
                        var ids = dealt.Take(handSize).Select(c => c.cardId).ToArray();
                        var seed = dealt.Skip(handSize).FirstOrDefault(c => c.cardType != CardType.Incantation);
                        int turn = 2 + h % 4; // Merit 3 to 6, Corruption building up as the fight goes on
                        System.Action<CombatStateData> Setup(CardDataSO onBoard) => s =>
                        {
                            s.enemyMerit = Mathf.Min(6, turn + 1); s.enemyCorruption = (h * 3) % 7; s.enemyCorruptionThreshold = 7;
                            s.enemyKhwan = s.maxEnemyKhwan = enemy.maxKhwan; s.turnNumber = turn;
                            PlayerBoard("s_bf02", 1); PlayerBoard("s_wf01", 2);
                            if (onBoard != null) EnemyBoard(onBoard.cardId, onBoard.cardType == CardType.Amulet ? EffectResolver.AmuletSlot : 3);
                        };

                        var plan = Plan(Setup(null), enemy.maxCardsPerTurn, ids);
                        float worth = lastCardsWorth;
                        if (plan.Count > 0) withPlay++;
                        cards += plan.Count;

                        if (seed != null)
                        {
                            Plan(Setup(seed), enemy.maxCardsPerTurn, ids);
                            overTurns += lastCardsWorth - worth;
                            seeded++;
                        }

                        var two = enemy.maxCardsPerTurn >= 2 ? plan : Plan(Setup(null), 2, ids);
                        if (two.Count < 2) continue;
                        float together = enemy.maxCardsPerTurn >= 2 ? worth : lastCardsWorth;
                        float alone = 0f;
                        foreach (var c in two)
                        {
                            Plan(Setup(null), 1, c.cardId);
                            alone += lastCardsWorth;
                        }
                        inTurn += together - alone;
                        pairs++;
                    }
                    float rate = (float)withPlay / hands;
                    check($"{enemy.name} has something to play", rate >= MinDeckPlayRate,
                        $"plays in {rate:P0} of {hands} hands, {(float)cards / hands:0.00} cards a turn; synergy in a turn "
                        + $"{(pairs > 0 ? inTurn / pairs : 0f):+0.0;-0.0} ({pairs} pairs), over turns {(seeded > 0 ? overTurns / seeded : 0f):+0.0;-0.0}");
                }
            }
            finally { timePlans = true; }
        }

        // Sets up a fight, gives the enemy these cards and returns what it would play this turn
        private static List<CardInstance> Plan(System.Action<CombatStateData> setup, int maxCardsPerTurn, params string[] handIds)
        {
            var go = new GameObject("EnemyAIPlanTest");
            var prev = (CombatManager.Instance, EffectResolver.Instance, CardManager.Instance);
            var profile = ScriptableObject.CreateInstance<EnemyProfileSO>();
            try
            {
                mgr = go.AddComponent<CombatManager>();
                res = go.AddComponent<EffectResolver>();
                var cardManager = go.AddComponent<CardManager>();
                cardManager.costSystemEnabled = true;
                Set(typeof(CombatManager), mgr); Set(typeof(EffectResolver), res); Set(typeof(CardManager), cardManager);
                res.ResetSheet7State();

                var s = mgr.State;
                s.maxEnemyKhwan = s.maxPlayerKhwan = 10;
                s.enemyKhwan = s.playerKhwan = 10;
                s.enemyCorruption = 0;
                s.enemyCorruptionThreshold = 7;
                s.turnNumber = 3;
                setup(s);
                res.RefreshAuras();

                profile.deck = handIds.Select(id => cards[id]).ToList();
                profile.maxCardsPerTurn = maxCardsPerTurn;
                var enemy = new EnemyCardPlayer();
                enemy.Init(profile);
                foreach (var id in handIds) enemy.AddToHand(new CardInstance(cards[id]));
                string before = Snapshot(s, enemy);
                var result = enemy.PlanTurn(s, EnemyCardPlayStyle.Balanced, out _, out lastCardsWorth); // the first also warms the code up
                string after = Snapshot(s, enemy);
                if (before != after) leftovers.Add($"{string.Join(",", handIds)}: {before} -> {after}");
                // The fastest of three batches: the editor is often still busy right after a recompile
                const int runs = 10;
                lastPlanMs = double.MaxValue;
                for (int batch = 0; batch < (timePlans ? 3 : 0); batch++)
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    for (int i = 0; i < runs; i++) enemy.PlanTurn(s, EnemyCardPlayStyle.Balanced);
                    lastPlanMs = System.Math.Min(lastPlanMs, watch.Elapsed.TotalMilliseconds / runs);
                }
                return result;
            }
            finally
            {
                Set(typeof(CombatManager), prev.Item1); Set(typeof(EffectResolver), prev.Item2); Set(typeof(CardManager), prev.Item3);
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(profile);
            }
        }

        private static void PlayerBoard(string id, int slot, int curse = 0) => Board(mgr.State.activeBoardCards, id, slot, curse);
        private static void EnemyBoard(string id, int slot, int curse = 0) => Board(mgr.State.enemyBoardCards, id, slot, curse);

        private static void Board(List<CardInstance> board, string id, int slot, int curse)
        {
            var c = new CardInstance(cards[id]) { boardSlot = slot };
            board.Add(c);
            if (curse > 0) res.AddCardStatus(c, CardStatusType.DoneKhong, curse, 99);
        }

        // Everything the planner touches while it plays cards on the board in its head
        private static string Snapshot(CombatStateData s, EnemyCardPlayer enemy)
        {
            string Side(List<CardInstance> board) => string.Join(" ", board.Select(c =>
                $"{c.cardId}@{c.boardSlot}:{string.Join("/", c.statuses.Select(st => $"{st.type}{st.stacks}"))}"));
            return $"hand [{string.Join(" ", enemy.Hand.Select(c => $"{c.cardId}@{c.boardSlot}"))}] ours [{Side(s.enemyBoardCards)}] "
                + $"theirs [{Side(s.activeBoardCards)}] khwan {s.enemyKhwan}";
        }

        private static bool Before(List<CardInstance> plan, string first, string then)
        {
            int a = plan.FindIndex(c => c.cardId == first), b = plan.FindIndex(c => c.cardId == then);
            return a >= 0 && b >= 0 && a < b;
        }

        private static string Names(List<CardInstance> plan) =>
            plan.Count == 0 ? "(nothing)" : string.Join(", ", plan.Select(c => $"{c.cardId} {c.cardNameThai} ({c.meritCost})"));

        private static void Set(System.Type t, object v) =>
            t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetSetMethod(true)?.Invoke(null, new[] { v });
    }
}
