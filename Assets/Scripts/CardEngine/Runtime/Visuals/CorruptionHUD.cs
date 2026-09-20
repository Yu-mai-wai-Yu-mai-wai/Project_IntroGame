using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    // Self-building Corruption (มลทิน) display: a label and a fill bar that turns redder as it nears
    // the backfire threshold. Bootstraps itself in any scene that has a CombatManager, unless a
    // CombatHUD already has its own corruption UI assigned. Sits just above the MeritHUD panel.
    public class CorruptionHUD : MonoBehaviour
    {
        private static readonly Color BarLow = new Color(0.55f, 0.30f, 0.75f);
        private static readonly Color BarHigh = new Color(0.90f, 0.15f, 0.20f);

        private RectTransform root;
        private Image fill;
        private TMP_Text label;
        private CombatManager combat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<CombatManager>() == null) return;
            if (FindFirstObjectByType<CorruptionHUD>() != null) return;

            foreach (var hud in FindObjectsByType<CombatHUD>(FindObjectsSortMode.None))
            {
                if (hud.corruptionSlider != null || hud.corruptionText != null) return;
            }

            new GameObject("CorruptionHUD").AddComponent<CorruptionHUD>();
        }

        private void Start()
        {
            BuildUI();

            combat = CombatManager.Instance;
            if (combat == null) return;

            combat.OnCorruptionChanged += HandleCorruptionChanged;
            combat.OnCurseBackfireTriggered += HandleBackfire;
            Refresh(combat.CurrentCorruption, combat.State.corruptionThreshold, animate: false);
        }

        private void OnDestroy()
        {
            if (combat == null) return;
            combat.OnCorruptionChanged -= HandleCorruptionChanged;
            combat.OnCurseBackfireTriggered -= HandleBackfire;
        }

        private void HandleCorruptionChanged(int current, int max)
        {
            Refresh(current, max, animate: true);
        }

        private void HandleBackfire()
        {
            root.DOKill();
            root.localScale = Vector3.one;
            root.DOShakeAnchorPos(0.5f, 20f, 25);
        }

        private void Refresh(int current, int max, bool animate)
        {
            float t = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            fill.color = Color.Lerp(BarLow, BarHigh, t);
            label.text = $"มลทิน {current} / {max}";

            var fillRt = fill.rectTransform;
            fillRt.DOKill();
            var target = new Vector2(t, 1f);
            if (animate) fillRt.DOAnchorMax(target, 0.25f);
            else fillRt.anchorMax = target;

            if (animate && current > 0)
            {
                root.DOKill();
                root.localScale = Vector3.one;
                root.DOPunchScale(Vector3.one * 0.12f, 0.25f, 6);
            }
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("CorruptionCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var rootGo = new GameObject("CorruptionPanel", typeof(RectTransform));
            rootGo.transform.SetParent(canvasGo.transform, false);
            root = rootGo.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot = Vector2.zero;
            root.anchoredPosition = new Vector2(40, 140);
            root.sizeDelta = new Vector2(360, 90);

            var bg = rootGo.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.04f, 0.03f, 0.75f);
            bg.raycastTarget = false;

            var labelGo = new GameObject("CorruptionLabel", typeof(RectTransform));
            labelGo.transform.SetParent(rootGo.transform, false);
            label = labelGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 30;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.75f, 0.55f, 0.95f);
            label.alignment = TextAlignmentOptions.Left;
            label.raycastTarget = false;
            var labelRt = label.rectTransform;
            labelRt.anchorMin = new Vector2(0f, 0.5f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.offsetMin = new Vector2(16, 0);
            labelRt.offsetMax = new Vector2(-16, -4);

            var trackGo = new GameObject("BarTrack", typeof(RectTransform));
            trackGo.transform.SetParent(rootGo.transform, false);
            var trackRt = (RectTransform)trackGo.transform;
            trackRt.anchorMin = new Vector2(0f, 0f);
            trackRt.anchorMax = new Vector2(1f, 0.5f);
            trackRt.offsetMin = new Vector2(16, 14);
            trackRt.offsetMax = new Vector2(-16, -6);
            var track = trackGo.AddComponent<Image>();
            track.color = new Color(0.15f, 0.12f, 0.18f, 0.9f);
            track.raycastTarget = false;

            var fillGo = new GameObject("BarFill", typeof(RectTransform));
            fillGo.transform.SetParent(trackGo.transform, false);
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
            fill = fillGo.AddComponent<Image>();
            fill.raycastTarget = false;
        }
    }
}
