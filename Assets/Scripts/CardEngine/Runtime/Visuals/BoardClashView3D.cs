using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    // One familiar's attack in a column. target == null means it hits the opposing player's Khwan.
    public class ClashStrike
    {
        public bool fromPlayer;
        public int column;          // the attacker's own column
        public CardInstance attacker;
        public CardInstance target; // opposing familiar being hit, or null for the player
        public int targetColumn;    // column of the target familiar (== column when hitting the player)
    }

    // Plays the end-of-turn board clash visuals. CombatManager decides who hits whom and applies the
    // damage through the callback at the moment of impact; this view only moves the physical cards:
    //  - HeadOn: the two familiars in a column charge each other and collide in the middle
    //  - Strike: one familiar charges its target (a familiar in some column, or the player's side)
    // Cards return to their slots afterwards; familiars that died are shrunk away. Bootstraps itself
    // in scenes that have both player and enemy board slots.
    public class BoardClashView3D : MonoBehaviour
    {
        public static BoardClashView3D Instance { get; private set; }

        [Header("Motion")]
        [Tooltip("How high the cards lift while charging.")]
        public float liftHeight = 0.5f;
        [Tooltip("Distance kept between the attacker and what it hits, along the charge direction.")]
        public float collisionGap = 0.35f;
        public float chargeDuration = 0.25f;
        public float impactPause = 0.2f;
        public float returnDuration = 0.3f;
        public float impactPunch = 0.2f;
        public float deathDuration = 0.3f;

        private readonly Dictionary<int, BoardSlotView> playerSlots = new Dictionary<int, BoardSlotView>();
        private readonly Dictionary<int, BoardSlotView> enemySlots = new Dictionary<int, BoardSlotView>();
        private readonly List<CardInstance> died = new List<CardInstance>();
        private EffectResolver resolver;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<CombatManager>() == null) return;
            if (FindFirstObjectByType<BoardClashView3D>() != null) return;

            bool hasPlayer = false, hasEnemy = false;
            foreach (var slot in FindObjectsByType<BoardSlotView>(FindObjectsSortMode.None))
            {
                if (!IsBoardSlot(slot)) continue;
                if (slot.side == BoardSlotView.SlotSide.Player) hasPlayer = true;
                else hasEnemy = true;
            }

            if (hasPlayer && hasEnemy) new GameObject("BoardClashView3D").AddComponent<BoardClashView3D>();
        }

        // The deck slots share BoardSlotView but are not board columns
        private static bool IsBoardSlot(BoardSlotView slot)
        {
            return slot.GetComponent<DeckPileView3D>() == null && !slot.name.Contains("Deck");
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            foreach (var slot in FindObjectsByType<BoardSlotView>(FindObjectsSortMode.None))
            {
                if (!IsBoardSlot(slot)) continue;
                if (slot.side == BoardSlotView.SlotSide.Player) playerSlots[slot.slotIndex] = slot;
                else enemySlots[slot.slotIndex] = slot;
            }
        }

        private void Start()
        {
            resolver = EffectResolver.Instance;
            if (resolver == null) return;
            resolver.OnFamiliarDamaged += HandleDamaged;
            resolver.OnFamiliarDied += HandleDied;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (resolver == null) return;
            resolver.OnFamiliarDamaged -= HandleDamaged;
            resolver.OnFamiliarDied -= HandleDied;
        }

        private void HandleDamaged(CardInstance card, int damage)
        {
            var view = FindView(card);
            if (view != null) view.RefreshLabel();
        }

        private void HandleDied(CardInstance card)
        {
            died.Add(card);
        }

        // The card physically sitting in the player's slot for this column (null if none)
        public CardInstance PlayerCardAt(int column)
        {
            var view = ViewIn(playerSlots, column);
            return view != null ? view.CardData : null;
        }

        private static CardView3D ViewIn(Dictionary<int, BoardSlotView> slots, int column)
        {
            return slots.TryGetValue(column, out var slot) ? slot.GetComponentInChildren<CardView3D>() : null;
        }

        private CardView3D FindView(CardInstance card)
        {
            foreach (var slot in playerSlots.Values)
            {
                var v = slot.GetComponentInChildren<CardView3D>();
                if (v != null && v.CardData == card) return v;
            }
            foreach (var slot in enemySlots.Values)
            {
                var v = slot.GetComponentInChildren<CardView3D>();
                if (v != null && v.CardData == card) return v;
            }
            return null;
        }

        // Two familiars in one column hit each other at the same moment
        public IEnumerator HeadOn(int column, ClashStrike playerStrike, ClashStrike enemyStrike, Action<ClashStrike> apply)
        {
            var playerView = ViewIn(playerSlots, column);
            var enemyView = ViewIn(enemySlots, column);
            playerSlots.TryGetValue(column, out var pSlot);
            enemySlots.TryGetValue(column, out var eSlot);

            if (pSlot == null || eSlot == null || playerView == null || enemyView == null)
            {
                apply(playerStrike);
                apply(enemyStrike);
                yield return ProcessDeaths();
                yield break;
            }

            Vector3 pPos = pSlot.transform.position;
            Vector3 ePos = eSlot.transform.position;
            Vector3 dir = Direction(pPos, ePos);
            Vector3 mid = (pPos + ePos) * 0.5f + Vector3.up * liftHeight;

            var seq = DOTween.Sequence();
            seq.Append(playerView.transform.DOMove(mid - dir * collisionGap, chargeDuration).SetEase(Ease.InQuad));
            seq.Join(enemyView.transform.DOMove(mid + dir * collisionGap, chargeDuration).SetEase(Ease.InQuad));
            seq.AppendCallback(() =>
            {
                apply(playerStrike);
                apply(enemyStrike);
                Punch(playerView);
                Punch(enemyView);
            });
            seq.AppendInterval(impactPause);
            seq.Append(playerView.transform.DOLocalMove(Vector3.zero, returnDuration).SetEase(Ease.OutQuad));
            seq.Join(enemyView.transform.DOLocalMove(Vector3.zero, returnDuration).SetEase(Ease.OutQuad));

            yield return seq.WaitForCompletion();
            playerView.transform.localPosition = Vector3.zero;
            enemyView.transform.localPosition = Vector3.zero;
            yield return ProcessDeaths();
        }

        // One familiar charges its target: a familiar in target column, or the opposing side's slot in
        // its own column when it is hitting the player
        public IEnumerator Strike(ClashStrike strike, Action<ClashStrike> apply)
        {
            var attackerSlots = strike.fromPlayer ? playerSlots : enemySlots;
            var opposingSlots = strike.fromPlayer ? enemySlots : playerSlots;

            var attackerView = ViewIn(attackerSlots, strike.column);
            attackerSlots.TryGetValue(strike.column, out var aSlot);
            opposingSlots.TryGetValue(strike.targetColumn, out var tSlot);

            if (attackerView == null || aSlot == null || tSlot == null)
            {
                apply(strike);
                yield return ProcessDeaths();
                yield break;
            }

            var targetView = strike.target != null ? ViewIn(opposingSlots, strike.targetColumn) : null;

            Vector3 aPos = aSlot.transform.position;
            Vector3 tPos = tSlot.transform.position;
            Vector3 dir = Direction(aPos, tPos);
            Vector3 chargeTo = tPos + Vector3.up * liftHeight - dir * collisionGap;

            var seq = DOTween.Sequence();
            seq.Append(attackerView.transform.DOMove(chargeTo, chargeDuration * 1.5f).SetEase(Ease.InQuad));
            seq.AppendCallback(() =>
            {
                apply(strike);
                Punch(attackerView);
                if (targetView != null) Punch(targetView);
            });
            seq.AppendInterval(impactPause);
            seq.Append(attackerView.transform.DOLocalMove(Vector3.zero, returnDuration).SetEase(Ease.OutQuad));

            yield return seq.WaitForCompletion();
            attackerView.transform.localPosition = Vector3.zero;
            yield return ProcessDeaths();
        }

        private static Vector3 Direction(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
        }

        private void Punch(CardView3D view)
        {
            if (view != null) view.transform.DOPunchScale(Vector3.one * impactPunch, impactPause, 8);
        }

        // Familiars whose Khwan hit 0 shrink away once the cards are back on their slots
        private IEnumerator ProcessDeaths()
        {
            if (died.Count == 0) yield break;

            foreach (var card in died)
            {
                var view = FindView(card);
                if (view == null) continue;

                view.transform.DOKill();
                view.transform.DOScale(Vector3.zero, deathDuration).SetEase(Ease.InBack)
                    .OnComplete(() => { if (view != null) Destroy(view.gameObject); });
            }
            died.Clear();
            yield return new WaitForSeconds(deathDuration);
        }
    }
}
