using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // The enemy plans its whole turn at once: of every run of cards in hand it can pay for (Merit), has the plays for
    // (maxCardsPerTurn) and the room for (free familiar columns, one amulet), it takes the run worth the most. The run
    // is valued together: the face damage its familiars block between them, the Corruption it builds, and the Merit it
    // leaves unspent (which is lost at the end of the turn). The plan is made again before every card, since each play
    // changes the board.
    // Cards work on each other (a heal and ผีแม่ย่านาง, a curse and คาถาสะกด, ผีบ้านผีเรือน and the familiars after it),
    // so a run is tried in order: each card is valued on the board the cards before it would leave (PlayVirtually).
    // The amulet is the plan's key: its skill works on every card played under it, so each plan is made around one
    // amulet (the one already in the slot, or one from hand played first).
    public partial class EnemyCardPlayer
    {
        // A card worth less than this stays in hand for a better turn
        private const float MinWorthPlaying = 0.5f;
        // Corruption is never paid back, so every point brings the backfire (CurseBackfireKhwanLoss) closer
        private const float CorruptionPointCost = 0.6f;
        // Playing an amulet over the one already in the slot throws that one away
        private const float ReplacedAmuletCost = 3f;
        // How much of a pit card's own worth counts when it would not be played this turn
        private const float PitFutureWeight = 0.3f;
        // The sets of each size kept to build on, the best first: with no cap on plays a full hand has hundreds
        private const int RunsKept = 12;

        private struct Option
        {
            public CardInstance card;
            public float score; // valued on the board the cards before it in the plan leave
            public int merit;
            public int corruption;
            public float corruptionCost; // counted when it is played: a heal before it can make a backfire survivable
            public bool familiar;
            public bool taunt;
            public bool amulet;
            public bool now; // allowed in this play phase; the others (incantations) wait for the spell phase
        }

        // What every plan of this turn shares: the board it meets and the plays left
        private sealed class PlanContext
        {
            public CombatStateData s;
            public EnemyCardPlayStyle style;
            public bool costsEnabled;
            public System.Predicate<CardInstance> filter;
            public int playsLeft;
            public bool hasAmulet;
            public int totalFaceDamage;
            public int unblockable;
            public readonly List<int> blockable = new List<int>(); // face damage through each free column, most first
            public int familiarsOnBoard;
            // ผ้าประเจียดดำ in the key amulet: +attackPerStep attack on every familiar per corruptionStep Corruption
            public int attackPerStep;
            public int corruptionStep;
        }

        // The next card of the best plan that this phase may play; null = nothing worth playing now
        private CardInstance NextPlannedCard(CombatStateData s, EnemyCardPlayStyle style, bool costsEnabled,
            System.Predicate<CardInstance> filter)
        {
            var plan = BestPlan(hand, s, style, costsEnabled, filter, out float planScore);
            // A slip (EnemyProfileSO.mistakeChance): the second best play, the first card of the best plan without
            // the best one's first card. The best card may still come later in the turn, out of its order.
            bool slip = false;
            if (plan.Count > 0 && plan[0].now && profile != null && profile.mistakeChance > 0f && Random.value < profile.mistakeChance)
            {
                var without = new List<CardInstance>(hand);
                without.Remove(plan[0].card);
                var second = BestPlan(without, s, style, costsEnabled, filter, out float secondScore);
                if (second.Count > 0 && second[0].now)
                {
                    slip = true;
                    plan = second;
                    planScore = secondScore;
                }
            }
            if (plan.Count > 0)
                Debug.Log($"[EnemyAI] {(slip ? "slip " : "")}plan ({planScore:0.0}, Merit {s.enemyMerit}, Corruption {s.enemyCorruption}/{s.enemyCorruptionThreshold}): "
                    + string.Join(" → ", plan.ConvertAll(o => $"{o.card.cardNameThai} {o.score:0.0}")));
            // The plan puts this phase's cards first; the rest wait for the spell phase
            return plan.Count > 0 && plan[0].now ? plan[0].card : null;
        }

        // The cards it would play over the rest of this turn (both play phases), in order; for tests and debugging
        public List<CardInstance> PlanTurn(CombatStateData s, EnemyCardPlayStyle style) => PlanTurn(s, style, out _, out _);

        // score = what the plan is worth in all; cardsWorth = the part from its cards' own scores and Corruption costs,
        // each valued where it comes in the plan (without what its familiars block between them)
        public List<CardInstance> PlanTurn(CombatStateData s, EnemyCardPlayStyle style, out float score, out float cardsWorth)
        {
            bool costsEnabled = CardManager.Instance != null && CardManager.Instance.costSystemEnabled;
            var plan = BestPlan(hand, s, style, costsEnabled, null, out score);
            cardsWorth = 0f;
            foreach (var o in plan) cardsWorth += o.score - o.corruptionCost;
            return plan.ConvertAll(o => o.card);
        }

        // หลุมกลาง: the card that adds the most to this turn's best plan, plus part of its own worth for later turns
        // (a card it cannot afford yet is still worth taking). A slip (EnemyProfileSO.pitMistakeChance) takes the one
        // that adds the least: the card that goes worst with the hand.
        public CardInstance ChoosePitCard(IReadOnlyList<CardInstance> offer, CombatStateData s, EnemyCardPlayStyle style)
        {
            bool costsEnabled = CardManager.Instance != null && CardManager.Instance.costSystemEnabled;
            BestPlan(hand, s, style, costsEnabled, null, out float baseline);

            bool slip = profile != null && profile.pitMistakeChance > 0f && Random.value < profile.pitMistakeChance;
            var withPick = new List<CardInstance>(hand) { null };
            CardInstance best = null;
            float bestScore = float.MinValue;
            foreach (var card in offer)
            {
                if (card == null) continue;
                withPick[withPick.Count - 1] = card;
                BestPlan(withPick, s, style, costsEnabled, null, out float planScore);
                float score = planScore - baseline + PitFutureWeight * Mathf.Max(0f, Score(card, s, style));
                if (slip) score = -score;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = card;
                }
            }
            if (slip && best != null) Debug.Log($"[EnemyAI] slip pit pick: {best.cardNameThai}");
            return best;
        }

        // The cards worth playing this turn, in the order to play them; empty = playing nothing is best.
        // filter = this phase's cards; incantations it leaves out still count, as the spell phase comes later.
        private List<Option> BestPlan(IReadOnlyList<CardInstance> cards, CombatStateData s, EnemyCardPlayStyle style,
            bool costsEnabled, System.Predicate<CardInstance> filter, out float bestScore)
        {
            var resolver = EffectResolver.Instance;
            var playable = new List<CardInstance>();
            var keys = new List<CardInstance> { null }; // null = the amulet already in the slot (or none)
            foreach (var card in cards)
            {
                if (card == null) continue;
                bool now = filter == null || filter(card);
                if (!now && card.cardType != CardType.Incantation) continue;
                if (resolver != null && !resolver.CanResolve(card, casterIsPlayer: false)) continue;
                if (costsEnabled && card.magicSchool == MagicSchool.WhiteMagic && card.meritCost > s.enemyMerit) continue;

                if (card.cardType == CardType.Amulet) keys.Add(card);
                else playable.Add(card);
            }

            var ctx = BuildContext(s, style);
            ctx.costsEnabled = costsEnabled;
            ctx.filter = filter;
            var best = new List<Option>();
            bestScore = 0f; // playing nothing
            var current = new List<Option>();
            foreach (var key in keys)
            {
                // The key amulet goes first, valued on the board as it is; the rest are valued under it
                System.Action undo = null;
                int meritLeft = s.enemyMerit;
                if (key != null)
                {
                    var amulet = MakeOption(key, ctx, current);
                    if (style != EnemyCardPlayStyle.Random && amulet.score < MinWorthPlaying) continue;
                    current.Add(amulet);
                    meritLeft -= amulet.merit;
                    undo = PlayVirtually(key, s);
                }
                try
                {
                    SetKey(ctx, EffectResolver.CardAt(s.enemyBoardCards, EffectResolver.AmuletSlot));
                    Search(playable, current, meritLeft, ctx, best, ref bestScore);
                }
                finally
                {
                    undo?.Invoke();
                    current.Clear();
                }
            }
            return best;
        }

        // A way to play a set of cards: in this order, worth `value` (the cards' scores less their Corruption costs)
        private sealed class Run
        {
            public readonly List<Option> plan;
            public readonly float value;
            public readonly int meritLeft;
            public Run(List<Option> plan, float value, int meritLeft) { this.plan = plan; this.value = value; this.meritLeft = meritLeft; }
        }

        // The best order of every set of the cards, a card longer at a time: the best way to play a set ends with one
        // of its cards, played after the best way to play the rest, on the board the rest leave. The work grows with
        // the sets, not with their orders, and only the RunsKept best sets of a size are built on (EnemyAIPlanTest times
        // it). `start` (the key amulet) is already on the board.
        private void Search(List<CardInstance> cards, List<Option> start, int meritLeft, PlanContext ctx,
            List<Option> best, ref float bestScore)
        {
            float startValue = 0f;
            foreach (var o in start) startValue += o.score - o.corruptionCost;
            var layer = new Dictionary<int, Run> { [0] = new Run(new List<Option>(start), startValue, meritLeft) };
            if (start.Count > 0) Consider(layer[0].plan, ctx, best, ref bestScore);

            for (int depth = start.Count; depth < ctx.playsLeft && layer.Count > 0; depth++)
            {
                var next = new Dictionary<int, Run>();
                foreach (var entry in layer)
                {
                    var run = entry.Value;
                    var undo = new List<System.Action>();
                    for (int i = start.Count; i < run.plan.Count; i++) undo.Add(PlayVirtually(run.plan[i].card, ctx.s));
                    try { Extend(cards, entry.Key, run, ctx, next); }
                    finally { for (int i = undo.Count - 1; i >= 0; i--) undo[i](); }
                }
                foreach (var run in next.Values) Consider(run.plan, ctx, best, ref bestScore);
                layer = next.Count <= RunsKept ? next
                    : next.OrderByDescending(e => e.Value.value).Take(RunsKept).ToDictionary(e => e.Key, e => e.Value);
            }
        }

        // Every card that can follow `run` (whose cards are the bits of `played`), each kept in `next` when it makes the
        // best way found yet to play its set
        private void Extend(List<CardInstance> cards, int played, Run run, PlanContext ctx, Dictionary<int, Run> next)
        {
            // Once a card waits for the spell phase, so does every card after it
            bool spellPhase = run.plan.Count > 0 && !run.plan[run.plan.Count - 1].now;
            for (int i = 0; i < cards.Count; i++)
            {
                if ((played & (1 << i)) != 0) continue;
                var card = cards[i];
                bool now = ctx.filter == null || ctx.filter(card);
                if (spellPhase && now) continue;
                int merit = ctx.costsEnabled && card.magicSchool == MagicSchool.WhiteMagic ? card.meritCost : 0;
                if (merit > run.meritLeft) continue;
                if (card.cardType == CardType.Familiar && EffectResolver.FreeSlot(ctx.s.enemyBoardCards) < 0) continue;

                var o = MakeOption(card, ctx, run.plan);
                o.now = now;
                if (ctx.style != EnemyCardPlayStyle.Random && o.score < MinWorthPlaying) continue;

                int set = played | (1 << i);
                float value = run.value + o.score - o.corruptionCost;
                if (next.TryGetValue(set, out var have) && have.value >= value) continue;
                next[set] = new Run(new List<Option>(run.plan) { o }, value, run.meritLeft - merit);
            }
        }

        private static void Consider(List<Option> plan, PlanContext ctx, List<Option> best, ref float bestScore)
        {
            float score = PlanScore(plan, ctx);
            if (score <= bestScore) return;
            bestScore = score;
            best.Clear();
            best.AddRange(plan);
        }

        // `card` played after the cards of `before`, on the board they leave
        private Option MakeOption(CardInstance card, PlanContext ctx, List<Option> before)
        {
            bool familiar = card.cardType == CardType.Familiar;
            int corruption = ctx.costsEnabled && card.magicSchool == MagicSchool.BlackMagic ? Mathf.Max(0, card.corruptionGain) : 0;
            int built = 0;
            foreach (var o in before) built += o.corruption;
            return new Option
            {
                card = card,
                score = Score(card, ctx.s, ctx.style),
                merit = ctx.costsEnabled && card.magicSchool == MagicSchool.WhiteMagic ? card.meritCost : 0,
                corruption = corruption,
                corruptionCost = CorruptionCost(built, corruption, ctx.s, ctx.style),
                familiar = familiar,
                taunt = familiar && EffectResolver.HasTaunt(card),
                amulet = card.cardType == CardType.Amulet,
                now = true,
            };
        }

        // ---------------------------------------------------------------- the board a plan leaves

        // Plays `card` on the board the later cards of a plan are valued on: out of the hand, a familiar into the column
        // PlayCards would put it in, an amulet into the slot, its curses on the player's familiars (and ตุ๊กตาคุณไสย's on
        // the one facing a familiar that comes in), its heal on our Khwan. Only what the scoring reads: nothing is
        // resolved, no event fires. Sacrifices and deaths are not played out. The returned action puts it all back.
        private System.Action PlayVirtually(CardInstance card, CombatStateData s)
        {
            var undo = new Stack<System.Action>();

            int handIndex = hand.IndexOf(card);
            if (handIndex >= 0)
            {
                hand.RemoveAt(handIndex);
                undo.Push(() => hand.Insert(handIndex, card));
            }

            if (card.cardType == CardType.Familiar || card.cardType == CardType.Amulet)
            {
                var board = s.enemyBoardCards;
                int slot = EffectResolver.AmuletSlot;
                if (card.cardType == CardType.Familiar)
                {
                    slot = BestBlockSlot(s);
                    if (slot < 0) slot = EffectResolver.FreeSlot(board, card.boardSlot);
                }
                var old = EffectResolver.CardAt(board, slot); // only an amulet ever replaces one
                int index = old != null ? board.IndexOf(old) : board.Count;
                int oldSlot = card.boardSlot;
                card.boardSlot = slot;
                if (old != null) board[index] = card; else board.Add(card);
                undo.Push(() =>
                {
                    if (old != null) board[index] = old; else board.RemoveAt(index);
                    card.boardSlot = oldSlot;
                });

                if (card.cardType == CardType.Familiar)
                {
                    var doll = EffectResolver.CardAt(board, EffectResolver.AmuletSlot);
                    int curse = IsLive(doll) ? PassiveValue(doll, AbilityEffect.CurseOnAllyEnter) : 0;
                    if (curse > 0) CurseVirtually(EffectResolver.CardAt(s.activeBoardCards, slot), curse, undo);
                }
            }

            if (card.abilities != null)
            {
                foreach (var a in card.abilities)
                {
                    if (a.trigger != AbilityTrigger.OnPlay || a.effect != AbilityEffect.ApplyStatus || a.status != CardStatusType.DoneKhong) continue;
                    var foes = s.activeBoardCards.FindAll(c => EffectResolver.IsAliveFamiliar(c));
                    if (a.target == AbilityTarget.AllEnemy || a.target == AbilityTarget.AllEnemyFamiliars)
                        foreach (var c in foes) CurseVirtually(c, a.value, undo);
                    else
                        CurseVirtually(Best(foes, c => Attack(c)), a.value, undo); // the one ApplyStatus scoring values most
                }
            }

            int heal = Heals(card);
            if (heal > 0)
            {
                int khwan = s.enemyKhwan;
                s.enemyKhwan = Mathf.Min(s.maxEnemyKhwan, khwan + heal);
                undo.Push(() => s.enemyKhwan = khwan);
            }

            return () => { while (undo.Count > 0) undo.Pop()(); };
        }

        private static void CurseVirtually(CardInstance target, int stacks, Stack<System.Action> undo)
        {
            if (!EffectResolver.IsAliveFamiliar(target) || stacks <= 0) return;
            var curse = target.statuses.Find(st => st.type == CardStatusType.DoneKhong);
            if (curse != null)
            {
                curse.stacks += stacks;
                undo.Push(() => curse.stacks -= stacks);
            }
            else
            {
                var added = new CardStatus(CardStatusType.DoneKhong, stacks, 99);
                target.statuses.Add(added);
                undo.Push(() => target.statuses.Remove(added));
            }
        }

        // ---------------------------------------------------------------- the amulet as the plan's key

        // What of the key amulet the plan's score needs beyond the cards' own scores
        private static void SetKey(PlanContext ctx, CardInstance amulet)
        {
            ctx.attackPerStep = ctx.corruptionStep = 0;
            if (!IsLive(amulet) || amulet.abilities == null || EffectResolver.AbilitiesBlocked(amulet)) return;
            var a = amulet.abilities.Find(x => x.effect == AbilityEffect.AttackPerCorruption && x.trigger == AbilityTrigger.Aura);
            if (a == null) return;
            ctx.attackPerStep = a.value;
            ctx.corruptionStep = Mathf.Max(1, a.duration);
        }

        // What the amulet in our slot adds to a card played under it. Heals and death triggers already read the slot
        // (ScoreHeal, SacrificeLoss); this is the rest: its auras on a familiar coming in, ตุ๊กตาคุณไสย's curse on the
        // one facing it, a curse that หัวกะโหลกอาถรรพ์ keeps from fading. Same scale as ScoreAbilities.
        private float AmuletBonus(CardInstance card, CombatStateData s)
        {
            var amulet = EffectResolver.CardAt(s.enemyBoardCards, EffectResolver.AmuletSlot);
            if (amulet == card || !IsLive(amulet) || amulet.abilities == null || EffectResolver.AbilitiesBlocked(amulet)) return 0f;

            bool familiar = card.cardType == CardType.Familiar && card.familiarHealth > 0;
            bool curses = card.abilities != null && card.abilities.Exists(x =>
                x.effect == AbilityEffect.ApplyStatus && x.status == CardStatusType.DoneKhong);
            float bonus = 0f;
            foreach (var a in amulet.abilities)
            {
                if (a.trigger != AbilityTrigger.Passive && a.trigger != AbilityTrigger.Aura) continue;
                switch (a.effect)
                {
                    case AbilityEffect.BuffAttack: // ตะกรุด
                        if (familiar && a.trigger == AbilityTrigger.Aura && a.value > 0) bonus += a.value * 3f;
                        break;
                    case AbilityEffect.BuffKhwan:
                        if (familiar && a.trigger == AbilityTrigger.Aura && a.value > 0) bonus += a.value * 0.4f;
                        break;
                    case AbilityEffect.AttackIfSturdy: // ผ้ายันต์กลับด้าน
                        if (familiar && card.maxKhwan >= a.duration) bonus += a.value * 3f;
                        break;
                    case AbilityEffect.AttackPerCorruption: // ผ้าประเจียดดำ; the Corruption this turn builds: PlanScore
                        if (familiar) bonus += a.value * (s.enemyCorruption / Mathf.Max(1, a.duration)) * 3f;
                        break;
                    case AbilityEffect.CurseOnAllyEnter: // ตุ๊กตาคุณไสย: pays when a free column faces one of theirs
                        if (familiar && FreeColumnFacingFoe(s)) bonus += a.value * 2f;
                        break;
                    case AbilityEffect.CurseNoDecay: // หัวกะโหลกอาถรรพ์
                        if (curses) bonus += 2f;
                        break;
                    case AbilityEffect.AllyDeathHurtsEnemy: // ตะกรุดดำ: a familiar that dies some day pays it
                        if (familiar) bonus += a.value * 1.25f;
                        break;
                    case AbilityEffect.AllyDeathSummon: // หุ่นพยนต์
                        if (familiar) bonus += 1f;
                        break;
                    case AbilityEffect.DeathSave: // ผ้ายันต์มหาอุด
                        if (familiar) bonus += 1.5f;
                        break;
                }
            }
            return bonus;
        }

        private static bool FreeColumnFacingFoe(CombatStateData s)
        {
            for (int col = EffectResolver.FirstFamiliarSlot; col < EffectResolver.MaxBoardSlots; col++)
                if (EffectResolver.CardAt(s.enemyBoardCards, col) == null
                    && EffectResolver.IsAliveFamiliar(EffectResolver.CardAt(s.activeBoardCards, col))) return true;
            return false;
        }

        // ---------------------------------------------------------------- scoring a plan

        private PlanContext BuildContext(CombatStateData s, EnemyCardPlayStyle style)
        {
            var ctx = new PlanContext
            {
                s = s,
                style = style,
                playsLeft = Mathf.Max(0, profile.maxCardsPerTurn - playedThisTurn),
                hasAmulet = EffectResolver.CardAt(s.enemyBoardCards, EffectResolver.AmuletSlot) != null,
            };

            var damage = IncomingFaceDamage(s, out int unblockable);
            ctx.unblockable = unblockable;
            ctx.totalFaceDamage = unblockable;
            for (int col = EffectResolver.FirstFamiliarSlot; col < damage.Length; col++)
            {
                ctx.totalFaceDamage += damage[col];
                if (EffectResolver.IsAliveFamiliar(EffectResolver.CardAt(s.enemyBoardCards, col))) ctx.familiarsOnBoard++;
                if (EffectResolver.CardAt(s.enemyBoardCards, col) != null) continue;
                ctx.blockable.Add(damage[col]);
            }
            ctx.blockable.Sort((a, b) => b.CompareTo(a));
            return ctx;
        }

        private static float PlanScore(List<Option> plan, PlanContext ctx)
        {
            float score = 0f;
            int merit = 0, corruption = 0, blockers = 0;
            float corruptionCost = 0f;
            bool taunt = false, amulet = false;
            foreach (var o in plan)
            {
                score += o.score;
                merit += o.merit;
                corruption += o.corruption;
                corruptionCost += o.corruptionCost;
                if (o.familiar) { blockers++; taunt |= o.taunt; }
                amulet |= o.amulet;
            }

            // Familiars played together block different columns: the first the column with the most damage coming
            // through it, the next the second most; a taunting one takes all of it
            if (ctx.style != EnemyCardPlayStyle.Random && blockers > 0)
            {
                int stopped = 0;
                if (taunt) stopped = ctx.totalFaceDamage - ctx.unblockable;
                else for (int i = 0; i < blockers && i < ctx.blockable.Count; i++) stopped += ctx.blockable[i];
                score += BlockValue(stopped, ctx.totalFaceDamage, ctx.s);
            }

            if (amulet && ctx.hasAmulet) score -= ReplacedAmuletCost;
            score -= corruptionCost;

            // ผ้าประเจียดดำ as the key: the Corruption this plan builds raises every familiar's attack
            if (ctx.attackPerStep > 0 && corruption > 0)
            {
                int c = ctx.s.enemyCorruption;
                int steps = (c + corruption) / ctx.corruptionStep - c / ctx.corruptionStep;
                score += steps * ctx.attackPerStep * 3f * (ctx.familiarsOnBoard + blockers);
            }

            // Small bonus for spending more Merit so ties use the turn's budget fully
            return score + merit * 0.01f;
        }

        // What building `gain` more Corruption costs, after the plan has built `built`: a little for every point, a lot
        // when it is the card that sets off the backfire (against the Khwan the plan has left it at that point)
        private static float CorruptionCost(int built, int gain, CombatStateData s, EnemyCardPlayStyle style)
        {
            if (gain <= 0 || style == EnemyCardPlayStyle.Random) return 0f;

            float cost = gain * CorruptionPointCost;
            int before = s.enemyCorruption + built;
            bool alreadyCounted = built > 0 && before >= s.enemyCorruptionThreshold;
            if (before + gain >= s.enemyCorruptionThreshold && !alreadyCounted)
            {
                // The backfire takes CurseBackfireKhwanLoss Khwan (never below 1), and kills a side already at 1
                if (s.enemyKhwan <= 1) return 1000f;
                int loss = Mathf.Min(EffectResolver.CurseBackfireKhwanLoss, s.enemyKhwan - 1);
                cost += loss * (style == EnemyCardPlayStyle.Aggressive ? 1.5f : 2.5f);
            }
            return cost;
        }
    }
}
