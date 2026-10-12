using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.VFX
{
    // Wraps one board card for the duration of a clash, then puts it back:
    //   (scene root) ClashMover > ClashAim > ClashPivot (Animator) > ClashUnaim > card
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
        public const string Ready = "Ready";           // normal only: get ready in the slot before the lunge
        public const string WindUp = "WindUp";         // critical only: pull back and charge up
        public const string Charge = "Charge";         // while lunging at the target
        public const string ChargeCrit = "ChargeCrit"; // lunge after WindUp (starts from WindUp's last pose)
        public const string Impact = "Impact";         // the attacker on contact (holds the OnImpact event)
        public const string ImpactCrit = "ImpactCrit";
        public const string Return = "Return";         // while flying back to the slot
        public const string Hit = "Hit";               // the card that was hit
        public const string HitCrit = "HitCrit";
        public const string Die = "Die";               // a familiar whose Khwan reached 0; the card is removed after it

        // ตีข้ามหัว: a familiar with the Overhead keyword flies over the cards. Same steps
        // as above with their own clips; if the controller has none of these, the clips above are used.
        public const string ReadyFace = "ReadyFace";
        public const string WindUpFace = "WindUpFace";
        public const string ChargeFace = "ChargeFace";
        public const string ChargeFaceCrit = "ChargeFaceCrit";
        public const string ImpactFace = "ImpactFace";
        public const string ImpactFaceCrit = "ImpactFaceCrit";
        public const string ReturnFace = "ReturnFace";

        // Used when the controller or a clip is missing
        private static readonly Dictionary<string, float> FallbackLengths = new Dictionary<string, float>
        {
            { Idle, 0f }, { WindUp, 0.55f }, { Charge, 0.3f }, { ChargeCrit, 0.2f }, { Impact, 0.2f },
            { ImpactCrit, 0.25f }, { Return, 0.3f }, { Hit, 0.2f }, { HitCrit, 0.3f }, { Die, 0.3f },
            { WindUpFace, 0.6f }, { ChargeFace, 0.9f }, { ChargeFaceCrit, 0.3f }, { ImpactFace, 0.25f },
            { ImpactFaceCrit, 0.3f }, { ReturnFace, 0.35f },
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
        private readonly Transform aim;
        private readonly Transform unaim;
        private Quaternion heading = Quaternion.identity;
        private readonly Animator animator;
        private readonly CardClashAnimEvents events;
        private readonly bool hasFaceParam;
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

            // The mover sits at the scene root so the clips move the card in world units. Slots are rotated
            // and scaled non-uniformly (lying flat, 1.8 x 3), and no scale under such a parent cancels that:
            // the clips' sideways moves picked up a forward part and their up/forward moves were squashed
            // and stretched.
            Mover = new GameObject("ClashMover").transform;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(Mover.gameObject, slot.gameObject.scene);
            Mover.position = card.position;
            Mover.rotation = Quaternion.identity;
            Home = Mover.localPosition;

            aim = new GameObject("ClashAim").transform;
            aim.SetParent(Mover, false);

            var pivot = new GameObject("ClashPivot").transform;
            pivot.SetParent(aim, false);

            unaim = new GameObject("ClashUnaim").transform;
            unaim.SetParent(pivot, false);
            Aim(faceDir);

            card.SetParent(unaim, worldPositionStays: true);

            if (Controller != null)
            {
                events = pivot.gameObject.AddComponent<CardClashAnimEvents>();
                animator = pivot.gameObject.AddComponent<Animator>();
                animator.applyRootMotion = false;
                animator.runtimeAnimatorController = Controller;
                hasFaceParam = System.Array.Exists(animator.parameters, p => p.name == FaceParam);
            }
        }

        public static Quaternion AimRotation(Vector3 faceDir)
        {
            faceDir.y = 0f;
            return faceDir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(faceDir.normalized, Vector3.up) : Quaternion.identity;
        }

        // Turns the clips' +Z toward faceDir; the card itself keeps facing the way it did in its slot
        public void Aim(Vector3 faceDir)
        {
            SetAim(AimRotation(faceDir));
        }

        public void SetAim(Quaternion rotation)
        {
            aim.localRotation = rotation;
            unaim.localRotation = Quaternion.Inverse(rotation) * heading;
        }

        public Quaternion CurrentAim => aim.localRotation;

        // Turns the card itself about the vertical (on top of the way it faced in its slot), e.g. to point its
        // head the way it lunges; the clips' moves keep following the aim
        public void SetHeading(Quaternion rotation)
        {
            heading = rotation;
            unaim.localRotation = Quaternion.Inverse(aim.localRotation) * heading;
        }

        public Quaternion CurrentHeading => heading;

        // Animator parameters: one trigger per step, "crit" picks the critical version of a step and "face"
        // the over-the-head version
        public const string CritParam = "crit";
        public const string FaceParam = "face";
        public static string TriggerFor(string state)
        {
            switch (state)
            {
                case Ready: case ReadyFace: return "ready";
                case WindUp: case WindUpFace: return "windUp";
                case Charge: case ChargeCrit: case ChargeFace: case ChargeFaceCrit: return "charge";
                case Impact: case ImpactCrit: case ImpactFace: case ImpactFaceCrit: return "impact";
                case Return: case ReturnFace: return "return";
                case Hit: case HitCrit: return "hit";
                case Die: return "die";
                default: return null;
            }
        }

        // Moves to a state through the controller's transitions (its trigger, with "crit" set) and returns
        // how long the state lasts. If the transitions were edited so the state is not reached, it is played
        // directly so the clash never stalls. speed plays the clip faster (> 1) or slower; the next Play resets it.
        public float Play(string state, float speed = 1f)
        {
            speed = Mathf.Max(0.05f, speed);
            expectsImpact = HasImpactEvent(state);
            if (events != null) events.ResetImpact();

            int hash = Animator.StringToHash(state);
            if (animator != null && animator.HasState(0, hash))
            {
                animator.speed = speed;
                string trigger = TriggerFor(state);
                if (trigger != null)
                {
                    animator.SetBool(CritParam, state.StartsWith(WindUp) || state.EndsWith("Crit"));
                    if (hasFaceParam) animator.SetBool(FaceParam, IsFace(state));
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
            return Length(state) / speed;
        }

        public static bool IsFace(string state)
        {
            return state.Contains("Face");
        }

        // The controller has a clip for this state
        public static bool Has(string state)
        {
            _ = Controller;
            return clips.ContainsKey(state);
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
