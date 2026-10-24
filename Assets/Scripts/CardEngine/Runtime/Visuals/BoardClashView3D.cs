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
        public bool overhead;       // ตีข้ามหัว over a card in the opposite slot: plays the *Face clips
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
        [Tooltip("Seconds an attacker takes, at the start of its lunge, to turn from straight across toward a target in another column.")]
        public float aimTurnTime = 0.15f;
        [Tooltip("How fast the lunge at the target plays: the Charge clips and the move toward the target both run this many times faster (1 = the clip's own length).")]
        [Min(0.1f)] public float chargeSpeed = 1.6f;
        [Tooltip("Height above the table of the burst when a familiar hits the player directly (FaceHitVfx).")]
        public float faceHitHeight = 0.6f;
        [Tooltip("How far past the opposing row (away from the attacker's row, toward the character) a familiar hitting the player directly goes.")]
        public float faceHitReach = 1.5f;

        [Header("Charge tail (empty = Resources/ClashTrail)")]
        [Tooltip("Tail behind a black-magic familiar while it lunges.")]
        public ClashChargeTrail blackChargeTrail;
        [Tooltip("Tail behind a white-magic familiar while it lunges.")]
        public ClashChargeTrail whiteChargeTrail;

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
            // Load (and warm up) the clash effects now, not on the first hit
            FaceHitVfx.Prepare(gameObject.scene);
            if (blackChargeTrail == null) blackChargeTrail = Resources.Load<ClashChargeTrail>(ClashChargeTrail.BlackResource);
            if (whiteChargeTrail == null) whiteChargeTrail = Resources.Load<ClashChargeTrail>(ClashChargeTrail.WhiteResource);
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
            // Mid-strike the attacker and its target are out of their slots, in a CardClashRig at the scene root,
            // and that is exactly when the damage lands: look for the card anywhere
            foreach (var v in FindObjectsByType<CardView3D>(FindObjectsSortMode.None))
            {
                if (v.CardData == card) return v;
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

            // Both get ready in their slots first: critical strikers pull back and charge up (WindUp), the
            // others play Ready
            float windUp = Mathf.Max(GetReady(pRig, playerStrike, false), GetReady(eRig, enemyStrike, false));
            if (windUp > 0f) yield return new WaitForSeconds(windUp);

            float charge = Mathf.Max(pRig.Play(ChargeState(playerStrike), chargeSpeed), eRig.Play(ChargeState(enemyStrike), chargeSpeed));
            var pTrail = StartTrail(playerStrike.attacker, playerView.transform);
            var eTrail = StartTrail(enemyStrike.attacker, enemyView.transform);
            var seq = DOTween.Sequence();
            seq.Append(pRig.Mover.DOMove(mid - dir * collisionGap, charge).SetEase(Ease.InQuad));
            seq.Join(eRig.Mover.DOMove(mid + dir * collisionGap, charge).SetEase(Ease.InQuad));
            yield return seq.WaitForCompletion();
            if (pTrail != null) pTrail.Stop();
            if (eTrail != null) eTrail.Stop();

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

        // One familiar charges its target: a familiar in target column, or the middle of the opposing side's
        // row when it is hitting the player
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
            bool face = strike.overhead;

            Vector3 aPos = aSlot.transform.position;
            // Hitting the player (no target card): the card goes straight across its own lane, past the opposing
            // side's slot in that column, toward the character behind it (Game Designer, 24 Oct: not to the middle)
            Vector3 tPos = strike.target != null ? tSlot.transform.position : FaceHitPoint(aSlot, attackerSlots, opposingSlots);
            Vector3 dir = Direction(aPos, tPos);
            Vector3 chargeTo = tPos + Vector3.up * liftHeight - dir * collisionGap;

            attackerView.transform.DOKill();
            // The attacker gets ready facing straight across, as in the clip preview: aimed at a target in
            // another column (or the row centre) the clips' sideways moves would also go forward. It turns
            // toward the target as it lunges.
            var aRig = new CardClashRig(attackerView.transform, aSlot.transform, FacingOf(aSlot));
            CardClashRig tRig = null;
            if (targetView != null)
            {
                targetView.transform.DOKill();
                tRig = new CardClashRig(targetView.transform, tSlot.transform, -dir); // +Z toward the attacker
            }

            // Get ready in the slot before the lunge (critical strikers pull back and charge up)
            float ready = GetReady(aRig, strike, face);
            if (ready > 0f) yield return new WaitForSeconds(ready);

            float charge = aRig.Play(Pick(face, strike.isCrit ? CardClashRig.ChargeFaceCrit : CardClashRig.ChargeFace, ChargeState(strike)), chargeSpeed);
            Quaternion aimFrom = aRig.CurrentAim;
            Quaternion aimTo = CardClashRig.AimRotation(dir);
            // The card's head turns the same way, so it points where it lunges
            Quaternion headTo = aimTo * Quaternion.Inverse(aimFrom);
            DOTween.To(() => 0f, t =>
                {
                    aRig.SetAim(Quaternion.Slerp(aimFrom, aimTo, t));
                    aRig.SetHeading(Quaternion.Slerp(Quaternion.identity, headTo, t));
                }, 1f, Mathf.Min(charge, aimTurnTime))
                .SetEase(Ease.OutQuad).SetTarget(aRig.Mover);
            var trail = StartTrail(strike.attacker, attackerView.transform);
            yield return aRig.Mover.DOMove(chargeTo, charge).SetEase(Ease.InQuad).WaitForCompletion();
            if (trail != null) trail.Stop();
            aRig.SetAim(aimTo);
            aRig.SetHeading(headTo);

            float impact = aRig.Play(Pick(face, strike.isCrit ? CardClashRig.ImpactFaceCrit : CardClashRig.ImpactFace, ImpactState(strike)));
            yield return WaitForImpact(impact, aRig);
            apply(strike);
            // A direct hit on the player: hit-stop and shake; the screen edges burst in the attacker's school
            // only when the hit ends the game
            if (strike.target == null && strike.attacker != null)
                FaceHitVfx.Play(tPos + Vector3.up * faceHitHeight, dir, strike.attacker.magicSchool == MagicSchool.BlackMagic, strike.isCrit,
                    IsFinisher(strike), gameObject.scene);
            float hit = tRig != null ? tRig.Play(strike.isCrit ? CardClashRig.HitCrit : CardClashRig.Hit) : 0f;
            float rest = Mathf.Max(impact - impactWaited, hit);
            if (rest > 0f) yield return new WaitForSeconds(rest);

            float back = aRig.Play(Pick(face, CardClashRig.ReturnFace, CardClashRig.Return));
            // Turns back to face straight across on the way home
            DOTween.To(() => 0f, t => aRig.SetHeading(Quaternion.Slerp(headTo, Quaternion.identity, t)), 1f, back)
                .SetEase(Ease.InOutQuad).SetTarget(aRig.Mover);
            yield return aRig.Mover.DOLocalMove(aRig.Home, back).SetEase(Ease.OutQuad).WaitForCompletion();

            aRig.Release();
            tRig?.Release();
            yield return ProcessDeaths();
        }

        // A tail in the attacker's school behind the lunging card; null when there is no prefab
        private ClashChargeTrail StartTrail(CardInstance attacker, Transform card)
        {
            bool black = attacker != null && attacker.magicSchool == MagicSchool.BlackMagic;
            var prefab = black
                ? (blackChargeTrail != null ? blackChargeTrail : blackChargeTrail = Resources.Load<ClashChargeTrail>(ClashChargeTrail.BlackResource))
                : (whiteChargeTrail != null ? whiteChargeTrail : whiteChargeTrail = Resources.Load<ClashChargeTrail>(ClashChargeTrail.WhiteResource));
            return ClashChargeTrail.Spawn(prefab, card);
        }

        // The strike's damage (already applied) took the opposing player's khwan to 0
        private static bool IsFinisher(ClashStrike strike)
        {
            var combat = CombatManager.Instance;
            if (combat == null || combat.State == null) return false;
            return strike.fromPlayer ? combat.State.enemyKhwan <= 0 : combat.State.playerKhwan <= 0;
        }

        // Plays WindUp (critical) or Ready in the slot and returns how long it lasts; 0 when the controller
        // has no Ready clip (the card then lunges at once)
        private static float GetReady(CardClashRig rig, ClashStrike strike, bool face)
        {
            if (strike.isCrit) return rig.Play(Pick(face, CardClashRig.WindUpFace, CardClashRig.WindUp));
            string ready = Pick(face, CardClashRig.ReadyFace, CardClashRig.Ready);
            return CardClashRig.Has(ready) ? rig.Play(ready) : 0f;
        }

        private static string ChargeState(ClashStrike strike)
        {
            return strike.isCrit ? CardClashRig.ChargeCrit : CardClashRig.Charge;
        }

        private static string ImpactState(ClashStrike strike)
        {
            return strike.isCrit ? CardClashRig.ImpactCrit : CardClashRig.Impact;
        }

        // The over-the-head (Overhead keyword) state when the controller has its clip, else the usual one
        private static string Pick(bool face, string faceState, string state)
        {
            return face && CardClashRig.Has(faceState) ? faceState : state;
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

        private Vector3 FaceHitPoint(BoardSlotView attackerSlot, Dictionary<int, BoardSlotView> attackerSlots,
            Dictionary<int, BoardSlotView> opposingSlots)
        {
            Vector3 across = Direction(RowCenter(attackerSlots), RowCenter(opposingSlots));
            // The slot across in the same column; without one, the attacker's own spot carried over to that row
            Vector3 lane = opposingSlots.TryGetValue(attackerSlot.slotIndex, out var opposite) && opposite != null
                ? opposite.transform.position
                : attackerSlot.transform.position + across * Vector3.Distance(RowCenter(attackerSlots), RowCenter(opposingSlots));
            return lane + across * faceHitReach;
        }

        private static Vector3 RowCenter(Dictionary<int, BoardSlotView> slots)
        {
            Vector3 sum = Vector3.zero;
            foreach (var slot in slots.Values) sum += slot.transform.position;
            return slots.Count > 0 ? sum / slots.Count : Vector3.zero;
        }

        private static Vector3 Direction(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
        }

        // The way a card in this slot faces: toward the slot across from it
        public Vector3 FacingOf(BoardSlotView slot)
        {
            if (slot == null) return Vector3.forward;
            var across = slot.side == BoardSlotView.SlotSide.Player ? enemySlots : playerSlots;
            return across.TryGetValue(slot.slotIndex, out var other) 
                ? Direction(slot.transform.position, other.transform.position) 
                : (slot.side == BoardSlotView.SlotSide.Player ? Vector3.forward : Vector3.back);
        }

        // Familiars whose Khwan hit 0 burn away (or play Die) once the cards are back on their slots, then are removed
        private IEnumerator ProcessDeaths()
        {
            if (died.Count == 0) yield break;

            float longest = 0f;
            foreach (var card in died)
            {
                var view = FindView(card);
                if (view == null || view.IsDying) continue;

                var slot = view.GetComponentInParent<BoardSlotView>();
                float length = view.PlayDeathAnimation(slot);
                longest = Mathf.Max(longest, length);
            }
            died.Clear();
            if (longest > 0f) yield return new WaitForSeconds(longest);
        }
    }
}
