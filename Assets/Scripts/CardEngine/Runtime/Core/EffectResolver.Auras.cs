using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Aura abilities (trigger = Aura) work only while their card is on the board: ตะกรุด's +1 Khwan,
    // ตะกรุดดำ's +1 attack, หัวกะโหลกอาถรรพ์'s -1 attack, เบี้ยแก้'s +1 max Corruption, สายสิญจน์'s formation...
    // ผ้ายันต์กลับด้าน's flip happens once when played and is turned back when it leaves the board.
    // RefreshAuras recomputes what every board card should get from the auras in play and applies only
    // the difference from what it already has (CardInstance.auraKhwan / auraAttack), so when the amulet
    // breaks or leaves, its bonus is taken back. Statuses from an aura (ตุ๊กตาคุณไสย's ผีบังตา) are marked
    // CardStatus.fromAura and are added or removed the same way.
    public partial class EffectResolver
    {
        private bool refreshingAuras;

        private static bool IsInPlay(CardInstance card)
        {
            return card != null && !card.IsDead && TryFindCard(card, out _, out _);
        }

        // Auras can blind (ผีบังตา) a card that has auras of its own, which then stop: passes repeat until no card
        // gains or loses ผีบังตา, and every board card's numbers are refreshed at the end
        public void RefreshAuras()
        {
            var combat = CombatManager.Instance;
            if (combat == null || refreshingAuras) return;

            for (int pass = 0; pass < 4; pass++)
            {
                var blockedBefore = BlockedOnBoard();
                ApplyAurasOnce();
                if (blockedBefore.SetEquals(BlockedOnBoard())) break;
            }

            for (int side = 0; side < 2; side++)
            {
                foreach (var card in BoardOf(side == 0).ToArray())
                {
                    if (card != null && !card.IsDead) NotifyChanged(card);
                }
            }
        }

        private HashSet<CardInstance> BlockedOnBoard()
        {
            var set = new HashSet<CardInstance>();
            for (int side = 0; side < 2; side++)
            {
                foreach (var card in BoardOf(side == 0))
                {
                    if (card != null && !card.IsDead && AbilitiesBlocked(card)) set.Add(card);
                }
            }
            return set;
        }

        private void ApplyAurasOnce()
        {
            var combat = CombatManager.Instance;
            refreshingAuras = true;

            var khwan = new Dictionary<CardInstance, int>();
            var attack = new Dictionary<CardInstance, int>();
            var statuses = new Dictionary<CardInstance, Dictionary<CardStatusType, int>>();

            for (int side = 0; side < 2; side++)
            {
                bool isPlayer = side == 0;
                var own = BoardOf(isPlayer);

                foreach (var source in own.ToArray())
                {
                    if (source == null || source.IsDead || source.abilities == null) continue;
                    if (AbilitiesBlocked(source)) continue; // ผีบังตา: its auras are off

                    foreach (var a in source.abilities)
                    {
                        if (a.trigger != AbilityTrigger.Aura) continue;

                        switch (a.effect)
                        {
                            case AbilityEffect.BuffKhwan:
                                foreach (var t in ResolveTargets(a, source, isPlayer, null, null)) Add(khwan, t, a.value);
                                break;

                            case AbilityEffect.BuffAttack:
                                foreach (var t in ResolveTargets(a, source, isPlayer, null, null))
                                {
                                    if (t.cardType == CardType.Familiar || t.cardType == CardType.Amulet) Add(attack, t, a.value);
                                }
                                break;

                            case AbilityEffect.ApplyStatus:
                                foreach (var t in ResolveTargets(a, source, isPlayer, null, null))
                                {
                                    if (!statuses.TryGetValue(t, out var onCard)) statuses[t] = onCard = new Dictionary<CardStatusType, int>();
                                    onCard.TryGetValue(a.status, out int n);
                                    onCard[a.status] = n + Mathf.Max(1, a.value);
                                }
                                break;
                        }
                    }
                }
            }

            for (int side = 0; side < 2; side++)
            {
                bool isPlayer = side == 0;
                foreach (var card in BoardOf(isPlayer).ToArray())
                {
                    if (card == null || card.IsDead) continue;
                    khwan.TryGetValue(card, out int k);
                    attack.TryGetValue(card, out int atk);
                    statuses.TryGetValue(card, out var auraStatuses);
                    ApplyAuraStatuses(card, auraStatuses);
                    ApplyAura(card, k, atk, isPlayer);
                }
            }

            refreshingAuras = false;
        }

        private static void Add(Dictionary<CardInstance, int> totals, CardInstance card, int value)
        {
            if (card == null) return;
            totals.TryGetValue(card, out int v);
            totals[card] = v + value;
        }

        private void ApplyAura(CardInstance card, int khwan, int attack, bool ownerIsPlayer)
        {
            int dk = khwan - card.auraKhwan;
            int da = attack - card.auraAttack;
            if (dk == 0 && da == 0) return;

            bool hasKhwan = card.maxKhwan > 0 || card.cardType == CardType.Familiar;
            if (dk != 0 && hasKhwan)
            {
                card.maxKhwan += dk;
                card.familiarHealth += dk;
                // Losing a bonus never kills; only a net curse aura (e.g. ผ้าประเจียดดำ) can
                if (khwan >= 0 && card.familiarHealth < 1) card.familiarHealth = 1;
                card.familiarHealth = Mathf.Min(card.familiarHealth, Mathf.Max(0, card.maxKhwan));
            }
            card.auraKhwan = khwan;

            card.familiarDamage += da;
            card.auraAttack = attack;

            Debug.Log($"[Aura] {card.cardNameThai}: Khwan {(dk >= 0 ? "+" : "")}{dk}, attack {(da >= 0 ? "+" : "")}{da}");
            NotifyChanged(card);

            if (card.IsDead) HandleDeath(card, null, !ownerIsPlayer);
        }

        // Brings the card's aura statuses in line with what the auras in play give it right now
        private void ApplyAuraStatuses(CardInstance card, Dictionary<CardStatusType, int> wanted)
        {
            bool changed = false;
            for (int i = card.statuses.Count - 1; i >= 0; i--)
            {
                var s = card.statuses[i];
                if (!s.fromAura) continue;
                int stacks = 0;
                if (wanted != null) wanted.TryGetValue(s.type, out stacks);
                if (stacks == s.stacks) continue;

                if (stacks <= 0) card.statuses.RemoveAt(i);
                else s.stacks = stacks;
                changed = true;
            }

            if (wanted != null)
            {
                foreach (var pair in wanted)
                {
                    if (card.statuses.Exists(s => s.fromAura && s.type == pair.Key)) continue;
                    card.statuses.Add(new CardStatus(pair.Key, pair.Value, 99) { fromAura = true });
                    changed = true;
                }
            }

            if (changed) NotifyChanged(card);
        }
    }
}
