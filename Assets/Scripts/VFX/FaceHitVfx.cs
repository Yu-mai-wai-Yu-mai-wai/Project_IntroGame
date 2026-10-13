using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.VFX
{
    /// <summary>
    /// A familiar hitting the opposing player directly, like a direct attack in Yu-Gi-Oh! Master Duel:
    ///  - a hit-stop (the game freezes for a moment on the impact), a camera shake and a zoom kick;
    ///  - on the blow that ends the game (finisher) the screen edges also burst in the school's colour;
    ///  - a small burst at the point of impact: Resources/FaceHit/FaceHitBlack.prefab (มนต์ดำ, blood red with black
    ///    smoke) or FaceHitWhite.prefab (มนต์ขาว, white-gold light), built by Tools > TawanOS > VFX > Create Face Hit VFX.
    /// The edge texture is drawn in code the first time they are needed.
    /// </summary>
    public static class FaceHitVfx
    {
        public const string BlackResource = "FaceHit/FaceHitBlack";
        public const string WhiteResource = "FaceHit/FaceHitWhite";

        /// <summary>Seconds before the effect object is removed; longer than any of its particles live.</summary>
        public const float Lifetime = 2.5f;

        // Real seconds the game freezes on the impact (a critical hit freezes longer)
        public const float HitStop = 0.09f;
        public const float HitStopCrit = 0.16f;
        public const float ShakeTime = 0.45f;
        public const float ShakeStrength = 0.45f;
        public const float ZoomKick = 5f; // degrees of field of view
        public const float EdgeTime = 0.6f;

        private static readonly Color BlackEdge = new Color(0.75f, 0.02f, 0.02f);
        private static readonly Color WhiteEdge = new Color(1f, 0.85f, 0.5f);

        private static GameObject blackPrefab, whitePrefab;
        private static Texture2D edgeTexture;
        private static Runner runner;

        /// <summary>Plays the hit at <paramref name="point"/>; <paramref name="direction"/> is the way the attack went.
        /// <paramref name="finisher"/>: the hit took the last of the opponent's khwan (adds the screen-edge burst).</summary>
        public static void Play(Vector3 point, Vector3 direction, bool blackMagic, bool crit, bool finisher, UnityEngine.SceneManagement.Scene scene)
        {
            var prefab = blackMagic
                ? (blackPrefab != null ? blackPrefab : blackPrefab = Resources.Load<GameObject>(BlackResource))
                : (whitePrefab != null ? whitePrefab : whitePrefab = Resources.Load<GameObject>(WhiteResource));
            if (prefab != null)
            {
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
                var fx = Object.Instantiate(prefab, point, Quaternion.LookRotation(direction.normalized, Vector3.up));
                if (scene.IsValid()) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fx, scene);
                Object.Destroy(fx, Lifetime);
            }

            var r = GetRunner();
            if (finisher) r.EdgeBurst(blackMagic ? BlackEdge : WhiteEdge, crit);
            if (!TawanOS.Settings.GameSettings.ReduceMotion)
            {
                r.Freeze(crit ? HitStopCrit : HitStop);
                ShakeCamera(crit ? 1.5f : 1f);
            }
        }

        /// <summary>
        /// Gets everything ready at the start of a fight, so the first direct hit does not stall: draws the edge
        /// texture and builds the overlay, loads both bursts and plays each once, shrunk to nothing in front of the
        /// camera, so their shaders are compiled before the real hit.
        /// </summary>
        public static void Prepare(UnityEngine.SceneManagement.Scene scene)
        {
            GetRunner();
            if (blackPrefab == null) blackPrefab = Resources.Load<GameObject>(BlackResource);
            if (whitePrefab == null) whitePrefab = Resources.Load<GameObject>(WhiteResource);

            var cam = Camera.main;
            Vector3 at = cam != null ? cam.transform.position + cam.transform.forward * 3f : Vector3.zero;
            foreach (var prefab in new[] { blackPrefab, whitePrefab })
            {
                if (prefab == null) continue;
                var fx = Object.Instantiate(prefab, at, Quaternion.identity);
                fx.name = prefab.name + " (warm-up)";
                fx.transform.localScale = Vector3.one * 0.0001f; // drawn, but far smaller than a pixel
                if (scene.IsValid()) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fx, scene);
                Object.Destroy(fx, 1f);
            }
        }

        private static void ShakeCamera(float strength)
        {
            var cam = Camera.main;
            if (cam == null) return;
            // Not while the camera is gliding to another view: the shake would end where it started.
            // Real time, so it already shakes during the hit-stop.
            if (!DOTween.IsTweening(cam.transform))
                cam.transform.DOShakePosition(ShakeTime, ShakeStrength * strength, 30, 90f, false, true).SetUpdate(true);
            if (!DOTween.IsTweening(cam))
            {
                float fov = cam.fieldOfView;
                DOTween.Sequence().SetTarget(cam).SetUpdate(true)
                    .Append(cam.DOFieldOfView(fov - ZoomKick * strength, 0.05f).SetEase(Ease.OutQuad))
                    .Append(cam.DOFieldOfView(fov, 0.35f).SetEase(Ease.OutQuad));
            }
        }

        private static Runner GetRunner()
        {
            if (runner != null) return runner;
            // An overlay canvas over everything; it lives in the combat scene and goes with it
            var go = new GameObject("FaceHitOverlay", typeof(Canvas));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            runner = go.AddComponent<Runner>();
            runner.edge = MakeLayer(go.transform, "Edge", edgeTexture != null ? edgeTexture : edgeTexture = DrawEdgeTexture());
            return runner;
        }

        private static RawImage MakeLayer(Transform parent, string name, Texture texture)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var image = go.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        // Clear in the middle, glowing toward the edges and most in the corners (stretched over the screen)
        private static Texture2D DrawEdgeTexture()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1.35f, r));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        // Animates the overlay and the hit-stop frame by frame in real time, with each frame's step capped: the
        // first hit can stall a frame (the VFX shaders load), and tweens would then jump straight to their end
        private class Runner : MonoBehaviour
        {
            private const float MaxStep = 1f / 30f;

            public RawImage edge;
            private Color edgeColor;
            private float edgePeak;
            private float edgeTime = -1f;
            private float freezeLeft, frozenScale = -1f, scaleBefore = 1f;

            public void EdgeBurst(Color edgeTint, bool crit)
            {
                edgeColor = edgeTint;
                edgePeak = crit ? 0.5f : 0.35f;
                edgeTime = 0f;
                edge.enabled = true;
                Apply();
            }

            /// <summary>Slows the game almost to a stop for <paramref name="seconds"/> of real time.</summary>
            public void Freeze(float seconds)
            {
                if (freezeLeft <= 0f)
                {
                    if (Time.timeScale < 0.01f) return; // paused
                    scaleBefore = Time.timeScale;
                    frozenScale = scaleBefore * 0.03f;
                    Time.timeScale = frozenScale;
                }
                freezeLeft = Mathf.Max(freezeLeft, seconds);
            }

            private void Update()
            {
                float step = Mathf.Min(Time.unscaledDeltaTime, MaxStep);

                if (freezeLeft > 0f)
                {
                    freezeLeft -= step;
                    if (freezeLeft <= 0f) Unfreeze();
                }

                if (edgeTime < 0f) return;
                edgeTime += step;
                Apply();
            }

            private void Apply()
            {
                // Edges: flare up, hold a moment, fade; the glow closes in a little as it flares
                if (edgeTime >= 0f)
                {
                    const float rise = 0.05f, hold = 0.08f;
                    float a = edgeTime < rise ? edgeTime / rise
                        : edgeTime < rise + hold ? 1f
                        : 1f - EaseIn((edgeTime - rise - hold) / EdgeTime);
                    var c = edgeColor;
                    c.a = edgePeak * Mathf.Clamp01(a);
                    edge.color = c;
                    edge.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, EaseOut(edgeTime / 0.25f));
                    if (edgeTime > rise + hold + EdgeTime)
                    {
                        edge.enabled = false;
                        edgeTime = -1f;
                    }
                }
            }

            private static float EaseIn(float t)
            {
                t = Mathf.Clamp01(t);
                return t * t;
            }

            private static float EaseOut(float t)
            {
                t = Mathf.Clamp01(t);
                return 1f - (1f - t) * (1f - t);
            }

            private void Unfreeze()
            {
                freezeLeft = 0f;
                // Leave it alone if something else (the pause menu) changed it meanwhile
                if (Mathf.Approximately(Time.timeScale, frozenScale)) Time.timeScale = scaleBefore;
            }

            private void OnDestroy()
            {
                if (freezeLeft > 0f) Unfreeze();
            }
        }
    }
}
