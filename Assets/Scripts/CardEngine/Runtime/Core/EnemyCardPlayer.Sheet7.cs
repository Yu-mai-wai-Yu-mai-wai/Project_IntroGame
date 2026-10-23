using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Values the enemy's sheet 7 effects (EffectResolver.Sheet7.cs) by the board and hand they would meet: a heal by the
    // Khwan it restores and the cards on the board that react to it, a curse payoff by the curses there are, a death
    // trigger by the familiars likely to die. Same scale as ScoreAbilities: 1 point of attack on a familiar ≈ 3, a
    // familiar's body ≈ 4 + attack + 0.4 per Khwan, 1 Khwan healed ≈ 1.5, 1 Khwan off the player ≈ 2.5.
    public partial class EnemyCardPlayer
    {
        private void ScoreSheet7(CardAbility a, CardInstance card, CombatStateData s,
            ref float offense, ref float defense, ref float utility)
        {
            var own = s.enemyBoardCards;
            var foe = s.activeBoardCards;
            var ownFamiliars = own.FindAll(c => EffectResolver.IsAliveFamiliar(c) && c != card);
            var foeFamiliars = foe.FindAll(c => EffectResolver.IsAliveFamiliar(c));
            int familiarsInHand = hand.FindAll(c => c != card && c.cardType == CardType.Familiar).Count;
            int freeColumns = 0;
            for (int col = EffectResolver.FirstFamiliarSlot; col < EffectResolver.MaxBoardSlots; col++)
                if (EffectResolver.CardAt(own, col) == null) freeColumns++;

            switch (a.effect)
            {
                // ---------------------------------------------------------------- heal
                case AbilityEffect.HealPlayer:
                    ScoreHeal(a.value, s, own, ref offense, ref defense);
                    break;

                case AbilityEffect.HealPerAlly:
                    if (ownFamiliars.Count > 0) ScoreHeal(a.value * ownFamiliars.Count, s, own, ref offense, ref defense);
                    break;

                case AbilityEffect.AttackOnHeal: // ผีแม่ย่านาง
                    int strikes = Mathf.Min(Mathf.Max(1, a.value), HealSources(card, own));
                    offense += strikes * (1.5f + card.familiarDamage * 1.5f);
                    break;

                case AbilityEffect.HealHurtsEnemy: // พระเครื่อง
                    offense += Mathf.Min(a.value, Mathf.Max(1, HealPotential(card, own))) * 2.5f + 1f;
                    break;

                case AbilityEffect.HealBonus: // ลูกประคำ
                    defense += a.value * HealSources(card, own) * 1.5f + 0.5f;
                    break;

                case AbilityEffect.EndRoundHealOrDraw: // ผ้ายันต์: a card or a heal every turn it stays
                    utility += 3f + (a.duration > 0 ? 3f : 0f);
                    if (s.enemyKhwan < s.maxEnemyKhwan) defense += a.value * 1.5f;
                    break;

                // ---------------------------------------------------------------- death and sacrifice
                case AbilityEffect.AllyDeathHurtsEnemy: // ตะกรุดดำ
                    offense += Mathf.Min(Mathf.Max(1, a.duration), LikelyDeaths(ownFamiliars, foe, familiarsInHand)) * a.value * 2.5f + 0.5f;
                    break;

                case AbilityEffect.AllyDeathSummon: // หุ่นพยนต์: a body every turn an ally dies
                    float puppet = a.relatedCard != null ? Body(a.relatedCard.familiarDamage, a.relatedCard.familiarHealth) * 0.6f : 2f;
                    defense += puppet * (ownFamiliars.Count + familiarsInHand > 0 ? 1.5f : 0.3f);
                    break;

                case AbilityEffect.AllyDeathBuff: // เปรต
                    utility += (a.value * 2f + a.duration) * Mathf.Min(2, ownFamiliars.Count + familiarsInHand) * 0.75f;
                    break;

                case AbilityEffect.SacrificeForAttack: // ผีปอบ
                {
                    var victim = Best(ownFamiliars, c => Attack(c) * 2f - c.familiarHealth * 0.1f);
                    if (victim == null) break; // nothing to take: just its own body
                    float gain = Attack(victim) * 2.5f + (a.value > 0 ? victim.familiarHealth * 1f : 0f);
                    utility += gain - SacrificeLoss(victim, foe, own);
                    break;
                }

                case AbilityEffect.SacrificeForHeal: // พิธีฝังรูปฝังรอย
                {
                    var victim = Best(ownFamiliars, c => c.familiarHealth - Attack(c) * 0.5f);
                    if (victim == null) break;
                    ScoreHeal(victim.familiarHealth, s, own, ref offense, ref defense);
                    if (a.value > 0) offense += victim.familiarHealth * 2.5f;
                    utility -= SacrificeLoss(victim, foe, own);
                    break;
                }

                case AbilityEffect.ReviveLast: // พิธีเรียกวิญญาณ: the graveyard's familiars stand in for the last dead
                {
                    var dead = discardPile.FindAll(c => c.cardType == CardType.Familiar);
                    int count = Mathf.Min(Mathf.Max(1, a.value), Mathf.Min(dead.Count, freeColumns));
                    if (count <= 0) break;
                    float bodies = 0f;
                    for (int i = 0; i < count; i++)
                    {
                        var c = dead[dead.Count - 1 - i];
                        bodies += Body(c.familiarDamage, c.maxKhwan > 0 ? c.maxKhwan : c.familiarHealth) * 0.8f; // exiled on a second death
                    }
                    utility += bodies;
                    if (a.duration > 0) ScoreHeal(a.duration * count, s, own, ref offense, ref defense);
                    break;
                }

                // ---------------------------------------------------------------- the player's familiars
                case AbilityEffect.ReturnToHand: // บทสวดส่งวิญญาณ: a cursed one is destroyed
                {
                    var target = Best(foeFamiliars, c => (CurseOn(c) > 0 ? 20f : 0f) + Attack(c) * 2f + c.familiarHealth);
                    if (target == null) break;
                    offense += CurseOn(target) > 0
                        ? 5f + Attack(target) * 1.5f + target.familiarHealth * 0.5f
                        : (Attack(target) * 2f + target.familiarHealth * 0.5f) * 0.6f; // the player can play it again
                    break;
                }

                case AbilityEffect.DamageHealOnKill: // คาถาดูดขวัญ
                {
                    float best = 0f;
                    foreach (var c in foeFamiliars)
                    {
                        bool kills = c.familiarHealth <= a.value;
                        float value = a.value * 2f + (kills ? 5f + Attack(c) * 1.5f : 0f);
                        if (kills && a.duration > 0) value += Mathf.Min(a.duration, s.maxEnemyKhwan - s.enemyKhwan) * 1.5f + HealTriggers(a.duration, own);
                        best = Mathf.Max(best, value);
                    }
                    offense += best;
                    break;
                }

                case AbilityEffect.CursedCannotAttack: // คาถาสะกด+: also counts the familiar the card curses first
                {
                    float stopped = 0f, uncursed = 0f;
                    foreach (var c in foeFamiliars)
                    {
                        if (CurseOn(c) > 0) stopped += Attack(c) * 1.5f + 0.5f;
                        else uncursed = Mathf.Max(uncursed, Attack(c) * 1.5f + 0.5f);
                    }
                    if (card.abilities.Exists(x => x.effect == AbilityEffect.ApplyStatus && x.status == CardStatusType.DoneKhong)) stopped += uncursed;
                    offense += stopped;
                    break;
                }

                case AbilityEffect.DestroyEnemyAmulet: // พิธีสะเดาะเคราะห์
                {
                    var amulet = EffectResolver.CardAt(foe, EffectResolver.AmuletSlot);
                    if (amulet != null && !amulet.IsDead) offense += 7f;
                    break;
                }

                case AbilityEffect.LinkLane: // ด้ายแดงผูกจิต: the lane where theirs is worth the most next to ours
                {
                    float best = 0f;
                    for (int lane = EffectResolver.FirstFamiliarSlot; lane < EffectResolver.MaxBoardSlots; lane++)
                    {
                        var theirs = EffectResolver.CardAt(foe, lane);
                        if (!EffectResolver.IsAliveFamiliar(theirs)) continue;
                        var mine = EffectResolver.CardAt(own, lane);
                        float theirWorth = Attack(theirs) * 2f + theirs.familiarHealth;
                        float myWorth = EffectResolver.IsAliveFamiliar(mine) ? (Attack(mine) * 2f + mine.familiarHealth) * (a.value > 0 ? 0.2f : 1f) : 0f;
                        // An empty lane on our side pays off only once we put a familiar there
                        float worth = mine != null ? theirWorth - myWorth : theirWorth * (familiarsInHand > 0 ? 0.4f : 0.15f);
                        best = Mathf.Max(best, worth);
                    }
                    offense += best;
                    break;
                }

                // ---------------------------------------------------------------- curse
                case AbilityEffect.MoveCurses: // คาถาถอนคุณไสย
                {
                    foreach (var mine in ownFamiliars)
                    {
                        int stacks = CurseOn(mine);
                        if (stacks <= 0) continue;
                        defense += stacks * 1.5f;
                        if (EffectResolver.IsAliveFamiliar(EffectResolver.CardAt(foe, mine.boardSlot))) offense += stacks * 2.5f;
                    }
                    utility += 4f * a.value;
                    break;
                }

                case AbilityEffect.CursedBonusDamage: // ผีตายโหง
                    bool curses = foeFamiliars.Exists(c => CurseOn(c) > 0) || CurseCardsInHand(card) > 0;
                    offense += curses ? (a.duration > 0 ? card.familiarDamage * 1.5f : a.value * 1.5f) + 1f : 0.3f;
                    break;

                case AbilityEffect.CurseNoDecay: // หัวกะโหลกอาถรรพ์
                {
                    int stacks = 0;
                    foreach (var c in foeFamiliars) stacks += CurseOn(c);
                    offense += stacks * 1.5f + CurseCardsInHand(card) * 2f + 0.5f + a.value;
                    break;
                }

                case AbilityEffect.CurseOnAllyEnter: // ตุ๊กตาคุณไสย
                    offense += a.value * 2f * (familiarsInHand + 0.5f) * (foeFamiliars.Count > 0 ? 1f : 0.5f);
                    break;

                case AbilityEffect.CurseImmunity: // เบี้ยแก้
                {
                    int harmed = 0;
                    foreach (var c in ownFamiliars) harmed += c.statuses.FindAll(st => CardStatus.IsHarmful(st.type)).Count;
                    defense += harmed * 3f + 1.5f;
                    break;
                }

                // ---------------------------------------------------------------- keywords and auras
                case AbilityEffect.DeathSave: // ผ้ายันต์มหาอุด
                    defense += Mathf.Min(Mathf.Max(1, a.value), ownFamiliars.Count + familiarsInHand) * 2.5f;
                    break;

                case AbilityEffect.AttackPerCorruption: // ผ้าประเจียดดำ: Corruption only grows, so count one step more
                    float perFamiliar = a.value * (s.enemyCorruption / Mathf.Max(1, a.duration)) + 0.5f;
                    offense += perFamiliar * (ownFamiliars.Count + familiarsInHand * 0.5f) * 3f;
                    break;

                case AbilityEffect.AttackIfSturdy: // ผ้ายันต์กลับด้าน
                {
                    int sturdy = ownFamiliars.FindAll(c => c.maxKhwan >= a.duration).Count
                        + hand.FindAll(c => c != card && c.cardType == CardType.Familiar && c.maxKhwan >= a.duration).Count;
                    offense += a.value * 3f * sturdy;
                    break;
                }

                case AbilityEffect.AttackPerAlly: // ควายธนู
                    offense += a.value * ownFamiliars.Count * 3f;
                    break;

                case AbilityEffect.BlockFlying: // สายสิญจน์
                {
                    float flyers = 0f;
                    foreach (var c in foeFamiliars) if (EffectResolver.HasKeyword(c, AbilityEffect.Overhead)) flyers += 2f + Attack(c);
                    defense += flyers > 0f ? flyers : 0.5f;
                    break;
                }

                case AbilityEffect.Thorns:
                    defense += a.value * foeFamiliars.Count * 1.5f + 0.5f;
                    break;

                case AbilityEffect.GrowOnAllyPlayed: // ผีบ้านผีเรือน
                    utility += (a.value * 0.6f + a.duration * 1.5f) * familiarsInHand + 0.5f;
                    break;

                case AbilityEffect.Pierce:
                    offense += 1f + card.familiarDamage * 0.5f;
                    break;

                case AbilityEffect.SummonRelated: // คาถาเรียกผี
                {
                    int count = Mathf.Min(a.value > 1 ? 3 : 1, freeColumns);
                    float body = a.relatedCard != null ? Body(a.relatedCard.familiarDamage, a.relatedCard.familiarHealth) : 4f;
                    utility += count * body;
                    break;
                }

                case AbilityEffect.DrawCheaper: // กุมารทอง+
                    utility += 4.5f * Mathf.Max(1, a.value);
                    break;

                default:
                    utility += 2f;
                    break;
            }
        }

        // A heal: the Khwan it restores, plus what reacts to any heal (ลูกประคำ, พระเครื่อง, ผีแม่ย่านาง), which counts
        // the full amount even at full Khwan
        private void ScoreHeal(int amount, CombatStateData s, List<CardInstance> own, ref float offense, ref float defense)
        {
            if (amount <= 0) return;
            foreach (var c in own) if (IsLive(c)) amount += PassiveValue(c, AbilityEffect.HealBonus);

            float missing = s.maxEnemyKhwan > 0 ? 1f - Mathf.Clamp01((float)s.enemyKhwan / s.maxEnemyKhwan) : 0f;
            defense += Mathf.Min(amount, s.maxEnemyKhwan - s.enemyKhwan) * (1.5f + missing) + 0.5f;
            offense += HealTriggers(amount, own);
        }

        private float HealTriggers(int amount, List<CardInstance> own)
        {
            float value = 0f;
            foreach (var c in own)
            {
                if (!IsLive(c)) continue;
                int cap = PassiveValue(c, AbilityEffect.HealHurtsEnemy);
                if (cap > 0) value += Mathf.Min(amount, cap) * 2.5f;
                if (EffectResolver.HasKeyword(c, AbilityEffect.AttackOnHeal)) value += 1.5f + Attack(c) * 1.5f;
            }
            return value;
        }

        // How many heals the enemy could make soon: cards in hand that heal, and ผ้ายันต์ on the board
        private int HealSources(CardInstance except, List<CardInstance> own)
        {
            int sources = 0;
            foreach (var c in hand) if (c != except && Heals(c) > 0) sources++;
            foreach (var c in own) if (IsLive(c) && EffectResolver.HasKeyword(c, AbilityEffect.EndRoundHealOrDraw)) sources++;
            return sources;
        }

        // The Khwan those heals would come to
        private int HealPotential(CardInstance except, List<CardInstance> own)
        {
            int total = 0;
            foreach (var c in hand) if (c != except) total += Heals(c);
            foreach (var c in own) if (IsLive(c)) total += PassiveValue(c, AbilityEffect.EndRoundHealOrDraw);
            return total;
        }

        private int Heals(CardInstance c)
        {
            if (c.abilities == null) return 0;
            int ownFamiliars = CombatManager.Instance != null
                ? CombatManager.Instance.State.enemyBoardCards.FindAll(x => EffectResolver.IsAliveFamiliar(x)).Count : 0;
            int total = 0;
            foreach (var a in c.abilities)
            {
                if (a.trigger != AbilityTrigger.OnPlay) continue;
                if (a.effect == AbilityEffect.HealPlayer) total += a.value;
                else if (a.effect == AbilityEffect.HealPerAlly) total += a.value * ownFamiliars;
                else if (a.effect == AbilityEffect.SacrificeForHeal) total += 2;
                else if (a.effect == AbilityEffect.ReviveLast) total += a.duration;
            }
            return total;
        }

        private int CurseCardsInHand(CardInstance except)
        {
            return hand.FindAll(c => c != except && c.abilities != null && c.abilities.Exists(a =>
                (a.effect == AbilityEffect.ApplyStatus && a.status == CardStatusType.DoneKhong)
                || a.effect == AbilityEffect.CurseOnAllyEnter)).Count;
        }

        // Familiars of ours likely to die soon: those facing one of the player's that hits as hard as their Khwan,
        // and some of the ones still in hand
        private float LikelyDeaths(List<CardInstance> ownFamiliars, List<CardInstance> foe, int familiarsInHand)
        {
            float deaths = familiarsInHand * 0.5f;
            foreach (var c in ownFamiliars)
            {
                var facing = EffectResolver.CardAt(foe, c.boardSlot);
                if (EffectResolver.IsAliveFamiliar(facing) && Attack(facing) >= c.familiarHealth) deaths += 1f;
                else if (CurseOn(c) >= c.familiarHealth) deaths += 1f;
            }
            return deaths;
        }

        // What losing one of our familiars to a sacrifice costs: its body (little when it would die this clash
        // anyway), less what our death triggers give back
        private float SacrificeLoss(CardInstance victim, List<CardInstance> foe, List<CardInstance> own)
        {
            var facing = EffectResolver.CardAt(foe, victim.boardSlot);
            bool doomed = EffectResolver.IsAliveFamiliar(facing) && Attack(facing) >= victim.familiarHealth;
            float loss = Body(Attack(victim), victim.familiarHealth) * (doomed ? 0.3f : 1f);

            foreach (var c in own)
            {
                if (!IsLive(c) || c == victim) continue;
                loss -= PassiveValue(c, AbilityEffect.AllyDeathHurtsEnemy) * 2.5f;
                if (EffectResolver.HasKeyword(c, AbilityEffect.AllyDeathSummon)) loss -= 2f;
                if (EffectResolver.HasKeyword(c, AbilityEffect.AllyDeathBuff)) loss -= 2f;
            }
            return loss;
        }

        private static float Body(int attack, int khwan) => 4f + attack + khwan * 0.4f;

        private static int Attack(CardInstance c) =>
            EffectResolver.Instance != null ? EffectResolver.Instance.EffectiveAttack(c) : c.familiarDamage;

        private static bool IsLive(CardInstance c) => c != null && !c.IsDead;

        private static int CurseOn(CardInstance c)
        {
            var curse = c?.statuses.Find(st => st.type == CardStatusType.DoneKhong);
            return curse != null ? Mathf.Max(0, curse.stacks) : 0;
        }

        // The value of a keyword or aura the card carries; 0 = it has none
        private static int PassiveValue(CardInstance c, AbilityEffect keyword)
        {
            if (c?.abilities == null || EffectResolver.AbilitiesBlocked(c)) return 0;
            var a = c.abilities.Find(x => x.effect == keyword && (x.trigger == AbilityTrigger.Passive || x.trigger == AbilityTrigger.Aura));
            return a != null ? Mathf.Max(1, a.value) : 0;
        }

        private static CardInstance Best(List<CardInstance> cards, System.Func<CardInstance, float> score)
        {
            CardInstance best = null;
            float bestScore = float.MinValue;
            foreach (var c in cards)
            {
                float value = score(c);
                if (value > bestScore) { bestScore = value; best = c; }
            }
            return best;
        }
    }
}
