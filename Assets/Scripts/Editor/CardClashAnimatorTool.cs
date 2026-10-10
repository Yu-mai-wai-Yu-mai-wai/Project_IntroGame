using System.IO;
using TawanOS.VFX;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TawanOS.EditorTools
{
    // Builds the card clash animator: one clip per step in Assets/Art/Animations/CardClash, the controller in
    // Resources/CardClash (CardClashRig loads it), and CardClashPreview.prefab to edit the clips on.
    // Clips are authored with +Y = up and +Z = toward the target; each clip's length is how long that step
    // takes in the game, and the OnImpact event in Impact / ImpactCrit is when the damage lands.
    // Safe to run again: clips, states and the preview that already exist are kept as they are, so edits are
    // never overwritten. Delete a clip to get the starter version back.
    // Batch: -executeMethod TawanOS.EditorTools.CardClashAnimatorTool.BuildFromCli
    public static class CardClashAnimatorTool
    {
        private const string ClipFolder = "Assets/Art/Animations/CardClash";
        private const string ControllerFolder = "Assets/Resources/CardClash";
        private const string ControllerPath = ControllerFolder + "/CardClash.controller";
        private const string PreviewPath = ClipFolder + "/CardClashPreview.prefab";
        private const string CardPrefabPath = "Assets/CardEngineData/Prefabs/CardCube3DPrefab.prefab";

        [MenuItem("Tools/TawanOS/VFX/Build Card Clash Animator")]
        public static void BuildFromMenu()
        {
            Build();
        }

        public static void BuildFromCli()
        {
            if (!Build()) EditorApplication.Exit(1);
        }

        public static bool Build()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[CardClashAnimatorTool] Leave Play Mode first.");
                return false;
            }

            EnsureFolder(ClipFolder);
            EnsureFolder(ControllerFolder);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                             ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;

            int row = 0;
            var idle = AddState(machine, CardClashRig.Idle, BuildIdle, ref row);
            var windUp = AddState(machine, CardClashRig.WindUp, BuildWindUp, ref row);
            var charge = AddState(machine, CardClashRig.Charge, BuildCharge, ref row);
            var chargeCrit = AddState(machine, CardClashRig.ChargeCrit, BuildChargeCrit, ref row);
            var impact = AddState(machine, CardClashRig.Impact, c => BuildImpact(c, 1.2f, 0.2f), ref row);
            var impactCrit = AddState(machine, CardClashRig.ImpactCrit, c => BuildImpact(c, 1.4f, 0.25f), ref row);
            var back = AddState(machine, CardClashRig.Return, BuildReturn, ref row);
            var hit = AddState(machine, CardClashRig.Hit, c => BuildHit(c, 0.15f, 1.15f, 0f, 0.2f), ref row);
            var hitCrit = AddState(machine, CardClashRig.HitCrit, c => BuildHit(c, 0.35f, 1.3f, 12f, 0.3f), ref row);
            var die = AddState(machine, CardClashRig.Die, BuildDie, ref row);
            machine.defaultState = idle;

            // Parameters: one trigger per step; "crit" picks the critical version
            AddParameter(controller, CardClashRig.CritParam, AnimatorControllerParameterType.Bool);
            foreach (string state in new[] { CardClashRig.WindUp, CardClashRig.Charge, CardClashRig.Impact, CardClashRig.Return, CardClashRig.Hit, CardClashRig.Die })
            {
                AddParameter(controller, CardClashRig.TriggerFor(state), AnimatorControllerParameterType.Trigger);
            }

            // Attacker:  Idle -> (WindUp ->) Charge / ChargeCrit -> Impact / ImpactCrit -> Return -> Idle
            // Defender:  Idle -> Hit / HitCrit -> Idle      Dying: Any State -> Die
            // Instant transitions (no exit time, no blend) so the clip lengths stay the step timings
            Link(idle, windUp, "windUp");
            Link(idle, charge, "charge", crit: false);
            Link(idle, chargeCrit, "charge", crit: true);
            Link(windUp, chargeCrit, "charge");
            Link(charge, impact, "impact");
            Link(chargeCrit, impactCrit, "impact");
            Link(impact, back, "return");
            Link(impactCrit, back, "return");
            Link(idle, hit, "hit", crit: false);
            Link(idle, hitCrit, "hit", crit: true);
            LinkAtEnd(back, idle);
            LinkAtEnd(hit, idle);
            LinkAtEnd(hitCrit, idle);
            if (System.Array.Find(machine.anyStateTransitions, t => t.destinationState == die) == null)
            {
                var toDie = machine.AddAnyStateTransition(die);
                Instant(toDie);
                toDie.canTransitionToSelf = false;
                toDie.AddCondition(AnimatorConditionMode.If, 0f, "die");
            }

            EditorUtility.SetDirty(controller);
            BuildPreview(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[CardClashAnimatorTool] Card clash animator ready: {ControllerPath}, clips in {ClipFolder}.</color>");
            return true;
        }

        // ---------------------------------------------------------------- states

        private static AnimatorState AddState(AnimatorStateMachine machine, string name, System.Action<AnimationClip> fill, ref int row)
        {
            var clip = LoadOrCreateClip(name, fill);

            AnimatorState state = null;
            foreach (var child in machine.states)
            {
                if (child.state.name == name) state = child.state;
            }
            if (state == null) state = machine.AddState(name, new Vector3(300f, 60f * row, 0f));
            if (state.motion == null) state.motion = clip;
            row++;
            return state;
        }

        private static void AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (System.Array.Exists(controller.parameters, p => p.name == name)) return;
            controller.AddParameter(name, type);
        }

        // A transition on a trigger (and on "crit" when given); skipped if one to that state already exists,
        // so transitions edited in the Animator window are kept
        private static void Link(AnimatorState from, AnimatorState to, string trigger, bool? crit = null)
        {
            if (System.Array.Exists(from.transitions, t => t.destinationState == to)) return;
            var t = from.AddTransition(to);
            Instant(t);
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            if (crit.HasValue) t.AddCondition(crit.Value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, CardClashRig.CritParam);
        }

        // Back to Idle once the clip has played through
        private static void LinkAtEnd(AnimatorState from, AnimatorState to)
        {
            if (System.Array.Exists(from.transitions, t => t.destinationState == to)) return;
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = 1f;
            t.hasFixedDuration = true;
            t.duration = 0f;
        }

        private static void Instant(AnimatorStateTransition t)
        {
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = 0f;
        }

        private static AnimationClip LoadOrCreateClip(string name, System.Action<AnimationClip> fill)
        {
            string path = $"{ClipFolder}/{name}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null) return existing; // keep the edited clip

            var clip = new AnimationClip { name = name, frameRate = 60f };
            fill(clip);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        // ---------------------------------------------------------------- starter clips
        // Poses are offsets from the card's place on its path: x = sideways, y = up, z = toward the target.

        private static void BuildIdle(AnimationClip c)
        {
            Position(c, (0f, Vector3.zero));
            Rotation(c, (0f, Vector3.zero));
            Scale(c, (0f, Vector3.one));
        }

        // Critical: pull back and rise, then hold there charging up (a small tremble)
        private static void BuildWindUp(AnimationClip c)
        {
            var back = new Vector3(0f, 0.3f, -1.1f);
            Position(c, (0f, Vector3.zero), (0.3f, back), (0.38f, back + new Vector3(0.03f, 0f, 0f)),
                (0.46f, back + new Vector3(-0.03f, 0f, 0f)), (0.55f, back));
            Rotation(c, (0f, Vector3.zero), (0.3f, new Vector3(-10f, 0f, 0f)), (0.55f, new Vector3(-10f, 0f, 0f)));
            Scale(c, (0f, Vector3.one), (0.55f, Vector3.one * 1.08f));
        }

        // Normal lunge: lean into the hit
        private static void BuildCharge(AnimationClip c)
        {
            Position(c, (0f, Vector3.zero), (0.3f, Vector3.zero));
            Rotation(c, (0f, Vector3.zero), (0.3f, new Vector3(12f, 0f, 0f)));
            Scale(c, (0f, Vector3.one), (0.3f, Vector3.one));
        }

        // Critical lunge: snaps out of WindUp's last pose
        private static void BuildChargeCrit(AnimationClip c)
        {
            Position(c, (0f, new Vector3(0f, 0.3f, -1.1f)), (0.2f, Vector3.zero));
            Rotation(c, (0f, new Vector3(-10f, 0f, 0f)), (0.2f, new Vector3(15f, 0f, 0f)));
            Scale(c, (0f, Vector3.one * 1.08f), (0.2f, Vector3.one));
        }

        // Contact: the damage lands on OnImpact (frame 0), then the card punches out and settles
        private static void BuildImpact(AnimationClip c, float punch, float length)
        {
            Position(c, (0f, Vector3.zero), (length, Vector3.zero));
            Rotation(c, (0f, new Vector3(12f, 0f, 0f)), (length, Vector3.zero));
            Scale(c, (0f, Vector3.one), (length * 0.3f, Vector3.one * punch), (length, Vector3.one));
            AnimationUtility.SetAnimationEvents(c, new[]
            {
                new AnimationEvent { time = 0f, functionName = CardClashAnimEvents.ImpactEvent },
            });
        }

        // Flying back to the slot
        private static void BuildReturn(AnimationClip c)
        {
            Position(c, (0f, Vector3.zero), (0.3f, Vector3.zero));
            Rotation(c, (0f, Vector3.zero), (0.3f, Vector3.zero));
            Scale(c, (0f, Vector3.one), (0.3f, Vector3.one));
        }

        // Knocked back (away from the attacker = -z), squashed, optionally shaken
        private static void BuildHit(AnimationClip c, float knock, float squash, float shake, float length)
        {
            Position(c, (0f, Vector3.zero), (length * 0.25f, new Vector3(0f, 0f, -knock)), (length, Vector3.zero));
            Rotation(c, (0f, Vector3.zero), (length * 0.2f, new Vector3(-8f, 0f, shake)), (length * 0.45f, new Vector3(0f, 0f, -shake)),
                (length, Vector3.zero));
            Scale(c, (0f, Vector3.one), (length * 0.25f, Vector3.one * squash), (length * 0.6f, Vector3.one * 0.95f), (length, Vector3.one));
        }

        // Swells a little, then shrinks away; the card is removed when the clip ends
        private static void BuildDie(AnimationClip c)
        {
            Position(c, (0f, Vector3.zero), (0.3f, new Vector3(0f, -0.1f, 0f)));
            Rotation(c, (0f, Vector3.zero), (0.3f, new Vector3(0f, 0f, 25f)));
            Scale(c, (0f, Vector3.one), (0.08f, Vector3.one * 1.1f), (0.3f, Vector3.zero));
        }

        // ---------------------------------------------------------------- curve helpers

        private static void Position(AnimationClip c, params (float t, Vector3 v)[] keys) => Vector(c, "m_LocalPosition", keys);
        private static void Rotation(AnimationClip c, params (float t, Vector3 v)[] keys) => Vector(c, "localEulerAnglesRaw", keys);
        private static void Scale(AnimationClip c, params (float t, Vector3 v)[] keys) => Vector(c, "m_LocalScale", keys);

        private static void Vector(AnimationClip c, string property, (float t, Vector3 v)[] keys)
        {
            string[] axes = { "x", "y", "z" };
            for (int a = 0; a < 3; a++)
            {
                var frames = new Keyframe[keys.Length];
                for (int i = 0; i < keys.Length; i++) frames[i] = new Keyframe(keys[i].t, keys[i].v[a]);
                var curve = new AnimationCurve(frames);
                for (int i = 0; i < frames.Length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                }
                var binding = EditorCurveBinding.FloatCurve("", typeof(Transform), $"{property}.{axes[a]}");
                AnimationUtility.SetEditorCurve(c, binding, curve);
            }
        }

        // ---------------------------------------------------------------- preview

        // CardClashPreview: select Pivot and open Window > Animation to edit the clips on a real card.
        // Target (+Z) marks where the hit goes.
        private static void BuildPreview(RuntimeAnimatorController controller)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPath) != null) return;

            var root = new GameObject("CardClashPreview");
            var pivot = new GameObject("Pivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.AddComponent<Animator>().runtimeAnimatorController = controller;
            pivot.AddComponent<CardClashAnimEvents>();

            var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            if (cardPrefab != null)
            {
                var card = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab);
                card.transform.SetParent(pivot.transform, false);
                card.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // lying on the table as on the board
            }

            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "Target (+Z)";
            target.transform.SetParent(root.transform, false);
            target.transform.localPosition = new Vector3(0f, 0f, 2.5f);
            target.transform.localScale = new Vector3(0.7f, 0.05f, 1f);
            Object.DestroyImmediate(target.GetComponent<Collider>());

            PrefabUtility.SaveAsPrefabAsset(root, PreviewPath);
            Object.DestroyImmediate(root);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
