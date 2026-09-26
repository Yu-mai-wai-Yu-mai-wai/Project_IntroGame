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
        public bool isCrit;         // critical hit: double damage, and the card pulls back to charge first
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
        public float liftHeight = 0.8f;
        [Tooltip("Distance kept between the attacker and what it hits, along the charge direction.")]
        public float collisionGap = 0.7f;
        public float chargeDuration = 0.25f;
        public float impactPause = 0.2f;
        public float returnDuration = 0.3f;
        public float impactPunch = 0.2f;
        public float deathDuration = 0.3f;

        [Header("Critical Hit Wind-up")]
        [Tooltip("How far a critical striker pulls back (away from its target) before charging.")]
        public float critPullBack = 1.1f;
        [Tooltip("Extra lift while pulling back for a critical hit.")]
        public float critLift = 0.3f;
        public float critPullBackDuration = 0.3f;
        [Tooltip("How long the card holds at the back, charging, before it strikes.")]
        public float critChargeHold = 0.25f;
        [Tooltip("Charge time multiplier for a critical strike (lower = faster lunge).")]
        public float critChargeSpeed = 0.7f;
        public float critImpactPunch = 0.4f;

        private readonly Dictionary<int, BoardSlotView> playerSlots = new Dictionary<int, BoardSlotView>();
        private readonly Dictionary<int, BoardSlotView> enemySlots = new Dictionary<int, BoardSlotView>();
        private readonly List<CardInstance> died = new List<CardInstance>();
        private EffectResolver resolver;

        // AfterSceneLoad fires only for the first scene played; the combat scene is usually loaded later from the map.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void HookSceneLoads()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded; // no double hook without domain reload
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => Bootstrap();

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
            bool anyCrit = playerStrike.isCrit || enemyStrike.isCrit;
            if (anyCrit)
            {
                // Critical strikers pull back and charge up first; the other card waits in its slot
                if (playerStrike.isCrit) AppendWindUp(seq, playerView, pPos, -dir, join: false);
                if (enemyStrike.isCrit) AppendWindUp(seq, enemyView, ePos, dir, join: playerStrike.isCrit);
                seq.AppendInterval(critChargeHold);
            }
            float charge = anyCrit ? chargeDuration * critChargeSpeed : chargeDuration;
            seq.Append(playerView.transform.DOMove(mid - dir * collisionGap, charge).SetEase(Ease.InQuad));
            seq.Join(enemyView.transform.DOMove(mid + dir * collisionGap, charge).SetEase(Ease.InQuad));
            seq.AppendCallback(() =>
            {
                apply(playerStrike);
                apply(enemyStrike);
                Punch(playerView, anyCrit);
                Punch(enemyView, anyCrit);
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
            float charge = chargeDuration * 1.5f;
            if (strike.isCrit)
            {
                // Pull back away from the target, hold to charge up, then lunge in faster
                AppendWindUp(seq, attackerView, aPos, -dir, join: false);
                seq.AppendInterval(critChargeHold);
                charge *= critChargeSpeed;
            }
            seq.Append(attackerView.transform.DOMove(chargeTo, charge).SetEase(Ease.InQuad));
            seq.AppendCallback(() =>
            {
                apply(strike);
                Punch(attackerView, strike.isCrit);
                if (targetView != null) Punch(targetView, strike.isCrit);
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

        // Critical wind-up: the card backs off from its slot (away from the target) and rises
        private void AppendWindUp(Sequence seq, CardView3D view, Vector3 slotPos, Vector3 awayDir, bool join)
        {
            Vector3 back = slotPos + awayDir * critPullBack + Vector3.up * (liftHeight + critLift);
            var move = view.transform.DOMove(back, critPullBackDuration).SetEase(Ease.OutQuad);
            if (join) seq.Join(move);
            else seq.Append(move);
        }

        private void Punch(CardView3D view, bool crit = false)
        {
            if (view != null) view.transform.DOPunchScale(Vector3.one * (crit ? critImpactPunch : impactPunch), impactPause, 8);
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
