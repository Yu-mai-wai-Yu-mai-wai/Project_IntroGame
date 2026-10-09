using System.IO;
using TawanOS.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Builds the painted main-menu backdrop from Assets/Art/MainMenu (Reference.PNG is the target look):
    /// full-frame painted layers at different depths in front of a perspective camera, so the mouse-follow
    /// camera (<see cref="MouseParallaxCamera"/>) gives real parallax; far trees, eyes in the dark, the main
    /// trees, Khwan with burning incense; fog, incense smoke and drifting embers as particles.
    ///
    /// Only the "IllustratedStage" object and the camera are rebuilt. The old 3D table stage (MainMenuStage)
    /// is switched off, not deleted, and the menu UI is not touched. Safe to run again.
    /// </summary>
    public static class MainMenuIllustratedStageTool
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private const string ArtFolder = "Assets/Art/MainMenu";
        private const string GeneratedFolder = ArtFolder + "/Generated";
        private const string TreePath = ArtFolder + "/Tree.PNG";
        private const string KhwanPath = ArtFolder + "/Khwan.PNG";
        private const string EyesPath = GeneratedFolder + "/Eyes.png";
        private const string SmokeTexPath = "Assets/Art/Particles/SoftSmoke.png";
        private const string SmokeMatPath = GeneratedFolder + "/Mat_MenuSmoke.mat";
        private const string EmberMatPath = GeneratedFolder + "/Mat_MenuEmber.mat";
        private const string SpriteMatPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string ProfilePath = GeneratedFolder + "/MainMenuVolumeProfile.asset";
        private const string StageName = "IllustratedStage";

        // The painting is 1920x1080; at 100 pixels per unit a layer is 19.2 x 10.8 units before scaling
        private const float PixelsPerUnit = 100f;
        private const float ArtHeight = 10.8f;

        private static readonly Vector3 CameraPosition = new Vector3(0f, 0f, -10f);
        private const float CameraFov = 40f;

        // Painted pixel (from the top-left of Khwan.PNG) where the incense burns
        private static readonly Vector2 IncenseTipPixel = new Vector2(1137f, 628f);

        [MenuItem("Tools/TawanOS/Main Menu/Build Illustrated Stage")]
        private static void BuildMenu()
        {
            if (TawanOS.EditorTools.SetupGuard.Confirm("Build Illustrated Main Menu Stage")) Build();
        }

        /// <summary>-executeMethod TawanOS.GameFlow.MainMenuIllustratedStageTool.BuildBatch</summary>
        public static void BuildBatch()
        {
            EditorApplication.Exit(Build() ? 0 : 1);
        }

        public static bool Build()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[MainMenuIllustratedStage] Leave Play mode first.");
                return false;
            }

            if (!AssetDatabase.IsValidFolder(GeneratedFolder)) AssetDatabase.CreateFolder(ArtFolder, "Generated");
            var tree = ImportSprite(TreePath, new Vector2(0.5f, 0.5f));
            var khwan = ImportSprite(KhwanPath, new Vector2(0.5f, 0.5f));
            var eyes = MakeEyesSprite();
            var spriteMat = AssetDatabase.LoadAssetAtPath<Material>(SpriteMatPath);
            var smokeTex = AssetDatabase.LoadAssetAtPath<Texture2D>(SmokeTexPath);
            if (tree == null || khwan == null || eyes == null || spriteMat == null || smokeTex == null)
            {
                Debug.LogError($"[MainMenuIllustratedStage] Missing art: tree={tree != null} khwan={khwan != null} eyes={eyes != null} spriteMat={spriteMat != null} smoke={smokeTex != null}");
                return false;
            }
            var smokeMat = ParticleMaterial(SmokeMatPath, smokeTex, additive: false);
            var emberMat = ParticleMaterial(EmberMatPath, smokeTex, additive: true);

            // Work on MainMenu without closing whatever the developer has open
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            Camera cam = null;
            Volume volume = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == StageName) Object.DestroyImmediate(root);
                else if (root.GetComponent<MainMenuStage>() != null) root.SetActive(false); // old 3D table stage, kept
                else
                {
                    if (cam == null) cam = root.GetComponent<Camera>();
                    if (volume == null) volume = root.GetComponent<Volume>();
                }
            }
            if (cam == null)
            {
                Debug.LogError("[MainMenuIllustratedStage] MainMenu has no root camera.");
                return false;
            }

            SetUpCamera(cam);
            if (volume != null) volume.sharedProfile = MenuProfile(volume.sharedProfile);

            var stage = new GameObject(StageName);
            SceneManager.MoveGameObjectToScene(stage, scene);

            // Far trees: the same painting mirrored, darker and smaller in the haze, far behind
            var far = Layer("FarTrees", stage.transform, tree, spriteMat, depth: 30f, overscan: 1.2f, order: 0,
                new Color(0.32f, 0.12f, 0.14f, 1f));
            far.transform.localScale = new Vector3(-far.transform.localScale.x, far.transform.localScale.y, 1f);
            Face(far, 0.3f);
            var farBreathe = far.gameObject.AddComponent<SpriteBreathe>();
            farBreathe.scaleAmount = 0f;
            farBreathe.pulseAmount = 0.12f;
            farBreathe.pulsePeriod = 5.5f;

            Fog("FarFog", stage.transform, smokeMat, depth: 20f, order: 1, height: 9f,
                new Color(0.32f, 0.16f, 0.18f, 0.2f), size: new Vector2(8f, 14f), rate: 3f, speed: 0.35f);

            BuildEyes(stage.transform, eyes, spriteMat, depth: 16f, order: 2);

            var main = Layer("Trees", stage.transform, tree, spriteMat, depth: 10f, overscan: 1.16f, order: 3, Color.white);
            Face(main, 0.6f);
            var mainBreathe = main.gameObject.AddComponent<SpriteBreathe>();
            mainBreathe.scaleAmount = 0f;
            mainBreathe.pulseAmount = 0.06f; // a slow red pulse in the bark, like a dying fire
            mainBreathe.pulsePeriod = 3.6f;

            // Thin mist wandering in front of the trees, then a heavier bank along the ground
            Fog("MidFog", stage.transform, smokeMat, depth: 8.5f, order: 4, height: 6f,
                new Color(0.34f, 0.26f, 0.30f, 0.06f), size: new Vector2(8f, 13f), rate: 2.5f, speed: 0.3f);
            Fog("GroundFog", stage.transform, smokeMat, depth: 6f, order: 4, height: 2.2f,
                new Color(0.42f, 0.30f, 0.34f, 0.18f), size: new Vector2(4f, 7f), rate: 4f, speed: 0.4f, lowY: -4.2f);
            Embers("EmbersBehind", stage.transform, emberMat, depthMin: 3f, depthMax: 9f, order: 4, rate: 6f);

            var hero = Layer("Khwan", stage.transform, khwan, spriteMat, depth: 2f, overscan: 1.15f, order: 5, Color.white);
            Face(hero, 1f);
            var heroBreathe = hero.gameObject.AddComponent<SpriteBreathe>();
            heroBreathe.scaleAmount = 0.004f;
            heroBreathe.period = 4.2f;
            Incense(hero, emberMat);

            Embers("EmbersFront", stage.transform, emberMat, depthMin: -4f, depthMax: 0.5f, order: 7, rate: 3f);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            Debug.Log(saved
                ? $"<color=green>[MainMenuIllustratedStage] Built '{StageName}' in {ScenePath}. The old table stage is switched off, not deleted.</color>"
                : $"[MainMenuIllustratedStage] Could not save {ScenePath}.");
            return saved;
        }

        // ---------------------------------------------------------------- camera

        private static void SetUpCamera(Camera cam)
        {
            cam.transform.SetPositionAndRotation(CameraPosition, Quaternion.identity);
            cam.fieldOfView = CameraFov;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.035f, 0.012f, 0.016f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;

            // Depth comes from sliding the camera; the turn stays small so the layers' edges never show
            var parallax = cam.GetComponent<MouseParallaxCamera>();
            if (parallax == null) parallax = cam.gameObject.AddComponent<MouseParallaxCamera>();
            parallax.maxYaw = 1.0f;
            parallax.maxPitch = 0.6f;
            parallax.maxShift = 0.5f;
            parallax.smoothTime = 0.6f;
        }

        // Height of the camera's view at a depth, so a layer fills the frame there
        private static float ViewHeightAt(float z)
        {
            float distance = z - CameraPosition.z;
            return 2f * distance * Mathf.Tan(CameraFov * 0.5f * Mathf.Deg2Rad);
        }

        // ---------------------------------------------------------------- painted layers

        private static SpriteRenderer Layer(string name, Transform parent, Sprite sprite, Material mat, float depth,
            float overscan, int order, Color tint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, depth);
            float scale = ViewHeightAt(depth) * overscan / ArtHeight;
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = mat;
            sr.color = tint;
            sr.sortingOrder = order;
            return sr;
        }

        // Pairs of eyes in the dark gaps, placed like the reference (fractions of the frame, 0,0 = bottom-left)
        private static void BuildEyes(Transform parent, Sprite eyes, Material mat, float depth, int order)
        {
            var root = new GameObject("Eyes");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 0f, depth);
            var watcher = root.AddComponent<GlowingEyes>();
            root.AddComponent<FaceCamera>().strength = 1f;

            Vector3[] spots =
            {
                new Vector3(0.31f, 0.86f, -8f), new Vector3(0.65f, 0.87f, 6f), new Vector3(0.25f, 0.62f, 12f),
                new Vector3(0.86f, 0.84f, -4f), new Vector3(0.86f, 0.55f, 4f), new Vector3(0.27f, 0.27f, -10f),
                new Vector3(0.46f, 0.50f, 0f), new Vector3(0.60f, 0.20f, 8f),
            };
            float h = ViewHeightAt(depth);
            float w = h * 16f / 9f;
            float size = h * 0.085f / (eyes.rect.width / PixelsPerUnit);

            var renderers = new SpriteRenderer[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                var go = new GameObject("EyePair" + i);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3((spots[i].x - 0.5f) * w, (spots[i].y - 0.5f) * h, 0f);
                go.transform.localEulerAngles = new Vector3(0f, 0f, spots[i].z);
                float s = size * Random.Range(0.8f, 1.15f);
                go.transform.localScale = new Vector3(s, s, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = eyes;
                sr.sharedMaterial = mat;
                sr.sortingOrder = order;
                sr.color = new Color(1f, 1f, 1f, 0f);
                renderers[i] = sr;
            }
            watcher.eyes = renderers;
            watcher.maxOpen = 3;
        }

        private static void Face(SpriteRenderer layer, float strength)
        {
            var face = layer.gameObject.AddComponent<FaceCamera>();
            face.strength = strength;
            face.maxAngle = 5f;
        }

        // The menu's own copy of the post-processing profile (the combat scene shares the original),
        // with a heavier, slightly red vignette. Made once; later runs keep whatever was tuned in it.
        private static VolumeProfile MenuProfile(VolumeProfile source)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            if (source != null)
            {
                foreach (var component in source.components)
                {
                    if (component == null) continue;
                    var copy = Object.Instantiate(component);
                    copy.name = component.name;
                    profile.components.Add(copy);
                    AssetDatabase.AddObjectToAsset(copy, profile);
                }
            }
            if (!profile.TryGet(out Vignette vignette))
            {
                vignette = profile.Add<Vignette>(true);
                AssetDatabase.AddObjectToAsset(vignette, profile);
            }
            vignette.active = true;
            vignette.color.Override(new Color(0.07f, 0f, 0.02f));
            vignette.intensity.Override(0.55f);
            vignette.smoothness.Override(0.55f);
            vignette.rounded.Override(false);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        // ---------------------------------------------------------------- particles

        private static ParticleSystem NewSystem(string name, Transform parent, Vector3 position, Material mat, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var ps = go.AddComponent<ParticleSystem>();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.sortingOrder = order;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        // Slow banks of mist drifting left to right across the frame at one depth
        private static void Fog(string name, Transform parent, Material mat, float depth, int order, float height,
            Color color, Vector2 size, float rate, float speed, float lowY = 0f)
        {
            float w = ViewHeightAt(depth) * 16f / 9f * 1.3f;
            var ps = NewSystem(name, parent, new Vector3(0f, lowY, depth), mat, order);

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 22f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(w, height, 1f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // Mist wanders both ways with a slight lean to the right, and the noise makes it swirl and sway
            velocity.x = new ParticleSystem.MinMaxCurve(-speed * 0.6f, speed);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.04f, 0.05f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.45f);
            noise.frequency = 0.08f;
            noise.scrollSpeed = 0.07f;
            noise.octaveCount = 2;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            var grow = ps.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.85f), new Keyframe(1f, 1.2f)));

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

            FadeInOut(ps, 0.25f, 0.7f);
        }

        // Tiny glowing embers and ash rising through a deep box, so near ones drift faster than far ones
        private static void Embers(string name, Transform parent, Material mat, float depthMin, float depthMax, int order, float rate)
        {
            float mid = (depthMin + depthMax) * 0.5f;
            float h = ViewHeightAt(depthMax);
            var ps = NewSystem(name, parent, new Vector3(0f, -h * 0.55f, mid), mat, order);

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.42f, 0.12f, 0.9f), new Color(1f, 0.75f, 0.35f, 0.9f));
            main.maxParticles = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(h * 16f / 9f * 1.2f, 0.5f, depthMax - depthMin);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.25f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.4f;
            noise.scrollSpeed = 0.2f;

            FadeInOut(ps, 0.15f, 0.75f);
        }

        // The glowing tip of the incense in Khwan's hands (the smoke is already painted)
        private static void Incense(SpriteRenderer hero, Material emberMat)
        {
            var sprite = hero.sprite;
            Vector2 local = new Vector2(
                (IncenseTipPixel.x - sprite.rect.width * 0.5f) / PixelsPerUnit,
                (sprite.rect.height * 0.5f - IncenseTipPixel.y) / PixelsPerUnit);

            var tip = NewSystem("IncenseGlow", hero.transform, new Vector3(local.x, local.y - 0.08f, -0.02f), emberMat, 6);
            var tm = tip.main;
            tm.scalingMode = ParticleSystemScalingMode.Local;
            tm.loop = true;
            tm.prewarm = true;
            tm.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            tm.startSpeed = 0f;
            tm.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
            tm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.3f, 0.1f, 0.7f), new Color(1f, 0.55f, 0.2f, 0.9f));
            tm.maxParticles = 20;
            tm.simulationSpace = ParticleSystemSimulationSpace.Local;
            var te = tip.emission;
            te.rateOverTime = 12f;
            var ts = tip.shape;
            ts.shapeType = ParticleSystemShapeType.Sphere;
            ts.radius = 0.03f;
            FadeInOut(tip, 0.3f, 0.6f);
        }

        private static void FadeInOut(ParticleSystem ps, float inUntil, float outFrom)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inUntil), new GradientAlphaKey(1f, outFrom), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        // ---------------------------------------------------------------- assets

        private static Sprite ImportSprite(string path, Vector2 pivot)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;

            bool changed = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || !Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit)
                || importer.spritePivot != pivot
                || importer.mipmapEnabled
                || !importer.alphaIsTransparency;
            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Two slanted, glowing slits: the eyes watching from between the trees in the reference
        private static Sprite MakeEyesSprite()
        {
            if (!File.Exists(EyesPath))
            {
                const int w = 256, h = 96;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var pixels = new Color[w * h];
                var core = new Color(1f, 0.55f, 0.25f);
                var glow = new Color(0.85f, 0.12f, 0.04f);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float best = 99f;
                        for (int e = -1; e <= 1; e += 2)
                        {
                            // Each eye leans in toward the other: a narrowed, hostile look
                            float cx = w * 0.5f + e * 58f, cy = h * 0.5f;
                            float a = -e * 18f * Mathf.Deg2Rad;
                            float dx = x - cx, dy = y - cy;
                            float rx = dx * Mathf.Cos(a) - dy * Mathf.Sin(a);
                            float ry = dx * Mathf.Sin(a) + dy * Mathf.Cos(a);
                            float d = Mathf.Sqrt((rx * rx) / (34f * 34f) + (ry * ry) / (9f * 9f));
                            best = Mathf.Min(best, d);
                        }
                        float coreA = 1f - Mathf.SmoothStep(0.65f, 1f, best);
                        float glowA = Mathf.Exp(-Mathf.Max(0f, best - 0.8f) * 2.6f) * 0.55f;
                        float alpha = Mathf.Clamp01(Mathf.Max(coreA, glowA));
                        var c = Color.Lerp(glow, core, coreA);
                        pixels[y * w + x] = new Color(c.r, c.g, c.b, alpha);
                    }
                }
                tex.SetPixels(pixels);
                File.WriteAllBytes(EyesPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(EyesPath);
            }
            return ImportSprite(EyesPath, new Vector2(0.5f, 0.5f));
        }

        private static Material ParticleMaterial(string path, Texture2D texture, bool additive)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            mat.SetTexture("_BaseMap", texture);
            mat.SetFloat("_Surface", 1f);                     // transparent
            mat.SetFloat("_Blend", additive ? 2f : 0f);       // additive glow / alpha mist
            mat.SetFloat("_ZWrite", 0f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
