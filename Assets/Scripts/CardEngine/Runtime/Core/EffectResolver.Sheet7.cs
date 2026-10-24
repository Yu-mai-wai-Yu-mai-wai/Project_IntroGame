using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Card sheet 7 (Narathorn, 21 Oct 2026; Docs/Plan/HANDOFF_sheet7_port.md). Both sides use the same rules.
    //  - Heal school: "ฮีล" restores the owner's Khwan (never past max); cards that react to a heal count the
    //    full amount (Game Designer, 22 Oct). ลูกประคำ adds to every heal, พระเครื่อง hurts the enemy, ผีแม่ย่านาง strikes.
    //  - Death: ตะกรุดดำ, หุ่นพยนต์, เปรต react to an ally dying; sacrifice, revive (exiled on a second death),
    //    return to hand.
    //  - สาป (CardStatusType.DoneKhong): only hurts the familiar it is on; when that familiar dies the rest of it is
    //    gone (no Khwan off its owner: that rule came from the port handoff, not the designer; taken out 22 Oct).
    //  - Amulets that break after a number of uses (usesLeft), ด้ายแดงผูกจิต's linked lane.
    public partial class EffectResolver
    {
        private readonly bool[] healedThisTurn = new bool[2];
        private readonly List<CardDataSO>[] recentDead = { new List<CardDataSO>(), new List<CardDataSO>() };

        private static int Side(bool isPlayer) => isPlayer ? 0 : 1;
        private static int CurrentTurn => CombatManager.Instance != null ? CombatManager.Instance.State.turnNumber : 0;

        public void ResetSheet7State()
        {
            healedThisTurn[0] = healedThisTurn[1] = false;
            recentDead[0].Clear();
            recentDead[1].Clear();
        }

        // Start of a side's turn: per-turn limits start over
        private void Sheet7TurnStart(bool isPlayer)
        {
            healedThisTurn[Side(isPlayer)] = false;
        }

        // A card's "this turn" counter, cleared the first time it is used on a new turn
        private static int UsedThisTurn(CardInstance card)
        {
            if (card.usedOnTurn != CurrentTurn) { card.usedOnTurn = CurrentTurn; card.usedThisTurn = 0; }
            return card.usedThisTurn;
        }

        private static IEnumerable<CardInstance> LiveWith(bool isPlayer, AbilityEffect keyword)
        {
            foreach (var c in new List<CardInstance>(BoardOf(isPlayer)))
            {
                if (c != null && !c.IsDead && HasKeyword(c, keyword)) yield return c;
            }
        }

        // ---------------------------------------------------------------- heal

        /// <summary>A heal for one side: ลูกประคำ's bonus, the Khwan itself, then every card that reacts to it.</summary>
        public void HealSide(bool isPlayer, int amount)
        {
            var combat = CombatManager.Instance;
            if (combat == null || amount <= 0) return;

            foreach (var c in LiveWith(isPlayer, AbilityEffect.HealBonus)) amount += KeywordValue(c, AbilityEffect.HealBonus);
            int restored = combat.HealKhwan(amount, isPlayer);
            healedThisTurn[Side(isPlayer)] = true;
            Debug.Log($"[Sheet7] {(isPlayer ? "Player" : "Enemy")} heals {amount} (+{restored} Khwan)");

            // พระเครื่อง: the enemy loses what was healed, up to its cap a turn
            foreach (var c in LiveWith(isPlayer, AbilityEffect.HealHurtsEnemy))
            {
                int room = KeywordValue(c, AbilityEffect.HealHurtsEnemy) - UsedThisTurn(c);
                int loss = Mathf.Min(amount, room);
                if (loss <= 0) continue;
                c.usedThisTurn += loss;
                ShowSkill(c, isPlayer, result: $"ฮีลแล้วศัตรูเสียขวัญ {loss}");
                combat.TakeDamage(loss, toPlayer: !isPlayer, cause: $"{c.cardNameThai}");
            }

            // ผีแม่ย่านาง: strikes right away, a number of times a turn
            foreach (var c in LiveWith(isPlayer, AbilityEffect.AttackOnHeal))
            {
                if (UsedThisTurn(c) >= KeywordValue(c, AbilityEffect.AttackOnHeal) || !CanFamiliarAttack(c)) continue;
                c.usedThisTurn++;
                ShowSkill(c, isPlayer, result: "ฮีลแล้วโจมตีทันที");
                ResolveFamiliarStrike(c, ChooseStrikeTarget(c, c.boardSlot, isPlayer), isPlayer);
            }
            RemoveDeadFamiliars();
        }

        // End of the round, before the round-end abilities: ผ้ายันต์ (EndRoundHealOrDraw)
        private void Sheet7RoundEnd(bool isPlayer)
        {
            foreach (var c in LiveWith(isPlayer, AbilityEffect.EndRoundHealOrDraw))
            {
                var a = c.abilities.Find(x => x.trigger == AbilityTrigger.Passive && x.effect == AbilityEffect.EndRoundHealOrDraw);
                bool healed = healedThisTurn[Side(isPlayer)];
                ShowSkill(c, isPlayer, result: healed && a.duration <= 0 ? "จบรอบ: จั่ว 1 ใบ" : !healed && a.duration <= 0 ? $"จบรอบ: ฮีล +{a.value}" : $"จบรอบ: จั่ว 1 ใบ และฮีล +{a.value}");
                if (healed || a.duration > 0) DrawFor(isPlayer, 1);
                if (!healed || a.duration > 0) HealSide(isPlayer, a.value);
            }
        }

        // ---------------------------------------------------------------- death

        // After a familiar on the board died (its own OnDeath abilities have run)
        private void Sheet7AllyDied(CardInstance dead, bool deadIsPlayer)
        {
            var combat = CombatManager.Instance;
            if (dead.cardType != CardType.Familiar || combat == null) return;

            // Remembered for พิธีเรียกวิญญาณ, unless it was already brought back once
            if (!dead.exileOnDeath && dead.source != null)
            {
                var list = recentDead[Side(deadIsPlayer)];
                list.Add(dead.source);
                if (list.Count > 5) list.RemoveAt(0);
            }

            // ด้ายแดงผูกจิต (either side's): a familiar dying in its lane takes the one across the lane with it, then it breaks.
            // The upgrade sends the amulet owner's familiar back to hand instead of dying.
            foreach (bool side in new[] { true, false })
            {
                var amulet = CardAt(BoardOf(side), AmuletSlot);
                if (amulet == null || amulet.IsDead || amulet.chosenLane != dead.boardSlot) continue;
                var link = amulet.abilities.Find(x => x.effect == AbilityEffect.LinkLane);
                if (link == null) continue;
                amulet.currentDurability = 0;
                amulet.pendingRemoval = true; // first: the second death must not fire it again
                var other = CardAt(BoardOf(!deadIsPlayer), dead.boardSlot);
                if (other == null || other.IsDead || other.cardType != CardType.Familiar) continue;
                Debug.Log($"[Sheet7] {amulet.cardNameThai}: {dead.cardNameThai} died, so {other.cardNameThai} goes too");
                ShowSkill(amulet, side, result: $"{dead.cardNameThai} ตาย พา {other.cardNameThai} ไปด้วย");
                bool otherIsOwners = !deadIsPlayer == side;
                if (link.value > 0 && otherIsOwners) ReturnCardToHand(other, !deadIsPlayer);
                else DamageCard(other, other.familiarHealth, ignoreArmor: true, source: null, sourceIsPlayer: deadIsPlayer,
                    cause: $"{amulet.cardNameThai}: {dead.cardNameThai} died across the lane");
            }

            // ตะกรุดดำ: the enemy loses Khwan, a set number of times
            foreach (var c in LiveWith(deadIsPlayer, AbilityEffect.AllyDeathHurtsEnemy))
            {
                ShowSkill(c, deadIsPlayer, result: $"บริวารตาย ศัตรูเสียขวัญ {KeywordValue(c, AbilityEffect.AllyDeathHurtsEnemy)}");
                combat.TakeDamage(KeywordValue(c, AbilityEffect.AllyDeathHurtsEnemy), toPlayer: !deadIsPlayer, cause: c.cardNameThai);
                UseUp(c, AbilityEffect.AllyDeathHurtsEnemy);
            }

            // หุ่นพยนต์: the first ally to die each turn leaves a puppet in its slot
            foreach (var c in LiveWith(deadIsPlayer, AbilityEffect.AllyDeathSummon))
            {
                if (UsedThisTurn(c) > 0) continue;
                var a = c.abilities.Find(x => x.trigger == AbilityTrigger.Passive && x.effect == AbilityEffect.AllyDeathSummon);
                if (a?.relatedCard == null) continue;
                c.usedThisTurn++;
                ShowSkill(c, deadIsPlayer, result: $"เรียก {a.relatedCard.cardNameThai} ลงช่องที่ตาย");
                pendingSummons.Add((a.relatedCard, deadIsPlayer, dead.boardSlot));
            }

            // เปรต: grows with every other ally that dies
            foreach (var c in LiveWith(deadIsPlayer, AbilityEffect.AllyDeathBuff))
            {
                if (c == dead) continue;
                var a = c.abilities.Find(x => x.trigger == AbilityTrigger.Passive && x.effect == AbilityEffect.AllyDeathBuff);
                c.familiarDamage += a.value;
                if (a.duration > 0) { c.maxKhwan += a.duration; c.familiarHealth += a.duration; }
                ShowSkill(c, deadIsPlayer, result: a.duration > 0 ? $"เพื่อนตาย ตี +{a.value} ขวัญ +{a.duration}" : $"เพื่อนตาย ตี +{a.value}");
                NotifyChanged(c);
            }
        }

        // Puppets wait until the dead card has left its slot (RemoveDeadFamiliars)
        private readonly List<(CardDataSO card, bool isPlayer, int slot)> pendingSummons = new List<(CardDataSO, bool, int)>();

        private void PlacePendingSummons()
        {
            if (pendingSummons.Count == 0) return;
            var todo = new List<(CardDataSO card, bool isPlayer, int slot)>(pendingSummons);
            pendingSummons.Clear();
            foreach (var (card, isPlayer, slot) in todo) Summon(card, isPlayer, slot);
        }

        private CardInstance Summon(CardDataSO template, bool isPlayer, int slot = -1)
        {
            if (template == null) return null;
            var card = new CardInstance(template) { summonedThisRound = true, boardSlot = slot };
            bool placed = isPlayer ? PlaceBoardCard(card) : PlaceEnemyBoardCard(card);
            if (!placed) return null;
            OnCardSummoned?.Invoke(card, isPlayer);
            return card;
        }

        // ผ้ายันต์มหาอุด: the first time each ally would die it stays at 1 Khwan, until the amulet's uses run out
        private bool TrySaveFromDeath(CardInstance target)
        {
            if (target.cardType != CardType.Familiar || target.savedFromDeath || target.familiarHealth > 0) return false;
            if (!TryFindCard(target, out bool isPlayer, out _)) return false;
            foreach (var c in LiveWith(isPlayer, AbilityEffect.DeathSave))
            {
                target.familiarHealth = 1;
                target.savedFromDeath = true;
                UseUp(c, AbilityEffect.DeathSave);
                ShowSkill(c, isPlayer, result: $"{target.cardNameThai} รอด เหลือขวัญ 1");
                Debug.Log($"[Sheet7] {c.cardNameThai} keeps {target.cardNameThai} at 1 Khwan");
                return true;
            }
            return false;
        }

        // An amulet with a use count breaks once the count is used up
        private void UseUp(CardInstance amulet, AbilityEffect keyword)
        {
            if (amulet.usesLeft < 0)
            {
                var a = amulet.abilities.Find(x => x.trigger == AbilityTrigger.Passive && x.effect == keyword);
                int uses = keyword == AbilityEffect.DeathSave ? a.value : a.duration;
                if (uses <= 0) return; // no count: lasts its turns
                amulet.usesLeft = uses;
            }
            amulet.usesLeft--;
            if (amulet.usesLeft > 0) return;
            Debug.Log($"[Sheet7] {amulet.cardNameThai} breaks");
            amulet.currentDurability = 0;
            amulet.pendingRemoval = true;
        }

        private void ReturnCardToHand(CardInstance card, bool isPlayer)
        {
            card.pendingRemoval = true;
            card.exileOnDeath = true; // leaves the board without going to the graveyard
            GiveToHand(isPlayer, card.source);
        }

        // ---------------------------------------------------------------- entering the board

        private void Sheet7AllyEntered(CardInstance card, bool isPlayer)
        {
            if (card.cardType != CardType.Familiar) return;

            // ตุ๊กตาคุณไสย: the enemy familiar facing it is cursed
            foreach (var c in LiveWith(isPlayer, AbilityEffect.CurseOnAllyEnter))
            {
                var facing = CardAt(BoardOf(!isPlayer), card.boardSlot);
                if (facing != null && !facing.IsDead && facing.cardType == CardType.Familiar)
                {
                    ShowSkill(c, isPlayer, result: $"สาป {KeywordValue(c, AbilityEffect.CurseOnAllyEnter)} → {facing.cardNameThai}");
                    AddCardStatus(facing, CardStatusType.DoneKhong, KeywordValue(c, AbilityEffect.CurseOnAllyEnter), 99);
                }
            }

            // ผีเฝ้าทรัพย์ (GrowOnAllyPlayed): every other familiar played makes it sturdier
            foreach (var c in LiveWith(isPlayer, AbilityEffect.GrowOnAllyPlayed))
            {
                if (c == card) continue;
                var a = c.abilities.Find(x => x.trigger == AbilityTrigger.Passive && x.effect == AbilityEffect.GrowOnAllyPlayed);
                c.maxKhwan += a.value;
                c.familiarHealth += a.value;
                c.familiarDamage += a.duration;
                ShowSkill(c, isPlayer, result: a.duration > 0 ? $"ขวัญ +{a.value} ตี +{a.duration}" : $"ขวัญ +{a.value}");
                NotifyChanged(c);
            }
        }

        // ---------------------------------------------------------------- curse and strikes

        private static bool IsCursed(CardInstance c) => c != null && c.statuses.Exists(s => s.type == CardStatusType.DoneKhong && s.stacks > 0);

        // A side protected from bad statuses (บริวารเราติดสาปและสถานะร้ายไม่ได้)
        private bool BlocksHarm(CardInstance card, CardStatusType type)
        {
            if (!CardStatus.IsHarmful(type) || !TryFindCard(card, out bool isPlayer, out _)) return false;
            foreach (var _ in LiveWith(isPlayer, AbilityEffect.CurseImmunity)) return true;
            return false;
        }

        // หัวกะโหลก on the other side: curses on this side's familiars do not go down
        private bool CurseFrozen(bool cardIsPlayer)
        {
            foreach (var _ in LiveWith(!cardIsPlayer, AbilityEffect.CurseNoDecay)) return true;
            return false;
        }

        // หัวกะโหลก+: a familiar that dies of its curse heals the amulet's owner
        private void CurseKilled(bool cardIsPlayer)
        {
            foreach (var c in LiveWith(!cardIsPlayer, AbilityEffect.CurseNoDecay))
            {
                int heal = KeywordValue(c, AbilityEffect.CurseNoDecay);
                if (heal > 0) ShowSkill(c, !cardIsPlayer, result: $"ศัตรูตายเพราะสาป ฮีล +{heal}");
                if (heal > 0) HealSide(!cardIsPlayer, heal);
            }
        }

        // ผีตายโหง hits a cursed familiar harder
        private static int StrikeDamageOn(CardInstance attacker, CardInstance target, int attack)
        {
            if (!IsCursed(target) || !HasKeyword(attacker, AbilityEffect.CursedBonusDamage)) return attack;
            var a = attacker.abilities.Find(x => x.trigger == AbilityTrigger.Passive && x.effect == AbilityEffect.CursedBonusDamage);
            return a.duration > 0 ? attack * 2 : attack + a.value;
        }

        // ควายธนู+ (ทะลวง): what a strike has left after killing the familiar it hit goes on to the opposing Khwan
        private void PierceOnStrike(CardInstance attacker, CardInstance target, int hit, int khwanBefore, bool fromPlayer)
        {
            if (!target.IsDead || !HasKeyword(attacker, AbilityEffect.Pierce)) return;
            int overflow = hit - khwanBefore;
            if (overflow > 0) ShowSkill(attacker, fromPlayer, result: $"ทะลวง {overflow} เข้าขวัญศัตรู");
            if (overflow > 0) HitOpposingPlayer(attacker, overflow, fromPlayer);
        }

        // สายสิญจน์ (Thorns): an enemy familiar that hits one of ours takes damage back
        private void ThornsOnStrike(CardInstance attacker, CardInstance target, bool attackerIsPlayer)
        {
            if (target == null || target.cardType != CardType.Familiar || attacker.IsDead) return;
            foreach (var c in LiveWith(!attackerIsPlayer, AbilityEffect.Thorns))
            {
                ShowSkill(c, !attackerIsPlayer, result: $"สะท้อน {KeywordValue(c, AbilityEffect.Thorns)} → {attacker.cardNameThai}");
                DamageCard(attacker, KeywordValue(c, AbilityEffect.Thorns), ignoreArmor: true, source: null, sourceIsPlayer: !attackerIsPlayer,
                    cause: $"thorns of {c.cardNameThai}");
            }
        }

        private static bool SideBlocksFlying(bool defenderIsPlayer)
        {
            foreach (var _ in LiveWith(defenderIsPlayer, AbilityEffect.BlockFlying)) return true;
            return false;
        }

        // ---------------------------------------------------------------- on-play effects

        // Returns true when the effect was handled here
        private bool ExecuteSheet7(CardAbility a, CardInstance source, bool ownerIsPlayer, CardInstance other, object explicitTarget)
        {
            var combat = CombatManager.Instance;
            switch (a.effect)
            {
                case AbilityEffect.AttackOnHeal:
                case AbilityEffect.HealHurtsEnemy:
                case AbilityEffect.HealBonus:
                case AbilityEffect.EndRoundHealOrDraw:
                case AbilityEffect.AllyDeathHurtsEnemy:
                case AbilityEffect.AllyDeathSummon:
                case AbilityEffect.AllyDeathBuff:
                case AbilityEffect.CursedBonusDamage:
                case AbilityEffect.CurseNoDecay:
                case AbilityEffect.CurseOnAllyEnter:
                case AbilityEffect.CurseImmunity:
                case AbilityEffect.DeathSave:
                case AbilityEffect.AttackPerCorruption:
                case AbilityEffect.AttackIfSturdy:
                case AbilityEffect.AttackPerAlly:
                case AbilityEffect.BlockFlying:
                case AbilityEffect.Thorns:
                case AbilityEffect.GrowOnAllyPlayed:
                case AbilityEffect.Pierce:
                    return true; // keywords and auras: read where they matter

                case AbilityEffect.HealPlayer:
                    HealSide(ownerIsPlayer, a.value);
                    return true;

                case AbilityEffect.DrawCheaper:
                    // กุมารทอง+: the drawn cards cost 1 less for the rest of the fight, for either side
                    for (int i = 0; i < Mathf.Max(1, a.value); i++)
                    {
                        IReadOnlyList<CardInstance> hand = ownerIsPlayer
                            ? CardManager.Instance?.Hand
                            : CombatManager.Instance?.EnemyCards?.Hand;
                        int before = hand != null ? hand.Count : 0;
                        DrawFor(ownerIsPlayer, 1);
                        if (hand == null || hand.Count <= before) continue;
                        var drawn = hand[hand.Count - 1];
                        drawn.meritCost = Mathf.Max(0, drawn.meritCost - 1);
                        drawn.corruptionGain = Mathf.Max(0, drawn.corruptionGain - 1);
                    }
                    return true;

                case AbilityEffect.HealPerAlly:
                    int allies = BoardOf(ownerIsPlayer).FindAll(c => IsAliveFamiliar(c)).Count;
                    HealSide(ownerIsPlayer, a.value * allies);
                    return true;

                case AbilityEffect.SacrificeForAttack:
                case AbilityEffect.SacrificeForHeal:
                {
                    var victim = PickedOrAuto(a, source, ownerIsPlayer, explicitTarget);
                    if (victim == null) return true;
                    int atk = EffectiveAttack(victim), khwan = victim.familiarHealth, maxK = victim.maxKhwan;
                    victim.familiarHealth = 0;
                    HandleDeath(victim, null, ownerIsPlayer, $"sacrificed to {source?.cardNameThai}");
                    if (a.effect == AbilityEffect.SacrificeForAttack && source != null)
                    {
                        source.familiarDamage += atk;
                        if (a.value > 0) { source.maxKhwan += maxK; source.familiarHealth += khwan; }
                        NotifyChanged(source);
                    }
                    else
                    {
                        HealSide(ownerIsPlayer, khwan);
                        if (a.value > 0) combat?.TakeDamage(khwan, toPlayer: !ownerIsPlayer, cause: source?.cardNameThai);
                    }
                    RemoveDeadFamiliars();
                    return true;
                }

                case AbilityEffect.ReviveLast:
                {
                    var list = recentDead[Side(ownerIsPlayer)];
                    int count = Mathf.Max(1, a.value), revived = 0;
                    for (int i = list.Count - 1; i >= 0 && revived < count; i--)
                    {
                        if (IsBoardFull(ownerIsPlayer)) break;
                        var card = new CardInstance(list[i]) { summonedThisRound = true, exileOnDeath = true };
                        bool placed = ownerIsPlayer ? PlaceBoardCard(card) : PlaceEnemyBoardCard(card);
                        if (!placed) break;
                        OnCardSummoned?.Invoke(card, ownerIsPlayer);
                        list.RemoveAt(i);
                        revived++;
                    }
                    if (revived > 0 && a.duration > 0) HealSide(ownerIsPlayer, a.duration * revived);
                    return true;
                }

                case AbilityEffect.ReturnToHand:
                {
                    var target = PickedOrAuto(a, source, ownerIsPlayer, explicitTarget);
                    if (target == null || !TryFindCard(target, out bool targetIsPlayer, out _)) return true;
                    if (targetIsPlayer != ownerIsPlayer && IsCursed(target))
                        DamageCard(target, target.familiarHealth, ignoreArmor: true, source, ownerIsPlayer, cause: $"destroyed by {source?.cardNameThai} (it was cursed)");
                    else
                        ReturnCardToHand(target, targetIsPlayer);
                    RemoveDeadFamiliars();
                    return true;
                }

                case AbilityEffect.MoveCurses:
                    foreach (var mine in new List<CardInstance>(BoardOf(ownerIsPlayer)))
                    {
                        var curse = mine.statuses.Find(s => s.type == CardStatusType.DoneKhong && !s.fromAura);
                        if (curse == null) continue;
                        mine.statuses.Remove(curse);
                        NotifyChanged(mine);
                        var facing = CardAt(BoardOf(!ownerIsPlayer), mine.boardSlot);
                        if (facing != null && !facing.IsDead) AddCardStatus(facing, CardStatusType.DoneKhong, curse.stacks, 99);
                    }
                    DrawFor(ownerIsPlayer, a.value);
                    return true;

                case AbilityEffect.LinkLane:
                    // The player clicked a column for it; the enemy ties the lane its strongest foe stands in
                    if (source != null && !IsFamiliarSlot(source.chosenLane)) source.chosenLane = BestLane(ownerIsPlayer);
                    Debug.Log($"[Sheet7] {source?.cardNameThai} ties lane {source?.chosenLane}");
                    return true;

                case AbilityEffect.DestroyEnemyAmulet:
                {
                    var amulet = CardAt(BoardOf(!ownerIsPlayer), AmuletSlot);
                    if (amulet != null) { amulet.currentDurability = 0; amulet.pendingRemoval = true; }
                    RemoveDeadFamiliars();
                    return true;
                }

                case AbilityEffect.DamageHealOnKill:
                {
                    var target = PickedOrAuto(a, source, ownerIsPlayer, explicitTarget);
                    if (target == null) return true;
                    DamageCard(target, a.value, a.ignoreArmor, source, ownerIsPlayer);
                    if (target.IsDead) HealSide(ownerIsPlayer, a.duration);
                    return true;
                }

                case AbilityEffect.SummonRelated:
                {
                    // The first one goes to the column the player put the spell on (the enemy: the leftmost free one);
                    // the upgrade adds one on each side of it if free
                    int chosen = source != null && IsFamiliarSlot(source.boardSlot) ? source.boardSlot : -1;
                    if (source != null) source.boardSlot = -1;
                    var first = Summon(a.relatedCard, ownerIsPlayer, chosen);
                    if (first == null || a.value <= 1) return true;
                    int slot = first.boardSlot;
                    var own = BoardOf(ownerIsPlayer);
                    foreach (int side in new[] { slot - 1, slot + 1 })
                        if (IsFamiliarSlot(side) && CardAt(own, side) == null) Summon(a.relatedCard, ownerIsPlayer, side);
                    return true;
                }

                case AbilityEffect.CursedCannotAttack:
                    foreach (var c in BoardOf(!ownerIsPlayer))
                        if (IsCursed(c)) AddCardStatus(c, CardStatusType.PhiAm, 1, 1);
                    return true;
            }
            return false;
        }

        private CardInstance PickedOrAuto(CardAbility a, CardInstance source, bool ownerIsPlayer, object explicitTarget)
        {
            var pool = Sheet7Pool(a, source, ownerIsPlayer);
            if (explicitTarget is CardInstance chosen && pool.Contains(chosen)) return chosen;
            CardInstance best = null;
            float bestScore = float.MinValue;
            foreach (var c in pool)
            {
                float score = Sheet7AutoScore(a, c, ownerIsPlayer);
                if (score > bestScore) { bestScore = score; best = c; }
            }
            return best;
        }

        // The cards a sheet 7 effect may be pointed at
        private List<CardInstance> Sheet7Pool(CardAbility a, CardInstance source, bool ownerIsPlayer)
        {
            var pool = new List<CardInstance>();
            void AddFrom(bool side) { foreach (var c in BoardOf(side)) if (IsAliveFamiliar(c) && c != source) pool.Add(c); }

            switch (a.effect)
            {
                case AbilityEffect.SacrificeForAttack:
                case AbilityEffect.SacrificeForHeal:
                    AddFrom(ownerIsPlayer);
                    break;
                case AbilityEffect.ReturnToHand:
                    AddFrom(!ownerIsPlayer);
                    if (a.target == AbilityTarget.AnyCard) AddFrom(ownerIsPlayer);
                    break;
                case AbilityEffect.DamageHealOnKill:
                    AddFrom(!ownerIsPlayer);
                    break;
            }
            return pool;
        }

        // The game's own pick, for the enemy and when there is only one sensible choice
        private float Sheet7AutoScore(CardAbility a, CardInstance c, bool ownerIsPlayer)
        {
            switch (a.effect)
            {
                case AbilityEffect.SacrificeForAttack: return EffectiveAttack(c) * 2f - c.familiarHealth * 0.1f;
                case AbilityEffect.SacrificeForHeal: return c.familiarHealth - EffectiveAttack(c) * 0.5f;
                case AbilityEffect.ReturnToHand: return (IsCursed(c) ? 20f : 0f) + EffectiveAttack(c) * 2f + c.familiarHealth;
                case AbilityEffect.DamageHealOnKill: return c.familiarHealth <= a.value ? 10f + EffectiveAttack(c) : -c.familiarHealth;
            }
            return 0f;
        }

        // คาถาเรียกผี: a spell that is put on a familiar column, which it summons into
        public static bool PicksSlot(CardInstance card) =>
            card != null && card.cardType == CardType.Incantation && card.abilities != null
            && card.abilities.Exists(a => a.trigger == AbilityTrigger.OnPlay && a.effect == AbilityEffect.SummonRelated);

        // ด้ายแดงผูกจิต: an amulet that asks for a lane (any familiar column, empty or not) when it is placed
        public static bool PicksLane(CardInstance card) =>
            card != null && card.cardType == CardType.Amulet && card.abilities != null
            && card.abilities.Exists(a => a.trigger == AbilityTrigger.OnPlay && a.effect == AbilityEffect.LinkLane);

        // The lane worth tying: the other side's strongest familiar against our weakest
        private int BestLane(bool ownerIsPlayer)
        {
            int best = FirstFamiliarSlot;
            float bestScore = float.MinValue;
            for (int lane = FirstFamiliarSlot; lane < MaxBoardSlots; lane++)
            {
                var theirs = CardAt(BoardOf(!ownerIsPlayer), lane);
                var mine = CardAt(BoardOf(ownerIsPlayer), lane);
                float score = (theirs != null ? EffectiveAttack(theirs) * 2f + theirs.familiarHealth : 0f) - (mine != null ? EffectiveAttack(mine) : 0f);
                if (score > bestScore) { bestScore = score; best = lane; }
            }
            return best;
        }

        public static bool IsSheet7Choice(AbilityEffect e) =>
            e == AbilityEffect.SacrificeForAttack || e == AbilityEffect.SacrificeForHeal || e == AbilityEffect.ReturnToHand
            || e == AbilityEffect.DamageHealOnKill;

        // ---------------------------------------------------------------- auras

        private void Sheet7Auras(bool isPlayer, CardInstance source, CardAbility a, Dictionary<CardInstance, int> attack)
        {
            var own = BoardOf(isPlayer);
            switch (a.effect)
            {
                case AbilityEffect.AttackPerCorruption:
                {
                    var s = CombatManager.Instance.State;
                    int corruption = isPlayer ? s.currentCorruption : s.enemyCorruption;
                    int bonus = a.value * (corruption / Mathf.Max(1, a.duration));
                    if (bonus > 0) foreach (var c in own) if (IsAliveFamiliar(c)) Add(attack, c, bonus);
                    break;
                }
                case AbilityEffect.AttackIfSturdy:
                    foreach (var c in own) if (IsAliveFamiliar(c) && c.maxKhwan >= a.duration) Add(attack, c, a.value);
                    break;
                case AbilityEffect.AttackPerAlly:
                    int others = own.FindAll(c => IsAliveFamiliar(c) && c != source).Count;
                    if (others > 0) Add(attack, source, a.value * others);
                    break;
            }
        }
    }
}
