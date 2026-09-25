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
        private const int BlindedMissPercent = 50;
        private const int FireBreakDamagePerStack = 2;
        private const int CommonFamiliarMaxCost = 3;
        private const int DefaultStatusDuration = 2;

        // คริ: every familiar starts at 5%; a critical hit deals double damage
        public const float BaseCritPercent = 5f;
        public const int CritDamageMultiplier = 2;

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

        public static bool HasKeyword(CardInstance card, AbilityEffect keyword)
        {
            return card != null && card.abilities.Exists(a => a.trigger == AbilityTrigger.Passive && a.effect == keyword);
        }

        private static int KeywordValue(CardInstance card, AbilityEffect keyword)
        {
            int total = 0;
            if (card == null) return total;
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
            foreach (var s in card.statuses) if (CardStatus.IsDebuff(s.type)) n++;
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
            stacks = Mathf.Max(1, stacks);
            duration = duration > 0 ? duration : DefaultStatusDuration;

            var existing = card.statuses.Find(s => s.type == type);
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
        private bool CanStrike(CardInstance card)
        {
            return card != null && EffectiveAttack(card) > 0 && !HasCardStatus(card, CardStatusType.PhiAm);
        }

        // Base crit plus every CritChanceBonus (ผ้ายันต์มหาอุด) on the attacker's side of the board
        public float CritChancePercent(CardInstance attacker, bool fromPlayer)
        {
            if (attacker == null || attacker.cardType != CardType.Familiar) return 0f;

            float chance = BaseCritPercent;
            foreach (var c in BoardOf(fromPlayer))
            {
                if (c == null || c.IsDead) continue;
                chance += KeywordValue(c, AbilityEffect.CritChanceBonus) * 0.1f;
            }
            return chance;
        }

        // Decided before the strike is animated so the view can wind up for a critical hit
        public bool RollCrit(CardInstance attacker, bool fromPlayer)
        {
            return Random.Range(0f, 100f) < CritChancePercent(attacker, fromPlayer);
        }

        public bool CanFamiliarAttack(CardInstance card)
        {
            return IsAliveFamiliar(card) && CanStrike(card);
        }

        // ---------------------------------------------------------------- triggers

        public void TriggerAbilities(CardInstance card, AbilityTrigger trigger, bool ownerIsPlayer,
            CardInstance other = null, object explicitTarget = null)
        {
            if (card == null || card.abilities == null) return;

            foreach (var ability in card.abilities)
            {
                if (ability.trigger == trigger) ExecuteAbility(ability, card, ownerIsPlayer, other, explicitTarget);
            }
        }

        // Start of a side's turn: draw / merit amulets and the like
        public void TriggerTurnStart(bool isPlayer)
        {
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

            foreach (bool isPlayer in new[] { true, false })
            {
                foreach (var card in new List<CardInstance>(BoardOf(isPlayer)))
                {
                    if (!card.IsDead) TriggerAbilities(card, AbilityTrigger.OnRoundEnd, isPlayer);
                }
            }
            RemoveDeadFamiliars();
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

                    if (s.type == CardStatusType.DoneKhong)
                    {
                        DamageCard(card, s.stacks, ignoreArmor: true, source: null, sourceIsPlayer: !isPlayer);
                        s.stacks--;
                        if (s.stacks <= 0) card.statuses.Remove(s);
                        continue;
                    }

                    if (s.type == CardStatusType.FireBreak)
                    {
                        DamageCard(card, FireBreakDamagePerStack * s.stacks, ignoreArmor: true, source: null, sourceIsPlayer: !isPlayer);
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
            if (card.cardType != CardType.Incantation || card.abilities == null || card.abilities.Count == 0) return true;

            foreach (var a in card.abilities)
            {
                if (a.trigger != AbilityTrigger.OnPlay) continue;

                if (a.effect == AbilityEffect.ReturnFromGraveyard)
                {
                    if (!HasGraveyardCards(casterIsPlayer)) return false;
                }
                else if (a.effect == AbilityEffect.FlipOmens)
                {
                    if (PickFlipTarget(casterIsPlayer, out _) == null) return false;
                }
                else if (a.target == AbilityTarget.FriendlyCard || a.target == AbilityTarget.EnemyCard)
                {
                    var pool = BoardOf(a.target == AbilityTarget.FriendlyCard ? casterIsPlayer : !casterIsPlayer);
                    if (AutoPick(a, pool) == null) return false;
                }
            }
            return true;
        }

        // ---------------------------------------------------------------- targeting

        // The first OnPlay ability of an incantation that acts on one chosen card (FriendlyCard / EnemyCard).
        // null = the card needs no target from the player.
        public static CardAbility GetTargetedAbility(CardInstance card)
        {
            if (card == null || card.cardType != CardType.Incantation || card.abilities == null) return null;
            return card.abilities.Find(a => a.trigger == AbilityTrigger.OnPlay
                && (a.target == AbilityTarget.FriendlyCard || a.target == AbilityTarget.EnemyCard));
        }

        // Cards the caster may pick for that ability (the same rules the auto-pick uses)
        public List<CardInstance> GetValidTargets(CardInstance card, bool casterIsPlayer)
        {
            var result = new List<CardInstance>();
            var a = GetTargetedAbility(card);
            if (a == null) return result;

            var pool = BoardOf(a.target == AbilityTarget.FriendlyCard ? casterIsPlayer : !casterIsPlayer);
            foreach (var c in pool)
            {
                if (c != null && !c.IsDead && TryScore(a, c, out _)) result.Add(c);
            }
            return result;
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

                case AbilityEffect.GrantArmor:
                    if (!hasKhwan) return false;
                    score = -(c.familiarHealth + c.armor);
                    return true;

                case AbilityEffect.CleanseLatest:
                case AbilityEffect.CleanseAll:
                    int debuffs = CountDebuffs(c);
                    if (debuffs == 0) return false;
                    score = debuffs;
                    return true;

                case AbilityEffect.DestroyBelowKhwan:
                    if (!hasKhwan || c.familiarHealth >= a.value) return false;
                    score = c.familiarDamage;
                    return true;

                case AbilityEffect.DamageCard:
                    if (!hasKhwan) return false;
                    score = -c.familiarHealth;
                    return true;

                case AbilityEffect.ApplyStatus:
                    if (!hasKhwan) return false;
                    if (HasCardStatus(c, a.status)) score -= 50f;
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
                        result.Add(CardAt(own, sourceIndex - 1));
                        result.Add(CardAt(own, sourceIndex + 1));
                    }
                    break;

                case AbilityTarget.Opposite:
                    if (sourceIndex >= 0) result.Add(CardAt(foe, sourceIndex));
                    break;

                case AbilityTarget.Killer:
                    if (other != null) result.Add(other);
                    break;

                case AbilityTarget.FriendlyCard:
                case AbilityTarget.EnemyCard:
                    var pool = a.target == AbilityTarget.FriendlyCard ? own : foe;
                    CardInstance pick = null;
                    if (explicitTarget is CardInstance chosen && pool.Contains(chosen) && !chosen.IsDead && TryScore(a, chosen, out _))
                    {
                        pick = chosen;
                    }
                    if (pick == null) pick = AutoPick(a, pool);
                    if (pick != null) result.Add(pick);
                    break;
            }

            result.RemoveAll(c => c == null || c.IsDead);
            return result;
        }

        // ---------------------------------------------------------------- executing abilities

        private void ExecuteAbility(CardAbility a, CardInstance source, bool ownerIsPlayer, CardInstance other, object explicitTarget)
        {
            var combat = CombatManager.Instance;

            switch (a.effect)
            {
                // Keywords are read where they matter (strikes, targeting), never executed
                case AbilityEffect.Taunt:
                case AbilityEffect.Overhead:
                case AbilityEffect.OverheadMagnet:
                case AbilityEffect.MultiStrike:
                case AbilityEffect.AdjacentAttackAura:
                case AbilityEffect.ReflectDamage:
                case AbilityEffect.CritChanceBonus:
                    return;

                case AbilityEffect.RaiseCorruptionCap:
                    combat?.RaiseCorruptionThreshold(a.value, ownerIsPlayer);
                    return;

                case AbilityEffect.Purify:
                    combat?.AddPurify(Mathf.Max(1, a.value), ownerIsPlayer);
                    return;

                case AbilityEffect.DrawCards:
                    DrawFor(ownerIsPlayer, a.value);
                    return;

                case AbilityEffect.GainMerit:
                    if (ownerIsPlayer) combat?.AddMerit(a.value);
                    else combat?.AddEnemyMerit(a.value);
                    return;

                case AbilityEffect.SummonRandomFamiliar:
                    GiveToHand(ownerIsPlayer, PickRandomCommonFamiliar());
                    return;

                case AbilityEffect.ReturnFromGraveyard:
                    ReturnFromGraveyard(ownerIsPlayer);
                    return;

                case AbilityEffect.GiveCardToHand:
                    GiveToHand(ownerIsPlayer, a.relatedCard);
                    return;

                case AbilityEffect.ThreadFormation:
                    return; // an aura: RefreshAuras applies it while the copies are on the board

                case AbilityEffect.DetonateWithOpposite:
                    TryDetonate(source, ownerIsPlayer);
                    return;

                case AbilityEffect.FlipOmens:
                    FlipOmens(source, ownerIsPlayer);
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

                case AbilityEffect.GrantArmor:
                    target.armor += a.value;
                    break;

                case AbilityEffect.BuffAttack:
                    target.familiarDamage = Mathf.Max(0, target.familiarDamage + a.value);
                    break;

                case AbilityEffect.DamageCard:
                    DamageCard(target, a.value, a.ignoreArmor, source, ownerIsPlayer);
                    return;

                case AbilityEffect.DestroyBelowKhwan:
                    if (target.familiarHealth < a.value) DamageCard(target, target.familiarHealth, ignoreArmor: true, source, ownerIsPlayer);
                    return;

                case AbilityEffect.ApplyStatus:
                    // โดนของ: value = starting stacks, and it wears off by itself
                    AddCardStatus(target, a.status, a.value, a.status == CardStatusType.DoneKhong ? 99 : a.duration);
                    return;

                case AbilityEffect.CleanseLatest:
                    for (int i = target.statuses.Count - 1; i >= 0; i--)
                    {
                        if (!CardStatus.IsDebuff(target.statuses[i].type)) continue;
                        target.statuses[i].stacks--;
                        if (target.statuses[i].stacks <= 0) target.statuses.RemoveAt(i);
                        break;
                    }
                    break;

                case AbilityEffect.CleanseAll:
                    target.statuses.RemoveAll(s => CardStatus.IsDebuff(s.type));
                    break;
            }
            NotifyChanged(target);
        }

        // ---------------------------------------------------------------- damage / death

        // Damage to a card on the board: Protect and armor soak it first (unless ignored), Khwan takes the rest.
        // Dead cards stay on the board until RemoveDeadFamiliars so slots do not shift mid-clash.
        public int DamageCard(CardInstance target, int amount, bool ignoreArmor, CardInstance source, bool sourceIsPlayer)
        {
            if (target == null || target.IsDead || amount <= 0) return 0;

            if (!ignoreArmor)
            {
                foreach (var s in target.statuses)
                {
                    if (s.type == CardStatusType.Protect) amount -= s.stacks;
                }
                amount = Mathf.Max(0, amount);

                if (target.armor > 0)
                {
                    int absorbed = Mathf.Min(target.armor, amount);
                    target.armor -= absorbed;
                    amount -= absorbed;
                }
            }

            target.familiarHealth = Mathf.Max(0, target.familiarHealth - amount);
            if (source != null) target.lastAttacker = source;
            OnFamiliarDamaged?.Invoke(target, amount);

            if (target.IsDead) HandleDeath(target, source, sourceIsPlayer);

            // หุ่นพยนต์: the hit comes back on the attacker (no source, so two reflectors cannot loop)
            if (amount > 0 && source != null && source != target && (source.cardType == CardType.Familiar || source.maxKhwan > 0)
                && HasKeyword(target, AbilityEffect.ReflectDamage))
            {
                DamageCard(source, amount, ignoreArmor: true, source: null, sourceIsPlayer: !sourceIsPlayer);
            }
            return amount;
        }

        private void HandleDeath(CardInstance dead, CardInstance killer, bool killerIsPlayer)
        {
            if (dead.deathHandled) return;
            dead.deathHandled = true;
            OnFamiliarDied?.Invoke(dead);

            bool deadIsPlayer = TryFindCard(dead, out bool isPlayer, out _) ? isPlayer : !killerIsPlayer;
            TriggerAbilities(dead, AbilityTrigger.OnDeath, deadIsPlayer, killer);

            // A broken amulet's aura ends right away
            RefreshAuras();

            if (killer != null && !killer.IsDead)
            {
                TriggerAbilities(killer, AbilityTrigger.OnKill, killerIsPlayer, dead);
                NotifyChanged(killer);
            }
        }

        // ---------------------------------------------------------------- strikes

        // Who a familiar in column `col` hits: null = the opposing player.
        // Overhead skips the cards (only a magnet like เปรต can catch it), taunt pulls every hit onto
        // itself, otherwise it is the live familiar (or amulet with Khwan) in the opposite slot.
        public CardInstance ChooseStrikeTarget(CardInstance attacker, int col, bool fromPlayer)
        {
            var defenders = BoardOf(!fromPlayer);

            if (HasKeyword(attacker, AbilityEffect.Overhead))
            {
                return defenders.Find(c => !c.IsDead && HasKeyword(c, AbilityEffect.OverheadMagnet));
            }

            var taunt = defenders.Find(c => !c.IsDead && c.maxKhwan > 0 && HasTaunt(c));
            if (taunt != null) return taunt;

            var opposite = CardAt(defenders, col);
            return IsStrikeable(opposite) ? opposite : null;
        }

        public int BoardIndexOf(CardInstance card, bool onPlayerSide)
        {
            return card != null && BoardOf(onPlayerSide).Contains(card) ? card.boardSlot : -1;
        }

        private List<CardInstance> CollectStrikeTargets(CardInstance attacker, CardInstance primary, bool fromPlayer)
        {
            var targets = new List<CardInstance>();
            int hits = Mathf.Max(1, KeywordValue(attacker, AbilityEffect.MultiStrike));
            var defenders = BoardOf(!fromPlayer);

            if (primary != null)
            {
                targets.Add(primary);
                if (hits > 1)
                {
                    int idx = defenders.Contains(primary) ? primary.boardSlot : -1;
                    for (int d = -1; d <= 1; d += 2)
                    {
                        var beside = idx >= 0 ? CardAt(defenders, idx + d) : null;
                        if (IsStrikeable(beside) && targets.Count < hits) targets.Add(beside);
                    }
                }
            }
            else if (hits > 1 && TryFindCard(attacker, out _, out int col))
            {
                // Nothing straight ahead: sweep the columns in front (left, centre, right)
                for (int d = -1; d <= 1 && targets.Count < hits; d++)
                {
                    var ahead = CardAt(defenders, col + d);
                    if (IsStrikeable(ahead)) targets.Add(ahead);
                }
            }
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

        private CardDataSO PickRandomCommonFamiliar()
        {
            var pool = new List<CardDataSO>();
            if (catalog != null)
            {
                foreach (var c in catalog.cards)
                {
                    if (c != null && c.cardType == CardType.Familiar && c.meritCost <= CommonFamiliarMaxCost) pool.Add(c);
                }
            }
            if (pool.Count == 0) return null;
            return pool[Random.Range(0, pool.Count)];
        }

        private bool HasGraveyardCards(bool isPlayer)
        {
            if (isPlayer) return CardManager.Instance != null && CardManager.Instance.DiscardPile.Count > 0;
            return CombatManager.Instance != null && CombatManager.Instance.EnemyCards.DiscardCount > 0;
        }

        private void ReturnFromGraveyard(bool isPlayer)
        {
            if (isPlayer) CardManager.Instance?.ReturnRandomFromDiscard();
            else CombatManager.Instance?.EnemyCards.ReturnRandomFromDiscard();
        }

        private void SendToGraveyard(CardInstance card, bool isPlayer)
        {
            if (isPlayer) CardManager.Instance?.SendToGraveyard(card);
            else CombatManager.Instance?.EnemyCards.SendToGraveyard(card);
        }

        // ด้ายแดงผูกจิต: blows itself up together with the enemy card in the same column
        // As an OnDeath ability (the card was just destroyed) it only takes the opposite card down with it.
        private void TryDetonate(CardInstance source, bool ownerIsPlayer)
        {
            if (source == null) return;
            if (!TryFindCard(source, out _, out int index)) return;

            var victim = CardAt(BoardOf(!ownerIsPlayer), index);
            if (victim == null || victim.IsDead) return;

            if (source.IsDead)
            {
                Debug.Log($"[Ability] {source.cardNameThai} was destroyed and takes {victim.cardNameThai} with it");
                if (victim.maxKhwan > 0 || victim.cardType == CardType.Familiar)
                {
                    DamageCard(victim, Mathf.Max(1, victim.familiarHealth), ignoreArmor: true, source, ownerIsPlayer);
                }
                else
                {
                    victim.pendingRemoval = true;
                    HandleDeath(victim, source, ownerIsPlayer);
                }
                return;
            }

            DamageCard(victim, Mathf.Max(1, victim.familiarHealth), ignoreArmor: true, source, ownerIsPlayer);
            source.pendingRemoval = true;
            HandleDeath(source, victim, !ownerIsPlayer);
        }

        // ผ้ายันต์กลับด้าน: our card with debuffs -> they become blessings; otherwise an enemy card's blessings -> debuffs
        private CardInstance PickFlipTarget(bool ownerIsPlayer, out bool toBlessing)
        {
            CardInstance ally = null;
            int allyDebuffs = 0;
            foreach (var c in BoardOf(ownerIsPlayer))
            {
                int n = c.IsDead ? 0 : CountDebuffs(c);
                if (n > allyDebuffs)
                {
                    allyDebuffs = n;
                    ally = c;
                }
            }
            if (ally != null)
            {
                toBlessing = true;
                return ally;
            }

            toBlessing = false;
            CardInstance enemy = null;
            int enemyBlessings = 0;
            foreach (var c in BoardOf(!ownerIsPlayer))
            {
                int n = c.IsDead ? 0 : CountBlessings(c);
                if (n > enemyBlessings)
                {
                    enemyBlessings = n;
                    enemy = c;
                }
            }
            return enemy;
        }

        // Each flip is remembered so it can be turned back when the amulet leaves the board (RefreshAuras)
        private void FlipOmens(CardInstance source, bool ownerIsPlayer)
        {
            var target = PickFlipTarget(ownerIsPlayer, out bool toBlessing);
            if (target == null) return;

            foreach (var s in target.statuses)
            {
                var original = s.type;
                if (toBlessing && CardStatus.IsDebuff(s.type)) s.type = CardStatus.ToBlessing(s.type);
                else if (!toBlessing && CardStatus.IsBlessing(s.type)) s.type = CardStatus.ToDebuff(s.type);
                else continue;

                if (source != null) activeFlips.Add(new StatusFlip { source = source, target = target, status = s, original = original });
            }
            NotifyChanged(target);
        }
    }
}
