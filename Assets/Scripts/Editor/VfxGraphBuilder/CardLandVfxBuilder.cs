using System.Linq;
using UnityEditor;
using UnityEditor.VFX;
using UnityEditor.VFX.Block;
using UnityEngine;
using UnityEngine.VFX;
using VfxTransform = UnityEditor.VFX.Transform;

namespace TawanOS.EditorTools.Vfx
{
    // The burst played under a card as it lands on a board slot (CardLandVfx at runtime), one per school:
    //  - Art/VFX/CardLand/CardLandBlack.vfx (มนต์ดำ): black smoke billowing out from under the card's edges, with
    //    glowing red wisps in the black-magic cards' red.
    //  - Art/VFX/CardLand/CardLandWhite.vfx (มนต์ขาว): streaks of warm light shooting out from under the card's
    //    edges, a soft flash beneath it and a few motes drifting up.
    // Local space: x = across the card, z = along it, y = up from the table. Everything is one burst on play.
    public static partial class DrawPitSmokeVfxBuilder
    {
        public const string CardLandFolder = "Assets/Art/VFX/CardLand/";
        public const string CardLandBlackPath = CardLandFolder + "CardLandBlack.vfx";
        public const string CardLandWhitePath = CardLandFolder + "CardLandWhite.vfx";

        // Half the size of a card lying in a board slot (1.72 x 2.45 world units)
        private const float CardHalfWidth = 0.86f;
        private const float CardHalfLength = 1.22f;

        // Builds only the files that do not exist yet: existing ones are tuned by hand in the VFX Graph window
        // and must not be overwritten (delete a file to rebuild it from these defaults)
        public static void BuildCardLand()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art/VFX/CardLand")) AssetDatabase.CreateFolder("Assets/Art/VFX", "CardLand");
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(CardLandBlackPath) == null) BuildCardLandBlack();
            else Debug.Log($"[DrawPitSmokeVfxBuilder] {CardLandBlackPath} already exists - kept.");
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(CardLandWhitePath) == null) BuildCardLandWhite();
            else Debug.Log($"[DrawPitSmokeVfxBuilder] {CardLandWhitePath} already exists - kept.");
        }

        private static void BuildCardLandBlack()
        {
            var (graph, resource) = OpenEmptyGraph(CardLandBlackPath);
            var wisp = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "SmokeWisp.png");
            var puff = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "SmokePuff.png");

            // Black smoke billowing out from under the card's edges and rising a little
            {
                var init = BurstSystem(graph, "Black Smoke", 60f, 80u);
                SpawnOnCardEdge(init, 0.35f);
                Outward(init, 0.7f, 1.7f, new Vector3(0f, 0.1f, 0f), new Vector3(0f, 0.55f, 0f));
                SetRandom(init, "lifetime", 0.9f, 1.6f);
                SetRandom(init, "size", 0.8f, 1.4f);
                SetRandom(init, "angleZ", 0f, 360f);
                SetRandom(init, "angularVelocityZ", -40f, 40f);
                SetColor(init, new Color(0.03f, 0.02f, 0.025f), 1f);
                var update = Update(graph, init, turbulence: 0.6f, drag: 2.2f);
                var output = Output(graph, update, VFXAbstractRenderedOutput.BlendMode.Alpha, wisp, 0);
                AddBlock<Orient>(output, ("mode", Orient.Mode.FaceCameraPlane));
                Curves(output, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(0.6f, 0.85f), new Keyframe(1f, 0f)),
                    new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.7f)));
            }

            // Glowing red wisps cutting through it
            {
                var init = BurstSystem(graph, "Red Wisps", 24f, 32u);
                SpawnOnCardEdge(init, 0.15f);
                Outward(init, 0.9f, 2f, new Vector3(0f, 0.3f, 0f), new Vector3(0f, 0.9f, 0f));
                SetRandom(init, "lifetime", 0.6f, 1.1f);
                SetRandom(init, "size", 0.45f, 0.8f);
                SetRandom(init, "angleZ", 0f, 360f);
                SetRandom(init, "angularVelocityZ", -90f, 90f);
                SetColor(init, new Color(1f, 0.06f, 0.05f), 2f); // above 1: glows with Bloom
                var update = Update(graph, init, turbulence: 0.9f, drag: 2.5f);
                var output = Output(graph, update, VFXAbstractRenderedOutput.BlendMode.Additive, puff, 1);
                AddBlock<Orient>(output, ("mode", Orient.Mode.FaceCameraPlane));
                Curves(output, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.1f, 1f), new Keyframe(1f, 0f)),
                    new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.3f)));
            }

            Save(graph, resource, CardLandBlackPath);
        }

        private static void BuildCardLandWhite()
        {
            var (graph, resource) = OpenEmptyGraph(CardLandWhitePath);
            var glow = SoftGlowTexture();
            var warm = new Color(1f, 0.88f, 0.6f);

            // Soft flash lying under the card
            {
                var init = BurstSystem(graph, "Flash", 1f, 1u);
                SetSlot(AddBlock<SetAttribute>(init, ("attribute", "position")), 0, new Position { position = new Vector3(0f, 0.03f, 0f) });
                SetSlot(AddBlock<SetAttribute>(init, ("attribute", "lifetime")), 0, 0.55f);
                SetSlot(AddBlock<SetAttribute>(init, ("attribute", "size")), 0, 4f);
                SetColor(init, warm, 2.5f);
                var update = Update(graph, init, turbulence: 0f, drag: 0f);
                var output = Output(graph, update, VFXAbstractRenderedOutput.BlendMode.Additive, glow, 0);
                LieFlat(output);
                Curves(output, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 0.8f), new Keyframe(1f, 0f)),
                    new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(1f, 1.2f)));
            }

            // Streaks of light shooting out from under the card's edges, stretched along their flight
            {
                var init = BurstSystem(graph, "Light Rays", 30f, 48u);
                SpawnOnCardEdge(init, 0f);
                Outward(init, 2.2f, 4.2f, new Vector3(0f, 0.4f, 0f), new Vector3(0f, 1.4f, 0f));
                SetRandom(init, "lifetime", 0.4f, 0.7f);
                SetRandom(init, "size", 0.14f, 0.22f);
                var stretch = AddBlock<SetAttribute>(init, ("attribute", "scale"), ("channels", VariadicChannelOptions.Y), ("Random", RandomMode.Uniform));
                SetSlot(stretch, 0, 5f);
                SetSlot(stretch, 1, 9f);
                SetColor(init, warm, 4f); // above 1: glows with Bloom
                var update = Update(graph, init, turbulence: 0f, drag: 3f);
                var output = Output(graph, update, VFXAbstractRenderedOutput.BlendMode.Additive, glow, 1);
                AddBlock<Orient>(output, ("mode", Orient.Mode.AlongVelocity));
                Curves(output, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 0.8f), new Keyframe(1f, 0f)),
                    new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.6f)));
            }

            // A few motes of light drifting up afterwards
            {
                var init = BurstSystem(graph, "Motes", 22f, 32u);
                SpawnOnCardEdge(init, 0.6f);
                Outward(init, 0.1f, 0.5f, new Vector3(-0.1f, 0.3f, -0.1f), new Vector3(0.1f, 0.9f, 0.1f));
                SetRandom(init, "lifetime", 0.8f, 1.5f);
                SetRandom(init, "size", 0.1f, 0.2f);
                SetColor(init, warm, 2.5f);
                var update = Update(graph, init, turbulence: 0.4f, drag: 1f);
                var output = Output(graph, update, VFXAbstractRenderedOutput.BlendMode.Additive, glow, 2);
                AddBlock<Orient>(output, ("mode", Orient.Mode.FaceCameraPlane));
                Curves(output, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f)),
                    new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.5f)));
            }

            Save(graph, resource, CardLandWhitePath);
        }

        // ---------------------------------------------------------------- card land helpers

        private static (VFXGraph, VisualEffectResource) OpenEmptyGraph(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
            if (asset == null) asset = VisualEffectAssetEditorUtility.CreateNewAsset(path);
            var resource = asset.GetResource();
            var graph = resource.GetOrCreateGraph();
            foreach (var child in graph.children.ToArray()) graph.RemoveChild(child);
            return (graph, resource);
        }

        private static void Save(VFXGraph graph, VisualEffectResource resource, string path)
        {
            graph.SetExpressionGraphDirty();
            graph.RecompileIfNeeded();
            resource.WriteAssetWithSubAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Debug.Log($"<color=green>[DrawPitSmokeVfxBuilder] {path} built.</color>");
        }

        // One burst when the effect plays, into an Initialize context in local space
        private static VFXBasicInitialize BurstSystem(VFXGraph graph, string label, float count, uint capacity)
        {
            var spawner = Add<VFXBasicSpawner>(graph);
            var burst = AddBlock<VFXSpawnerBurst>(spawner);
            SetSlot(burst, "Count", count);
            SetSlot(burst, "Delay", 0f);
            var init = Add<VFXBasicInitialize>(graph);
            spawner.LinkTo(init);
            init.SetSettingValue("capacity", capacity);
            init.space = VFXSpace.Local;
            SetSlot(init, "bounds", new AABox { center = new Vector3(0f, 1f, 0f), size = new Vector3(8f, 4f, 8f) });
            spawner.label = label;
            init.label = label;
            return init;
        }

        // Around the card's outline (an ellipse through its edges) on the table; thickness 0 = only on the
        // outline, 1 = anywhere under the card
        private static void SpawnOnCardEdge(VFXContext init, float thickness)
        {
            var position = AddBlock<PositionShape>(init, ("shape", PositionShapeBase.Type.Circle), ("positionMode", PositionBase.PositionMode.ThicknessRelative));
            SetSlot(position, "arcCircle", new TArcCircle
            {
                circle = new TCircle
                {
                    transform = new VfxTransform
                    {
                        position = new Vector3(0f, 0.05f, 0f),
                        angles = new Vector3(90f, 0f, 0f),
                        scale = new Vector3(CardHalfWidth * 1.1f, CardHalfLength * 1.1f, 1f),
                    },
                    radius = 1f,
                },
                arc = Mathf.PI * 2f,
            });
            SetSlotIfAny(position, "Thickness", thickness);
        }

        // Away from the card's centre at minSpeed..maxSpeed, plus a random extra (e.g. upward) push
        private static void Outward(VFXContext init, float minSpeed, float maxSpeed, Vector3 extraMin, Vector3 extraMax)
        {
            var spherical = AddBlock<VelocitySpherical>(init, ("composition", AttributeCompositionMode.Overwrite), ("speedMode", VelocityBase.SpeedMode.Random));
            SetSlotIfAny(spherical, "center", Vector3.zero);
            SetSlot(spherical, "MinSpeed", minSpeed);
            SetSlot(spherical, "MaxSpeed", maxSpeed);
            var extra = AddBlock<SetAttribute>(init, ("attribute", "velocity"), ("Composition", AttributeCompositionMode.Add), ("Random", RandomMode.PerComponent));
            SetSlot(extra, 0, new Vector { vector = extraMin });
            SetSlot(extra, 1, new Vector { vector = extraMax });
        }

        // colour as picked in a colour field (gamma); glow above 1 lights up Bloom
        private static void SetColor(VFXContext init, Color color, float glow)
        {
            var linear = color.linear * glow;
            SetSlot(AddBlock<SetAttribute>(init, ("attribute", "color")), 0, new Vector3(linear.r, linear.g, linear.b));
        }

        private static VFXBasicUpdate Update(VFXGraph graph, VFXBasicInitialize init, float turbulence, float drag)
        {
            var update = Add<VFXBasicUpdate>(graph);
            init.LinkTo(update);
            if (turbulence > 0f || drag > 0f)
            {
                // Turbulence's drag also slows the burst down, so it puffs out and hangs
                var t = AddBlock<Turbulence>(update);
                SetSlot(t, "Intensity", turbulence);
                SetSlot(t, "Drag", drag);
                SetSlot(t, "frequency", 1.5f);
            }
            update.label = init.label;
            return update;
        }

        private static VFXPlanarPrimitiveOutput Output(VFXGraph graph, VFXBasicUpdate update, VFXAbstractRenderedOutput.BlendMode blend, Texture2D texture, int sortPriority)
        {
            var output = Add<VFXPlanarPrimitiveOutput>(graph, ("blendMode", blend), ("vfxSystemSortPriority", sortPriority));
            update.LinkTo(output);
            SetSlot(output, "mainTexture", texture);
            output.label = update.label;
            return output;
        }

        private static void LieFlat(VFXContext output)
        {
            var orient = AddBlock<Orient>(output, ("mode", Orient.Mode.Advanced), ("axes", Orient.AxesPair.ZY));
            SetSlot(orient, 0, new DirectionType { direction = Vector3.down });
            SetSlot(orient, 1, new DirectionType { direction = Vector3.forward });
        }

        private static void Curves(VFXContext output, AnimationCurve alphaOverLife, AnimationCurve sizeOverLife)
        {
            var alpha = AddBlock<AttributeFromCurve>(output, ("attribute", "alpha"), ("SampleMode", AttributeFromCurve.CurveSampleMode.OverLife));
            SetSlot(alpha, 0, alphaOverLife);
            var size = AddBlock<AttributeFromCurve>(output, ("attribute", "size"), ("SampleMode", AttributeFromCurve.CurveSampleMode.OverLife), ("Composition", AttributeCompositionMode.Multiply));
            SetSlot(size, 0, sizeOverLife);
        }
    }
}
