#if UNITY_EDITOR
using System.IO;
using TawanOS.VFX;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    // Builds the draw pit (หลุมจั่ว) smoke: three smoke textures (Art/VFX/DrawPitSmoke/*.png), a TawanOS/DarkSmoke
    // material for each layer, and Resources/DrawPit/DrawPitSmoke.prefab with three Particle Systems (Cover, Wisps,
    // RedStrands). Tune everything on the prefab in the Particle System editor afterwards.
    // Create only makes what is missing, so tuning is kept; Reset rebuilds the prefab from these defaults.
    public static class DrawPitSmokeSetupTool
    {
        private const string ShaderName = "TawanOS/DarkSmoke";
        private const string ArtFolder = "Assets/Art/VFX/DrawPitSmoke";
        private const string PrefabFolder = "Assets/Resources/DrawPit";
        private const string PrefabPath = PrefabFolder + "/DrawPitSmoke.prefab";
        private const string VfxPrefabPath = PrefabFolder + "/DrawPitSmokeVFX.prefab";
        private const string VfxAssetPath = ArtFolder + "/DrawPitSmoke.vfx";
        private const string CardTrailVfxPath = ArtFolder + "/PitCardTrail.vfx";
        private const string CardTrailPrefabPath = PrefabFolder + "/PitCardTrail.prefab";
        // Lives in the editor assembly that may use VFX Graph internals (Editor/VfxGraphBuilder)
        private const string VfxBuilderType = "TawanOS.EditorTools.Vfx.DrawPitSmokeVfxBuilder, Unity.Testing.VisualEffectGraph.Editor";

        private static readonly Color SmokeColor = new Color(0.02f, 0.018f, 0.022f, 1f);
        private static readonly Color StrandColor = new Color(1f, 0.05f, 0.06f, 1f);
        private static readonly Color StrandGlow = new Color(1.9f, 1.9f, 1.9f, 1f); // material tint: above 1 glows
        private const float OrbitTurnsPerSecond = 0.18f;
        private const float PullIn = 0.06f;

        [MenuItem("Tools/TawanOS/VFX/Create Draw Pit Smoke")]
        private static void Create_Menu()
        {
            Create();
        }

        [MenuItem("Tools/TawanOS/VFX/Reset Draw Pit Smoke Prefab")]
        private static void Reset_Menu()
        {
            if (!EditorUtility.DisplayDialog("Reset Draw Pit Smoke", "Rebuild DrawPitSmoke.prefab and its smoke textures from the defaults? Tuning done on the prefab is lost.", "Reset", "Cancel")) return;
            Rebuild();
        }

        // Batch: -executeMethod TawanOS.EditorTools.DrawPitSmokeSetupTool.Rebuild (no dialog)
        public static void Rebuild()
        {
            Build(overwritePrefab: true);
        }

        // Batch: -executeMethod TawanOS.EditorTools.DrawPitSmokeSetupTool.Create
        public static void Create()
        {
            Build(overwritePrefab: false);
        }

        private static void Build(bool overwritePrefab)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[DrawPitSmokeSetupTool] Shader '{ShaderName}' not found (Assets/Art/Shaders/DarkSmoke.shader).");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            EnsureFolder(ArtFolder);
            EnsureFolder(PrefabFolder);

            var puff = MakeTexture("SmokePuff", PuffPixel, overwritePrefab);
            var wisp = MakeTexture("SmokeWisp", WispPixel, overwritePrefab);

            var coverMat = MakeMaterial("DrawPitSmoke_Cover", shader, puff, Color.white);
            var wispMat = MakeMaterial("DrawPitSmoke_Wisps", shader, wisp, Color.white);
            // The red layer uses the soft puff too, glowing red, instead of thin strands
            var strandMat = MakeMaterial("DrawPitSmoke_RedStrands", shader, puff, StrandGlow);

            BuildVfx(overwritePrefab);
            BuildCardTrail(overwritePrefab);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null && !overwritePrefab)
            {
                Debug.Log($"[DrawPitSmokeSetupTool] {PrefabPath} already exists - kept (Reset Draw Pit Smoke Prefab rebuilds it).");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                return;
            }

            var root = new GameObject("DrawPitSmoke");
            var smoke = root.AddComponent<DrawPitSmoke>();
            smoke.cover = BuildCover(root.transform, coverMat);
            smoke.wisps = BuildWisps(root.transform, wispMat);
            smoke.redStrands = BuildRedStrands(root.transform, strandMat);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Selection.activeObject = prefab;
            Debug.Log($"<color=green>[DrawPitSmokeSetupTool] {PrefabPath} built; tune its Cover / Wisps / RedStrands Particle Systems in the Inspector.</color>");
        }

        // The VFX Graph version: builds the .vfx (DrawPitSmokeVfxBuilder) and a prefab with a VisualEffect playing it
        private static void BuildVfx(bool overwrite)
        {
            if (overwrite || AssetDatabase.LoadAssetAtPath<UnityEngine.VFX.VisualEffectAsset>(VfxAssetPath) == null)
            {
                var builder = System.Type.GetType(VfxBuilderType);
                if (builder == null)
                {
                    Debug.LogWarning("[DrawPitSmokeSetupTool] VFX Graph builder not found - only the Particle System smoke is built.");
                    return;
                }
                builder.GetMethod("Build").Invoke(null, null);
            }
            var vfxAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.VFX.VisualEffectAsset>(VfxAssetPath);
            if (vfxAsset == null) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(VfxPrefabPath) != null && !overwrite) return;

            var root = new GameObject("DrawPitSmokeVFX");
            var smoke = root.AddComponent<DrawPitSmoke>();
            smoke.visualEffect = root.AddComponent<UnityEngine.VFX.VisualEffect>();
            smoke.visualEffect.visualEffectAsset = vfxAsset;
            smoke.visualEffect.startSeed = 0;
            smoke.visualEffect.resetSeedOnPlay = true;
            PrefabUtility.SaveAsPrefabAsset(root, VfxPrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"<color=green>[DrawPitSmokeSetupTool] {VfxPrefabPath} built; the draw pit uses the VFX Graph smoke (delete this prefab to go back to the Particle System one).</color>");
        }

        // The red smoke trailing a card pulled out of the pit: PitCardTrail.vfx and Resources/DrawPit/PitCardTrail.prefab
        private static void BuildCardTrail(bool overwrite)
        {
            if (overwrite || AssetDatabase.LoadAssetAtPath<UnityEngine.VFX.VisualEffectAsset>(CardTrailVfxPath) == null)
            {
                var builder = System.Type.GetType(VfxBuilderType);
                if (builder == null) return;
                builder.GetMethod("BuildCardTrail").Invoke(null, null);
            }
            var vfxAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.VFX.VisualEffectAsset>(CardTrailVfxPath);
            if (vfxAsset == null) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CardTrailPrefabPath) != null && !overwrite) return;

            var root = new GameObject("PitCardTrail");
            var trail = root.AddComponent<PitCardTrail>();
            trail.visualEffect = root.AddComponent<UnityEngine.VFX.VisualEffect>();
            trail.visualEffect.visualEffectAsset = vfxAsset;
            PrefabUtility.SaveAsPrefabAsset(root, CardTrailPrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"<color=green>[DrawPitSmokeSetupTool] {CardTrailPrefabPath} built; cards pulled from the pit trail red smoke.</color>");
        }

        // The combat scene keeps its own DrawPitSmoke object, tuned there, instead of the pit placing the prefab
        // at runtime: an object named DrawPitSmoke with a VisualEffect gets the DrawPitSmoke component, and when
        // there is none the VFX prefab is placed. The pit moves it onto itself when combat starts.
        [MenuItem("Tools/TawanOS/VFX/Use Scene Draw Pit Smoke")]
        private static void UseSceneSmoke_Menu()
        {
            UseSceneSmoke(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        // Batch: -executeMethod TawanOS.EditorTools.DrawPitSmokeSetupTool.UseSceneSmokeInCombatScene
        public static void UseSceneSmokeInCombatScene()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/CombatTestScene.unity");
            if (!UseSceneSmoke(scene) && Application.isBatchMode) EditorApplication.Exit(1);
        }

        private static bool UseSceneSmoke(UnityEngine.SceneManagement.Scene scene)
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[DrawPitSmokeSetupTool] Leave Play Mode first.");
                return false;
            }

            DrawPitSmoke smoke = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                smoke = root.GetComponentInChildren<DrawPitSmoke>(true);
                if (smoke != null) break;
            }
            if (smoke == null)
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var vfx in root.GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true))
                    {
                        if (vfx.name != "DrawPitSmoke") continue;
                        smoke = Undo.AddComponent<DrawPitSmoke>(vfx.gameObject);
                        smoke.visualEffect = vfx;
                        break;
                    }
                    if (smoke != null) break;
                }
            }
            if (smoke == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<DrawPitSmoke>(VfxPrefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"[DrawPitSmokeSetupTool] {VfxPrefabPath} is missing - run Create Draw Pit Smoke first.");
                    return false;
                }
                smoke = (DrawPitSmoke)PrefabUtility.InstantiatePrefab(prefab, scene);
                smoke.name = "DrawPitSmoke";
                Undo.RegisterCreatedObjectUndo(smoke.gameObject, "Use Scene Draw Pit Smoke");
            }

            if (smoke.hoverLight == null)
            {
                // Red light in the middle of the pit while the mouse is over it (DrawPitSmoke.SetHover)
                var lightGo = new GameObject("HoverLight");
                Undo.RegisterCreatedObjectUndo(lightGo, "Use Scene Draw Pit Smoke");
                lightGo.transform.SetParent(smoke.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.08f, 0.05f);
                light.range = 3f;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
                light.enabled = false;
                smoke.hoverLight = light;
            }

            EditorUtility.SetDirty(smoke);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Selection.activeObject = smoke.gameObject;
            Debug.Log($"<color=green>[DrawPitSmokeSetupTool] {scene.name}: the draw pit uses the scene's '{smoke.name}' smoke.</color>");
            return true;
        }

        // Play Mode preview: the gust the pit sends up when a card is pulled out
        [MenuItem("Tools/TawanOS/VFX/Preview Draw Pit Smoke Burst (Play Mode)")]
        private static void PreviewBurst()
        {
            var smoke = Object.FindFirstObjectByType<DrawPitSmoke>();
            if (!Application.isPlaying || smoke == null)
            {
                Debug.LogWarning("[DrawPitSmokeSetupTool] Enter Play Mode in the combat scene first.");
                return;
            }
            smoke.Burst();
        }

        // ---------------------------------------------------------------- the three layers

        private static ParticleSystem BuildCover(Transform parent, Material material)
        {
            var ps = MakeSystem("Cover", parent, 0.04f, material, 64);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.3f, 1.9f);
            main.startColor = SmokeColor;

            var shape = ps.shape;
            shape.radius = 0.25f;

            // Lying flat on the table so the table does not cut the puffs in half
            ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;

            var emission = ps.emission;
            emission.rateOverTime = 9f;

            Orbit(ps, 0f, 0f, PullIn);
            Fade(ps, 0.95f, 0.25f, 0.7f);
            Grow(ps, 0.8f, 1.15f);
            return ps;
        }

        private static ParticleSystem BuildWisps(Transform parent, Material material)
        {
            var ps = MakeSystem("Wisps", parent, 0.35f, material, 160);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            main.startColor = SmokeColor;

            var shape = ps.shape;
            shape.radius = 0.45f;
            shape.radiusThickness = 0.6f; // mostly from the outer part of the ring

            var emission = ps.emission;
            emission.rateOverTime = 16f;

            Orbit(ps, 0.08f, 0.22f, PullIn);

            // Curling a little as they turn
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.25f;
            noise.damping = true;
            noise.octaveCount = 2;

            Fade(ps, 0.8f, 0.2f, 0.55f);
            Grow(ps, 0.7f, 1.15f);
            return ps;
        }

        private static ParticleSystem BuildRedStrands(Transform parent, Material material)
        {
            var ps = MakeSystem("RedStrands", parent, 0.35f, material, 64);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
            main.startColor = StrandColor;

            // On the rim of the smoke ball only, so the middle stays black
            var shape = ps.shape;
            shape.radius = 0.7f;
            shape.radiusThickness = 0f;

            // Drawn over the black smoke so the red is not hidden behind it
            ps.GetComponent<ParticleSystemRenderer>().sortingFudge = -20f;

            var emission = ps.emission;
            emission.rateOverTime = 4.5f;

            Orbit(ps, 0.04f, 0.11f, 0f); // not pulled in, so they do not pile up in the middle
            Fade(ps, 0.7f, 0.25f, 0.6f);
            Grow(ps, 0.8f, 1.1f);
            return ps;
        }

        private static ParticleSystem MakeSystem(string name, Transform parent, float height, Material material, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.prewarm = true; // already smoking when the scene starts
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.rotation = new Vector3(90f, 0f, 0f); // lying flat on the table

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        // Turns the particles around the system's centre (the pit) and pulls them in by `pull`; rises at
        // riseMin..riseMax. Every velocity curve uses the same two-constant mode, as Unity requires.
        private static void Orbit(ParticleSystem ps, float riseMin, float riseMax, float pull)
        {
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.y = new ParticleSystem.MinMaxCurve(riseMin, riseMax);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            float turn = OrbitTurnsPerSecond * Mathf.PI * 2f;
            vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.orbitalY = new ParticleSystem.MinMaxCurve(turn * 0.8f, turn * 1.2f);
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.radial = new ParticleSystem.MinMaxCurve(-pull, -pull * 0.5f);
        }

        // Fades in, holds, fades out
        private static void Fade(ParticleSystem ps, float alpha, float inUntil, float outFrom)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, inUntil), new GradientAlphaKey(alpha, outFrom), new GradientAlphaKey(0f, 1f) });
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        private static void Grow(ParticleSystem ps, float from, float to)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, from), new Keyframe(1f, to)));
        }

        // ---------------------------------------------------------------- assets

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static Material MakeMaterial(string name, Shader shader, Texture2D texture, Color tint)
        {
            string path = $"{ArtFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                // Keep the tuning on an existing material, but point it at this layer's texture
                if (mat.mainTexture != texture)
                {
                    mat.mainTexture = texture;
                    EditorUtility.SetDirty(mat);
                }
                return mat;
            }
            mat = new Material(shader) { name = name, mainTexture = texture };
            mat.SetColor("_Tint", tint);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private delegate (float density, float thin) SmokeFn(float x, float y, float r, float seed);

        // Writes a 128x128 smoke texture (alpha = density, rgb = how thin) as a PNG and imports it. Overwriting
        // keeps the file's .meta, so the materials keep pointing at it.
        private static Texture2D MakeTexture(string name, SmokeFn fn, bool overwrite)
        {
            string path = $"{ArtFolder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null && !overwrite) return existing;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            const float seed = 11.7f; // fixed, so a rebuild gives the same smoke
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float x = px / (float)(size - 1) * 2f - 1f;
                    float y = py / (float)(size - 1) * 2f - 1f;
                    float r = Mathf.Sqrt(x * x + y * y);
                    var (density, thin) = fn(x, y, r, seed);
                    byte t = (byte)(Mathf.Clamp01(thin) * 255f);
                    pixels[py * size + px] = new Color32(t, t, t, (byte)(Mathf.Clamp01(density) * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = false; // rgb is data (how thin), not a colour
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed; // compression smears the thin strands into dots
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Dense, soft-edged billow
        private static (float, float) PuffPixel(float x, float y, float r, float seed)
        {
            float body = Fbm(x * 1.5f + seed, y * 1.5f + seed, 3);
            float edge = r + (body - 0.5f) * 0.55f; // lumpy outline
            float density = Mathf.Clamp01(Smooth(1f, 0.45f, edge) * (0.75f + body * 0.5f));
            return (density, 1f - density);
        }

        // Curling strands with translucent sheets between them, like the reference image
        private static (float, float) WispPixel(float x, float y, float r, float seed)
        {
            float qx = Fbm(x * 1.1f + seed, y * 1.1f + seed + 3.1f, 2);
            float qy = Fbm(x * 1.1f + seed + 7.7f, y * 1.1f + seed + 1.3f, 2);
            float n = Fbm(x * 1.4f + qx * 2.6f + seed, y * 1.4f + qy * 2.6f + seed, 2);
            float ridge = 1f - Mathf.Abs(n * 2f - 1f); // 1 along the strands
            float strands = Mathf.Pow(ridge, 3f);
            float sheet = Smooth(0.35f, 0.75f, n) * 0.35f;
            float mask = Smooth(1f, 0.25f, r);
            return (Mathf.Clamp01((strands * 1.3f + sheet) * mask), Mathf.Clamp01(1f - strands));
        }

        // Shader-style smoothstep: 0 at e0, 1 at e1 (either order)
        private static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static float Fbm(float x, float y, int octaves)
        {
            float n = 0f, amp = 0.5f, total = 0f, f = 1f;
            for (int o = 0; o < octaves; o++)
            {
                n += Mathf.PerlinNoise(x * f + o * 17.3f, y * f + o * 9.1f) * amp;
                total += amp;
                amp *= 0.5f;
                f *= 2f;
            }
            return n / total;
        }
    }
}
#endif
