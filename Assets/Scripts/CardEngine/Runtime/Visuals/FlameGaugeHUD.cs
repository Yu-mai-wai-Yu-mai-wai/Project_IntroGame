using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using TawanOS.Settings;
using TawanOS.UI;

namespace TawanOS.CardEngine
{
    // กุศล and มลทิน as two flame rings stacked on the left of the screen (Game Designer, 13 Oct), replacing the old
    // bottom-left Merit and Corruption panels. Each gauge is drawn by one shader (Resources/TawanOS_UIFlameGauge),
    // after the drawn flame ring the Game Designer sent: a ring the colour of the fire, live flames burning out from
    // behind it, and a solid black plate with "current/max" on it. Sizes, places, colours and fire speed come from
    // FlameGaugeStyleSO (Resources/FlameGaugeStyle), which redraws the gauges when edited during play.
    // มลทิน warns before it breaks: with warnPoints or fewer left the ring beats, its fire climbs and its number glows
    // red, faster with 1 left. When it breaks the ring swells, bursts into sparks and its fire burns back up from low;
    // the screen-wide part of the break (flash, lightning, shake, khwan lost) is CorruptionBreakFx (Game Designer, 13 Oct).
    // Bootstraps itself in any scene that has a CombatManager.
    public class FlameGaugeHUD : MonoBehaviour
    {
        private const string GaugeShader = "TawanOS_UIFlameGauge";

        private class Gauge
        {
            public RectTransform root;
            public RectTransform face;
            public Material material;
            public TMP_Text number;
        }

        private Gauge merit;
        private Gauge corruption;
        private CombatManager combat;
        private FlameGaugeStyleSO style;
        private RectTransform canvasRect;
        private static Texture2D sparkTexture;

        // มลทิน warning and break
        private int corruptionNow, corruptionMax;
        private float warnLevel;                 // 0 = calm, 0.5 = 2 left, 1 = 1 left (with warnPoints = 2)
        private float warnPhase;
        private float regrow = 1f;              // the fire's height after a break: 0 = out, 1 = full
        private float holdFullUntil;            // the number shows max/max a moment after the break

        // AfterSceneLoad fires only for the first scene played; the combat scene is usually loaded later from the map.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void HookSceneLoads()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded; // no double hook without domain reload
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => Bootstrap();

        private static void Bootstrap()
        {
            if (FindFirstObjectByType<CombatManager>() == null) return;
            if (FindFirstObjectByType<FlameGaugeHUD>() != null) return;
            new GameObject("FlameGaugeHUD").AddComponent<FlameGaugeHUD>();
        }

        private void Start()
        {
            style = FlameGaugeStyleSO.Load();
            BuildUI();
            ApplyStyle();
            FlameGaugeStyleSO.Changed += ApplyStyle;

            combat = CombatManager.Instance;
            if (combat == null) return;

            combat.OnMeritChanged += HandleMeritChanged;
            combat.OnCorruptionChanged += HandleCorruptionChanged;
            combat.OnCurseBackfireTriggered += HandleBackfire;
            Refresh(merit, combat.CurrentMerit, combat.State.playerMaxMerit, animate: false);
            Refresh(corruption, combat.CurrentCorruption, combat.State.corruptionThreshold, animate: false);
        }

        private void OnDestroy()
        {
            FlameGaugeStyleSO.Changed -= ApplyStyle;
            if (combat != null)
            {
                combat.OnMeritChanged -= HandleMeritChanged;
                combat.OnCorruptionChanged -= HandleCorruptionChanged;
                combat.OnCurseBackfireTriggered -= HandleBackfire;
            }
            foreach (var g in new[] { merit, corruption })
                if (g != null && g.material != null) Destroy(g.material);
        }

        private void HandleMeritChanged(int current, int max) => Refresh(merit, current, max, animate: true);
        private void HandleCorruptionChanged(int current, int max) => Refresh(corruption, current, max, animate: true);

        // The ring swells, bursts into sparks and its fire goes low, then burns back up; the number shows it full
        // for a moment before it drops to what carried over
        private void HandleBackfire()
        {
            if (corruption == null) return;
            corruption.number.text = $"{corruptionMax}/{corruptionMax}";
            holdFullUntil = Time.time + 0.45f;

            var face = corruption.face;
            face.DOKill();
            face.localScale = Vector3.one;
            DOTween.Sequence().SetLink(gameObject)
                .Append(face.DOScale(1.22f, 0.12f).SetEase(Ease.OutQuad))
                .AppendCallback(() =>
                {
                    Burst(corruption, style.corruption);
                    DOTween.Kill(this);
                    regrow = 0.1f;
                    DOVirtual.Float(0.1f, 1f, style.breakRegrow, v => regrow = v).SetEase(Ease.OutCubic).SetId(this).SetLink(gameObject);
                })
                .Append(face.DOScale(1f, 0.25f).SetEase(Ease.OutBack));
            if (!GameSettings.ReduceMotion)
            {
                corruption.root.DOComplete();
                corruption.root.DOShakeAnchorPos(0.5f, 20f, 25);
            }
        }

        // Sparks flying out from the ring and fading
        private void Burst(Gauge g, FlameGaugeStyleSO.Palette pal)
        {
            if (canvasRect == null) return;
            int count = style.breakSparks;
            float radius = style.size * style.outerRadius;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Spark", typeof(RectTransform));
                go.transform.SetParent(canvasRect, false);
                var img = go.AddComponent<RawImage>();
                img.texture = SparkTexture();
                img.raycastTarget = false;
                img.color = Color.Lerp(pal.flame, pal.tip, Random.value);
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = g.root.anchorMin;
                rt.sizeDelta = Vector2.one * Random.Range(10f, 22f);
                float angle = (i + Random.value * 0.6f) / Mathf.Max(count, 1) * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                rt.anchoredPosition = g.root.anchoredPosition + dir * radius;
                float flight = Random.Range(0.45f, 0.8f);
                var target = g.root.anchoredPosition + dir * radius * Random.Range(1.6f, 2.4f) + Vector2.up * Random.Range(10f, 40f);
                DOTween.Sequence().SetLink(go)
                    .Append(rt.DOAnchorPos(target, flight).SetEase(Ease.OutCubic))
                    .Join(rt.DOScale(0.2f, flight).SetEase(Ease.InQuad))
                    .Join(DOVirtual.Float(1f, 0f, flight, a => { var c = img.color; c.a = a; img.color = c; }).SetEase(Ease.InQuad))
                    .OnComplete(() => Destroy(go));
            }
        }

        // The มลทิน warning beats and the fire's regrowth after a break, every frame
        private void Update()
        {
            if (corruption == null || style == null) return;
            bool still = GameSettings.ReduceMotion;

            int left = corruptionMax - corruptionNow;
            float target = corruptionNow > 0 && style.warnPoints > 0 && left <= style.warnPoints
                ? 1f - (left - 1f) / style.warnPoints : 0f;
            warnLevel = Mathf.MoveTowards(warnLevel, target, Time.deltaTime * 3f);

            float beats = style.warnBeats * (1f + warnLevel);           // twice as fast at 1 left
            warnPhase += Time.deltaTime * beats * Mathf.PI;
            float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(warnPhase)), 6f); // a sharp thump, like a heartbeat

            if (!still && !DOTween.IsTweening(corruption.face))
                corruption.face.localScale = Vector3.one * (1f + style.warnPulse * warnLevel * beat);

            float speed = still ? style.reducedMotionSpeed : style.flameSpeed;
            corruption.material.SetFloat("_FlameScale", style.flameHeight * regrow * (1f + style.warnFlameBoost * warnLevel * (0.7f + 0.3f * beat)));
            corruption.material.SetFloat("_Motion", speed * (1f + warnLevel * 0.8f));

            if (holdFullUntil > 0f && Time.time >= holdFullUntil)
            {
                holdFullUntil = 0f;
                corruption.number.text = $"{corruptionNow}/{corruptionMax}";
            }
            corruption.number.color = Color.Lerp(style.corruption.number, style.warnNumber, warnLevel * (still ? 1f : 0.5f + 0.5f * beat));
        }

        private void Refresh(Gauge g, int current, int max, bool animate)
        {
            if (g == null) return;
            if (g == corruption) { corruptionNow = current; corruptionMax = max; }
            if (g == corruption && Time.time < holdFullUntil) return; // the break is still showing it full
            g.number.text = $"{current}/{max}";
            if (!animate) return;
            g.root.DOComplete();
            g.root.localScale = Vector3.one;
            g.root.DOPunchScale(Vector3.one * 0.08f, 0.3f, 6);
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("FlameGaugeCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvasRect = (RectTransform)canvasGo.transform;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var shader = Resources.Load<Shader>(GaugeShader);
            if (shader == null)
            {
                Debug.LogError($"[FlameGaugeHUD] Missing shader Resources/{GaugeShader}.");
                return;
            }

            corruption = BuildGauge(canvasGo.transform, "CorruptionGauge", shader);
            merit = BuildGauge(canvasGo.transform, "MeritGauge", shader);
        }

        private static Gauge BuildGauge(Transform parent, string name, Shader shader)
        {
            var g = new Gauge();

            var rootGo = new GameObject(name, typeof(RectTransform));
            rootGo.transform.SetParent(parent, false);
            g.root = (RectTransform)rootGo.transform;
            g.root.anchorMin = g.root.anchorMax = new Vector2(0f, 0.5f);
            g.root.pivot = new Vector2(0.5f, 0.5f);

            // Plate, ring and fire, all from the shader
            var faceGo = new GameObject("Gauge", typeof(RectTransform));
            faceGo.transform.SetParent(g.root, false);
            var face = faceGo.AddComponent<RawImage>();
            face.texture = Texture2D.whiteTexture;
            face.raycastTarget = false;
            g.face = face.rectTransform;
            g.face.anchorMin = g.face.anchorMax = new Vector2(0.5f, 0.5f);
            g.material = new Material(shader);
            face.material = g.material;

            // current/max on the plate
            var numGo = new GameObject("Value", typeof(RectTransform));
            numGo.transform.SetParent(g.root, false);
            g.number = numGo.AddComponent<TextMeshProUGUI>();
            g.number.fontStyle = FontStyles.Bold;
            g.number.alignment = TextAlignmentOptions.Center;
            g.number.raycastTarget = false;
            UiFactory.EnableThaiMarks(g.number);
            var numRt = g.number.rectTransform;
            numRt.anchorMin = numRt.anchorMax = new Vector2(0.5f, 0.5f);
            numRt.anchoredPosition = Vector2.zero;

            return g;
        }

        // Lays out and colours both gauges from the style asset; runs at start and whenever the asset is edited
        private void ApplyStyle()
        {
            if (style == null || merit == null || corruption == null) return;
            float size = style.size;
            if (style.topLeftRow)
            {
                // A row at the top-left corner: the 'upper' gauge on the left
                float y = -(style.topMargin + size * 0.5f);
                float left = style.leftMargin + size * 0.5f, right = left + size + style.rowGap;
                Apply(merit, style.merit, new Vector2(0f, 1f), new Vector2(style.meritOnTop ? left : right, y));
                Apply(corruption, style.corruption, new Vector2(0f, 1f), new Vector2(style.meritOnTop ? right : left, y));
                return;
            }
            float upper = style.upperY, lower = style.lowerY, x = style.leftMargin + size * 0.5f;
            Apply(merit, style.merit, new Vector2(0f, 0.5f), new Vector2(x, style.meritOnTop ? upper : lower));
            Apply(corruption, style.corruption, new Vector2(0f, 0.5f), new Vector2(x, style.meritOnTop ? lower : upper));
        }

        private void Apply(Gauge g, FlameGaugeStyleSO.Palette pal, Vector2 anchor, Vector2 position)
        {
            float size = style.size;
            g.root.sizeDelta = Vector2.one * size;
            g.root.anchorMin = g.root.anchorMax = anchor;
            g.root.anchoredPosition = position;
            g.face.sizeDelta = Vector2.one * size;

            g.material.SetColor("_FlameColor", pal.flame);
            g.material.SetColor("_TipColor", pal.tip);
            g.material.SetFloat("_InnerRadius", style.innerRadius);
            g.material.SetFloat("_OuterRadius", style.outerRadius);
            g.material.SetFloat("_FlameScale", style.flameHeight);
            g.material.SetFloat("_Motion", GameSettings.ReduceMotion ? style.reducedMotionSpeed : style.flameSpeed);

            float plate = size * style.innerRadius * 2f;
            g.number.fontSize = style.numberSize;
            g.number.color = pal.number;
            g.number.rectTransform.sizeDelta = new Vector2(plate * 0.85f, plate * 0.6f);
        }
    
        // A small soft dot of light
        private static Texture2D SparkTexture()
        {
            if (sparkTexture != null) return sparkTexture;
            const int size = 32;
            sparkTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FlameGaugeSpark" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            sparkTexture.SetPixels32(pixels);
            sparkTexture.Apply();
            return sparkTexture;
        }
    }
}
