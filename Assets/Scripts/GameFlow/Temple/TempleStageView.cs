using System.Collections;
using DG.Tweening;
using TawanOS.Settings;
using TawanOS.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// The วัด as a 3D place (Game Designer, 15 Oct): the scene opens inside the old temple, kneeling in front of the
    /// altar, with a กราบไหว้ button and the khwan bar at the bottom middle of the screen. Bowing dips the view, the
    /// candles and the gold light on the empty dais (where the Buddha image will stand) flare, gold sparks of merit
    /// rise from the player, and the bar fills up to its new value; then the screen fades and the run goes on.
    /// Healing itself is <see cref="TempleManager.Pray"/>. Placed by Tools/TawanOS/Temple/Setup Temple Stage.
    /// </summary>
    public class TempleStageView : MonoBehaviour
    {
        [Header("Scene")]
        public Camera viewCamera;
        [Tooltip("Lights by the candles on the altar; they flare when the player bows.")]
        public Light[] candleLights;
        [Tooltip("The gold light on the dais, where the Buddha image will stand.")]
        public Light daisGlow;
        public float daisGlowBlessed = 9f;

        [Header("Bow")]
        [Tooltip("How far the view dips when bowing, in degrees.")]
        public float bowAngle = 14f;
        [Min(0.1f)] public float bowSeconds = 0.9f;
        [Tooltip("How long the khwan bar takes to fill.")]
        [Min(0.1f)] public float fillSeconds = 1.4f;
        [Tooltip("The wait after the bar is full before the screen fades back to the map.")]
        [Min(0f)] public float holdSeconds = 1.6f;
        [Min(0.05f)] public float fadeSeconds = 0.6f;

        [Header("Merit sparks")]
        public Color sparkColour = new Color(1f, 0.82f, 0.4f);
        [Min(0)] public int sparkCount = 160;

        [Header("Text")]
        public string prayLabel = "กราบไหว้";
        public string fullLabel = "กราบลา";

        private TempleManager temple;
        private Button prayButton;
        private TextMeshProUGUI prayText;
        private RectTransform barFill;
        private TextMeshProUGUI barText;
        private Image fade;
        private ParticleSystem sparks;
        private float[] candleBase;
        private float shownHp;
        private bool used;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            candleBase = new float[candleLights != null ? candleLights.Length : 0];
            for (int i = 0; i < candleBase.Length; i++) candleBase[i] = candleLights[i] != null ? candleLights[i].intensity : 0f;
            BuildUI();
            BuildSparks();
        }

        private void Update()
        {
            // Candle flicker
            for (int i = 0; i < candleBase.Length; i++)
            {
                if (candleLights[i] == null) continue;
                float wave = Mathf.PerlinNoise(Time.time * 5f + i * 3.1f, 0.4f) - 0.5f;
                candleLights[i].intensity = candleBase[i] * (1f + wave * 0.35f);
            }
        }

        /// <summary>Called by <see cref="TempleManager"/> when the visit begins.</summary>
        public void Show(TempleManager manager)
        {
            temple = manager;
            used = false;
            if (fade != null) fade.color = new Color(0f, 0f, 0f, 0f);
            shownHp = RunState.Current.CurrentHp;
            SetBar(shownHp);
            prayText.text = temple != null && temple.CanPray ? prayLabel : fullLabel;
            prayButton.interactable = true;
            var group = prayButton.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f;
        }

        private void OnPray()
        {
            if (used || temple == null) return;
            used = true;
            prayButton.interactable = false;
            // The button gives way so the blessing is seen
            var group = prayButton.GetComponent<CanvasGroup>();
            if (group == null) group = prayButton.gameObject.AddComponent<CanvasGroup>();
            group.DOFade(0f, 0.3f).SetLink(gameObject);
            StartCoroutine(Bow());
        }

        private IEnumerator Bow()
        {
            float before = RunState.Current.CurrentHp;
            bool still = GameSettings.ReduceMotion;

            // Head down, then up
            if (!still && viewCamera != null)
            {
                var t = viewCamera.transform;
                var up = t.rotation;
                var down = up * Quaternion.Euler(bowAngle, 0f, 0f);
                yield return t.DORotateQuaternion(down, bowSeconds * 0.5f).SetEase(Ease.InOutSine).SetLink(gameObject).WaitForCompletion();
                TawanOS.Audio.AudioManager.Instance?.PlaySfx("sfx_merit_gain");
                yield return t.DORotateQuaternion(up, bowSeconds * 0.5f).SetEase(Ease.InOutSine).SetLink(gameObject).WaitForCompletion();
            }
            else TawanOS.Audio.AudioManager.Instance?.PlaySfx("sfx_merit_gain");

            // The blessing: khwan comes back (only when it is short), the light flares, merit rises from the player
            temple.Pray();
            if (daisGlow != null) daisGlow.DOIntensity(daisGlowBlessed, 0.8f).SetLink(gameObject);
            for (int i = 0; i < candleBase.Length; i++) candleBase[i] *= 1.6f;
            if (sparks != null) sparks.Emit(sparkCount);

            float after = RunState.Current.CurrentHp;
            if (after > before)
            {
                yield return DOTween.To(() => shownHp, v => { shownHp = v; SetBar(v); }, after, fillSeconds)
                    .SetEase(Ease.OutCubic).SetLink(gameObject).WaitForCompletion();
                barFill.DOPunchScale(new Vector3(0.04f, 0.4f, 0f), 0.4f, 6).SetLink(gameObject);
            }

            yield return new WaitForSeconds(holdSeconds);
            yield return fade.DOFade(1f, fadeSeconds).SetEase(Ease.InQuad).SetLink(gameObject).WaitForCompletion();
            temple.Leave();
        }

        private void SetBar(float hp)
        {
            var run = RunState.Current;
            float t = run.MaxHp > 0 ? Mathf.Clamp01(hp / run.MaxHp) : 0f;
            barFill.anchorMax = new Vector2(t, 1f);
            barText.text = $"ขวัญ {Mathf.RoundToInt(hp)} / {run.MaxHp}";
        }

        // ---- UI ----

        private void BuildUI()
        {
            var theme = UIThemeSO.Current;
            var canvas = UiFactory.CreateOverlayCanvas("TempleCanvas", 10, transform);
            var root = canvas.transform;

            // Khwan bar, bottom middle
            var bar = UiFactory.CreateImage("KhwanBar", root, new Color(0f, 0f, 0f, 0.75f));
            var barRect = bar.rectTransform;
            barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 48f);
            barRect.sizeDelta = new Vector2(640f, 64f);
            barText = UiFactory.CreateText(bar.transform, "Label", "", theme.bodySize, theme.text, TextAlignmentOptions.Center, theme.bodyFont);
            var labelRect = barText.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var track = UiFactory.CreateImage("Track", bar.transform, new Color(0.15f, 0.12f, 0.12f, 0.95f));
            var trackRect = track.rectTransform;
            trackRect.anchorMin = new Vector2(0f, 0f);
            trackRect.anchorMax = new Vector2(1f, 0.5f);
            trackRect.offsetMin = new Vector2(18f, 12f);
            trackRect.offsetMax = new Vector2(-18f, -2f);
            var fill = UiFactory.CreateImage("Fill", track.transform, new Color(0.3f, 0.75f, 0.45f));
            barFill = fill.rectTransform;
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = Vector2.one;
            barFill.offsetMin = barFill.offsetMax = Vector2.zero;
            barFill.pivot = new Vector2(0f, 0.5f);
            foreach (var g in bar.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;

            // กราบไหว้ button above the bar
            prayButton = UiFactory.CreateButton(root, "PrayButton", prayLabel, theme, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            prayButton.onClick.AddListener(OnPray);
            var buttonRect = (RectTransform)prayButton.transform;
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 140f);
            buttonRect.sizeDelta = new Vector2(320f, 72f);
            prayText = prayButton.GetComponentInChildren<TextMeshProUGUI>();
            prayText.fontSize = theme.bodySize * 1.5f;
            prayText.color = new Color(1f, 0.9f, 0.62f);
            var outline = prayButton.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.93f, 0.78f, 0.45f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            var fadeCanvas = UiFactory.CreateOverlayCanvas("TempleFadeCanvas", 900, transform);
            fade = UiFactory.CreateImage("Fade", fadeCanvas.transform, Color.black);
            fade.raycastTarget = false;
            UiFactory.Stretch(fade.rectTransform, 0f);
            fade.color = new Color(0f, 0f, 0f, 0f);
        }

        // Gold motes of merit rising round the player: emitted in a ring just in front of and below the camera,
        // drifting up and out, fading as they go
        private void BuildSparks()
        {
            var shader = Resources.Load<Shader>("TawanOS_MeruParticle");
            if (shader == null || viewCamera == null) return;
            var go = new GameObject("MeritSparks");
            go.SetActive(false);
            go.transform.SetParent(viewCamera.transform, false);
            go.transform.localPosition = new Vector3(0f, -2.2f, 3.5f);
            sparks = go.AddComponent<ParticleSystem>();
            var main = sparks.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
            main.startColor = sparkColour;
            main.maxParticles = 400;
            main.gravityModifier = -0.12f;
            var emission = sparks.emission;
            emission.rateOverTime = 0f;
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 2.2f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var colour = sparks.colorOverLifetime;
            colour.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(sparkColour, 0.5f), new GradientColorKey(sparkColour, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colour.color = g;
            var noise = sparks.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.8f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            var material = new Material(shader) { mainTexture = SparkTexture() };
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_Premultiply", 1f);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(true);
        }

        // A bright point with a soft halo
        private static Texture2D SparkTexture()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "TempleSpark" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size - 0.5f, v = (y + 0.5f) / size - 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v) * 2f;
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * 0.7f + Mathf.Clamp01(1f - r * 4f) * 0.3f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
