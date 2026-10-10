using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.VFX
{
    // Wraps one board card for the duration of a clash, then puts it back:
    //   slot > ClashMover > ClashAim > ClashPivot (Animator) > ClashUnaim > card
    // ClashMover travels along the path (DOTween in BoardClashView3D), ClashAim turns +Z toward the other
    // card, and the Animator plays the CardClash clips on ClashPivot. The clips are therefore authored with
    // +Y = up and +Z = toward the target (Assets/Art/Animations/CardClash/CardClashPreview.prefab shows it).
    // ClashUnaim undoes the turn so the card keeps facing the way it did in its slot.
    // Every state has its own clip, and the clip's length is how long that step takes.
    public class CardClashRig
    {
        public const string ControllerResource = "CardClash/CardClash";

        // State names = clip names
        public const string Idle = "Idle";
        public const string WindUp = "WindUp";         // critical only: pull back and charge up
        public const string Charge = "Charge";         // while lunging at the target
        public const string ChargeCrit = "ChargeCrit"; // lunge after WindUp (starts from WindUp's last pose)
        public const string Impact = "Impact";         // the attacker on contact (holds the OnImpact event)
        public const string ImpactCrit = "ImpactCrit";
        public const string Return = "Return";         // while flying back to the slot
        public const string Hit = "Hit";               // the card that was hit
        public const string HitCrit = "HitCrit";
        public const string Die = "Die";               // a familiar whose Khwan reached 0; the card is removed after it

        // Used when the controller or a clip is missing
        private static readonly Dictionary<string, float> FallbackLengths = new Dictionary<string, float>
        {
            { Idle, 0f }, { WindUp, 0.55f }, { Charge, 0.3f }, { ChargeCrit, 0.2f }, { Impact, 0.2f },
            { ImpactCrit, 0.25f }, { Return, 0.3f }, { Hit, 0.2f }, { HitCrit, 0.3f }, { Die, 0.3f },
        };

        private static RuntimeAnimatorController controller;
        private static Dictionary<string, AnimationClip> clips;

        public Transform Mover { get; }
        public Vector3 Home { get; }

        private readonly Transform card;
        private readonly Transform originalParent;
        private readonly Vector3 originalPosition;
        private readonly Quaternion originalRotation;
        private readonly Vector3 originalScale;
        private readonly Animator animator;
        private readonly CardClashAnimEvents events;
        private bool expectsImpact;

        private static RuntimeAnimatorController Controller
        {
            get
            {
                if (clips != null) return controller;
                controller = Resources.Load<RuntimeAnimatorController>(ControllerResource);
                clips = new Dictionary<string, AnimationClip>();
                if (controller != null)
                {
                    foreach (var clip in controller.animationClips)
                    {
                        if (clip != null) clips[clip.name] = clip;
                    }
                }
                else
                {
                    Debug.LogWarning($"[CardClashRig] No animator controller at Resources/{ControllerResource}; clash cards only move. " +
                                     "Run Tools > TawanOS > VFX > Build Card Clash Animator.");
                }
                return controller;
            }
        }

        // faceDir: the direction of the other card (only its horizontal part is used)
        public CardClashRig(Transform card, Transform slot, Vector3 faceDir)
        {
            this.card = card;
            originalParent = card.parent;
            originalPosition = card.localPosition;
            originalRotation = card.localRotation;
            originalScale = card.localScale;

            Mover = new GameObject("ClashMover").transform;
            Mover.SetParent(slot, false);
            Mover.position = card.position;
            Mover.rotation = Quaternion.identity;
            // Undo the slot's scale so the clips move the card in world units
            Vector3 s = slot.lossyScale;
            Mover.localScale = new Vector3(Inverse(s.x), Inverse(s.y), Inverse(s.z));
            Home = Mover.localPosition;

            faceDir.y = 0f;
            var aim = new GameObject("ClashAim").transform;
            aim.SetParent(Mover, false);
            aim.rotation = faceDir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(faceDir.normalized, Vector3.up) : Quaternion.identity;

            var pivot = new GameObject("ClashPivot").transform;
            pivot.SetParent(aim, false);

            var unaim = new GameObject("ClashUnaim").transform;
            unaim.SetParent(pivot, false);
            unaim.localRotation = Quaternion.Inverse(aim.localRotation);

            card.SetParent(unaim, worldPositionStays: true);

            if (Controller != null)
            {
                events = pivot.gameObject.AddComponent<CardClashAnimEvents>();
                animator = pivot.gameObject.AddComponent<Animator>();
                animator.applyRootMotion = false;
                animator.runtimeAnimatorController = Controller;
            }
        }

        private static float Inverse(float v)
        {
            return Mathf.Abs(v) > 0.0001f ? 1f / v : 1f;
        }

        // Animator parameters: one trigger per step, and "crit" picks the critical version of a step
        public const string CritParam = "crit";
        public static string TriggerFor(string state)
        {
            switch (state)
            {
                case WindUp: return "windUp";
                case Charge: case ChargeCrit: return "charge";
                case Impact: case ImpactCrit: return "impact";
                case Return: return "return";
                case Hit: case HitCrit: return "hit";
                case Die: return "die";
                default: return null;
            }
        }

        // Moves to a state through the controller's transitions (its trigger, with "crit" set) and returns
        // how long the state lasts. If the transitions were edited so the state is not reached, it is played
        // directly so the clash never stalls.
        public float Play(string state)
        {
            expectsImpact = HasImpactEvent(state);
            if (events != null) events.ResetImpact();

            int hash = Animator.StringToHash(state);
            if (animator != null && animator.HasState(0, hash))
            {
                string trigger = TriggerFor(state);
                if (trigger != null)
                {
                    animator.SetBool(CritParam, state == WindUp || state.EndsWith("Crit"));
                    animator.SetTrigger(trigger);
                    animator.Update(0f); // take the transition and show the first frame now
                }
                bool reached = animator.GetCurrentAnimatorStateInfo(0).shortNameHash == hash
                               || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == hash);
                if (!reached)
                {
                    if (trigger != null) animator.ResetTrigger(trigger);
                    animator.Play(hash, 0, 0f);
                    animator.Update(0f);
                }
            }
            return Length(state);
        }

        public static float Length(string state)
        {
            _ = Controller;
            if (clips.TryGetValue(state, out var clip)) return clip.length;
            return FallbackLengths.TryGetValue(state, out float fallback) ? fallback : 0f;
        }

        private static bool HasImpactEvent(string state)
        {
            _ = Controller;
            if (!clips.TryGetValue(state, out var clip)) return false;
            foreach (var e in clip.events)
            {
                if (e.functionName == CardClashAnimEvents.ImpactEvent) return true;
            }
            return false;
        }

        // The last played state has an OnImpact event that has not fired yet
        public bool WaitingForImpact => expectsImpact && events != null && !events.ImpactFired;

        public bool ExpectsImpact => expectsImpact && events != null;

        // Puts the card back in its slot exactly as it was and removes the rig
        public void Release()
        {
            if (card != null)
            {
                card.SetParent(originalParent, false);
                card.localPosition = originalPosition;
                card.localRotation = originalRotation;
                card.localScale = originalScale;
            }
            if (Mover != null) Object.Destroy(Mover.gameObject);
        }

        // Leaves the card in the rig and removes both after `delay` (a card that died)
        public void DestroyWithCard(float delay)
        {
            if (Mover != null) Object.Destroy(Mover.gameObject, delay);
        }
    }
}
