using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.VFX;
using UnityEditor.VFX.Block;
using UnityEngine;
using UnityEngine.VFX;
using VfxTransform = UnityEditor.VFX.Transform;

namespace TawanOS.EditorTools.Vfx
{
    // Builds Assets/Art/VFX/DrawPitSmoke/DrawPitSmoke.vfx: the draw pit (หลุมจั่ว) smoke as a VFX Graph, the same
    // look as the Particle System prefab: a black Cover lying on the pit, Wisps rising around it and RedStrands on
    // the rim. Each layer has a looping spawner and a "Burst" event spawner (VisualEffect.SendEvent("Burst")).
    // The graph is in the effect's local space; the runtime turns the effect's transform to make it orbit.
    //
    // This assembly is named Unity.Testing.VisualEffectGraph.Editor because the VFX Graph package lets an assembly
    // of that name use its internal graph API (VFXGraph, VFXContext, blocks), which is the only way to build a
    // graph from code. It is editor-only; after building, open and tune the .vfx in the VFX Graph window.
    public static partial class DrawPitSmokeVfxBuilder
    {
        public const string AssetPath = "Assets/Art/VFX/DrawPitSmoke/DrawPitSmoke.vfx";
        private const string TextureFolder = "Assets/Art/VFX/DrawPitSmoke/";

        private struct Layer
        {
            public string name;
            public string texture;
            public uint capacity;
            public float rate;
            public float burstCount;
            public Vector2 life;
            public Vector2 size;
            public Color color;  // as picked in a colour field (gamma)
            public float glow;   // multiplies the colour; above 1 glows with Bloom
            public float alpha;
            public float spawnRadius;
            public float spawnThickness; // 1 = whole disc, 0 = only the rim
            public float height;
            public Vector3 velocityMin, velocityMax;
            public Vector3 burstVelocityMin, burstVelocityMax;
            public float turbulence;
            public bool flat;               // lying on the table instead of facing the camera
            public int sortPriority;
            public float growTo;
        }

        public static void Build()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(AssetPath);
            if (asset == null) asset = VisualEffectAssetEditorUtility.CreateNewAsset(AssetPath);
            var resource = asset.GetResource();
            var graph = resource.GetOrCreateGraph();
            foreach (var child in graph.children.ToArray()) graph.RemoveChild(child);

            var smoke = new Color(0.02f, 0.018f, 0.022f, 1f);
            AddLayer(graph, new Layer
            {
                name = "Cover", texture = "SmokePuff", capacity = 64, rate = 9f, burstCount = 0f,
                life = new Vector2(3f, 5f), size = new Vector2(1.3f, 1.9f), color = smoke, alpha = 0.95f,
                spawnRadius = 0.25f, spawnThickness = 1f, height = 0.04f,
                velocityMin = Vector3.zero, velocityMax = Vector3.zero,
                turbulence = 0f, flat = true, sortPriority = 0, growTo = 1.15f,
            });
            AddLayer(graph, new Layer
            {
                name = "Wisps", texture = "SmokeWisp", capacity = 192, rate = 16f, burstCount = 22f,
                life = new Vector2(2.5f, 4.2f), size = new Vector2(0.8f, 1.3f), color = smoke, alpha = 0.8f,
                spawnRadius = 0.45f, spawnThickness = 0.6f, height = 0.35f,
                velocityMin = new Vector3(0f, 0.08f, 0f), velocityMax = new Vector3(0f, 0.22f, 0f),
                burstVelocityMin = new Vector3(-0.35f, 1f, -0.35f), burstVelocityMax = new Vector3(0.35f, 1.7f, 0.35f),
                turbulence = 0.6f, flat = false, sortPriority = 1, growTo = 1.15f,
            });
            AddLayer(graph, new Layer
            {
                name = "RedStrands", texture = "SmokePuff", capacity = 64, rate = 4.5f, burstCount = 4f,
                life = new Vector2(1.6f, 2.6f), size = new Vector2(0.45f, 0.75f),
                color = new Color(1f, 0.05f, 0.06f, 1f), glow = 1.9f, alpha = 0.7f, // glow above 1: Bloom
                spawnRadius = 0.7f, spawnThickness = 0f, height = 0.35f,
                velocityMin = new Vector3(0f, 0.04f, 0f), velocityMax = new Vector3(0f, 0.11f, 0f),
                burstVelocityMin = new Vector3(-0.3f, 0.7f, -0.3f), burstVelocityMax = new Vector3(0.3f, 1.4f, 0.3f),
                turbulence = 0.3f, flat = false, sortPriority = 2, growTo = 1.1f,
            });

            // Red smoke trails on two rings: a wide low one going one way and a smaller, higher one going the
            // other. Each ring has two trails chasing each other half a turn apart (a head with a smoky,
            // spreading tail, like the Head & Trail template).
            var trailRed = new Color(1f, 0.08f, 0.06f, 1f);
            foreach (var (name, start) in new[] { ("RedTrail Low A", 0f), ("RedTrail Low B", 0.5f) })
            {
                AddOrbitTrail(graph, new OrbitTrail
                {
                    name = name, radius = 0.62f, height = 0.18f, turnsPerSecond = 0.7f, startTurn = start,
                    tailTurns = 0.35f, width = 0.16f, spreadTo = 2.6f, color = trailRed, glow = 2.2f,
                    turbulence = 0.9f, rise = 0.12f, clockwise = false,
                });
            }
            foreach (var (name, start) in new[] { ("RedTrail High A", 0.25f), ("RedTrail High B", 0.75f) })
            {
                AddOrbitTrail(graph, new OrbitTrail
                {
                    name = name, radius = 0.5f, height = 0.42f, turnsPerSecond = 0.6f, startTurn = start,
                    tailTurns = 0.3f, width = 0.12f, spreadTo = 2.4f, color = trailRed, glow = 1.9f,
                    turbulence = 0.9f, rise = 0.12f, clockwise = true,
                });
            }

            AddHoverGlow(graph);

            graph.SetExpressionGraphDirty();
            graph.RecompileIfNeeded();
            resource.WriteAssetWithSubAssets();
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"<color=green>[DrawPitSmokeVfxBuilder] {AssetPath} built.</color>");
        }

        private static void AddLayer(VFXGraph graph, Layer l)
        {
            // Looping spawner, and a one-shot spawner fired by the "Burst" event
            var spawner = Add<VFXBasicSpawner>(graph);
            var rate = AddBlock<VFXSpawnerConstantRate>(spawner);
            SetSlot(rate, "Rate", l.rate);
            var spawnVelocity = AddBlock<VFXSpawnerSetAttribute>(spawner, ("attribute", "velocity"), ("randomMode", RandomMode.PerComponent));
            SetSlot(spawnVelocity, 0, l.velocityMin);
            SetSlot(spawnVelocity, 1, l.velocityMax);

            var init = Add<VFXBasicInitialize>(graph);
            spawner.LinkTo(init);

            if (l.burstCount > 0f)
            {
                var burstEvent = Add<VFXBasicEvent>(graph, ("eventName", "Burst"));
                var burstSpawner = Add<VFXBasicSpawner>(graph);
                burstEvent.LinkTo(burstSpawner);
                var burst = AddBlock<VFXSpawnerBurst>(burstSpawner);
                SetSlot(burst, "Count", l.burstCount);
                SetSlot(burst, "Delay", 0f);
                var burstVelocity = AddBlock<VFXSpawnerSetAttribute>(burstSpawner, ("attribute", "velocity"), ("randomMode", RandomMode.PerComponent));
                SetSlot(burstVelocity, 0, l.burstVelocityMin);
                SetSlot(burstVelocity, 1, l.burstVelocityMax);
                burstSpawner.LinkTo(init);
            }

            init.SetSettingValue("capacity", l.capacity);
            init.space = VFXSpace.Local;
            SetSlot(init, "bounds", new AABox { center = new Vector3(0f, 0.8f, 0f), size = new Vector3(4f, 4f, 4f) });

            var position = AddBlock<PositionShape>(init, ("shape", PositionShapeBase.Type.Circle), ("positionMode", PositionBase.PositionMode.ThicknessRelative));
            SetSlot(position, "arcCircle", new TArcCircle
            {
                circle = new TCircle
                {
                    transform = new VfxTransform { position = new Vector3(0f, l.height, 0f), angles = new Vector3(90f, 0f, 0f), scale = Vector3.one },
                    radius = l.spawnRadius,
                },
                arc = Mathf.PI * 2f,
            });
            SetSlotIfAny(position, "Thickness", l.spawnThickness);

            SetRandom(init, "lifetime", l.life.x, l.life.y);
            SetRandom(init, "size", l.size.x, l.size.y);
            SetRandom(init, "angleZ", 0f, 360f);
            SetRandom(init, "angularVelocityZ", -20f, 20f);
            // The color attribute is linear, unlike Particle System colours, so convert or the smoke turns grey
            var linear = l.color.linear * (l.glow > 0f ? l.glow : 1f);
            var color = AddBlock<SetAttribute>(init, ("attribute", "color"));
            SetSlot(color, 0, new Vector3(linear.r, linear.g, linear.b));
            AddBlock<SetAttribute>(init, ("attribute", "velocity"), ("Source", SetAttribute.ValueSource.Source));

            var update = Add<VFXBasicUpdate>(graph);
            init.LinkTo(update);
            if (l.turbulence > 0f)
            {
                var turbulence = AddBlock<Turbulence>(update);
                SetSlot(turbulence, "Intensity", l.turbulence);
                SetSlot(turbulence, "Drag", 1f);
                SetSlot(turbulence, "frequency", 1.2f);
            }

            var output = Add<VFXPlanarPrimitiveOutput>(graph,
                ("blendMode", VFXAbstractRenderedOutput.BlendMode.Alpha),
                ("useSoftParticle", true),
                ("vfxSystemSortPriority", l.sortPriority));
            update.LinkTo(output);
            SetSlot(output, "mainTexture", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + l.texture + ".png"));
            SetSlotIfAny(output, "softParticleFadeDistance", 0.3f);

            if (l.flat)
            {
                // Facing up, lying on the table, so the table does not cut the puffs in half
                var orient = AddBlock<Orient>(output, ("mode", Orient.Mode.Advanced), ("axes", Orient.AxesPair.ZY));
                SetSlot(orient, 0, new DirectionType { direction = Vector3.down });
                SetSlot(orient, 1, new DirectionType { direction = Vector3.forward });
            }
            else
            {
                AddBlock<Orient>(output, ("mode", Orient.Mode.FaceCameraPlane));
            }

            // Fades in, holds, fades out; grows a little
            var alpha = AddBlock<AttributeFromCurve>(output, ("attribute", "alpha"), ("SampleMode", AttributeFromCurve.CurveSampleMode.OverLife));
            SetSlot(alpha, 0, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.25f, l.alpha), new Keyframe(0.6f, l.alpha), new Keyframe(1f, 0f)));
            var grow = AddBlock<AttributeFromCurve>(output, ("attribute", "size"), ("SampleMode", AttributeFromCurve.CurveSampleMode.OverLife), ("Composition", AttributeCompositionMode.Multiply));
            SetSlot(grow, 0, new AnimationCurve(new Keyframe(0f, 0.8f), new Keyframe(1f, l.growTo)));

            // Labels the layer in the VFX Graph window
            spawner.label = l.name;
            init.label = l.name;
            update.label = l.name;
            output.label = l.name;
        }

        public const string CardTrailPath = "Assets/Art/VFX/DrawPitSmoke/PitCardTrail.vfx";

        // Builds PitCardTrail.vfx: the red smoke a card pulled out of the pit trails behind it as it flies to the
        // hand. World space, emitting at the effect's position, so moving the effect (PitCardTrail follows the
        // card) leaves the trail behind. Stop() (the OnStop event) ends the emission and the trail fades out.
        public static void BuildCardTrail()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(CardTrailPath);
            if (asset == null) asset = VisualEffectAssetEditorUtility.CreateNewAsset(CardTrailPath);
            var resource = asset.GetResource();
            var graph = resource.GetOrCreateGraph();
            foreach (var child in graph.children.ToArray()) graph.RemoveChild(child);

            var wisp = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "SmokeWisp.png");

            // A short, soft glowing red ribbon close behind the card, puffing out and fading fast
            {
                var spawner = Add<VFXBasicSpawner>(graph);
                SetSlot(AddBlock<VFXSpawnerConstantRate>(spawner), "Rate", 60f);
                var init = Add<VFXBasicInitialize>(graph);
                var data = init.GetData();
                data.SetSettingValue("dataType", VFXDataParticle.DataType.ParticleStrip);
                data.SetSettingValue("stripCapacity", 1u);
                data.SetSettingValue("particlePerStripCount", 64u);
                spawner.LinkTo(init);
                init.space = VFXSpace.World;
                SetCardTrailBounds(init);
                SetAtEffect(init);
                SetSlot(AddBlock<SetAttribute>(init, ("attribute", "lifetime")), 0, 0.2f);
                SetSlot(AddBlock<SetAttribute>(init, ("attribute", "size")), 0, 0.5f);
                var red = new Color(1f, 0.07f, 0.05f, 1f).linear * 1.5f;
                SetSlot(AddBlock<SetAttribute>(init, ("attribute", "color")), 0, new Vector3(red.r, red.g, red.b));
                SetSlot(AddBlock<SetAttribute>(init, ("attribute", "velocity")), 0, new Vector { vector = new Vector3(0f, 0.15f, 0f) });

                var update = Add<VFXBasicUpdate>(graph);
                init.LinkTo(update);
                var turbulence = AddBlock<Turbulence>(update);
                SetSlot(turbulence, "Intensity", 1.2f);
                SetSlot(turbulence, "Drag", 1.5f);
                SetSlot(turbulence, "frequency", 2.5f);

                var output = Add<VFXQuadStripOutput>(graph,
                    ("blendMode", VFXAbstractRenderedOutput.BlendMode.Additive),
                    ("vfxSystemSortPriority", 1));
                update.LinkTo(output);
                SetSlotIfAny(output, "mainTexture", wisp);
                var alpha = AddBlock<AttributeFromCurve>(output, ("attribute", "alpha"), ("SampleMode", AttributeFromCurve.CurveSampleMode.OverLife));
                SetSlot(alpha, 0, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(1f, 0f)));
                var spread = AddBlock<AttributeFromCurve>(output, ("attribute", "size"), ("SampleMode", AttributeFromCurve.CurveSampleMode.OverLife), ("Composition", AttributeCompositionMode.Multiply));
                SetSlot(spread, 0, new AnimationCurve(new Keyframe(0f, 0.8f), new Keyframe(1f, 2.2f)));
                spawner.label = init.label = update.label = output.label = "CardTrail Ribbon";
            }

            // Red smoke puffs shed along the way, drifting up and curling: most of the trail's look
            {
                var spawner = Add<VFXBasicSpawner>(graph);
                SetSlot(AddBlock<VFXSpawnerConstantRate>(spawner), "Rate", 90f);
                var init = Add<VFXBasicInitialize>(graph);
                spawner.LinkTo(init);
                init.SetSettingValue("capacity", 160u);
                init.space = VFXSpace.World;
                SetCardTrailBounds(init);
                SetAtEffect(init);
                var drift = AddBlock<SetAttribute>(init, ("attribute", "velocity"), ("Random", RandomMode.PerComponent));
                SetSlot(drift, 0, new Vector { vector = new Vector3(-0.25f, 0.05f, -0.25f) });
                SetSlot(drift, 1, new Vector { vector = new Vector3(0.25f, 0.45f, 0.25f) });
                SetRandom(init, "lifetime", 0.45f, 0.8f);
                SetRandom(init, "size", 0.7f, 1.2f);
                SetRandom(init, "angleZ", 0f, 360f);
                SetRandom(init, "angularVelocityZ", -60f, 60f);
                var smoke = new Color(0.85f, 0.06f, 0.05f, 1f).linear;
                SetSlot(AddBlock<SetAttribute>(init, ("attribute", "color")), 0, new Vector3(smoke.r, smoke.g, smoke.b));

                var update = Add<VFXBasicUpdate>(graph);
                init.LinkTo(update);
                var turbulence = AddBlock<Turbulence>(update);
                SetSlot(turbulence, "Intensity", 0.8f);
                SetSlot(turbulence, "Drag", 1f);
                SetSlot(turbulence, "frequency", 1.5f);

                var output = Add<VFXPlanarPrimitiveOutput>(graph,
                    ("blendMode", VFXAbstractRenderedOutput.BlendMode.Alpha),
                    ("vfxSystemSortPriority", 0));
                update.LinkTo(output);
                SetSlot(output, "mainTexture", wisp);
                AddBlock<Orient>(output, ("mode", Orient.Mode.FaceCameraPlane));
                var alpha = AddBlock<AttributeFromCurve>(output, ("attribute", "alpha"), ("SampleMode", AttributeFromCurve.CurveSampleMode.OverLife));
                SetSlot(alpha, 0, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 0.9f), new Keyframe(1f, 0f)));
                var grow = AddBlock<AttributeFromCurve>(output, ("attribute", "size"), ("SampleMode", AttributeFromCurve.CurveSampleMode.OverLife), ("Composition", AttributeCompositionMode.Multiply));
                SetSlot(grow, 0, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.8f)));
                spawner.label = init.label = update.label = output.label = "CardTrail Smoke";
            }

            graph.SetExpressionGraphDirty();
            graph.RecompileIfNeeded();
            resource.WriteAssetWithSubAssets();
            AssetDatabase.ImportAsset(CardTrailPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"<color=green>[DrawPitSmokeVfxBuilder] {CardTrailPath} built.</color>");
        }

        // Born where the effect is now (its local origin), though the system lives in world space
        private static void SetAtEffect(VFXContext init)
        {
            var position = AddBlock<SetAttribute>(init, ("attribute", "position"));
            position.inputSlots[0].value = new Position { position = Vector3.zero };
            position.inputSlots[0].space = VFXSpace.Local;
        }

        // World space bounds covering the table, since the card flies from the pit across to either hand
        private static void SetCardTrailBounds(VFXContext init)
        {
            var bounds = init.inputSlots.First(s => s.name == "bounds");
            bounds.value = new AABox { center = Vector3.zero, size = new Vector3(60f, 30f, 60f) };
            bounds.space = VFXSpace.World;
        }

        // Red light welling up from the middle of the pit while the mouse is over it: soft red glows born small at
        // the centre that swell outward and fade, drawn over the black smoke. Always running, but multiplied by
        // the exposed float "Glow" (0 = invisible), which DrawPitSmoke.SetHover fades in and out.
        private static void AddHoverGlow(VFXGraph graph)
        {
            var glow = (VFXParameter)VFXLibrary.GetParameters().First(p => p.variant.modelType == typeof(float)).variant.CreateInstance();
            glow.SetSettingValue("m_ExposedName", "Glow"); // DrawPitSmoke.GlowProperty
            glow.SetSettingValue("m_Exposed", true);
            glow.value = 0f;
            graph.AddChild(glow);

            var spawner = Add<VFXBasicSpawner>(graph);
            SetSlot(AddBlock<VFXSpawnerConstantRate>(spawner), "Rate", 5f);

            var init = Add<VFXBasicInitialize>(graph);
            spawner.LinkTo(init);
            init.SetSettingValue("capacity", 16u);
            init.space = VFXSpace.Local;
            SetSlot(init, "bounds", new AABox { center = new Vector3(0f, 0.3f, 0f), size = new Vector3(3f, 2f, 3f) });
            SetSlot(AddBlock<SetAttribute>(init, ("attribute", "position")), 0, new Position { position = new Vector3(0f, 0.12f, 0f) });
            SetRandom(init, "lifetime", 1.1f, 1.5f);
            SetRandom(init, "size", 0.9f, 1.2f);
            SetRandom(init, "angleZ", 0f, 360f);
            var red = new Color(1f, 0.06f, 0.04f, 1f).linear * 2.5f; // above 1: glows with Bloom
            SetSlot(AddBlock<SetAttribute>(init, ("attribute", "color")), 0, new Vector3(red.r, red.g, red.b));

            var update = Add<VFXBasicUpdate>(graph);
            init.LinkTo(update);

            var output = Add<VFXPlanarPrimitiveOutput>(graph,
                ("blendMode", VFXAbstractRenderedOutput.BlendMode.Additive),
                ("vfxSystemSortPriority", 5));
            update.LinkTo(output);
            SetSlot(output, "mainTexture", SoftGlowTexture());
            // Lying flat on the pit, so it reads as light coming up out of it
            var orient = AddBlock<Orient>(output, ("mode", Orient.Mode.Advanced), ("axes", Orient.AxesPair.ZY));
            SetSlot(orient, 0, new DirectionType { direction = Vector3.down });
            SetSlot(orient, 1, new DirectionType { direction = Vector3.forward });

            // Swells outward from the centre and fades
            var alpha = AddBlock<AttributeFromCurve>(output, ("attribute", "alpha"), ("SampleMode", AttributeFromCurve.CurveSampleMode.OverLife));
            SetSlot(alpha, 0, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 0.9f), new Keyframe(1f, 0f)));
            var swell = AddBlock<AttributeFromCurve>(output, ("attribute", "size"), ("SampleMode", AttributeFromCurve.CurveSampleMode.OverLife), ("Composition", AttributeCompositionMode.Multiply));
            SetSlot(swell, 0, new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(1f, 1.8f)));
            // Hidden unless hovered
            var hover = AddBlock<SetAttribute>(output, ("attribute", "alpha"), ("Composition", AttributeCompositionMode.Multiply));
            glow.outputSlots[0].Link(hover.inputSlots[0]);

            spawner.label = "HoverGlow";
            init.label = "HoverGlow";
            update.label = "HoverGlow";
            output.label = "HoverGlow";
        }

        // A white dot fading out very gradually (gaussian), so the overlapping glows show no edges or rings
        private static Texture2D SoftGlowTexture()
        {
            string path = TextureFolder + "SoftGlow.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Exp(-r * r * 6f) * Mathf.Clamp01((1f - r) * 4f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private struct OrbitTrail
        {
            public string name;
            public float radius;
            public float height;
            public float turnsPerSecond;
            public float startTurn;   // 0..1, where on the circle the head starts
            public float tailTurns;   // length of the tail, as a part of one turn
            public float width;
            public float spreadTo;    // width multiplier at the end of the tail, as smoke spreads out
            public Color color;       // gamma, as picked in a colour field
            public float glow;
            public float turbulence;  // curls the tail like smoke
            public float rise;        // the tail drifts up, world units per second
            public bool clockwise;
        }

        // One particle strip: particles are spawned one after another on the next point of a circle (Set Position
        // Sequential: Circle, indexed by particle ID) and stay where they were born, so the newest one is the head
        // going round and the older ones, fading out, are its tail.
        private static void AddOrbitTrail(VFXGraph graph, OrbitTrail t)
        {
            const uint pointsPerTurn = 96;
            float rate = pointsPerTurn * t.turnsPerSecond;
            float life = t.tailTurns / t.turnsPerSecond;
            uint alive = (uint)Mathf.CeilToInt(rate * life * 1.5f) + 8;

            var spawner = Add<VFXBasicSpawner>(graph);
            SetSlot(AddBlock<VFXSpawnerConstantRate>(spawner), "Rate", rate);

            var init = Add<VFXBasicInitialize>(graph);
            var data = init.GetData();
            data.SetSettingValue("dataType", VFXDataParticle.DataType.ParticleStrip);
            data.SetSettingValue("stripCapacity", 1u);
            data.SetSettingValue("particlePerStripCount", alive);
            spawner.LinkTo(init);
            init.space = VFXSpace.Local;
            SetSlot(init, "bounds", new AABox { center = new Vector3(0f, 0.5f, 0f), size = new Vector3(3f, 2f, 3f) });

            var circle = AddBlock<PositionSequential>(init,
                ("compositionPosition", AttributeCompositionMode.Overwrite),
                ("shape", PositionSequential.SequentialShape.Circle),
                ("mode", VFXOperatorUtility.SequentialAddressingMode.Wrap));
            SetSlot(circle, "Count", pointsPerTurn);
            SetSlot(circle, "Center", new Position { position = new Vector3(0f, t.height, 0f) });
            // The circle lies flat on the table; turning the normal over runs it the other way round
            SetSlot(circle, "Normal", new DirectionType { direction = t.clockwise ? Vector3.down : Vector3.up });
            SetSlot(circle, "Up", new DirectionType { direction = Vector3.forward });
            SetSlot(circle, "Radius", t.radius);
            SetSlot(circle, "OffsetIndex", Mathf.RoundToInt(t.startTurn * pointsPerTurn));

            SetSlot(AddBlock<SetAttribute>(init, ("attribute", "lifetime")), 0, life);
            SetSlot(AddBlock<SetAttribute>(init, ("attribute", "size")), 0, t.width);
            var linear = t.color.linear * t.glow;
            SetSlot(AddBlock<SetAttribute>(init, ("attribute", "color")), 0, new Vector3(linear.r, linear.g, linear.b));
            SetSlot(AddBlock<SetAttribute>(init, ("attribute", "velocity")), 0, new Vector { vector = new Vector3(0f, t.rise, 0f) });

            var update = Add<VFXBasicUpdate>(graph);
            init.LinkTo(update);
            if (t.turbulence > 0f)
            {
                var turbulence = AddBlock<Turbulence>(update);
                SetSlot(turbulence, "Intensity", t.turbulence);
                SetSlot(turbulence, "Drag", 1.5f);
                SetSlot(turbulence, "frequency", 2.5f);
            }

            var output = Add<VFXQuadStripOutput>(graph,
                ("blendMode", VFXAbstractRenderedOutput.BlendMode.Additive),
                ("vfxSystemSortPriority", 3));
            update.LinkTo(output);
            // The soft smoke wisp instead of a hard line: ragged, uneven edges along the strip
            SetSlotIfAny(output, "mainTexture", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "SmokeWisp.png"));

            // Bright at the head; the tail fades and spreads out like smoke toward its end (the oldest particles)
            var alpha = AddBlock<AttributeFromCurve>(output, ("attribute", "alpha"), ("SampleMode", AttributeFromCurve.CurveSampleMode.OverLife));
            SetSlot(alpha, 0, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.35f, 0.6f), new Keyframe(1f, 0f)));
            var spread = AddBlock<AttributeFromCurve>(output, ("attribute", "size"), ("SampleMode", AttributeFromCurve.CurveSampleMode.OverLife), ("Composition", AttributeCompositionMode.Multiply));
            SetSlot(spread, 0, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, t.spreadTo)));

            spawner.label = t.name;
            init.label = t.name;
            update.label = t.name;
            output.label = t.name;
        }

        // ---------------------------------------------------------------- helpers

        private static T Add<T>(VFXGraph graph, params (string name, object value)[] settings) where T : VFXContext
        {
            var c = ScriptableObject.CreateInstance<T>();
            foreach (var (name, value) in settings) c.SetSettingValue(name, value);
            graph.AddChild(c);
            return c;
        }

        private static T AddBlock<T>(VFXContext context, params (string name, object value)[] settings) where T : VFXBlock
        {
            var b = ScriptableObject.CreateInstance<T>();
            foreach (var (name, value) in settings) b.SetSettingValue(name, value);
            context.AddChild(b);
            return b;
        }

        private static void SetRandom(VFXContext context, string attribute, float min, float max)
        {
            var b = AddBlock<SetAttribute>(context, ("attribute", attribute), ("Random", RandomMode.Uniform));
            SetSlot(b, 0, min);
            SetSlot(b, 1, max);
        }

        private static void SetSlot(IVFXSlotContainer container, int index, object value)
        {
            container.inputSlots[index].value = value;
        }

        private static void SetSlot(IVFXSlotContainer container, string name, object value)
        {
            var slot = container.inputSlots.FirstOrDefault(s => s.name == name);
            if (slot == null)
                throw new System.Exception($"{container.GetType().Name} has no input '{name}' (has: {string.Join(", ", container.inputSlots.Select(s => s.name))})");
            slot.value = value;
        }

        private static void SetSlotIfAny(IVFXSlotContainer container, string name, object value)
        {
            var slot = container.inputSlots.FirstOrDefault(s => s.name == name);
            if (slot != null) slot.value = value;
            else Debug.LogWarning($"[DrawPitSmokeVfxBuilder] {container.GetType().Name} has no input '{name}' (has: {string.Join(", ", container.inputSlots.Select(s => s.name))})");
        }
    }
}
