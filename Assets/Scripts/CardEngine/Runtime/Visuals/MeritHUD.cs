using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    // Self-building Merit (กุศล) display: a label plus one pip per point of max Merit.
    // Bootstraps itself in any scene that has a CombatManager, unless a CombatHUD already
    // has its own meritValueText assigned.
    public class MeritHUD : MonoBehaviour
    {
        private static readonly Color PipFull = new Color(0.98f, 0.82f, 0.25f);
        private static readonly Color PipEmpty = new Color(0.25f, 0.22f, 0.15f, 0.8f);

        private readonly List<Image> pips = new List<Image>();
        private RectTransform root;
        private RectTransform pipRow;
        private TMP_Text label;
        private CombatManager combat;

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
            if (FindFirstObjectByType<MeritHUD>() != null) return;

            foreach (var hud in FindObjectsByType<CombatHUD>(FindObjectsSortMode.None))
            {
                if (hud.meritValueText != null) return;
            }

            new GameObject("MeritHUD").AddComponent<MeritHUD>();
        }

        private void Start()
        {
            BuildUI();

            combat = CombatManager.Instance;
            if (combat == null) return;

            combat.OnMeritChanged += HandleMeritChanged;
            Refresh(combat.CurrentMerit, combat.State.maxMerit, animate: false);
        }

        private void OnDestroy()
        {
            if (combat != null) combat.OnMeritChanged -= HandleMeritChanged;
        }

        private void HandleMeritChanged(int current, int max)
        {
            Refresh(current, max, animate: true);
        }

        private void Refresh(int current, int max, bool animate)
        {
            while (pips.Count < max) pips.Add(CreatePip());
            for (int i = 0; i < pips.Count; i++)
            {
                pips[i].gameObject.SetActive(i < max);
                pips[i].color = i < current ? PipFull : PipEmpty;
            }

            label.text = $"กุศล {current} / {max}";

            if (animate)
            {
                root.DOKill();
                root.localScale = Vector3.one;
                root.DOPunchScale(Vector3.one * 0.12f, 0.25f, 6);
            }
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("MeritCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var rootGo = new GameObject("MeritPanel", typeof(RectTransform));
            rootGo.transform.SetParent(canvasGo.transform, false);
            root = rootGo.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.anchoredPosition = new Vector2(40, 40);
            root.sizeDelta = new Vector2(360, 90);

            var bg = rootGo.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.04f, 0.03f, 0.75f);
            bg.raycastTarget = false;

            var labelGo = new GameObject("MeritLabel", typeof(RectTransform));
            labelGo.transform.SetParent(rootGo.transform, false);
            label = labelGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 30;
            label.fontStyle = FontStyles.Bold;
            label.color = PipFull;
            label.alignment = TextAlignmentOptions.Left;
            label.raycastTarget = false;
            var labelRt = label.rectTransform;
            labelRt.anchorMin = new Vector2(0f, 0.5f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.offsetMin = new Vector2(16, 0);
            labelRt.offsetMax = new Vector2(-16, -4);

            var rowGo = new GameObject("MeritPips", typeof(RectTransform));
            rowGo.transform.SetParent(rootGo.transform, false);
            pipRow = rowGo.GetComponent<RectTransform>();
            pipRow.anchorMin = new Vector2(0f, 0f);
            pipRow.anchorMax = new Vector2(1f, 0.5f);
            pipRow.offsetMin = new Vector2(16, 8);
            pipRow.offsetMax = new Vector2(-16, 0);
            var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
        }

        private Image CreatePip()
        {
            var go = new GameObject("Pip", typeof(RectTransform));
            go.transform.SetParent(pipRow, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(30, 30);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            return img;
        }
    }
}
