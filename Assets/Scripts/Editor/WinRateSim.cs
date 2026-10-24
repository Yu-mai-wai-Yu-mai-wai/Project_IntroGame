using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using TawanOS.CardEngine;
using TawanOS.GameFlow;
using TawanOS.MapEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// How often a player wins: whole fights played out headless with the game's own rules (CombatManager, the enemy's
    /// planner, the center pit, the clash), turn by turn in TurnPhaseController.TurnLoop's order with every wait skipped.
    /// The player is a bot (PlayerBot below). By default it thinks with the enemy's own planner (EnemyCardPlayer, the
    /// board seen from the player's side), so both sides play equally well; plannerBot = false is the old greedy bot.
    /// Each fight: a full 20 Khwan (RunState.DefaultMaxHp), the enemy's Khwan and deck for the floor
    /// (EnemyDeckProgressionSO, as GameFlowManager sets them, no upgraded player cards), and the player's deck:
    /// - Win Rate Sim: the starter deck on every floor.
    /// - Win Rate Sim (Grown Deck): the starter deck plus the cards an average run has picked up before that floor
    ///   (GrowthBefore): one per fight won (pick 1 of CardRewardConfigSO.minorChoices, rolled the way RewardManager
    ///   rolls them, the bot taking the best) and one per กองของเซ่น (floor 4). No upgrades, burns or shop buys.
    /// Khwan does not carry over between fights.
    /// Menu: Tools/TawanOS/Tests/Win Rate Sim (+ Grown Deck). Batch: -executeMethod TawanOS.EditorTools.WinRateSim.Run / RunGrown
    /// </summary>
    public static class WinRateSim
    {
        private const int FightsPerScenario = 100;
        private const int MaxTurns = 40; // a fight still going after this counts as a draw

        // What-ifs for the boss breakdown, applied in the sim only: the enemy takes nothing from the pit / has a lower
        // Merit cap than the player (null = the game's rules)
        private static bool enemySkipsPit;
        private static int? enemyMaxMerit;
        // The player bot plans with the enemy's planner (true) or plays the strongest card it can (false)
        private static bool plannerBot = true;
        // Told of every card the player plays (Card Power counts them); null = nobody listens
        private static System.Action<CardInstance> onPlayerPlayed;

        [MenuItem("Tools/TawanOS/Tests/Win Rate Sim")]
        public static void RunFromMenu() { Execute(false, grown: false); }

        public static void Run() { Execute(true, grown: false); }

        [MenuItem("Tools/TawanOS/Tests/Win Rate Sim (Grown Deck)")]
        public static void RunGrownFromMenu() { Execute(false, grown: true); }

        public static void RunGrown() { Execute(true, grown: true); }

        private const string RewardConfigPath = "Assets/GameFlowData/CardRewardConfig.asset";
        private const int BossFloor = 8; // DefaultMapConfig: 8 floors, then the boss

        // Cards an average run has before a fight on this floor: (fights won, กองของเซ่น opened). MapGraphGenerator:
        // floor 0 is always a fight, floors 1-3 are one 35% of the time, floor 4 is a กองของเซ่น, floors 5-6 a fight
        // 43% of the time, floor 7 a เมรุ. Before the boss that is 1 + 3 x 0.35 + 2 x 0.43 = 2.9 fights.
        private static (int rewards, int offerings) GrowthBefore(int floor)
        {
            double fights = floor > 0 ? 1 : 0;
            for (int f = 1; f < floor && f <= 6; f++)
                if (f != 4) fights += f <= 3 ? 0.35 : 0.43;
            return ((int)System.Math.Round(fights), floor > 4 ? 1 : 0);
        }

        private struct Result { public bool won, draw; public int turns, khwanLeft, played, failed, pitTaken; }

        private static void Execute(bool exit, bool grown)
        {
            var catalog = CardCatalogSO.Load();
            var table = Resources.Load<EncounterTableSO>("EncounterTable");
            if (catalog == null || catalog.starterDeck == null || table == null)
            {
                Debug.LogError("[WinRateSim] Needs Resources/CardCatalog with a starter deck and Resources/EncounterTable.");
                if (exit) EditorApplication.Exit(1);
                return;
            }

            var scenarios = new List<(string name, EnemyProfileSO enemy, NodeType node, int floor)>();
            foreach (var minor in table.minor.Where(e => e != null))
                foreach (int floor in new[] { 0, 3, 6 })
                    scenarios.Add(($"{minor.enemyName} floor {floor}", minor, NodeType.MinorEnemy, floor));
            foreach (var boss in table.boss.Where(e => e != null))
                scenarios.Add(($"{boss.enemyName} (boss)", boss, NodeType.Boss, BossFloor));

            var rewardConfig = grown ? AssetDatabase.LoadAssetAtPath<CardRewardConfigSO>(RewardConfigPath) : null;
            if (grown && rewardConfig == null)
            {
                Debug.LogError($"[WinRateSim] {RewardConfigPath} not found.");
                if (exit) EditorApplication.Exit(1);
                return;
            }

            var report = new StringBuilder();
            report.AppendLine($"[WinRateSim] {FightsPerScenario} fights each, {(grown ? "grown deck" : "starter deck")} "
                + $"(starter {catalog.starterDeck.startingCards.Count} cards), player {RunState.DefaultMaxHp} Khwan, bot player");
            int errors = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();

            // The enemies' cards a turn as the profiles have them, and as the sheet 7 decision had them (normal 2, boss 1):
            // set on the loaded profiles for the run only, never saved
            var enemies = scenarios.Select(x => x.enemy).Concat(table.boss.Where(b => b != null && b.phase2Profile != null).Select(b => b.phase2Profile))
                .Distinct().ToList();
            var caps = enemies.ToDictionary(e => e, e => e.maxCardsPerTurn);
            foreach (bool sheet7Cap in new[] { false, true })
            {
                foreach (var e in enemies) e.maxCardsPerTurn = sheet7Cap ? (e.isBoss ? 1 : 2) : caps[e];
                report.AppendLine(sheet7Cap ? " Enemy cards a turn: normal 2, boss 1 (sheet 7 decision)"
                    : $" Enemy cards a turn as set: {string.Join(", ", enemies.Select(e => $"{e.enemyName} {e.maxCardsPerTurn}"))}");
                try { RunScenarios(catalog, rewardConfig, scenarios, report, ref errors); }
                finally { foreach (var e in enemies) e.maxCardsPerTurn = caps[e]; }
            }
            report.AppendLine($"  ({watch.Elapsed.TotalSeconds:0.0} s)");
            Debug.Log(report.ToString());
            if (exit) EditorApplication.Exit(errors == 0 ? 0 : 1);
        }

        // rewardConfig null = the starter deck; otherwise the deck grown for the floor (GrowthBefore)
        private static void RunScenarios(CardCatalogSO catalog, CardRewardConfigSO rewardConfig,
            List<(string name, EnemyProfileSO enemy, NodeType node, int floor)> scenarios, StringBuilder report, ref int errors)
        {
            foreach (var (name, enemy, node, floor) in scenarios)
            {
                var results = new List<Result>();
                string firstError = null;
                var (rewards, offerings) = rewardConfig != null ? GrowthBefore(floor) : (0, 0);
                var added = new Dictionary<string, int>();
                for (int i = 0; i < FightsPerScenario; i++)
                {
                    List<CardDataSO> deck = null;
                    if (rewardConfig != null)
                    {
                        deck = GrownDeck(catalog, rewardConfig, rewards, offerings, seed: 5000 + i);
                        foreach (var c in deck.Skip(catalog.starterDeck.startingCards.Count))
                            added[c.cardNameThai] = added.TryGetValue(c.cardNameThai, out int n) ? n + 1 : 1;
                    }
                    try { results.Add(Fight(catalog, enemy, node, floor, seed: 1000 + i, deck: deck)); }
                    catch (System.Exception e)
                    {
                        errors++;
                        firstError ??= $"{e.GetType().Name}: {e.Message}";
                    }
                }
                var wins = results.Where(r => r.won).ToList();
                int khwan = (EnemyDeckProgressionSO.Load()?.KhwanFor(floor, enemy)) ?? enemy.maxKhwan;
                report.AppendLine($"  {name} (Khwan {khwan}): win {(float)wins.Count / Mathf.Max(1, results.Count):P0}, "
                    + $"draw {results.Count(r => r.draw)}, {(results.Count > 0 ? results.Average(r => r.turns) : 0):0.0} turns, "
                    + $"Khwan left when won {(wins.Count > 0 ? wins.Average(r => r.khwanLeft) : 0):0.0}; bot plays "
                    + $"{(results.Count > 0 ? results.Sum(r => r.played) / (float)results.Sum(r => r.turns) : 0):0.00} cards a turn, "
                    + $"{results.Sum(r => r.failed)} refused, pit {(results.Count > 0 ? results.Sum(r => r.pitTaken) / (float)results.Sum(r => r.turns) : 0):0.00} a turn"
                    + (firstError != null ? $"  ERROR x{FightsPerScenario - results.Count}: {firstError}" : ""));
                if (rewardConfig != null && rewards + offerings > 0)
                    report.AppendLine($"      deck +{rewards} reward(s) +{offerings} offering(s); most taken: "
                        + string.Join(", ", added.OrderByDescending(x => x.Value).Take(6).Select(x => $"{x.Key} {x.Value}")));
            }
        }

        // The starter deck plus `rewards` fight rewards (pick 1 of minorChoices, rolled as RewardManager.RollChoices
        // rolls them) and `offerings` กองของเซ่น (offeringChoices, the one card offered); the bot takes the best of each
        private static List<CardDataSO> GrownDeck(CardCatalogSO catalog, CardRewardConfigSO config, int rewards, int offerings, int seed)
        {
            Random.InitState(seed);
            var deck = new List<CardDataSO>(catalog.starterDeck.startingCards.Where(c => c != null));
            for (int i = 0; i < rewards + offerings; i++)
            {
                int count = config.ChoicesFor(i < rewards ? RewardTier.Minor : RewardTier.Offering);
                var offer = config.cardTypeOdds != null
                    ? config.cardTypeOdds.Pick(config.GetPool(), count, differentSchools: true)
                    : config.GetPool().OrderBy(_ => Random.value).Take(count).ToList();
                var best = offer.Where(c => c != null).OrderByDescending(c => PlayerBot.Worth(c.meritCost, c.corruptionGain, c.cardType,
                    c.familiarDamage, c.familiarHealth)).FirstOrDefault();
                if (best != null) deck.Add(best);
            }
            return deck;
        }

        // ---------------------------------------------------------------- one fight

        // deck: the player's cards (null = the starter deck)
        private static Result Fight(CardCatalogSO catalog, EnemyProfileSO enemy, NodeType node, int floor, int seed,
            Breakdown breakdown = null, List<CardDataSO> deck = null)
        {
            Random.InitState(seed);
            var go = new GameObject("WinRateSim");
            var prev = (CombatManager.Instance, EffectResolver.Instance, CardManager.Instance);
            var prevTurns = TurnPhaseController.Instance; // one in an open scene would gate the bot's plays by its phase
            DeckConfigSO playerDeck = null;
            PlayerBot bot = null;
            Set(typeof(TurnPhaseController), null);
            try
            {
                var combat = go.AddComponent<CombatManager>();
                var resolver = go.AddComponent<EffectResolver>();
                var cards = go.AddComponent<CardManager>();
                Set(typeof(CombatManager), combat); Set(typeof(EffectResolver), resolver); Set(typeof(CardManager), cards);
                cards.costSystemEnabled = true;
                cards.defaultDeckConfig = catalog.starterDeck;
                if (deck != null)
                {
                    playerDeck = ScriptableObject.CreateInstance<DeckConfigSO>();
                    playerDeck.startingCards = deck;
                    cards.defaultDeckConfig = playerDeck;
                }

                // What GameFlowManager does before a fight on this floor
                var progression = EnemyDeckProgressionSO.Load();
                combat.enemyDeckOverride = progression?.DeckFor(node, floor, enemy, 0);
                combat.enemyMaxKhwanOverride = progression?.KhwanFor(floor, enemy);
                combat.SetStartingKhwan(RunState.DefaultMaxHp, RunState.DefaultMaxHp);
                combat.StartCombat(enemy); // its own turn coroutine never runs in edit mode: the turns are played below
                if (enemyMaxMerit.HasValue) combat.State.maxMerit = enemyMaxMerit.Value; // the player's is playerMaxMerit

                var s = combat.State;
                breakdown?.Watch(combat);
                if (onPlayerPlayed != null) cards.OnCardPlayed += onPlayerPlayed;
                var pit = CenterPit.Ensure(go);
                bot = new PlayerBot(combat, cards, resolver, deck ?? catalog.starterDeck.startingCards);

                cards.DrawCards(3);
                combat.DrawEnemyOpeningHand();

                int turn = 0;
                while (!combat.IsCombatOver && turn < MaxTurns)
                {
                    turn++;
                    s.turnNumber = turn;
                    combat.RefillMeritForTurn(turn);
                    bool firstTurn = turn == 1; // no draw and no pit on turn 1

                    if (!firstTurn) cards.DrawCards(1);
                    resolver.TriggerTurnStart(isPlayer: true);
                    Step(combat.EnemyTurnStart(draw: !firstTurn));

                    if (!firstTurn)
                    {
                        pit.Deal(turn);
                        bool playerFirst = CenterPit.PlayerPicksFirst(turn);
                        for (int i = 0; i < 2 && !combat.IsCombatOver; i++)
                        {
                            if ((i == 0) == playerFirst) bot.PitPick(pit);
                            else if (!enemySkipsPit) combat.EnemyTakeFromPit(pit);
                        }
                        pit.Clear();
                    }
                    if (combat.IsCombatOver) break;

                    Step(combat.EnemyPlayCards(c => c.cardType != CardType.Incantation));
                    if (combat.IsCombatOver) break;
                    bot.PlayPhase(boardCards: true);
                    if (combat.IsCombatOver) break;

                    Step(combat.EnemyPlayCards(c => c.cardType == CardType.Incantation));
                    combat.ResolveEnemyIntentMove();
                    if (combat.IsCombatOver) break;
                    bot.PlayPhase(boardCards: false);
                    if (combat.IsCombatOver) break;

                    Step(combat.ClashAndRoundEnd());
                    breakdown?.EndTurn();
                    if (combat.IsCombatOver) break;
                    combat.PickNextEnemyMove(turn + 1);
                }
                breakdown?.EndFight(combat, turn);

                bool won = s.currentPhase == CombatPhase.Victory;
                return new Result { won = won, draw = !combat.IsCombatOver, turns = turn, khwanLeft = won ? s.playerKhwan : 0,
                    played = bot.played, failed = bot.failed, pitTaken = bot.pitTaken };
            }
            finally
            {
                breakdown?.Unwatch();
                Set(typeof(CombatManager), prev.Item1); Set(typeof(EffectResolver), prev.Item2); Set(typeof(CardManager), prev.Item3);
                Set(typeof(TurnPhaseController), prevTurns);
                Object.DestroyImmediate(go);
                if (playerDeck != null) Object.DestroyImmediate(playerDeck);
                bot?.Dispose();
            }
        }

        // ---------------------------------------------------------------- deck strength

        [MenuItem("Tools/TawanOS/Tests/Win Rate Sim (Deck Strength)")]
        public static void DeckStrengthFromMenu() { DeckStrength(false); }

        // Batch: -executeMethod TawanOS.EditorTools.WinRateSim.DeckStrengthBatch
        public static void DeckStrengthBatch() { DeckStrength(true); }

        private const int StrengthFights = 150;
        private const int StrengthKhwan = 12;

        // Decks from before the 22 Oct rework (git HEAD at the time), for comparison
        private static readonly (string name, (string id, int count)[] deck)[] OldDecks =
        {
            ("ผีกระสือ (old)", new[] { ("s_bf02", 2), ("s_bf03", 2), ("s_bf07", 2), ("s_bi01", 2), ("s_bi03", 2), ("s_ba07", 1) }),
            ("ผีพรายน้ำนอง (old)", new[] { ("s_bi05", 2), ("s_ba01", 2), ("s_ba05", 2), ("s_ba06", 2), ("s_bf06", 2), ("s_wa03", 2), ("s_wi04", 2) }),
            ("ผีตายโหง boss (old)", new[] { ("s_bf01", 2), ("s_bf04", 2), ("s_bi02", 2), ("s_ba02", 2), ("s_ba03", 2), ("s_wf01", 2), ("s_wi01", 2), ("s_bf05", 2) }),
        };

        // How strong a deck is, played by itself: every deck goes in the same enemy (a minor enemy's profile with
        // StrengthKhwan Khwan, floor 0, no phase 2) against the same player (the bot, starter deck, 20 Khwan). Only the
        // cards differ, so the numbers compare decks directly.
        // strength = % of fights the player loses; pressure = player Khwan lost a turn
        private static void DeckStrength(bool exit)
        {
            var catalog = CardCatalogSO.Load();
            var table = Resources.Load<EncounterTableSO>("EncounterTable");
            var shellSource = table != null ? table.minor.FirstOrDefault(e => e != null) : null;
            if (catalog == null || catalog.starterDeck == null || shellSource == null)
            {
                Debug.LogError("[WinRateSim] Needs Resources/CardCatalog with a starter deck and a minor enemy in Resources/EncounterTable.");
                if (exit) EditorApplication.Exit(1);
                return;
            }
            var byId = AssetDatabase.FindAssets("t:CardDataSO").Select(g => AssetDatabase.LoadAssetAtPath<CardDataSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null && !string.IsNullOrEmpty(c.cardId)).GroupBy(c => c.cardId).ToDictionary(g => g.Key, g => g.First());

            var decks = new List<(string name, List<CardDataSO> cards)>();
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyProfileSO", new[] { "Assets/CardEngineData/Enemies" }))
            {
                var e = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (e == null || e.scriptedDeck || e.deck == null || e.deck.Count == 0) continue;
                if (decks.Exists(d => d.cards.SequenceEqual(e.deck))) continue; // the boss's two phases share one deck
                decks.Add(($"{e.enemyName} (now)", new List<CardDataSO>(e.deck)));
            }
            foreach (var (name, ids) in OldDecks)
                decks.Add((name, ids.SelectMany(x => Enumerable.Repeat(byId.TryGetValue(x.id, out var c) ? c : null, x.count)).Where(c => c != null).ToList()));
            decks.Add(("player starter deck", catalog.starterDeck.startingCards.Where(c => c != null).ToList()));

            var shell = Object.Instantiate(shellSource);
            shell.isBoss = false;
            shell.phase2Profile = null;
            shell.cardEachTurn = null;
            shell.maxKhwan = StrengthKhwan;
            var report = new StringBuilder();
            report.AppendLine($"[WinRateSim] deck strength: each deck in the same enemy ({StrengthKhwan} Khwan, floor 0) vs the bot "
                + $"(starter deck, {RunState.DefaultMaxHp} Khwan), {StrengthFights} fights each");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // Mirror: the starter deck on both sides with the same Khwan; a bot as good as the enemy wins about half
                shell.deck = catalog.starterDeck.startingCards.Where(c => c != null).ToList();
                shell.maxKhwan = RunState.DefaultMaxHp;
                shell.maxCardsPerTurn = 6;
                foreach (bool planner in new[] { false, true })
                {
                    plannerBot = planner;
                    int wins = 0;
                    for (int i = 0; i < StrengthFights; i++)
                        if (Fight(catalog, shell, NodeType.MinorEnemy, 0, 1000 + i).won) wins++;
                    report.AppendLine($" Mirror (starter deck both sides, {RunState.DefaultMaxHp} Khwan each): "
                        + $"{(planner ? "planner bot" : "greedy bot")} wins {100f * wins / StrengthFights:0}%");
                }
                plannerBot = true;
                shell.maxKhwan = StrengthKhwan;

                foreach (int cap in new[] { 6, 2 })
                {
                    shell.maxCardsPerTurn = cap;
                    report.AppendLine($" Enemy cards a turn up to {cap}:");
                    var rows = new List<(string line, float strength)>();
                    foreach (var (name, cards) in decks)
                    {
                        shell.deck = cards;
                        int losses = 0, turns = 0, lost = 0, enemyPlays = 0;
                        for (int i = 0; i < StrengthFights; i++)
                        {
                            var counter = new Breakdown();
                            var r = Fight(catalog, shell, NodeType.MinorEnemy, 0, 1000 + i, counter);
                            if (!r.won) losses++;
                            turns += r.turns;
                            lost += RunState.DefaultMaxHp - (r.won ? r.khwanLeft : 0);
                            enemyPlays += counter.Plays;
                        }
                        float strength = 100f * losses / StrengthFights;
                        int white = cards.Count(c => c.magicSchool == MagicSchool.WhiteMagic);
                        rows.Add(($"   {name,-28} strength {strength,5:0}%  pressure {(float)lost / Mathf.Max(1, turns),4:0.0} Khwan/turn  "
                            + $"enemy plays {(float)enemyPlays / Mathf.Max(1, turns):0.00}/turn  {(float)turns / StrengthFights:0.0} turns  "
                            + $"({cards.Count} cards, White {white})", strength));
                    }
                    foreach (var row in rows.OrderByDescending(r => r.strength)) report.AppendLine(row.line);
                }
            }
            finally { Object.DestroyImmediate(shell); plannerBot = true; }
            report.AppendLine($"  ({watch.Elapsed.TotalSeconds:0} s)");
            Debug.Log(report.ToString());
            if (exit) EditorApplication.Exit(0);
        }

        // ---------------------------------------------------------------- trying decks out

        // Candidate decks measured the way DeckStrength measures (same enemy shell, same bot), to tune a deck to a target
        // strength before setting it in EnemyDeckSetupTool. Edit the list, then run the menu.
        private static readonly (string name, (string id, int count)[] deck)[] Candidates =
        {
            ("Prai before", new[] { ("s_bf07", 3), ("s_bi05", 2), ("s_bi01", 1), ("s_ba01", 1), ("s_wi07", 2), ("s_bf01", 1), ("s_wf04", 2), ("s_wf05", 1), ("s_wa01", 1) }),
            ("Prai now (B: no ผีตายโหง, +ถอนคุณไสย)", new[] { ("s_bf07", 3), ("s_bi05", 2), ("s_bi01", 1), ("s_ba01", 1), ("s_wi07", 2), ("s_wi05", 1), ("s_wf04", 2), ("s_wf05", 1), ("s_wa01", 1) }),
            ("C: ผีเจ้าที่ 1, +กันภัย", new[] { ("s_bf07", 3), ("s_bi05", 2), ("s_bi01", 1), ("s_ba01", 1), ("s_wi07", 2), ("s_bf01", 1), ("s_wf04", 1), ("s_wi04", 1), ("s_wf05", 1), ("s_wa01", 1) }),
            ("A+B", new[] { ("s_bf07", 2), ("s_bi05", 2), ("s_bi01", 1), ("s_ba01", 1), ("s_wi07", 2), ("s_wi05", 1), ("s_wf04", 2), ("s_wf05", 2), ("s_wa01", 1) }),
            ("A+C", new[] { ("s_bf07", 2), ("s_bi05", 2), ("s_bi01", 1), ("s_ba01", 1), ("s_wi07", 2), ("s_bf01", 1), ("s_wf04", 1), ("s_wi04", 1), ("s_wf05", 2), ("s_wa01", 1) }),
        };

        private const int TryFights = 300; // a deck's strength wanders about 8 points between runs of 150

        [MenuItem("Tools/TawanOS/Tests/Win Rate Sim (Try Decks)")]
        public static void TryDecksFromMenu()
        {
            var catalog = CardCatalogSO.Load();
            var table = Resources.Load<EncounterTableSO>("EncounterTable");
            var shellSource = table != null ? table.minor.FirstOrDefault(e => e != null) : null;
            if (catalog == null || catalog.starterDeck == null || shellSource == null) { Debug.LogError("[WinRateSim] Needs the catalog and EncounterTable."); return; }
            var byId = AssetDatabase.FindAssets("t:CardDataSO").Select(g => AssetDatabase.LoadAssetAtPath<CardDataSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null && !string.IsNullOrEmpty(c.cardId)).GroupBy(c => c.cardId).ToDictionary(g => g.Key, g => g.First());

            var shell = Object.Instantiate(shellSource);
            shell.isBoss = false; shell.phase2Profile = null; shell.cardEachTurn = null;
            shell.maxKhwan = StrengthKhwan;
            shell.maxCardsPerTurn = 6;
            var report = new StringBuilder($"[WinRateSim] try decks ({StrengthKhwan} Khwan, up to 6 a turn, {TryFights} fights each)\n");
            try
            {
                foreach (var (name, ids) in Candidates)
                {
                    shell.deck = ids.SelectMany(x => Enumerable.Repeat(byId[x.id], x.count)).ToList();
                    int losses = 0;
                    for (int i = 0; i < TryFights; i++)
                        if (!Fight(catalog, shell, NodeType.MinorEnemy, 0, 1000 + i).won) losses++;
                    report.AppendLine($"   {name,-30} strength {100f * losses / TryFights:0}%  ({shell.deck.Count} cards)");
                }
            }
            finally { Object.DestroyImmediate(shell); }
            Debug.Log(report.ToString());
        }

        // ---------------------------------------------------------------- how much the enemy's slips matter

        private const int MistakeFights = 200;

        [MenuItem("Tools/TawanOS/Tests/Win Rate Sim (Enemy Mistakes)")]
        public static void MistakesFromMenu() { Mistakes(false, grown: false); }

        [MenuItem("Tools/TawanOS/Tests/Win Rate Sim (Enemy Mistakes, Grown Deck)")]
        public static void MistakesGrownFromMenu() { Mistakes(false, grown: true); }

        // Batch: -executeMethod TawanOS.EditorTools.WinRateSim.MistakesBatch
        public static void MistakesBatch() { Mistakes(true, grown: false); }

        // Batch: -executeMethod TawanOS.EditorTools.WinRateSim.MistakesGrownBatch
        public static void MistakesGrownBatch() { Mistakes(true, grown: true); }

        // Every enemy in the EncounterTable as it is (its Khwan, deck and floor) against the bot, with the enemy never
        // slipping, slipping as its profile says (EnemyProfileSO.mistakeChance, pitMistakeChance), and taking the pit
        // card that goes worst with its hand at a few rates. A boss's two phases slip alike. grown = the bot's deck grown for the floor (GrowthBefore), minor enemies on floor 6
        // (the last before the boss); otherwise the starter deck, minor enemies on floor 0.
        private static void Mistakes(bool exit, bool grown)
        {
            var catalog = CardCatalogSO.Load();
            var table = Resources.Load<EncounterTableSO>("EncounterTable");
            if (catalog == null || catalog.starterDeck == null || table == null)
            {
                Debug.LogError("[WinRateSim] Needs Resources/CardCatalog with a starter deck and Resources/EncounterTable.");
                if (exit) EditorApplication.Exit(1);
                return;
            }
            var rewardConfig = grown ? AssetDatabase.LoadAssetAtPath<CardRewardConfigSO>(RewardConfigPath) : null;
            if (grown && rewardConfig == null)
            {
                Debug.LogError($"[WinRateSim] {RewardConfigPath} not found.");
                if (exit) EditorApplication.Exit(1);
                return;
            }
            int minorFloor = grown ? 6 : 0;
            var enemies = table.minor.Where(e => e != null).Select(e => (e, NodeType.MinorEnemy, minorFloor))
                .Concat(table.elite.Where(e => e != null).Select(e => (e, NodeType.EliteEnemy, minorFloor)))
                .Concat(table.boss.Where(e => e != null).Select(e => (e, NodeType.Boss, BossFloor))).ToList();
            var report = new StringBuilder();
            report.AppendLine($"[WinRateSim] enemy mistakes: bot with the {(grown ? "grown" : "starter")} deck ({RunState.DefaultMaxHp} Khwan), "
                + $"{MistakeFights} fights each; player win %, turns, Khwan left when won");
            foreach (var (enemy, node, floor) in enemies)
            {
                var forms = new[] { enemy, enemy.phase2Profile }.Where(e => e != null).Distinct().ToList();
                var set = forms.ToDictionary(e => e, e => e.mistakeChance);
                var pitSet = forms.ToDictionary(e => e, e => e.pitMistakeChance);
                var (rewards, offerings) = grown ? GrowthBefore(floor) : (0, 0);
                var decks = Enumerable.Range(0, MistakeFights)
                    .Select(i => grown ? GrownDeck(catalog, rewardConfig, rewards, offerings, seed: 5000 + i) : null).ToList();
                int khwan = (EnemyDeckProgressionSO.Load()?.KhwanFor(floor, enemy)) ?? enemy.maxKhwan;
                report.AppendLine($" {enemy.enemyName} floor {floor} ({khwan} Khwan, {enemy.deck.Count} cards)"
                    + (grown ? $", player deck +{rewards} reward(s) +{offerings} offering(s)" : ""));
                try
                {
                    // chance = EnemyProfileSO.mistakeChance, pit = pitMistakeChance; null = as the profile has it
                    var variants = new (string, float?, float?)[]
                    {
                        ("never slips", 0f, 0f),
                        ($"as set ({set[enemy]:0%}, pit {pitSet[enemy]:0%})", null, null),
                        ("pit slips 25%", null, 0.25f),
                        ("pit slips 40%", null, 0.4f),
                        ("pit slips 50%", null, 0.5f),
                        ("pit slips 75%", null, 0.75f),
                    };
                    foreach (var (name, chance, pit) in variants)
                    {
                        foreach (var e in forms)
                        {
                            e.mistakeChance = chance ?? set[e];
                            e.pitMistakeChance = pit ?? pitSet[e];
                        }
                        var results = new List<Result>();
                        for (int i = 0; i < MistakeFights; i++) results.Add(Fight(catalog, enemy, node, floor, 1000 + i, deck: decks[i]));
                        var wins = results.Where(r => r.won).ToList();
                        report.AppendLine($"   {name,-24} win {100f * wins.Count / MistakeFights,3:0}%   {results.Average(r => r.turns):0.0} turns   "
                            + $"Khwan left {(wins.Count > 0 ? wins.Average(r => r.khwanLeft) : 0):0.0}");
                    }
                }
                finally
                {
                    foreach (var e in forms)
                    {
                        e.mistakeChance = set[e];
                        e.pitMistakeChance = pitSet[e];
                    }
                }
            }
            Debug.Log(report.ToString());
            if (exit) EditorApplication.Exit(0);
        }

        // ---------------------------------------------------------------- how much each card is worth to the player

        private const int PowerMinorFights = 80, PowerBossFights = 40;

        [MenuItem("Tools/TawanOS/Tests/Win Rate Sim (Card Power)")]
        public static void CardPowerFromMenu() { CardPower(false); }

        // Batch: -executeMethod TawanOS.EditorTools.WinRateSim.CardPowerBatch
        public static void CardPowerBatch() { CardPower(true); }

        // Each card of the reward pool, two copies added to the starter deck, against every minor enemy on floor 6 and
        // the boss, the same seeds for every deck. power = the win % it adds over the starter deck alone, averaged over
        // the enemies; plays = how often the bot plays one of the copies a fight (a card it never plays shows nothing
        // of its worth). The numbers wander about 7 points, so only the ends of the list say much.
        private static void CardPower(bool exit)
        {
            var catalog = CardCatalogSO.Load();
            var table = Resources.Load<EncounterTableSO>("EncounterTable");
            var rewardConfig = AssetDatabase.LoadAssetAtPath<CardRewardConfigSO>(RewardConfigPath);
            if (catalog == null || catalog.starterDeck == null || table == null || rewardConfig == null)
            {
                Debug.LogError("[WinRateSim] Needs Resources/CardCatalog with a starter deck, Resources/EncounterTable and " + RewardConfigPath + ".");
                if (exit) EditorApplication.Exit(1);
                return;
            }
            var matchups = table.minor.Where(e => e != null).Select(e => (e, NodeType.MinorEnemy, 6, PowerMinorFights))
                .Concat(table.boss.Where(e => e != null).Select(e => (e, NodeType.Boss, BossFloor, PowerBossFights))).ToList();
            var starter = catalog.starterDeck.startingCards.Where(c => c != null).ToList();
            var watch = System.Diagnostics.Stopwatch.StartNew();

            // Win % against each matchup, and the copies of `card` played a fight
            (float[] win, float plays) Measure(CardDataSO card)
            {
                var deck = new List<CardDataSO>(starter);
                if (card != null) { deck.Add(card); deck.Add(card); }
                int plays = 0, fights = 0;
                onPlayerPlayed = c => { if (card != null && c.source == card) plays++; };
                try
                {
                    var win = new float[matchups.Count];
                    for (int m = 0; m < matchups.Count; m++)
                    {
                        var (enemy, node, floor, n) = matchups[m];
                        int wins = 0;
                        for (int i = 0; i < n; i++)
                            if (Fight(catalog, enemy, node, floor, 1000 + i, deck: deck).won) wins++;
                        win[m] = 100f * wins / n;
                        fights += n;
                    }
                    return (win, (float)plays / fights);
                }
                finally { onPlayerPlayed = null; }
            }

            var baseline = Measure(null);
            var rows = new List<(CardDataSO card, float power, float[] win, float plays)>();
            foreach (var card in rewardConfig.GetPool().Distinct().OrderBy(c => c.cardId))
            {
                var (win, plays) = Measure(card);
                rows.Add((card, win.Zip(baseline.win, (a, b) => a - b).Average(), win, plays));
            }

            var report = new StringBuilder();
            report.AppendLine($"[WinRateSim] card power: starter deck + 2 copies, {PowerMinorFights} fights a minor enemy "
                + $"(floor 6), {PowerBossFights} the boss; power = win % added over the starter deck, averaged");
            report.AppendLine($" starter deck alone: {string.Join(", ", matchups.Select((x, m) => $"{x.Item1.enemyName} {baseline.win[m]:0}%"))}");
            foreach (var r in rows.OrderByDescending(r => r.power))
                report.AppendLine($"   {r.card.cardId} {r.card.cardNameThai,-22} power {r.power,6:+0.0;-0.0}  plays {r.plays:0.00}  "
                    + $"({r.card.magicSchool}, {r.card.cardType}, cost {(r.card.magicSchool == MagicSchool.WhiteMagic ? r.card.meritCost : r.card.corruptionGain)}, "
                    + $"{r.card.familiarDamage}/{r.card.familiarHealth})  win {string.Join(" / ", r.win.Select(w => $"{w:0}"))}");
            report.AppendLine($"  ({watch.Elapsed.TotalSeconds:0} s)");
            Debug.Log(report.ToString());
            if (exit) EditorApplication.Exit(0);
        }

        // ---------------------------------------------------------------- where a boss fight is lost

        [MenuItem("Tools/TawanOS/Tests/Win Rate Sim (Boss Breakdown)")]
        public static void BossBreakdownFromMenu() { BossBreakdown(false, grown: false); }

        [MenuItem("Tools/TawanOS/Tests/Win Rate Sim (Boss Breakdown, Grown Deck)")]
        public static void BossBreakdownGrownFromMenu() { BossBreakdown(false, grown: true); }

        // Batch: -executeMethod TawanOS.EditorTools.WinRateSim.BossBreakdownBatch
        public static void BossBreakdownBatch() { BossBreakdown(true, grown: false); }

        // Batch: -executeMethod TawanOS.EditorTools.WinRateSim.BossBreakdownGrownBatch
        public static void BossBreakdownGrownBatch() { BossBreakdown(true, grown: true); }

        // The boss fights again, with the profiles' cap and with what-ifs (pit, Merit, cards a turn, smaller bars), and
        // what happens in them: the player's Khwan lost by cause, the cards the boss plays a turn and which, how far its
        // two bars get. grown = the player's deck grown for the boss floor (GrowthBefore); otherwise the starter deck.
        private static void BossBreakdown(bool exit, bool grown)
        {
            const int fights = 50;
            var catalog = CardCatalogSO.Load();
            var table = Resources.Load<EncounterTableSO>("EncounterTable");
            var boss = table != null ? table.boss.FirstOrDefault(b => b != null) : null;
            if (catalog == null || catalog.starterDeck == null || boss == null)
            {
                Debug.LogError("[WinRateSim] Needs Resources/CardCatalog with a starter deck and a boss in Resources/EncounterTable.");
                if (exit) EditorApplication.Exit(1);
                return;
            }
            var rewardConfig = grown ? AssetDatabase.LoadAssetAtPath<CardRewardConfigSO>(RewardConfigPath) : null;
            if (grown && rewardConfig == null)
            {
                Debug.LogError($"[WinRateSim] {RewardConfigPath} not found.");
                if (exit) EditorApplication.Exit(1);
                return;
            }
            var (rewards, offerings) = grown ? GrowthBefore(BossFloor) : (0, 0);
            var decks = Enumerable.Range(0, fights)
                .Select(i => grown ? GrownDeck(catalog, rewardConfig, rewards, offerings, seed: 5000 + i) : null).ToList();

            var forms = new[] { boss, boss.phase2Profile }.Where(e => e != null).Distinct().ToList();
            var caps = forms.ToDictionary(e => e, e => e.maxCardsPerTurn);
            var bars = forms.ToDictionary(e => e, e => e.maxKhwan);
            var pitSlips = forms.ToDictionary(e => e, e => e.pitMistakeChance);
            var report = new StringBuilder();
            report.AppendLine($"[WinRateSim] boss breakdown, {fights} fights each, bars of {boss.maxKhwan} Khwan, player "
                + (grown ? $"deck grown (+{rewards} reward(s) +{offerings} offering(s))" : "starter deck"));
            try
            {
                // bar = each phase's Khwan as a share of what the profiles have; pitSlip = EnemyProfileSO.pitMistakeChance
                // (null = as the profiles have it)
                var variants = new (string name, int cap, bool skipPit, int? merit, float bar, float? pitSlip)[]
                {
                    ($"as set (up to {caps[boss]} a turn, pit slips {pitSlips[boss]:0%})", caps[boss], false, null, 1f, null),
                    ("boss takes nothing from the pit", caps[boss], true, null, 1f, null),
                    ("pit: worst fit 25% of picks", caps[boss], false, null, 1f, 0.25f),
                    ("pit: worst fit 50% of picks", caps[boss], false, null, 1f, 0.5f),
                    ("pit: worst fit 75% of picks", caps[boss], false, null, 1f, 0.75f),
                    ("pit: worst fit every pick", caps[boss], false, null, 1f, 1f),
                    ("pit worst fit 50%, up to 3 a turn", 3, false, null, 1f, 0.5f),
                    ("boss 2 cards a turn", 2, false, null, 1f, null),
                    ("bars half as big", caps[boss], false, null, 0.5f, null),
                };
                foreach (var (variant, cap, skipPit, merit, bar, pitSlip) in variants)
                {
                    foreach (var e in forms)
                    {
                        e.maxCardsPerTurn = cap;
                        e.maxKhwan = Mathf.Max(1, Mathf.RoundToInt(bars[e] * bar));
                        e.pitMistakeChance = pitSlip ?? pitSlips[e];
                    }
                    enemySkipsPit = skipPit;
                    enemyMaxMerit = merit;
                    var b = new Breakdown();
                    int wins = 0;
                    for (int i = 0; i < fights; i++)
                        if (Fight(catalog, boss, NodeType.Boss, BossFloor, 1000 + i, b, decks[i]).won) wins++;
                    report.AppendLine($" {variant}: win {wins}/{fights}");
                    b.AppendTo(report, fights);
                }
            }
            finally
            {
                foreach (var e in forms)
                {
                    e.maxCardsPerTurn = caps[e];
                    e.maxKhwan = bars[e];
                    e.pitMistakeChance = pitSlips[e];
                }
                enemySkipsPit = false;
                enemyMaxMerit = null;
            }
            Debug.Log(report.ToString());
            if (exit) EditorApplication.Exit(0);
        }

        private sealed class Breakdown
        {
            private readonly Dictionary<string, int> lostBy = new Dictionary<string, int>();
            private readonly Dictionary<string, int> bossPlayed = new Dictionary<string, int>();
            private readonly List<int> playsPerTurn = new List<int>();
            private int reachedPhase2, turnSum, bossKhwanLeftOnLoss, losses, playsThisTurn;
            // How the boss pays for its cards and what they cost it
            private int black, white, fromPit, meritSpent, backfires, backfireKhwan;
            private CombatManager combat;

            public void Watch(CombatManager c)
            {
                combat = c;
                Application.logMessageReceived += OnLog;
                c.EnemyCards.OnCardPlayed += OnBossPlayed;
                c.OnEnemyCurseBackfireTriggered += OnBossBackfire;
            }

            private void OnBossBackfire() => backfires++;

            public void Unwatch()
            {
                Application.logMessageReceived -= OnLog;
                if (combat != null) { combat.EnemyCards.OnCardPlayed -= OnBossPlayed; combat.OnEnemyCurseBackfireTriggered -= OnBossBackfire; }
                combat = null;
            }

            public int Plays => black + white;

            private void OnBossPlayed(CardInstance card)
            {
                playsThisTurn++;
                if (card.magicSchool == MagicSchool.BlackMagic) black++; else { white++; meritSpent += card.meritCost; }
                if (card.fromPit) fromPit++;
                bossPlayed[card.cardNameThai] = bossPlayed.TryGetValue(card.cardNameThai, out int n) ? n + 1 : 1;
            }

            // CombatManager logs every Khwan the player loses as a notice: "[Notice] −3 ขวัญ ถูก ผีตายโหง โจมตี"
            private void OnLog(string message, string stack, LogType type)
            {
                // "[EffectResolver] Curse backfire triggered! Enemy khwan 14 -> 9."
                const string backfire = "Curse backfire triggered! Enemy khwan ";
                int at = message.IndexOf(backfire);
                if (at >= 0)
                {
                    var parts = message.Substring(at + backfire.Length).TrimEnd('.').Split(new[] { " -> " }, System.StringSplitOptions.None);
                    if (parts.Length == 2 && int.TryParse(parts[0], out int before) && int.TryParse(parts[1], out int after))
                        backfireKhwan += before - after;
                    return;
                }

                const string mark = "[Notice] −";
                if (!message.StartsWith(mark)) return;
                var rest = message.Substring(mark.Length);
                int space = rest.IndexOf(' ');
                if (space < 0 || !int.TryParse(rest.Substring(0, space), out int lost)) return;
                string cause = rest.Substring(space + 1).Replace("ขวัญ", "").Trim();
                if (cause.Length == 0) cause = "(no cause given)";
                lostBy[cause] = lostBy.TryGetValue(cause, out int n) ? n + lost : lost;
            }

            public void EndTurn()
            {
                playsPerTurn.Add(playsThisTurn);
                playsThisTurn = 0;
            }

            public void EndFight(CombatManager c, int turn)
            {
                if (playsThisTurn > 0) EndTurn(); // the fight ended before the clash
                if (c.EnemyPhase >= 2) reachedPhase2++;
                turnSum += turn;
                if (c.State.currentPhase != CombatPhase.Victory) { losses++; bossKhwanLeftOnLoss += c.State.enemyKhwan; }
            }

            public void AppendTo(StringBuilder report, int fights)
            {
                int totalLost = lostBy.Values.Sum();
                report.AppendLine($"   fight length {(float)turnSum / fights:0.0} turns, reached phase 2 in {reachedPhase2}/{fights}, "
                    + $"boss Khwan left when the player lost {(losses > 0 ? (float)bossKhwanLeftOnLoss / losses : 0):0.0}");
                report.AppendLine($"   boss cards a turn: avg {(playsPerTurn.Count > 0 ? playsPerTurn.Average() : 0):0.00}, turns with 0..6: "
                    + string.Join(" ", Enumerable.Range(0, 7).Select(k => playsPerTurn.Count(p => p == k))));
                report.AppendLine($"   player Khwan lost a fight {(float)totalLost / fights:0.0}, by cause: "
                    + string.Join(", ", lostBy.OrderByDescending(x => x.Value).Take(10).Select(x => $"{x.Key} {(float)x.Value / fights:0.0}")));
                int played = black + white;
                report.AppendLine($"   boss cards: Black Magic {(float)black / Mathf.Max(1, played):P0} (no Merit), White {(float)white / Mathf.Max(1, played):P0} "
                    + $"(Merit {(float)meritSpent / fights:0.0} a fight), from the pit {(float)fromPit / Mathf.Max(1, played):P0}; "
                    + $"backfires {(float)backfires / fights:0.0} a fight costing it {(float)backfireKhwan / fights:0.0} Khwan");
                report.AppendLine("   boss cards played a fight: "
                    + string.Join(", ", bossPlayed.OrderByDescending(x => x.Value).Take(12).Select(x => $"{x.Key} {(float)x.Value / fights:0.0}")));
            }
        }


        // Runs a coroutine to the end at once: waits (WaitForSeconds and the like) are skipped
        private static void Step(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            for (int guard = 0; stack.Count > 0 && guard < 100000; guard++)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator inner && !(top.Current is CustomYieldInstruction)) stack.Push(inner);
            }
        }

        private static void Set(System.Type t, object v) =>
            t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetSetMethod(true)?.Invoke(null, new[] { v });

        // ---------------------------------------------------------------- the player bot

        // A plain, greedy player: plays what it can afford, the strongest first, blocks the enemy's strongest familiar,
        // keeps one amulet, and stays away from a Corruption backfire at low Khwan. Targets are the game's own auto picks
        // (PlayCard with no target), as when a card has a single sensible target.
        private sealed class PlayerBot
        {
            private readonly CombatManager combat;
            private readonly CardManager cards;
            private readonly EffectResolver resolver;
            public int played, failed, pitTaken;

            // The enemy's planner, given the player's hand; brainProfile only makes it active (any deck, no cap on plays)
            private readonly EnemyCardPlayer brain;
            private readonly EnemyProfileSO brainProfile;

            public PlayerBot(CombatManager combat, CardManager cards, EffectResolver resolver, List<CardDataSO> deck)
            {
                this.combat = combat; this.cards = cards; this.resolver = resolver;
                if (!plannerBot) return;
                brainProfile = ScriptableObject.CreateInstance<EnemyProfileSO>();
                brainProfile.deck = new List<CardDataSO>(deck.Where(c => c != null));
                brainProfile.maxCardsPerTurn = 6;
                brainProfile.maxHandSize = 30;
                brainProfile.scriptedDeck = false;
                brain = new EnemyCardPlayer();
            }

            public void Dispose()
            {
                if (brainProfile != null) Object.DestroyImmediate(brainProfile);
            }

            private CombatStateData S => combat.State;

            // Asks the planner with the two sides swapped, so its "enemy" is the player; swapped back before returning
            private T Think<T>(System.Func<CombatStateData, T> ask)
            {
                brain.Init(brainProfile);
                foreach (var c in cards.Hand) brain.AddToHand(c);
                Swap(S);
                try { return ask(S); }
                finally { Swap(S); }
            }

            private static void Swap(CombatStateData s)
            {
                (s.playerKhwan, s.enemyKhwan) = (s.enemyKhwan, s.playerKhwan);
                (s.maxPlayerKhwan, s.maxEnemyKhwan) = (s.maxEnemyKhwan, s.maxPlayerKhwan);
                (s.currentMerit, s.enemyMerit) = (s.enemyMerit, s.currentMerit);
                (s.currentCorruption, s.enemyCorruption) = (s.enemyCorruption, s.currentCorruption);
                (s.corruptionThreshold, s.enemyCorruptionThreshold) = (s.enemyCorruptionThreshold, s.corruptionThreshold);
                (s.playerShield, s.enemyShield) = (s.enemyShield, s.playerShield);
                (s.playerPurify, s.enemyPurify) = (s.enemyPurify, s.playerPurify);
                (s.playerAuraCorruptionCap, s.enemyAuraCorruptionCap) = (s.enemyAuraCorruptionCap, s.playerAuraCorruptionCap);
                (s.activeBoardCards, s.enemyBoardCards) = (s.enemyBoardCards, s.activeBoardCards);
                (s.playerStatuses, s.enemyStatuses) = (s.enemyStatuses, s.playerStatuses);
            }

            public void PitPick(CenterPit pit)
            {
                if (pit.Offer.Count == 0 || cards.Hand.Count >= cards.maxHandSize) return;
                var pick = plannerBot ? Think(s => brain.ChoosePitCard(pit.Offer, s, EnemyCardPlayStyle.Balanced)) : null;
                pick ??= pit.Offer.OrderByDescending(Worth).First();
                pit.BeginPlayerPick(); // as TurnPhaseController.PlayerPitPick does before the click
                if (pit.TakeForPlayer(pick)) pitTaken++;
            }

            public void PlayPhase(bool boardCards)
            {
                for (int guard = 0; guard < 12 && !combat.IsCombatOver; guard++)
                {
                    CardInstance card;
                    if (plannerBot)
                    {
                        // The planner's whole turn, made again before every card (as the enemy does); this phase's first card
                        var plan = Think(s => brain.PlanTurn(s, EnemyCardPlayStyle.Balanced));
                        card = plan.FirstOrDefault(c => (c.cardType != CardType.Incantation) == boardCards && cards.Hand.Contains(c));
                    }
                    else
                    {
                        card = cards.Hand
                            .Where(c => (c.cardType != CardType.Incantation) == boardCards && CanPlay(c))
                            .OrderByDescending(Worth).FirstOrDefault();
                    }
                    if (card == null) return;
                    if (card.cardType == CardType.Familiar) card.boardSlot = BestColumn();
                    // The target the enemy would pick in its place (EnemyCardPlayer.PlayCards does the same)
                    var target = plannerBot && card.cardType == CardType.Incantation ? resolver.PickTarget(card, casterIsPlayer: true) : null;
                    if (!cards.PlayCard(card, target)) { failed++; Debug.LogWarning($"[WinRateSim] bot could not play {card.cardNameThai}"); return; }
                    played++;
                }
            }

            private bool CanPlay(CardInstance c)
            {
                if (c.magicSchool == MagicSchool.WhiteMagic && c.meritCost > S.currentMerit) return false;
                if (c.cardType == CardType.Amulet && EffectResolver.CardAt(S.activeBoardCards, EffectResolver.AmuletSlot) != null) return false;
                if (!resolver.CanResolve(c, casterIsPlayer: true)) return false;
                // A backfire takes CurseBackfireKhwanLoss Khwan: only when there is plenty to spare
                bool backfires = c.magicSchool == MagicSchool.BlackMagic && c.corruptionGain > 0
                    && S.currentCorruption + c.corruptionGain >= S.corruptionThreshold;
                return !backfires || S.playerKhwan > EffectResolver.CurseBackfireKhwanLoss * 2;
            }

            private static float Worth(CardInstance c) =>
                Worth(c.meritCost, c.corruptionGain, c.cardType, c.familiarDamage, c.familiarHealth);

            public static float Worth(int merit, int corruption, CardType type, int attack, int khwan)
            {
                float w = Mathf.Max(merit, corruption);
                if (type == CardType.Familiar) w += attack * 2f + khwan;
                else if (type == CardType.Amulet) w += 3f;
                else w += 2f;
                return w;
            }

            // The free column facing the enemy's hardest-hitting familiar, else the first free one
            private int BestColumn()
            {
                int best = -1, bestAttack = -1;
                for (int col = EffectResolver.FirstFamiliarSlot; col < EffectResolver.MaxBoardSlots; col++)
                {
                    if (EffectResolver.CardAt(S.activeBoardCards, col) != null) continue;
                    var foe = EffectResolver.CardAt(S.enemyBoardCards, col);
                    int attack = EffectResolver.IsAliveFamiliar(foe) ? resolver.EffectiveAttack(foe) : 0;
                    if (attack > bestAttack) { bestAttack = attack; best = col; }
                }
                return best;
            }
        }
    }
}
