using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using TawanOS.Settings;

namespace TawanOS.CardEngine
{
    // กุศล and มลทิน as two flame rings stacked on the left of the screen (Game Designer, 13 Oct), replacing the old
    // bottom-left Merit and Corruption panels. Each gauge is drawn by one shader (Resources/TawanOS_UIFlameGauge),
    // after the drawn flame ring the Game Designer sent: a ring the colour of the fire, live flames burning out from
    // behind it, and a solid black plate with "current/max" on it. Sizes, places, colours and fire speed come from
    // FlameGaugeStyleSO (Resources/FlameGaugeStyle), which redraws the gauges when edited during play.
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
            Refresh(merit, combat.CurrentMerit, combat.State.maxMerit, animate: false);
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

        private void HandleBackfire()
        {
            if (corruption == null) return;
            var root = corruption.root;
            root.DOComplete();
            if (!GameSettings.ReduceMotion) root.DOShakeAnchorPos(0.5f, 20f, 25);
        }

        private void Refresh(Gauge g, int current, int max, bool animate)
        {
            if (g == null) return;
            g.number.text = $"{current}/{max}";
            if (!animate) return;
            g.root.DOComplete();
            g.root.localScale = Vector3.one;
            g.root.DOPunchScale(Vector3.one * 0.08f, 0.3f, 6);
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("FlameGaugeCanvas");
            canvasGo.transform.SetParent(transform, false);
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
            var numRt = g.number.rectTransform;
            numRt.anchorMin = numRt.anchorMax = new Vector2(0.5f, 0.5f);
            numRt.anchoredPosition = Vector2.zero;

            return g;
        }

        // Lays out and colours both gauges from the style asset; runs at start and whenever the asset is edited
        private void ApplyStyle()
        {
            if (style == null || merit == null || corruption == null) return;
            float upper = style.upperY, lower = style.lowerY;
            Apply(merit, style.merit, style.meritOnTop ? upper : lower);
            Apply(corruption, style.corruption, style.meritOnTop ? lower : upper);
        }

        private void Apply(Gauge g, FlameGaugeStyleSO.Palette pal, float y)
        {
            float size = style.size;
            g.root.sizeDelta = Vector2.one * size;
            g.root.anchoredPosition = new Vector2(style.leftMargin + size * 0.5f, y);
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
    }
}
