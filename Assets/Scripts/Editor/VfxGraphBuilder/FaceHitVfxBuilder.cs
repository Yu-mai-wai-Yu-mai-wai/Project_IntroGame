using System.Linq;
using UnityEditor;
using UnityEditor.VFX;
using UnityEditor.VFX.Block;
using UnityEngine;
using UnityEngine.VFX;
using VfxTransform = UnityEditor.VFX.Transform;

namespace TawanOS.EditorTools.Vfx
{
    // The burst at the point where a familiar hits the opposing player directly (FaceHitVfx at runtime; the
    // big part, after Yu-Gi-Oh! Master Duel, is on the screen edges): a jagged impact star (a struck blow, not a
    // round power burst), streaks bursting out and a lens-flare line, one per school:
    //  - Art/VFX/FaceHit/FaceHitBlack.vfx (มนต์ดำ): blood-red flash and streaks, black smoke blown out, embers.
    //  - Art/VFX/FaceHit/FaceHitWhite.vfx (มนต์ขาว): white-gold flash and rays of light, motes drifting up.
    // Local space: z = the way the attack went, y = up. Everything is one burst on play.
    public static partial class DrawPitSmokeVfxBuilder
    {
        public const string FaceHitFolder = "Assets/Art/VFX/FaceHit/";
        public const string FaceHitBlackPath = FaceHitFolder + "FaceHitBlack.vfx";
        public const string FaceHitWhitePath = FaceHitFolder + "FaceHitWhite.vfx";

        // Builds only the files that do not exist yet: existing ones are tuned by hand in the VFX Graph window
        // and must not be overwritten (delete a file to rebuild it from these defaults)
        public static void BuildFaceHit()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art/VFX/FaceHit")) AssetDatabase.CreateFolder("Assets/Art/VFX", "FaceHit");
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(FaceHitBlackPath) == null)
                BuildFaceHit(FaceHitBlackPath, new Color(1f, 0.08f, 0.05f), new Color(1f, 0.35f, 0.2f), true);
            else Debug.Log($"[DrawPitSmokeVfxBuilder] {FaceHitBlackPath} already exists - kept.");
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(FaceHitWhitePath) == null)
                BuildFaceHit(FaceHitWhitePath, new Color(1f, 0.85f, 0.5f), new Color(1f, 0.97f, 0.9f), false);
            else Debug.Log($"[DrawPitSmokeVfxBuilder] {FaceHitWhitePath} already exists - kept.");
        }

        // main = the school's colour, core = the hot centre of the flash and the rays
        private static void BuildFaceHit(string path, Color main, Color core, bool blackMagic)
        {
            var (graph, resource) = OpenEmptyGraph(path);
            var glow = SoftGlowTexture();
            var wisp = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "SmokeWisp.png");

            AddImpactStars(graph, main, core, blackMagic);

            // Lens-flare line: a thin streak straight across the screen
            {
                var init = BurstSystem(graph, "Flare Line", 1f, 1u);
                SetSlot(AddBlock<SetAttribute>(init, ("attribute", "lifetime")), 0, 0.45f);
                SetSlot(AddBlock<SetAttribute>(init, ("attribute", "size")), 0, 0.35f);
                SetSlot(AddBlock<SetAttribute>(init, ("attribute", "scale"), ("channels", VariadicChannelOptions.X)), 0, 16f);
                SetColor(init, main, 5f);
                var update = Update(graph, init, turbulence: 0f, drag: 0f);
                var output = Output(graph, update, VFXAbstractRenderedOutput.BlendMode.Additive, glow, 7);
                OnTop(output);
                AddBlock<Orient>(output, ("mode", Orient.Mode.FaceCameraPlane));
                Curves(output, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)),
                    new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));
            }

            // Streaks bursting out in every direction, leaning the way the attack went
            {
                var init = BurstSystem(graph, "Streaks", blackMagic ? 45f : 60f, 64u);
                SpawnInSphere(init, 0.3f);
                Outward(init, 4f, 8f, new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 1.5f, 2f));
                SetRandom(init, "lifetime", 0.25f, 0.5f);
                SetRandom(init, "size", 0.18f, 0.3f);
                var stretch = AddBlock<SetAttribute>(init, ("attribute", "scale"), ("channels", VariadicChannelOptions.Y), ("Random", RandomMode.Uniform));
                SetSlot(stretch, 0, 6f);
                SetSlot(stretch, 1, 12f);
                SetColor(init, blackMagic ? main : core, 5f);
                var update = Update(graph, init, turbulence: 0f, drag: 4f);
                var output = Output(graph, update, VFXAbstractRenderedOutput.BlendMode.Additive, glow, 5);
                AddBlock<Orient>(output, ("mode", Orient.Mode.AlongVelocity));
                Curves(output, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 0.8f), new Keyframe(1f, 0f)),
                    new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.4f)));
            }

            if (blackMagic)
            {
                // Black smoke blown out of the impact, hanging afterwards
                {
                    var init = BurstSystem(graph, "Black Smoke", 50f, 64u);
                    SpawnInSphere(init, 0.5f);
                    Outward(init, 1.5f, 3.5f, new Vector3(0f, 0f, 0.3f), new Vector3(0f, 0.8f, 1.2f));
                    SetRandom(init, "lifetime", 0.9f, 1.6f);
                    SetRandom(init, "size", 1f, 2f);
                    SetRandom(init, "angleZ", 0f, 360f);
                    SetRandom(init, "angularVelocityZ", -60f, 60f);
                    SetColor(init, new Color(0.03f, 0.015f, 0.02f), 1f);
                    var update = Update(graph, init, turbulence: 0.8f, drag: 3f);
                    var output = Output(graph, update, VFXAbstractRenderedOutput.BlendMode.Alpha, wisp, 0);
                    AddBlock<Orient>(output, ("mode", Orient.Mode.FaceCameraPlane));
                    Curves(output, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.1f, 0.95f), new Keyframe(0.6f, 0.8f), new Keyframe(1f, 0f)),
                        new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(1f, 1.8f)));
                }

                // Embers flung out and drifting
                {
                    var init = BurstSystem(graph, "Embers", 40f, 48u);
                    SpawnInSphere(init, 0.4f);
                    Outward(init, 3f, 7f, new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 2f, 1.5f));
                    SetRandom(init, "lifetime", 0.6f, 1.3f);
                    SetRandom(init, "size", 0.06f, 0.14f);
                    SetColor(init, main, 4f);
                    var update = Update(graph, init, turbulence: 1.2f, drag: 2.5f);
                    var output = Output(graph, update, VFXAbstractRenderedOutput.BlendMode.Additive, glow, 3);
                    AddBlock<Orient>(output, ("mode", Orient.Mode.FaceCameraPlane));
                    Curves(output, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 0.8f), new Keyframe(1f, 0f)),
                        new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.5f)));
                }
            }
            else
            {
                // Motes of light drifting up afterwards
                {
                    var init = BurstSystem(graph, "Motes", 40f, 48u);
                    SpawnInSphere(init, 0.8f);
                    Outward(init, 1f, 3f, new Vector3(-0.2f, 0.5f, -0.2f), new Vector3(0.2f, 1.5f, 0.6f));
                    SetRandom(init, "lifetime", 0.8f, 1.6f);
                    SetRandom(init, "size", 0.08f, 0.18f);
                    SetColor(init, main, 3f);
                    var update = Update(graph, init, turbulence: 0.4f, drag: 2f);
                    var output = Output(graph, update, VFXAbstractRenderedOutput.BlendMode.Additive, glow, 3);
                    AddBlock<Orient>(output, ("mode", Orient.Mode.FaceCameraPlane));
                    Curves(output, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f)),
                        new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.5f)));
                }
            }

            Save(graph, resource, path);
        }

        // Systems of the first version that made the hit look like a round burst of power
        private static readonly string[] RoundBurstLabels = { "Flash", "Shockwave", "Ground Wave" };

        // Swaps the round flash and shockwave rings in the existing FaceHit .vfx files for the impact stars, in
        // place: every other system (streaks, smoke, embers, motes, flare line) and its tuning is kept
        public static void ReplaceRoundBurstWithImpactStars()
        {
            ReplaceRoundBurst(FaceHitBlackPath, new Color(1f, 0.08f, 0.05f), new Color(1f, 0.35f, 0.2f));
            ReplaceRoundBurst(FaceHitWhitePath, new Color(1f, 0.85f, 0.5f), new Color(1f, 0.97f, 0.9f));
        }

        private static void ReplaceRoundBurst(string path, Color main, Color core)
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
            if (asset == null)
            {
                Debug.LogWarning($"[DrawPitSmokeVfxBuilder] {path} not found - nothing to replace.");
                return;
            }
            var resource = asset.GetResource();
            var graph = resource.GetOrCreateGraph();
            bool hasStars = false;
            foreach (var child in graph.children.ToArray())
            {
                if (!(child is VFXContext context)) continue;
                if (context.label == "Impact Star" || context.label == "Impact Core") hasStars = true;
                if (System.Array.IndexOf(RoundBurstLabels, context.label) < 0) continue;
                context.UnlinkAll();
                graph.RemoveChild(context);
            }
            if (!hasStars) AddImpactStars(graph, main, core, path == FaceHitBlackPath);
            Save(graph, resource, path);
        }

        // Second pass on the impact, in place: the star gets a bigger solid middle (its texture is redrawn, same
        // asset), the core becomes a solid lumpy blob, and small stars fly out around it. Other systems are kept.
        public static void UpdateImpactStars()
        {
            DrawImpactStar(FaceHitFolder + "ImpactStar.png");
            var blob = ImpactBlobTexture();
            UpdateImpactStars(FaceHitBlackPath, new Color(1f, 0.08f, 0.05f), blob);
            UpdateImpactStars(FaceHitWhitePath, new Color(1f, 0.85f, 0.5f), blob);
        }

        private static void UpdateImpactStars(string path, Color main, Texture2D blob)
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
            if (asset == null)
            {
                Debug.LogWarning($"[DrawPitSmokeVfxBuilder] {path} not found - nothing to update.");
                return;
            }
            var resource = asset.GetResource();
            var graph = resource.GetOrCreateGraph();
            bool hasShards = false;
            foreach (var child in graph.children)
            {
                if (!(child is VFXContext context)) continue;
                if (context.label == "Impact Shards") hasShards = true;
                if (context.label == "Impact Core" && context is VFXPlanarPrimitiveOutput coreOutput)
                    SetSlot(coreOutput, "mainTexture", blob);
            }
            if (!hasShards) AddImpactShards(graph, "Impact Shards", main, 5f, VFXAbstractRenderedOutput.BlendMode.Additive, 14f, 6);
            Save(graph, resource, path);
        }

        // Third pass, in place: the shards become sharp slivers flying point-first (the round little stars are
        // replaced), and มนต์ดำ gets black slivers and a burst of black smoke around the blow. Other systems are kept.
        public static void SharpenShardsAndAddSmoke()
        {
            SharpenShards(FaceHitBlackPath, new Color(1f, 0.08f, 0.05f), true);
            SharpenShards(FaceHitWhitePath, new Color(1f, 0.85f, 0.5f), false);
        }

        private static void SharpenShards(string path, Color main, bool blackMagic)
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
            if (asset == null)
            {
                Debug.LogWarning($"[DrawPitSmokeVfxBuilder] {path} not found - nothing to update.");
                return;
            }
            var resource = asset.GetResource();
            var graph = resource.GetOrCreateGraph();
            var shard = ImpactShardTexture();
            bool sharp = false, hasDark = false, hasSmoke = false;
            foreach (var child in graph.children.ToArray())
            {
                if (!(child is VFXContext context)) continue;
                if (context.label == "Impact Shards Dark") hasDark = true;
                if (context.label == "Impact Smoke") hasSmoke = true;
                if (context.label == "Impact Shards" && context is VFXPlanarPrimitiveOutput o
                    && Equals(GetSlotValue(o, "mainTexture"), shard)) sharp = true;
            }
            if (!sharp)
            {
                foreach (var child in graph.children.ToArray())
                {
                    if (!(child is VFXContext context) || context.label != "Impact Shards") continue;
                    context.UnlinkAll();
                    graph.RemoveChild(context);
                }
                AddImpactShards(graph, "Impact Shards", main, 5f, VFXAbstractRenderedOutput.BlendMode.Additive, 14f, 6);
            }
            if (blackMagic && !hasDark) AddDarkShards(graph);
            if (blackMagic && !hasSmoke) AddImpactSmoke(graph);
            Save(graph, resource, path);
        }

        private static object GetSlotValue(IVFXSlotContainer container, string name)
        {
            foreach (var slot in container.inputSlots)
            {
                if (slot.name == name) return slot.value;
            }
            return null;
        }

        // A jagged star in the school's colour snapping open at the point of impact, a solid hotter blob in the
        // middle, each at a random turn, facing the camera and drawn over everything, and sharp slivers flying
        // out; มนต์ดำ also throws black slivers and black smoke
        private static void AddImpactStars(VFXGraph graph, Color main, Color core, bool blackMagic)
        {
            AddImpactStar(graph, "Impact Star", ImpactStarTexture(), main, 4f, 2u, 3.2f, 4.2f, 0.32f, 6);
            AddImpactStar(graph, "Impact Core", ImpactBlobTexture(), core, 6f, 1u, 1.4f, 1.8f, 0.2f, 7);
            AddImpactShards(graph, "Impact Shards", main, 5f, VFXAbstractRenderedOutput.BlendMode.Additive, 14f, 6);
            if (blackMagic)
            {
                AddDarkShards(graph);
                AddImpactSmoke(graph);
            }
        }

        private static void AddDarkShards(VFXGraph graph)
        {
            AddImpactShards(graph, "Impact Shards Dark", new Color(0.04f, 0.01f, 0.01f), 1f, VFXAbstractRenderedOutput.BlendMode.Alpha, 12f, 8);
        }

        // Sharp slivers thrown out all around the blow, flying point-first and slowing as they go
        private static void AddImpactShards(VFXGraph graph, string label, Color color, float glow,
            VFXAbstractRenderedOutput.BlendMode blend, float count, int sortPriority)
        {
            var init = BurstSystem(graph, label, count, (uint)Mathf.CeilToInt(count) + 2u);
            SpawnInSphere(init, 0.25f);
            Outward(init, 6f, 11f, Vector3.zero, new Vector3(0f, 1f, 0f));
            SetRandom(init, "lifetime", 0.25f, 0.45f);
            SetRandom(init, "size", 0.25f, 0.45f);
            var stretch = AddBlock<SetAttribute>(init, ("attribute", "scale"), ("channels", VariadicChannelOptions.Y), ("Random", RandomMode.Uniform));
            SetSlot(stretch, 0, 2.5f);
            SetSlot(stretch, 1, 4.5f);
            SetColor(init, color, glow);
            var update = Update(graph, init, turbulence: 0f, drag: 6f);
            var output = Output(graph, update, blend, ImpactShardTexture(), sortPriority);
            OnTop(output);
            AddBlock<Orient>(output, ("mode", Orient.Mode.AlongVelocity));
            Curves(output, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f)),
                new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.1f, 1f), new Keyframe(1f, 0.35f)));
        }

        // Black smoke puffing out of the blow around the star, drawn over the table but under the star, so the hit
        // reads red on black
        private static void AddImpactSmoke(VFXGraph graph)
        {
            var wisp = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "SmokeWisp.png");
            var init = BurstSystem(graph, "Impact Smoke", 16f, 16u);
            SpawnInSphere(init, 0.4f);
            Outward(init, 2f, 4.5f, Vector3.zero, new Vector3(0f, 0.6f, 0f));
            SetRandom(init, "lifetime", 0.5f, 0.9f);
            SetRandom(init, "size", 1.4f, 2.6f);
            SetRandom(init, "angleZ", 0f, 360f);
            SetRandom(init, "angularVelocityZ", -90f, 90f);
            SetColor(init, new Color(0.02f, 0.008f, 0.01f), 1f);
            var update = Update(graph, init, turbulence: 0.6f, drag: 4f);
            var output = Output(graph, update, VFXAbstractRenderedOutput.BlendMode.Alpha, wisp, 5);
            OnTop(output);
            AddBlock<Orient>(output, ("mode", Orient.Mode.FaceCameraPlane));
            Curves(output, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.08f, 1f), new Keyframe(0.5f, 0.85f), new Keyframe(1f, 0f)),
                new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.6f)));
        }

        // A thin sliver pointed at both ends, longer toward the top (+Y, the way it flies)
        private static Texture2D ImpactShardTexture()
        {
            string path = FaceHitFolder + "ImpactShard.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int size = 128;
            const float widest = 0.35f; // where it is widest, from the bottom
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                float t = v < widest ? v / widest : (1f - v) / (1f - widest);
                float halfWidth = 0.22f * t;
                for (int x = 0; x < size; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / size * 2f - 1f);
                    float alpha = Mathf.Clamp01((halfWidth - u) * 40f);
                    // Hotter along the spine
                    alpha *= Mathf.Lerp(1f, 0.6f, halfWidth > 0f ? u / halfWidth : 1f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            return SaveTexture(path, pixels, size);
        }

        private static void AddImpactStar(VFXGraph graph, string label, Texture2D texture, Color color, float glow, uint count,
            float minSize, float maxSize, float lifetime, int sortPriority)
        {
            var init = BurstSystem(graph, label, count, count);
            SetSlot(AddBlock<SetAttribute>(init, ("attribute", "lifetime")), 0, lifetime);
            SetRandom(init, "size", minSize, maxSize);
            SetRandom(init, "angleZ", 0f, 360f);
            SetColor(init, color, glow);
            var update = Update(graph, init, turbulence: 0f, drag: 0f);
            var output = Output(graph, update, VFXAbstractRenderedOutput.BlendMode.Additive, texture, sortPriority);
            OnTop(output);
            AddBlock<Orient>(output, ("mode", Orient.Mode.FaceCameraPlane));
            // Snaps open almost at once, holds a beat at full strength, then thins out while still growing a little
            Curves(output, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)),
                new AnimationCurve(new Keyframe(0f, 0.3f, 0f, 0f), new Keyframe(0.12f, 1f, 2f, 2f), new Keyframe(1f, 1.15f, 0f, 0f)));
        }

        // A comic impact star: uneven sharp spikes around a big solid middle, hard-edged
        private static Texture2D ImpactStarTexture()
        {
            string path = FaceHitFolder + "ImpactStar.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return existing != null ? existing : DrawImpactStar(path);
        }

        // (Re)draws the star into path; an existing asset keeps its GUID, so effects using it pick up the change
        private static Texture2D DrawImpactStar(string path)
        {
            return DrawSpikyBurst(path, 256, 21, body: 0.16f, longSpikes: 11, longLength: new Vector2(0.78f, 0.98f),
                shortSpikes: 13, shortLength: new Vector2(0.42f, 0.62f), width: new Vector2(9f, 15f));
        }

        // The hot middle of the blow: a dense knot of short sharp spikes, no round outline anywhere
        private static Texture2D ImpactBlobTexture()
        {
            string path = FaceHitFolder + "ImpactBlob.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return existing != null ? existing : DrawImpactBlob(path);
        }

        private static Texture2D DrawImpactBlob(string path)
        {
            return DrawSpikyBurst(path, 128, 5, body: 0.2f, longSpikes: 9, longLength: new Vector2(0.75f, 0.95f),
                shortSpikes: 14, shortLength: new Vector2(0.45f, 0.65f), width: new Vector2(10f, 16f));
        }

        // A solid middle of radius body with long and short sharp spikes all around it (in random order and a
        // little off even spacing), and fine teeth between them, so its outline is jagged everywhere. Hard-edged,
        // brightest in the middle and dimming toward the outline. width = degrees either side at a spike's base.
        private static Texture2D DrawSpikyBurst(string path, int size, int seed, float body, int longSpikes, Vector2 longLength,
            int shortSpikes, Vector2 shortLength, Vector2 width)
        {
            const int teeth = 40;
            var rng = new System.Random(seed);
            int spikes = longSpikes + shortSpikes;
            var isLong = new bool[spikes];
            for (int i = 0; i < longSpikes; i++) isLong[i] = true;
            for (int i = spikes - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (isLong[i], isLong[j]) = (isLong[j], isLong[i]);
            }

            int count = spikes + teeth;
            var angle = new float[count];
            var length = new float[count];
            var halfWidth = new float[count];
            for (int i = 0; i < count; i++)
            {
                float jitter = ((float)rng.NextDouble() - 0.5f) * 0.6f;
                float rand = (float)rng.NextDouble();
                if (i < spikes)
                {
                    angle[i] = (i + jitter) / spikes * 360f;
                    var range = isLong[i] ? longLength : shortLength;
                    length[i] = Mathf.Lerp(range.x, range.y, rand);
                    halfWidth[i] = Mathf.Lerp(width.x, width.y, (float)rng.NextDouble());
                }
                else
                {
                    angle[i] = (i - spikes + jitter) / teeth * 360f;
                    length[i] = body + 0.08f + rand * 0.12f;
                    halfWidth[i] = 360f / teeth * 0.6f;
                }
            }

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    float edge = body;
                    for (int i = 0; i < count; i++)
                    {
                        float d = Mathf.Abs(Mathf.DeltaAngle(a, angle[i]));
                        if (d < halfWidth[i]) edge = Mathf.Max(edge, Mathf.Lerp(length[i], body, d / halfWidth[i]));
                    }
                    float alpha = Mathf.Clamp01((edge - r) * size * 0.25f);
                    // Brightest at the centre, dimming toward the outline along every direction, so the bright
                    // part is star-shaped too instead of a round disc
                    alpha *= Mathf.Lerp(1f, 0.45f, Mathf.Pow(Mathf.Clamp01(r / edge), 0.8f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            return SaveTexture(path, pixels, size);
        }

        // Redraws the star and the middle as spiky bursts into their existing assets (same GUIDs), so both .vfx
        // files pick them up without being touched
        public static void RedrawImpactTextures()
        {
            DrawImpactStar(FaceHitFolder + "ImpactStar.png");
            DrawImpactBlob(FaceHitFolder + "ImpactBlob.png");
        }

        private static Texture2D SaveTexture(string path, Color32[] pixels, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Drawn over the table and the cards instead of going behind them, like a flash on the lens
        private static void OnTop(VFXContext output)
        {
            output.SetSettingValue("zTestMode", VFXAbstractRenderedOutput.ZTestMode.Always);
        }

        // Anywhere in a small ball around the impact point
        private static void SpawnInSphere(VFXContext init, float radius)
        {
            var position = AddBlock<PositionShape>(init, ("shape", PositionShapeBase.Type.Sphere), ("positionMode", PositionBase.PositionMode.ThicknessRelative));
            SetSlot(position, "arcSphere", new TArcSphere
            {
                sphere = new TSphere { transform = new VfxTransform { scale = Vector3.one }, radius = radius },
                arc = Mathf.PI * 2f,
            });
            SetSlotIfAny(position, "Thickness", 1f);
        }
    }
}
