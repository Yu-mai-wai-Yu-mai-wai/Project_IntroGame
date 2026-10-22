using System;
using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    public partial class EffectResolver : MonoBehaviour, IEffectResolver
    {
        public static EffectResolver Instance { get; private set; }

        public event Action<int, bool> OnDamageDealt;
        public event Action<int, bool> OnShieldGranted;
        public event Action<StatusEffectType, int, bool> OnStatusApplied;
        public event Action<CardInstance, int> OnSlotOccupied;
        public event Action<int> OnSlotCleared;
        public event Action<CardInstance, int> OnEnemySlotOccupied;
        public event Action<int> OnEnemySlotCleared;
        public event Action<CardInstance, int> OnFamiliarDamaged; // familiar, damage
        public event Action<CardInstance> OnFamiliarDied;
        // A card put on the board by an ability rather than played from the hand (คาถาเรียกผี); the player's
        // board has no view for it yet. card, onPlayerSide
        public event Action<CardInstance, bool> OnCardSummoned;

        public const int MaxBoardSlots = 5;
        // Column 0 (far left) is each side's amulet slot: one amulet at a time, a new one replaces it.
        // Columns 1-4 hold familiars. Amulets do not take part in the clash.
        public const int AmuletSlot = 0;
        public const int FirstFamiliarSlot = 1;
        public static bool IsFamiliarSlot(int slot) => slot >= FirstFamiliarSlot && slot < MaxBoardSlots;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            catalog = CardCatalogSO.Load();
        }

        public void ResolveCardEffect(CardInstance card, object target = null)
        {
            ResolveCardEffect(card, target, casterIsPlayer: true);
        }

        // casterIsPlayer = false: the enemy is playing the card, so "enemy" targets mean the player
        public void ResolveCardEffect(CardInstance card, object target, bool casterIsPlayer)
        {
            if (card == null) return;

            switch (card.cardType)
            {
                case CardType.Incantation:
                    ExecuteIncantation(card, target, casterIsPlayer);
                    break;
                case CardType.Amulet:
                case CardType.Familiar:
                    bool placed = casterIsPlayer ? PlaceBoardCard(card) : PlaceEnemyBoardCard(card);
                    if (placed) RunOnPlayAbilities(card, target, casterIsPlayer);
                    break;
            }

            // A card can die from the play itself, not only from abilities: an aura in play takes it to 0 Khwan as it
            // lands (ผ้าประเจียดดำ on a 1-Khwan familiar). It leaves the board now, not at the next clash.
            RemoveDeadFamiliars();
        }

        private void RunOnPlayAbilities(CardInstance card, object target, bool casterIsPlayer)
        {
            if (card.abilities == null || card.abilities.Count == 0) return;

            TriggerAbilities(card, AbilityTrigger.OnPlay, casterIsPlayer, null, target);
            RemoveDeadFamiliars();
        }

        private void ExecuteIncantation(CardInstance card, object target, bool casterIsPlayer)
        {
            // Cards with abilities are fully data-driven; the rest keep the older baseValue/targetType rules
            if (card.abilities != null && card.abilities.Count > 0)
            {
                RunOnPlayAbilities(card, target, casterIsPlayer);
                return;
            }

            if (card.targetType == TargetType.SingleEnemy || card.targetType == TargetType.AllEnemies)
            {
                // Deal damage to the caster's opponent
                int damage = ApplyDamageStatusModifier(card.baseValue, attackerIsPlayer: casterIsPlayer);
                if (CombatManager.Instance != null)
                {
                    string cause = !casterIsPlayer && card != null ? $"ถูก {card.cardNameThai} โจมตี" : null;
                    CombatManager.Instance.TakeDamage(damage, toPlayer: !casterIsPlayer, cause: cause);
                }
                OnDamageDealt?.Invoke(damage, !casterIsPlayer);
            }
            else if (card.targetType == TargetType.Self)
            {
                // Grant shield (Khwan itself can only be restored at a temple, per design)
                int shield = card.baseValue;
                CombatManager.Instance?.AddShield(shield, toPlayer: casterIsPlayer);
                OnShieldGranted?.Invoke(shield, casterIsPlayer);
            }
        }

        private bool PlaceEnemyBoardCard(CardInstance card)
        {
            if (CombatManager.Instance == null) return false;

            var list = CombatManager.Instance.State.enemyBoardCards;
            if (!PutOnBoard(list, card, isPlayer: false)) return false;
            RefreshAuras();
            ResyncEnemySlots(list);
            Sheet7AllyEntered(card, isPlayer: false);
            return true;
        }

        private void ResyncEnemySlots(List<CardInstance> list)
        {
            for (int i = 0; i < MaxBoardSlots; i++)
            {
                var card = CardAt(list, i);
                if (card != null) OnEnemySlotOccupied?.Invoke(card, i);
                else OnEnemySlotCleared?.Invoke(i);
            }
        }

        // The card in board column `slot`, or null when that column is empty
        public static CardInstance CardAt(List<CardInstance> board, int slot)
        {
            if (board == null || slot < 0) return null;
            return board.Find(c => c != null && c.boardSlot == slot);
        }

        // The familiar column a new familiar goes to: the requested one when it is free, else the leftmost free
        // one (-1 = all four familiar columns are taken). The amulet slot is never handed out here.
        public static int FreeSlot(List<CardInstance> board, int preferred = -1)
        {
            if (IsFamiliarSlot(preferred) && CardAt(board, preferred) == null) return preferred;
            for (int i = FirstFamiliarSlot; i < MaxBoardSlots; i++)
            {
                if (CardAt(board, i) == null) return i;
            }
            return -1;
        }

        // True when all four familiar columns are taken (the amulet slot never fills up: a new amulet replaces the old)
        public bool IsBoardFull(bool playerSide)
        {
            if (CombatManager.Instance == null) return false;
            var list = playerSide ? CombatManager.Instance.State.activeBoardCards : CombatManager.Instance.State.enemyBoardCards;
            return FreeSlot(list) < 0;
        }

        // Places a card in a fixed column. An amulet always goes to the amulet slot and sends the amulet already there
        // to the graveyard. A familiar takes a free familiar column; returns false when there is none (CanResolve
        // stops that earlier).
        private bool PutOnBoard(List<CardInstance> list, CardInstance card, bool isPlayer)
        {
            if (card.cardType == CardType.Amulet)
            {
                var old = CardAt(list, AmuletSlot);
                if (old != null)
                {
                    list.Remove(old);
                    old.boardSlot = -1;
                    SendToGraveyard(old, isPlayer);
                    Debug.Log($"[EffectResolver] {card.cardNameThai} replaces {old.cardNameThai} in the amulet slot");
                }
                card.boardSlot = AmuletSlot;
                list.Add(card);
                return true;
            }

            int slot = FreeSlot(list, card.boardSlot);
            if (slot < 0)
            {
                Debug.LogWarning($"[EffectResolver] Board is full; {card.cardNameThai} cannot be placed");
                return false;
            }
            card.boardSlot = slot;
            list.Add(card);
            return true;
        }

        private int ApplyDamageStatusModifier(int baseDamage, bool attackerIsPlayer)
        {
            if (CombatManager.Instance == null) return baseDamage;

            // KhwanPhawa (ขวัญผวา): reduces the afflicted attacker's outgoing damage by 25%
            if (CombatManager.Instance.HasStatus(StatusEffectType.KhwanPhawa, attackerIsPlayer))
            {
                baseDamage = Mathf.Max(0, Mathf.RoundToInt(baseDamage * 0.75f));
            }
            return baseDamage;
        }

        private bool PlaceBoardCard(CardInstance card)
        {
            if (CombatManager.Instance == null) return false;

            var list = CombatManager.Instance.State.activeBoardCards;
            if (!PutOnBoard(list, card, isPlayer: true)) return false;
            RefreshAuras();
            ResyncSlots(list);
            Sheet7AllyEntered(card, isPlayer: true);
            return true;
        }

        private void ResyncSlots(List<CardInstance> list)
        {
            for (int i = 0; i < MaxBoardSlots; i++)
            {
                var card = CardAt(list, i);
                if (card != null) OnSlotOccupied?.Invoke(card, i);
                else OnSlotCleared?.Invoke(i);
            }
        }

        public void TickAmuletDurability()
        {
            if (CombatManager.Instance == null) return;

            var list = CombatManager.Instance.State.activeBoardCards;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].cardType != CardType.Amulet || list[i].currentDurability <= 0) continue;

                list[i].currentDurability--;
                if (list[i].currentDurability <= 0)
                {
                    list[i].boardSlot = -1;
                    list.RemoveAt(i);
                }
            }
            ResyncSlots(list);

            // Enemy amulets wear out too
            var enemyList = CombatManager.Instance.State.enemyBoardCards;
            for (int i = enemyList.Count - 1; i >= 0; i--)
            {
                if (enemyList[i].cardType != CardType.Amulet || enemyList[i].currentDurability <= 0) continue;

                enemyList[i].currentDurability--;
                if (enemyList[i].currentDurability <= 0)
                {
                    enemyList[i].boardSlot = -1;
                    enemyList.RemoveAt(i);
                }
            }
            ResyncEnemySlots(enemyList);

            // Amulets that wore out take their auras with them
            RemoveDeadFamiliars();
        }

        public void ResolveEnemyIntent(EnemyIntent intent, int value, StatusEffectType status = StatusEffectType.KhwanPhawa)
        {
            string enemyCause = CombatManager.Instance?.currentEnemyProfile != null && !string.IsNullOrEmpty(CombatManager.Instance.currentEnemyProfile.enemyName)
                ? $"ถูก {CombatManager.Instance.currentEnemyProfile.enemyName} โจมตี"
                : "ถูกโจมตี";

            switch (intent)
            {
                case EnemyIntent.Attack:
                case EnemyIntent.HeavyAttack:
                    int damage = ApplyDamageStatusModifier(value, attackerIsPlayer: false);
                    if (CombatManager.Instance != null)
                    {
                        CombatManager.Instance.TakeDamage(damage, toPlayer: true, cause: enemyCause);
                    }
                    OnDamageDealt?.Invoke(damage, true);
                    break;

                case EnemyIntent.Defend:
                    CombatManager.Instance?.AddShield(value, toPlayer: false);
                    OnShieldGranted?.Invoke(value, false);
                    break;

                case EnemyIntent.DebuffCurse:
                    ApplyStatusEffect(status, 2, toPlayer: true);
                    break;

                default:
                    int fallbackDamage = ApplyDamageStatusModifier(value, attackerIsPlayer: false);
                    if (CombatManager.Instance != null)
                    {
                        CombatManager.Instance.TakeDamage(fallbackDamage, toPlayer: true, cause: enemyCause);
                    }
                    OnDamageDealt?.Invoke(fallbackDamage, true);
                    break;
            }
        }

        public void ResolveMinionCombat()
        {
            if (CombatManager.Instance == null) return;

            var board = CombatManager.Instance.State.activeBoardCards;
            for (int i = board.Count - 1; i >= 0; i--)
            {
                ResolveFamiliarAttack(board[i], fromPlayer: true);
            }
        }

        public static bool IsAliveFamiliar(CardInstance card)
        {
            return card != null && card.cardType == CardType.Familiar && card.familiarHealth > 0;
        }

        // Cards that can strike in the clash: live familiars. Amulets sit in their own slot and never fight.
        public static bool IsStrikerType(CardInstance card)
        {
            return card != null && card.cardType == CardType.Familiar && card.familiarHealth > 0;
        }

        // Cards a familiar can hit: live familiars (amulets in the amulet slot cannot be struck)
        public static bool IsStrikeable(CardInstance card)
        {
            if (card == null || card.IsDead) return false;
            return card.cardType == CardType.Familiar;
        }

        // A familiar strikes: the target card loses Khwan (armor first); with no target the hit lands on the
        // opposing player's Khwan. Attackers are not checked for being alive so that two familiars can
        // trade blows at the same moment. Multi-strikers also hit the cards beside the target.
        // isCrit (rolled beforehand with RollCrit) doubles the damage.
        public void ResolveFamiliarStrike(CardInstance attacker, CardInstance targetFamiliar, bool fromPlayer, bool isCrit = false)
        {
            if (!CanStrike(attacker)) return;

            int attack = CombatManager.Instance != null
                ? ApplyDamageStatusModifier(EffectiveAttack(attacker), attackerIsPlayer: fromPlayer)
                : EffectiveAttack(attacker);
            if (attack <= 0) return;

            if (isCrit)
            {
                attack *= CritDamageMultiplier;
                Debug.Log($"[Clash] {attacker.cardNameThai} lands a critical hit ({attack})");
            }

            var targets = CollectStrikeTargets(attacker, targetFamiliar, fromPlayer, out int faceHits);
            if (targets.Count == 0 && faceHits == 0)
            {
                HitOpposingPlayer(attacker, attack, fromPlayer);
            }
            else
            {
                // ควายธนู: each empty column in front of it is a hit on the opposing Khwan
                for (int i = 0; i < faceHits; i++) HitOpposingPlayer(attacker, attack, fromPlayer);
                foreach (var t in targets)
                {
                    int hit = StrikeDamageOn(attacker, t, attack); // ผีตายโหง on a cursed card
                    Debug.Log($"[Clash] {attacker.cardNameThai} hits {t.cardNameThai} for {hit}");
                    int khwanBefore = t.familiarHealth;
                    DamageCard(t, hit, ignoreArmor: false, attacker, fromPlayer);
                    PierceOnStrike(attacker, t, hit, khwanBefore, fromPlayer);
                    ThornsOnStrike(attacker, t, fromPlayer);
                    // ผีตายโหง: riders on the card it just hit
                    if (!t.IsDead) TriggerAbilities(attacker, AbilityTrigger.OnHit, fromPlayer, t);
                }
            }

            // กรรมตามสนอง: the damage dealt comes back on the attacker
            if (HasCardStatus(attacker, CardStatusType.Karma)) DamageCard(attacker, attack, ignoreArmor: true, source: null, sourceIsPlayer: !fromPlayer);
        }

        // Called once the whole clash is over. The other cards keep their columns.
        // Dead player cards go to the graveyard so they can be brought back.
        public void RemoveDeadFamiliars()
        {
            if (CombatManager.Instance == null) return;

            // Removing a card can end its aura, and losing an aura can (rarely) kill another card
            for (int pass = 0; pass < 5; pass++)
            {
                bool removed = RemoveDeadFrom(CombatManager.Instance.State.activeBoardCards, true);
                removed |= RemoveDeadFrom(CombatManager.Instance.State.enemyBoardCards, false);
                RefreshAuras();
                if (!removed) break;
            }
            PlacePendingSummons(); // หุ่นพยนต์ takes the dead card's slot once it is free
        }

        private bool RemoveDeadFrom(List<CardInstance> list, bool isPlayer)
        {
            var dead = list.FindAll(c => c.IsDead);
            if (dead.Count == 0) return false;

            list.RemoveAll(c => c.IsDead);
            foreach (var c in dead)
            {
                c.boardSlot = -1;
                if (!c.exileOnDeath) SendToGraveyard(c, isPlayer); // revived or returned to hand: not to the graveyard
            }
            if (isPlayer) ResyncSlots(list);
            else ResyncEnemySlots(list);
            return true;
        }

        // One familiar attacks the opposing side's Khwan directly
        public void ResolveFamiliarAttack(CardInstance card, bool fromPlayer)
        {
            if (!IsStrikerType(card)) return;
            ResolveFamiliarStrike(card, null, fromPlayer, RollCrit(card, fromPlayer));
        }

        private void HitOpposingPlayer(CardInstance card, int damage, bool fromPlayer)
        {
            var combat = CombatManager.Instance;
            if (combat == null) return;

            int shield = fromPlayer ? combat.CurrentEnemyShield : combat.CurrentPlayerShield;
            Debug.Log($"[Clash] {card.cardNameThai} hits {(fromPlayer ? "enemy" : "player")} Khwan for {damage} (target shield {shield} absorbs first)");
            string cause = null;
            if (!fromPlayer && card != null)
            {
                cause = HasKeyword(card, AbilityEffect.Overhead)
                    ? $"{card.cardNameThai}บินข้ามมา"
                    : $"ถูก {card.cardNameThai} โจมตี";
            }
            combat.TakeDamage(damage, toPlayer: !fromPlayer, cause: cause);
            OnDamageDealt?.Invoke(damage, !fromPlayer);
        }

        // Enemy familiars attack the player
        public void ResolveEnemyMinionCombat()
        {
            if (CombatManager.Instance == null) return;

            var board = CombatManager.Instance.State.enemyBoardCards;
            for (int i = board.Count - 1; i >= 0; i--)
            {
                ResolveFamiliarAttack(board[i], fromPlayer: false);
            }
        }

        public void ApplyStatusEffect(StatusEffectType status, int duration, bool toPlayer)
        {
            CombatManager.Instance?.ApplyStatus(status, duration, toPlayer);
            OnStatusApplied?.Invoke(status, duration, toPlayer);
        }

        public void TriggerCurseBackfire()
        {
            TriggerCurseBackfire(toPlayer: true);
        }

        // Khwan a side loses when its Corruption fills up
        public const int CurseBackfireKhwanLoss = 5;

        // Crossing the Corruption threshold takes CurseBackfireKhwanLoss khwan straight off (no shield, no status
        // reduces it), but never below 1: with less left it drops to 1, and only a side already at 1 dies of it
        public void TriggerCurseBackfire(bool toPlayer)
        {
            var combat = CombatManager.Instance;
            if (combat == null || combat.State == null) return;
            var state = combat.State;
            int before = toPlayer ? state.playerKhwan : state.enemyKhwan;
            int after = before <= 1 ? 0 : Mathf.Max(1, before - CurseBackfireKhwanLoss);
            if (toPlayer) state.playerKhwan = after;
            else state.enemyKhwan = after;
            Debug.LogWarning($"[EffectResolver] Curse backfire triggered! {(toPlayer ? "Player" : "Enemy")} khwan {before} -> {after}.");

            int lost = before - after;
            if (lost > 0)
            {
                OnDamageDealt?.Invoke(lost, toPlayer);
                if (toPlayer)
                {
                    TawanOS.UI.PlayerNotice.Show($"−{lost} ขวัญ คำสาปย้อน", TawanOS.UI.NoticeKind.Warning);
                }
            }
            // Decides the outcome now if that was the last of it
            combat.BeginOutcomeBatch();
            combat.EndOutcomeBatch();
        }
    }
}
