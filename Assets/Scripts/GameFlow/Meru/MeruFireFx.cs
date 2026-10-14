using UnityEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// The เมรุ furnace's fire as particles (Game Designer, 14 Oct: the flat flames looked fake, as if the door itself
    /// burned). Inside the furnace many soft tongues rise from the floor, sway and shrink as they cool from white-yellow
    /// to orange to dark red, with sparks flying up. When <see cref="MeruStageView"/> bursts the fire out, flames jet
    /// from the gaps round the shut door (both sides and the top), toward the viewer and upward, and dark smoke rolls up
    /// after them. The particle textures are made here and drawn with TawanOS_MeruParticle, so nothing needs an asset.
    /// Placed by Tools/TawanOS/Meru/Setup Meru Stage.
    /// </summary>
    public class MeruFireFx : MonoBehaviour
    {
        [Header("Furnace")]
        [Tooltip("Middle of the furnace floor (world space), where the fire burns.")]
        public Vector3 furnaceFloor;
        [Tooltip("Width, height and depth of the furnace inside.")]
        public Vector3 furnaceSize = new Vector3(8f, 14f, 12f);
        [Tooltip("Across the furnace (the viewer's right as they face the door).")]
        public Vector3 furnaceRight = Vector3.right;
        public float flameRate = 110f;
        public float sparkRate = 6f;

        [Header("Door")]
        [Tooltip("Middle of the shut door (world space).")]
        public Vector3 doorCenter;
        public Vector2 doorSize = new Vector2(7.5f, 15f);
        [Tooltip("The way the door faces, out toward the viewer.")]
        public Vector3 doorFacing = Vector3.back;
        [Tooltip("Flames a second out of the door gaps at the height of the burst.")]
        public float burstRate = 420f;
        public float smokeRate = 30f;

        [Header("Colour")]
        public Gradient flameColour;
        public Color smokeColour = new Color(0.17f, 0.16f, 0.16f, 0.6f);

        private ParticleSystem flames, sparks, jets, smoke;
        private float jetCarry, smokeCarry;

        private void Reset() => flameColour = DefaultFlame();

        private void Awake()
        {
            if (flameColour == null || flameColour.colorKeys.Length < 2) flameColour = DefaultFlame();
            var shader = Resources.Load<Shader>("TawanOS_MeruParticle");
            if (shader == null) return;
            var flameMat = Material(shader, PuffTexture(64, 1.1f, 11), additive: true);
            var sparkMat = Material(shader, DotTexture(16), additive: true);
            var smokeMat = Material(shader, PuffTexture(64, 0.9f, 23), additive: false);

            flames = BuildFlames(flameMat);
            sparks = BuildSparks(sparkMat);
            jets = BuildJets(flameMat);
            smoke = BuildSmoke(smokeMat);
        }

        private static Gradient DefaultFlame()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.93f, 0.7f), 0f),
                    new GradientColorKey(new Color(1f, 0.72f, 0.28f), 0.2f),
                    new GradientColorKey(new Color(1f, 0.38f, 0.08f), 0.55f),
                    new GradientColorKey(new Color(0.45f, 0.06f, 0.02f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.9f, 0.1f),
                    new GradientAlphaKey(0.5f, 0.42f),
                    new GradientAlphaKey(0.12f, 0.72f),
                    new GradientAlphaKey(0f, 1f),
                });
            return g;
        }

        /// <summary>How hard the fire bursts out of the shut door, 0 (not at all) to 1 (full); call every frame.</summary>
        public void SetBurst(float strength)
        {
            if (jets == null) return;
            jetCarry += burstRate * strength * Time.deltaTime;
            while (jetCarry >= 1f) { EmitJet(); jetCarry -= 1f; }
            if (strength > 0.05f && Random.value < strength * 10f * Time.deltaTime) EmitSpark(true);
        }

        /// <summary>Smoke rolling up off the door once the flames die down, 0 to 1; call every frame.</summary>
        public void SetSmoke(float strength)
        {
            if (smoke == null) return;
            smokeCarry += smokeRate * strength * Time.deltaTime;
            while (smokeCarry >= 1f) { EmitSmoke(); smokeCarry -= 1f; }
        }

        // ---- Systems ----

        private ParticleSystem NewSystem(string name, Material material, int max)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.playOnAwake = true;
            main.loop = true;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.maxParticleSize = 2f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        // Tongues rising off the furnace floor round the cart, swaying, shrinking as they cool
        private ParticleSystem BuildFlames(Material material)
        {
            var ps = NewSystem("Flames", material, 400);
            ps.transform.position = furnaceFloor;
            ps.transform.rotation = Quaternion.LookRotation(Vector3.Cross(furnaceRight, Vector3.up), Vector3.up);
            var main = ps.main;
            float h = furnaceSize.y;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(h * 0.2f, h * 0.36f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = ps.emission;
            emission.rateOverTime = flameRate;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(furnaceSize.x * 0.75f, 0.2f, furnaceSize.z * 0.6f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(h * 0.45f, h * 0.75f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            Common(ps, h * 0.08f);
            ps.gameObject.SetActive(true);
            return ps;
        }

        private ParticleSystem BuildSparks(Material material)
        {
            var ps = NewSystem("Sparks", material, 200);
            ps.transform.position = furnaceFloor + Vector3.up * furnaceSize.y * 0.15f;
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.startColor = new Color(1f, 0.75f, 0.35f);
            main.gravityModifier = -0.15f;
            var emission = ps.emission;
            emission.rateOverTime = sparkRate;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(furnaceSize.x * 0.6f, 0.2f, furnaceSize.z * 0.5f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            velocity.y = new ParticleSystem.MinMaxCurve(4f, 9f);
            velocity.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 1.2f;
            noise.frequency = 0.6f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 1.5f;
            ps.gameObject.SetActive(true);
            return ps;
        }

        // Fed by hand (EmitJet): flames jetting out of the door gaps
        private ParticleSystem BuildJets(Material material)
        {
            var ps = NewSystem("DoorJets", material, 800);
            Common(ps, doorSize.y * 0.06f);
            ps.gameObject.SetActive(true);
            return ps;
        }

        private ParticleSystem BuildSmoke(Material material)
        {
            var ps = NewSystem("DoorSmoke", material, 200);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            colour.color = g;
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            ps.gameObject.SetActive(true);
            return ps;
        }

        // Colour by age, shrink as they rise, turn slowly, and waver
        private void Common(ParticleSystem ps, float noiseStrength)
        {
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            colour.color = flameColour;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var shrink = new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.25f));
            size.size = new ParticleSystem.MinMaxCurve(1f, shrink);
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = noiseStrength;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.8f;
            noise.octaveCount = 2;
        }

        // ---- Burst ----

        // A point in the gap round the shut door: up either side (more of them) or across the top
        private Vector3 GapPoint(out Vector3 outward)
        {
            var right = Vector3.Cross(Vector3.up, -doorFacing).normalized;
            float halfW = doorSize.x * 0.5f, halfH = doorSize.y * 0.5f;
            float pick = Random.value;
            Vector3 p;
            if (pick < 0.7f)
            {
                float side = pick < 0.35f ? -1f : 1f;
                p = doorCenter + right * side * halfW + Vector3.up * Random.Range(-halfH * 0.85f, halfH * 0.95f);
                outward = right * side;
            }
            else
            {
                p = doorCenter + Vector3.up * halfH * 0.97f + right * Random.Range(-halfW * 0.8f, halfW * 0.8f);
                outward = Vector3.up;
            }
            return p + doorFacing * 0.3f;
        }

        private void EmitJet()
        {
            var p = GapPoint(out var outward);
            float h = doorSize.y;
            var emit = new ParticleSystem.EmitParams
            {
                position = p,
                velocity = doorFacing * Random.Range(h * 0.15f, h * 0.45f) + Vector3.up * Random.Range(h * 0.25f, h * 0.55f)
                           + outward * Random.Range(0f, h * 0.15f),
                startSize = Random.Range(h * 0.14f, h * 0.28f),
                startLifetime = Random.Range(0.45f, 0.85f),
                rotation = Random.Range(0f, 360f),
                startColor = Color.white,
            };
            jets.Emit(emit, 1);
        }

        private void EmitSmoke()
        {
            var p = GapPoint(out _);
            float h = doorSize.y;
            var emit = new ParticleSystem.EmitParams
            {
                position = p + Vector3.up * h * 0.1f,
                velocity = Vector3.up * Random.Range(h * 0.2f, h * 0.35f) + doorFacing * Random.Range(h * 0.05f, h * 0.15f),
                startSize = Random.Range(h * 0.3f, h * 0.55f),
                startLifetime = Random.Range(1.8f, 2.8f),
                rotation = Random.Range(0f, 360f),
                startColor = smokeColour,
            };
            smoke.Emit(emit, 1);
        }

        private void EmitSpark(bool fromDoor)
        {
            if (sparks == null) return;
            var p = fromDoor ? GapPoint(out _) : furnaceFloor;
            var emit = new ParticleSystem.EmitParams
            {
                position = p,
                velocity = doorFacing * Random.Range(3f, 8f) + Vector3.up * Random.Range(4f, 10f) + Random.insideUnitSphere * 2f,
                startSize = Random.Range(0.12f, 0.3f),
                startLifetime = Random.Range(0.8f, 1.6f),
                startColor = new Color(1f, 0.8f, 0.4f),
            };
            sparks.Emit(emit, 1);
        }

        // ---- Materials and textures ----

        private static Material Material(Shader shader, Texture texture, bool additive)
        {
            var m = new Material(shader) { mainTexture = texture };
            m.SetFloat("_SrcBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.SrcAlpha));
            m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_Premultiply", additive ? 1f : 0f);
            return m;
        }

        // A soft round puff broken up by noise, so stacked puffs read as flame or smoke rather than discs
        private static Texture2D PuffTexture(int size, float breakup, int seed)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "MeruPuff" };
            var pixels = new Color32[size * size];
            float ox = seed * 13.1f, oy = seed * 7.7f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size - 0.5f, v = (y + 0.5f) / size - 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v) * 2f;
                    float n = Mathf.PerlinNoise(ox + u * 4f, oy + v * 4f) * 0.65f + Mathf.PerlinNoise(ox + u * 9f, oy + v * 9f) * 0.35f;
                    float edge = Mathf.Clamp01(1f - r * (1f + (n - 0.5f) * breakup));
                    float a = edge * edge * (3f - 2f * edge);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D DotTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "MeruSpark" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size - 0.5f, v = (y + 0.5f) / size - 0.5f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v) * 2f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
