using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Card abilities (CardAbility lists on CardDataSO), per-card statuses, armor and the combat rules
    // that depend on them (taunt, overhead strikes, multi strikes, kill/death triggers).
    // Targets are picked automatically because the game has no target-selection UI yet; an explicit
    // CardInstance passed to PlayCard is honoured for single-target abilities.
    public partial class EffectResolver
    {
        private const int FireBreakDamagePerStack = 2;
        private const int CommonFamiliarMaxCost = 3;
        private const int DefaultStatusDuration = 2;

        private CardCatalogSO catalog;

        // ---------------------------------------------------------------- helpers

        private static List<CardInstance> BoardOf(bool player)
        {
            var combat = CombatManager.Instance;
            if (combat == null) return new List<CardInstance>();
            return player ? combat.State.activeBoardCards : combat.State.enemyBoardCards;
        }

        private static bool TryFindCard(CardInstance card, out bool isPlayer, out int index)
        {
            isPlayer = false;
            index = -1;
            var combat = CombatManager.Instance;
            if (combat == null || card == null) return false;

            if (combat.State.activeBoardCards.Contains(card))
            {
                isPlayer = true;
                index = card.boardSlot;
                return true;
            }

            if (!combat.State.enemyBoardCards.Contains(card)) return false;
            index = card.boardSlot;
            return true;
        }

        // Fires the card-changed refresh that the board views already listen to
        private void NotifyChanged(CardInstance card)
        {
            OnFamiliarDamaged?.Invoke(card, 0);
        }

        public static bool HasCardStatus(CardInstance card, CardStatusType type)
        {
            return card != null && card.statuses.Exists(s => s.type == type);
        }

        // ผีบังตา: while a card has it, none of its abilities work (triggers, keywords, auras); it still strikes
        public static bool AbilitiesBlocked(CardInstance card)
        {
            return HasCardStatus(card, CardStatusType.Blinded);
        }

        public static bool HasKeyword(CardInstance card, AbilityEffect keyword)
        {
            return card != null && !AbilitiesBlocked(card) && card.abilities.Exists(a => a.trigger == AbilityTrigger.Passive && a.effect == keyword);
        }

        private static int KeywordValue(CardInstance card, AbilityEffect keyword)
        {
            int total = 0;
            if (card == null || AbilitiesBlocked(card)) return total;
            foreach (var a in card.abilities)
            {
                if (a.trigger == AbilityTrigger.Passive && a.effect == keyword) total += a.value;
            }
            return total;
        }

        public static bool HasTaunt(CardInstance card)
        {
            return HasKeyword(card, AbilityEffect.Taunt) || HasCardStatus(card, CardStatusType.Taunt);
        }

        private static int CountDebuffs(CardInstance card)
        {
            int n = 0;
            foreach (var s in card.statuses) if (CardStatus.IsDebuff(s.type) && !s.fromAura) n++;
            return n;
        }

        private static int CountBlessings(CardInstance card)
        {
            int n = 0;
            foreach (var s in card.statuses) if (CardStatus.IsBlessing(s.type)) n++;
            return n;
        }

        public void AddCardStatus(CardInstance card, CardStatusType type, int stacks, int duration)
        {
            if (card == null || card.IsDead) return;
            if (BlocksHarm(card, type)) return;
            stacks = Mathf.Max(1, stacks);
            duration = duration > 0 ? duration : DefaultStatusDuration;

            var existing = card.statuses.Find(s => s.type == type && !s.fromAura);
            if (existing != null)
            {
                existing.stacks += stacks;
                existing.duration = Mathf.Max(existing.duration, duration);
            }
            else
            {
                card.statuses.Add(new CardStatus(type, stacks, duration));
            }
            NotifyChanged(card);
            if (type == CardStatusType.Blinded) RefreshAuras(); // its own auras stop
        }

        // Attack after permanent buffs, status effects and the adjacent-attack aura (ตะกรุดดำ)
        public int EffectiveAttack(CardInstance card)
        {
            if (card == null) return 0;

            int atk = card.familiarDamage;
            foreach (var s in card.statuses)
            {
                if (s.type == CardStatusType.Phawa) atk -= s.stacks;
                else if (s.type == CardStatusType.Might) atk += s.stacks;
            }

            if (TryFindCard(card, out bool isPlayer, out int index))
            {
                var board = BoardOf(isPlayer);
                for (int d = -1; d <= 1; d += 2)
                {
                    int j = index + d;
                    var neighbour = CardAt(board, j);
                    if (neighbour == null || neighbour.IsDead) continue;
                    atk += KeywordValue(neighbour, AbilityEffect.AdjacentAttackAura);
                }
            }
            return Mathf.Max(0, atk);
        }

        // A card may strike this round (dead attackers are allowed: head-on trades hit at the same moment)
        // Whether this card will strike in the next clash (the enemy bot reads it to see what is coming)
        public bool WillStrike(CardInstance card)
        {
            return CanStrike(card) && !card.IsDead && IsStrikerType(card);
        }

        private bool CanStrike(CardInstance card)
        {
            return card != null && !card.summonedThisRound && EffectiveAttack(card) > 0 && !HasCardStatus(card, CardStatusType.PhiAm);
        }

        public bool CanFamiliarAttack(CardInstance card)
        {
            return IsStrikerType(card) && CanStrike(card);
        }

        // ---------------------------------------------------------------- triggers

        public void TriggerAbilities(CardInstance card, AbilityTrigger trigger, bool ownerIsPlayer,
            CardInstance other = null, object explicitTarget = null)
        {
            if (card == null || card.abilities == null) return;
            if (AbilitiesBlocked(card))
            {
                if (card.abilities.Exists(a => a.trigger == trigger)) Debug.Log($"[Ability] {card.cardNameThai} has ผีบังตา: its {trigger} abilities do nothing");
                return;
            }

            foreach (var ability in card.abilities)
            {
                if (ability.trigger == trigger) ExecuteAbility(ability, card, ownerIsPlayer, other, explicitTarget);
            }
        }

        // Start of a side's turn: draw / merit amulets and the like
        public void TriggerTurnStart(bool isPlayer)
        {
            Sheet7TurnStart(isPlayer);
            foreach (var card in new List<CardInstance>(BoardOf(isPlayer)))
            {
                if (!card.IsDead) TriggerAbilities(card, AbilityTrigger.OnTurnStart, isPlayer);
            }
            RemoveDeadFamiliars();
        }

        // End of every round: status ticks (โดนของ, ไฟแตก...), then round-end abilities
        public void TickCardStatuses()
        {
            if (CombatManager.Instance == null) return;

            TickBoardStatuses(true);
            TickBoardStatuses(false);

            // Familiars summoned this round may strike from the next one
            foreach (bool side in new[] { true, false })
            {
                foreach (var card in BoardOf(side)) card.summonedThisRound = false;
            }

            Sheet7RoundEnd(true);
            Sheet7RoundEnd(false);

            foreach (bool isPlayer in new[] { true, false })
            {
                foreach (var card in new List<CardInstance>(BoardOf(isPlayer)))
                {
                    if (!card.IsDead) TriggerAbilities(card, AbilityTrigger.OnRoundEnd, isPlayer);
                }
            }
            RemoveDeadFamiliars();
            RefreshAuras(); // a ผีบังตา that wore off gives the card its auras back
        }

        private void TickBoardStatuses(bool isPlayer)
        {
            foreach (var card in new List<CardInstance>(BoardOf(isPlayer)))
            {
                if (card.IsDead) continue;

                for (int i = card.statuses.Count - 1; i >= 0; i--)
                {
                    if (i >= card.statuses.Count) continue;
                    var s = card.statuses[i];
                    if (s.fromAura) continue; // lasts while its aura's card is on the board

                    if (s.type == CardStatusType.DoneKhong)
                    {
                        DamageCard(card, s.stacks, ignoreArmor: true, source: null, sourceIsPlayer: !isPlayer, cause: $"สาป {s.stacks}");
                        if (card.IsDead) { CurseKilled(isPlayer); break; }
                        if (!CurseFrozen(isPlayer)) s.stacks--; // หัวกะโหลก on the other side keeps it
                        if (s.stacks <= 0) card.statuses.Remove(s);
                        continue;
                    }

                    if (s.type == CardStatusType.FireBreak)
                    {
                        DamageCard(card, FireBreakDamagePerStack * s.stacks, ignoreArmor: true, source: null, sourceIsPlayer: !isPlayer, cause: "ไฟแตก");
                    }
                    else if (s.type == CardStatusType.Vital && card.maxKhwan > 0)
                    {
                        card.familiarHealth = Mathf.Min(card.maxKhwan, card.familiarHealth + s.stacks);
                    }

                    s.duration--;
                    if (s.duration <= 0) card.statuses.Remove(s);
                }
                NotifyChanged(card);
            }
        }

        // ---------------------------------------------------------------- playability

        // False when an incantation has nothing to act on (e.g. a heal with no card on the board),
        // so the card stays in hand and nothing is spent.
        public bool CanResolve(CardInstance card, bool casterIsPlayer)
        {
            if (card == null) return false;

            // Four familiars fill the board; an amulet always fits (it replaces the one in the amulet slot)
            if (card.cardType == CardType.Familiar && IsBoardFull(casterIsPlayer)) return false;

            if (card.cardType != CardType.Incantation || card.abilities == null || card.abilities.Count == 0) return true;

            foreach (var a in card.abilities)
            {
                if (a.trigger != AbilityTrigger.OnPlay) continue;

                if (IsSheet7Choice(a.effect))
                {
                    if (Sheet7Pool(a, null, casterIsPlayer).Count == 0) return false;
                }
                else if (a.effect == AbilityEffect.SummonRelated)
                {
                    if (IsBoardFull(casterIsPlayer)) return false; // nowhere to summon into
                }
                else if (IsChosenCardTarget(a.target))
                {
                    if (AutoPickFor(a, casterIsPlayer) == null) return false;
                }
            }
            return true;
        }

        // ---------------------------------------------------------------- targeting

        // The first OnPlay ability that acts on one card the player picks: an incantation's FriendlyCard / EnemyCard /
        // AnyCard, or any card's FlipOmens (ผ้ายันต์กลับด้าน, an amulet: picked after its slot).
        // null = the card needs no target from the player.
        public static CardAbility GetTargetedAbility(CardInstance card)
        {
            if (card == null || card.abilities == null) return null;
            return card.abilities.Find(a => a.trigger == AbilityTrigger.OnPlay
                && (IsSheet7Choice(a.effect)
                    || (card.cardType == CardType.Incantation && IsChosenCardTarget(a.target))));
        }

        private static bool IsChosenCardTarget(AbilityTarget t)
        {
            return t == AbilityTarget.FriendlyCard || t == AbilityTarget.EnemyCard || t == AbilityTarget.AnyCard;
        }

        // The cards a FriendlyCard / EnemyCard / AnyCard ability may land on
        private List<CardInstance> ChosenCardPool(CardAbility a, bool ownerIsPlayer)
        {
            if (a.target == AbilityTarget.FriendlyCard) return BoardOf(ownerIsPlayer);
            if (a.target == AbilityTarget.EnemyCard) return BoardOf(!ownerIsPlayer);
            var all = new List<CardInstance>(BoardOf(ownerIsPlayer));
            all.AddRange(BoardOf(!ownerIsPlayer));
            return all;
        }

        // The game's own pick. AnyCard looks first on the side the effect is meant for: harm goes to the other side,
        // help (ยั่วยุ, a heal) to our own
        private CardInstance AutoPickFor(CardAbility a, bool ownerIsPlayer)
        {
            if (a.target != AbilityTarget.AnyCard) return AutoPick(a, ChosenCardPool(a, ownerIsPlayer));
            bool harms = a.effect == AbilityEffect.DamageCard
                || (a.effect == AbilityEffect.ApplyStatus && CardStatus.IsDebuff(a.status));
            return AutoPick(a, BoardOf(harms ? !ownerIsPlayer : ownerIsPlayer))
                ?? AutoPick(a, BoardOf(harms ? ownerIsPlayer : !ownerIsPlayer));
        }

        // Cards the caster may pick for that ability (the same rules the auto-pick uses)
        public List<CardInstance> GetValidTargets(CardInstance card, bool casterIsPlayer)
        {
            var result = new List<CardInstance>();
            var a = GetTargetedAbility(card);
            if (a == null) return result;

            if (IsSheet7Choice(a.effect)) return Sheet7Pool(a, null, casterIsPlayer);

            foreach (var c in ChosenCardPool(a, casterIsPlayer))
            {
                if (c != null && !c.IsDead && TryScore(a, c, out _)) result.Add(c);
            }
            return result;
        }

        // The card an incantation would act on if the caster lets the game choose (the enemy always does).
        // null = the card needs no chosen target, or nothing on the board fits.
        public CardInstance PickTarget(CardInstance card, bool casterIsPlayer)
        {
            var a = GetTargetedAbility(card);
            if (a == null) return null;
            if (IsSheet7Choice(a.effect)) return PickedOrAuto(a, null, casterIsPlayer, null);
            return AutoPickFor(a, casterIsPlayer);
        }

        private CardInstance AutoPick(CardAbility a, List<CardInstance> pool)
        {
            CardInstance best = null;
            float bestScore = float.MinValue;
            foreach (var c in pool)
            {
                if (c == null || c.IsDead) continue;
                if (!TryScore(a, c, out float score)) continue;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }
            return best;
        }

        private static bool TryScore(CardAbility a, CardInstance c, out float score)
        {
            score = 0f;
            bool hasKhwan = c.maxKhwan > 0 || c.cardType == CardType.Familiar;

            switch (a.effect)
            {
                case AbilityEffect.HealKhwan:
                    if (!hasKhwan) return false;
                    score = (c.maxKhwan - c.familiarHealth) + c.familiarDamage * 0.1f;
                    return true;

                case AbilityEffect.BuffKhwan:
                    if (!hasKhwan) return false;
                    score = c.familiarDamage * 2f - c.familiarHealth * 0.1f;
                    return true;

                case AbilityEffect.CleanseAll:
                    int debuffs = CountDebuffs(c);
                    if (debuffs == 0) return false;
                    score = debuffs;
                    return true;

                case AbilityEffect.DamageCard:
                    if (!hasKhwan) return false;
                    score = -c.familiarHealth;
                    return true;

                case AbilityEffect.ApplyStatus:
                    if (!hasKhwan) return false;
                    // ผวา lowers สะเทือนขวัญ and its card says บริวาร; ผีบังตา switches off a card's abilities, so it takes an
                    // amulet too (ตุ๊กตาคุณไสย, ตะกรุดดำ, หุ่นพยนต์ live on their abilities)
                    if (a.status == CardStatusType.Phawa && c.cardType != CardType.Familiar) return false;
                    if (!CardStatus.IsDebuff(a.status) && a.status != CardStatusType.DoneKhong && c.cardType != CardType.Familiar) return false;
                    if (HasCardStatus(c, a.status)) score -= 50f;
                    if (a.status == CardStatusType.Blinded)
                        score += c.familiarDamage * 2f + (c.abilities != null ? c.abilities.Count * 3f : 0f); // the card with most to lose
                    else
                        score += CardStatus.IsDebuff(a.status)
                            ? c.familiarDamage * 2f + c.familiarHealth * 0.1f   // hinder the most dangerous card
                            : c.familiarHealth;                                 // taunt/blessing on the sturdiest card
                    return true;

                default:
                    return true;
            }
        }

        private List<CardInstance> ResolveTargets(CardAbility a, CardInstance source, bool ownerIsPlayer,
            CardInstance other, object explicitTarget)
        {
            var result = new List<CardInstance>();
            var own = BoardOf(ownerIsPlayer);
            var foe = BoardOf(!ownerIsPlayer);
            int sourceIndex = source != null && own.Contains(source) ? source.boardSlot : -1;

            switch (a.target)
            {
                case AbilityTarget.Self:
                    if (source != null) result.Add(source);
                    break;

                case AbilityTarget.AllFriendly:
                    result.AddRange(own);
                    break;

                case AbilityTarget.AllFriendlyExceptSelf:
                    foreach (var c in own) if (c != source) result.Add(c);
                    break;

                case AbilityTarget.AllEnemy:
                    result.AddRange(foe);
                    break;

                case AbilityTarget.AllEnemyFamiliars:
                    foreach (var c in foe) if (c.cardType == CardType.Familiar) result.Add(c);
                    break;

                case AbilityTarget.Adjacent:
                    if (sourceIndex >= 0)
                    {
                        // The amulet slot is not a neighbour of the first familiar column
                        if (IsFamiliarSlot(sourceIndex - 1)) result.Add(CardAt(own, sourceIndex - 1));
                        if (IsFamiliarSlot(sourceIndex + 1)) result.Add(CardAt(own, sourceIndex + 1));
                    }
                    break;

                case AbilityTarget.Opposite:
                    if (sourceIndex >= 0) result.Add(CardAt(foe, sourceIndex));
                    break;

                case AbilityTarget.Killer:
                case AbilityTarget.StruckCard:
                    if (other != null) result.Add(other);
                    break;

                case AbilityTarget.AllOnBoard:
                    result.AddRange(own);
                    result.AddRange(foe);
                    break;

                case AbilityTarget.FriendlyCard:
                case AbilityTarget.EnemyCard:
                case AbilityTarget.AnyCard:
                    var pool = ChosenCardPool(a, ownerIsPlayer);
                    CardInstance pick = null;
                    if (explicitTarget is CardInstance chosen && pool.Contains(chosen) && !chosen.IsDead && TryScore(a, chosen, out _))
                    {
                        pick = chosen;
                    }
                    if (pick == null) pick = AutoPickFor(a, ownerIsPlayer);
                    if (pick != null) result.Add(pick);
                    break;
            }

            result.RemoveAll(c => c == null || c.IsDead);
            return result;
        }

        // ---------------------------------------------------------------- executing abilities

        // Runs an ability, noting which cards it is used on; anything triggered (passive keywords are read where they
        // matter) is then shown in the skill feed, with those cards
        private void ExecuteAbility(CardAbility a, CardInstance source, bool ownerIsPlayer, CardInstance other, object explicitTarget)
        {
            var outer = skillTargets;
            var targets = new List<CardInstance>();
            skillTargets = targets;
            try { ExecuteAbilityCore(a, source, ownerIsPlayer, other, explicitTarget); }
            finally
            {
                skillTargets = outer;
                if (targets.Count == 0 && explicitTarget is CardInstance picked) targets.Add(picked);
                if (a.trigger != AbilityTrigger.Passive) ShowSkill(source, ownerIsPlayer, a, null, targets);
            }
        }

        private void ExecuteAbilityCore(CardAbility a, CardInstance source, bool ownerIsPlayer, CardInstance other, object explicitTarget)
        {
            var combat = CombatManager.Instance;
            if (ExecuteSheet7(a, source, ownerIsPlayer, other, explicitTarget)) return;

            switch (a.effect)
            {
                // Keywords are read where they matter (strikes, targeting), never executed
                case AbilityEffect.Taunt:
                case AbilityEffect.Overhead:
                case AbilityEffect.OverheadMagnet:
                case AbilityEffect.AdjacentAttackAura:
                    return;

                case AbilityEffect.DrawCards:
                    DrawFor(ownerIsPlayer, a.value);
                    return;

                case AbilityEffect.GiveCardToHand:
                    GiveToHand(ownerIsPlayer, a.relatedCard);
                    return;
            }

            var targets = ResolveTargets(a, source, ownerIsPlayer, other, explicitTarget);
            if (targets.Count == 0) Debug.Log($"[Ability] {source?.cardNameThai}: {a.effect} found no target");
            foreach (var target in targets)
            {
                Debug.Log($"[Ability] {source?.cardNameThai} -> {target.cardNameThai}: {a.effect} {a.value}");
                ApplyToCard(a, target, source, ownerIsPlayer);
            }
        }

        private void ApplyToCard(CardAbility a, CardInstance target, CardInstance source, bool ownerIsPlayer)
        {
            if (skillTargets != null && target != null && !skillTargets.Contains(target)) skillTargets.Add(target);
            switch (a.effect)
            {
                case AbilityEffect.BuffKhwan:
                    if (target.maxKhwan <= 0 && target.cardType != CardType.Familiar) return;
                    target.maxKhwan += a.value;
                    target.familiarHealth += a.value;
                    break;

                case AbilityEffect.HealKhwan:
                    if (target.maxKhwan <= 0) return;
                    target.familiarHealth = Mathf.Min(target.maxKhwan, target.familiarHealth + a.value);
                    break;

                case AbilityEffect.BuffAttack:
                    if (target.cardType == CardType.Amulet) return; // an amulet with attack would start to strike
                    target.familiarDamage = Mathf.Max(0, target.familiarDamage + a.value);
                    break;

                case AbilityEffect.DamageCard:
                    DamageCard(target, a.value, a.ignoreArmor, source, ownerIsPlayer, cause: $"{source?.cardNameThai}'s {a.trigger} skill");
                    return;

                case AbilityEffect.ApplyStatus:
                    // โดนของ: value = starting stacks, and it wears off by itself. เกราะคุ้มภัย lasts until it blocks a hit.
                    bool untilUsed = a.status == CardStatusType.DoneKhong || a.status == CardStatusType.Protect;
                    AddCardStatus(target, a.status, a.value, untilUsed ? 99 : a.duration);
                    return;

                case AbilityEffect.CleanseAll:
                    target.statuses.RemoveAll(s => CardStatus.IsHarmful(s.type) && !s.fromAura); // สาป and ผีอำ too (sheet 7)
                    break;
            }
            NotifyChanged(target);
            if (a.effect == AbilityEffect.CleanseAll) RefreshAuras();
        }

        // ---------------------------------------------------------------- damage / death

        // Damage to a card on the board: เกราะคุ้มภัย blocks the whole hit (one stack per hit), then armor soaks it
        // (both skipped when armor is ignored), Khwan takes the rest. หุ่นพยนต์ sends the whole hit back either way.
        // Dead cards stay on the board until RemoveDeadFamiliars so slots do not shift mid-clash.
        // cause: what the damage is, for the death log (null = `source`'s skill)
        public int DamageCard(CardInstance target, int amount, bool ignoreArmor, CardInstance source, bool sourceIsPlayer,
            string cause = null)
        {
            if (target == null || target.IsDead || amount <= 0) return 0;
            int incoming = amount;

            if (!ignoreArmor)
            {
                if (ProtectBlocks(target, source))
                {
                    Debug.Log($"[Ability] {target.cardNameThai}'s เกราะคุ้มภัย blocks {amount} damage");
                    return 0;
                }

                if (target.armor > 0)
                {
                    int absorbed = Mathf.Min(target.armor, amount);
                    target.armor -= absorbed;
                    amount -= absorbed;
                }
            }

            target.familiarHealth = Mathf.Max(0, target.familiarHealth - amount);
            TrySaveFromDeath(target); // มหาอุด
            if (source != null) target.lastAttacker = source;
            OnFamiliarDamaged?.Invoke(target, amount);

            if (target.IsDead)
                HandleDeath(target, source, sourceIsPlayer, $"{cause ?? (source != null ? $"{source.cardNameThai}'s skill" : "unknown")}, {amount} damage");

            return amount;
        }

        // cause: what killed it, for the log ("[Death] กุมารทอง [s_wf05] (player, column 2) died: hit by เปรต, 4 damage")
        private void HandleDeath(CardInstance dead, CardInstance killer, bool killerIsPlayer, string cause)
        {
            if (dead.deathHandled) return;
            dead.deathHandled = true;

            bool deadIsPlayer = TryFindCard(dead, out bool isPlayer, out _) ? isPlayer : !killerIsPlayer;
            Debug.Log($"[Death] {dead.cardNameThai} [{dead.cardId}] ({(deadIsPlayer ? "player" : "enemy")}, column {dead.boardSlot}) died: {cause}");
            OnFamiliarDied?.Invoke(dead);
            TriggerAbilities(dead, AbilityTrigger.OnDeath, deadIsPlayer, killer);
            Sheet7AllyDied(dead, deadIsPlayer);

            // A broken amulet's aura ends right away
            RefreshAuras();

            if (killer != null && !killer.IsDead)
            {
                TriggerAbilities(killer, AbilityTrigger.OnKill, killerIsPlayer, dead);
                NotifyChanged(killer);
            }
        }

        // เกราะคุ้มภัย: one stack blocks one hit, and with it everything else `source` does to `target` in the same turn.
        // In a head-on trade the two cards strike at once, so ด้ายแดงผูกจิต's own strike and its explosion as it dies
        // would otherwise take two stacks, and a card with one stack would die in spite of it.
        private static bool ProtectBlocks(CardInstance target, CardInstance source)
        {
            int turn = CombatManager.Instance != null ? CombatManager.Instance.State.turnNumber : 0;
            if (source != null && target.protectedFrom == source && target.protectedTurn == turn) return true;

            var protect = target.statuses.Find(s => s.type == CardStatusType.Protect && s.stacks > 0);
            if (protect == null) return false;
            protect.stacks--;
            if (protect.stacks <= 0) target.statuses.Remove(protect);
            target.protectedFrom = source;
            target.protectedTurn = turn;
            Instance?.NotifyChanged(target);
            return true;
        }

        // ---------------------------------------------------------------- strikes

        // Who a familiar in column `col` hits: null = the opposing player.
        // Overhead skips the cards (only a magnet like เปรต in the same lane can catch it), taunt pulls every other hit onto
        // itself, otherwise it is the live familiar (or amulet with Khwan) in the opposite slot.
        public CardInstance ChooseStrikeTarget(CardInstance attacker, int col, bool fromPlayer)
        {
            var defenders = BoardOf(!fromPlayer);

            if (HasKeyword(attacker, AbilityEffect.Overhead) && !SideBlocksFlying(!fromPlayer))
            {
                // ดักบิน catches a flyer only in its own lane (Game Designer, 25 Oct); elsewhere it flies over
                return defenders.Find(c => !c.IsDead && HasKeyword(c, AbilityEffect.OverheadMagnet) && c.boardSlot == col);
            }

            var taunt = defenders.Find(c => IsStrikeable(c) && HasTaunt(c));
            if (taunt != null) return taunt;

            var opposite = CardAt(defenders, col);
            return IsStrikeable(opposite) ? opposite : null;
        }

        public int BoardIndexOf(CardInstance card, bool onPlayerSide)
        {
            return card != null && BoardOf(onPlayerSide).Contains(card) ? card.boardSlot : -1;
        }

        // The cards a strike lands on: its target, or none when it goes to the opposing player's Khwan.
        // faceHits stays 0 now that no card hits several columns (sheet 7).
        private List<CardInstance> CollectStrikeTargets(CardInstance attacker, CardInstance primary, bool fromPlayer, out int faceHits)
        {
            faceHits = 0;
            var targets = new List<CardInstance>();
            if (primary != null) targets.Add(primary);
            return targets;
        }

        // ---------------------------------------------------------------- side / hand effects

        private void DrawFor(bool isPlayer, int count)
        {
            if (count <= 0) return;
            if (isPlayer) CardManager.Instance?.DrawCards(count);
            else CombatManager.Instance?.EnemyCards.DrawExtra(count);
        }

        private void GiveToHand(bool isPlayer, CardDataSO template)
        {
            if (template == null) return;
            var card = new CardInstance(template);
            if (isPlayer) CardManager.Instance?.AddToHand(card);
            else CombatManager.Instance?.EnemyCards.AddToHand(card);
        }

        private void SendToGraveyard(CardInstance card, bool isPlayer)
        {
            if (isPlayer) CardManager.Instance?.SendToGraveyard(card);
            else CombatManager.Instance?.EnemyCards.SendToGraveyard(card);
        }
    }
}
