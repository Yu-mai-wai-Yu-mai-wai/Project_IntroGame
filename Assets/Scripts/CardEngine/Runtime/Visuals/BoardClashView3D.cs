using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using TawanOS.VFX;

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
    // DOTween carries each card along its path; the poses come from the CardClash animator (CardClashRig),
    // one state per step (WindUp, Charge, Impact, Return, Hit, Die...), and each clip's length is that
    // step's duration. The damage lands on the OnImpact Animation Event. Cards return to their slots
    // afterwards; familiars that died play Die. Bootstraps itself in scenes that have both player and
    // enemy board slots.
    public class BoardClashView3D : MonoBehaviour
    {
        public static BoardClashView3D Instance { get; private set; }

        [Header("Path (the poses and timing are the CardClash animator clips)")]
        [Tooltip("How high the cards lift while charging.")]
        public float liftHeight = 0.8f;
        [Tooltip("Distance kept between the attacker and what it hits, along the charge direction.")]
        public float collisionGap = 0.7f;

        private readonly Dictionary<int, BoardSlotView> playerSlots = new Dictionary<int, BoardSlotView>();
        private readonly Dictionary<int, BoardSlotView> enemySlots = new Dictionary<int, BoardSlotView>();
        private readonly List<CardInstance> died = new List<CardInstance>();
        private EffectResolver resolver;
        private float impactWaited;

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

            playerView.transform.DOKill();
            enemyView.transform.DOKill();
            var pRig = new CardClashRig(playerView.transform, pSlot.transform, dir);
            var eRig = new CardClashRig(enemyView.transform, eSlot.transform, -dir);

            // Critical strikers pull back and charge up first; the other card waits in its slot
            float windUp = 0f;
            if (playerStrike.isCrit) windUp = Mathf.Max(windUp, pRig.Play(CardClashRig.WindUp));
            if (enemyStrike.isCrit) windUp = Mathf.Max(windUp, eRig.Play(CardClashRig.WindUp));
            if (windUp > 0f) yield return new WaitForSeconds(windUp);

            float charge = Mathf.Max(pRig.Play(ChargeState(playerStrike)), eRig.Play(ChargeState(enemyStrike)));
            var seq = DOTween.Sequence();
            seq.Append(pRig.Mover.DOMove(mid - dir * collisionGap, charge).SetEase(Ease.InQuad));
            seq.Join(eRig.Mover.DOMove(mid + dir * collisionGap, charge).SetEase(Ease.InQuad));
            yield return seq.WaitForCompletion();

            // Both cards play their impact; the damage lands on the first OnImpact event
            float impact = Mathf.Max(pRig.Play(ImpactState(playerStrike)), eRig.Play(ImpactState(enemyStrike)));
            yield return WaitForImpact(impact, pRig, eRig);
            apply(playerStrike);
            apply(enemyStrike);
            if (impact > impactWaited) yield return new WaitForSeconds(impact - impactWaited);

            float back = Mathf.Max(pRig.Play(CardClashRig.Return), eRig.Play(CardClashRig.Return));
            seq = DOTween.Sequence();
            seq.Append(pRig.Mover.DOLocalMove(pRig.Home, back).SetEase(Ease.OutQuad));
            seq.Join(eRig.Mover.DOLocalMove(eRig.Home, back).SetEase(Ease.OutQuad));
            yield return seq.WaitForCompletion();

            pRig.Release();
            eRig.Release();
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

            attackerView.transform.DOKill();
            var aRig = new CardClashRig(attackerView.transform, aSlot.transform, dir);
            CardClashRig tRig = null;
            if (targetView != null)
            {
                targetView.transform.DOKill();
                tRig = new CardClashRig(targetView.transform, tSlot.transform, -dir); // +Z toward the attacker
            }

            // Critical strikers pull back and charge up before the lunge
            if (strike.isCrit) yield return new WaitForSeconds(aRig.Play(CardClashRig.WindUp));

            float charge = aRig.Play(ChargeState(strike));
            yield return aRig.Mover.DOMove(chargeTo, charge).SetEase(Ease.InQuad).WaitForCompletion();

            float impact = aRig.Play(ImpactState(strike));
            yield return WaitForImpact(impact, aRig);
            apply(strike);
            float hit = tRig != null ? tRig.Play(strike.isCrit ? CardClashRig.HitCrit : CardClashRig.Hit) : 0f;
            float rest = Mathf.Max(impact - impactWaited, hit);
            if (rest > 0f) yield return new WaitForSeconds(rest);

            float back = aRig.Play(CardClashRig.Return);
            yield return aRig.Mover.DOLocalMove(aRig.Home, back).SetEase(Ease.OutQuad).WaitForCompletion();

            aRig.Release();
            tRig?.Release();
            yield return ProcessDeaths();
        }

        private static string ChargeState(ClashStrike strike)
        {
            return strike.isCrit ? CardClashRig.ChargeCrit : CardClashRig.Charge;
        }

        private static string ImpactState(ClashStrike strike)
        {
            return strike.isCrit ? CardClashRig.ImpactCrit : CardClashRig.Impact;
        }

        // Waits (at most `limit` seconds) for the first OnImpact event among the rigs; impactWaited is how
        // long it took. A clip without the event hits at once.
        private IEnumerator WaitForImpact(float limit, params CardClashRig[] rigs)
        {
            impactWaited = 0f;
            bool expected = false;
            foreach (var rig in rigs) expected |= rig.ExpectsImpact;
            if (!expected) yield break;

            while (impactWaited < limit)
            {
                foreach (var rig in rigs)
                {
                    if (rig.ExpectsImpact && !rig.WaitingForImpact) yield break;
                }
                yield return null;
                impactWaited += Time.deltaTime;
            }
        }

        private static Vector3 Direction(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
        }

        // The way a card in this slot faces: toward the slot across from it
        private Vector3 FacingOf(BoardSlotView slot)
        {
            if (slot == null) return Vector3.forward;
            var across = slot.side == BoardSlotView.SlotSide.Player ? enemySlots : playerSlots;
            return across.TryGetValue(slot.slotIndex, out var other) ? Direction(slot.transform.position, other.transform.position) : Vector3.forward;
        }

        // Familiars whose Khwan hit 0 play Die once the cards are back on their slots, then are removed
        private IEnumerator ProcessDeaths()
        {
            if (died.Count == 0) yield break;

            float longest = 0f;
            foreach (var card in died)
            {
                var view = FindView(card);
                if (view == null) continue;

                var slot = view.GetComponentInParent<BoardSlotView>();
                view.transform.DOKill();
                var rig = new CardClashRig(view.transform, slot != null ? slot.transform : view.transform.parent, FacingOf(slot));
                float length = rig.Play(CardClashRig.Die);
                rig.DestroyWithCard(length);
                longest = Mathf.Max(longest, length);
            }
            died.Clear();
            if (longest > 0f) yield return new WaitForSeconds(longest);
        }
    }
}
