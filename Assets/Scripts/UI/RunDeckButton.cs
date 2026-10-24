using System.Collections.Generic;
using TawanOS.CardEngine;
using TawanOS.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.UI
{
    /// <summary>
    /// The run deck as a small pile of face-down cards at the bottom middle of the screen with "สำรับ N ใบ" beside it,
    /// like the pile in ศาลตายาย (Game Designer, 24 Oct: it replaces the "สำรับ (N)" buttons in the top-left corner).
    /// Clicking it opens the deck screen (DeckViewerPanelUI); under the pointer it glows. Shows on the map and the
    /// screens of a run outside a fight: หมอกดำ, the temple, เมรุ, กองของเซ่น and the reward screen. ศาลตายาย has its own
    /// pile (ShopCardShelf, the bought cards fly into it), which opens the same deck screen. Created by itself at start
    /// and kept across scenes; it hides while the deck screen, the pause menu, the settings or the เมรุ's upgrade panel
    /// are open.
    /// </summary>
    public class RunDeckButton : MonoBehaviour
    {
        // Scenes it shows in, and how high its middle sits above the bottom of the screen (1920x1080 units). The เมรุ
        // and กองของเซ่น have a hint band along the bottom, the temple its pray button and Khwan bar: it sits above them
        private static readonly Dictionary<string, float> Places = new Dictionary<string, float>
        {
            { "MapTestScene", 78f },
            { "EventScene", 78f },
            { "TempleScene", 300f },
            { "MeruScene", 190f },
            { "OfferingScene", 190f },
            { "RewardScene", 78f },
        };

        private const float CardAspect = 939f / 1312f;
        private const float CardWidth = 72f;

        private static RunDeckButton instance;

        private Canvas canvas;
        private RectTransform pile;
        private Image glow;
        private TextMeshProUGUI label;
        private bool inRunScene;
        private bool hovered;
        private int shownCount = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("RunDeckButton");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<RunDeckButton>();
        }

        /// <summary>Opens the run deck screen (also used by ศาลตายาย's pile).</summary>
        public static void OpenDeck()
        {
            if (PauseMenu.IsPaused) return;
            DeckViewerPanelUI.Ensure().Show("สำรับ", RunState.Current.Deck, "ยังไม่มีการ์ดในสำรับ");
        }

        private void Awake()
        {
            Build();
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (instance == this) instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive) return;
            inRunScene = Places.TryGetValue(scene.name, out float y);
            if (inRunScene) pile.anchoredPosition = new Vector2(0f, y);
            // The first map screen of a run opens the deck by itself (Game Designer, 25 Oct); the player closes it
            introAt = scene.name == MapSceneName && !RunState.Current.DeckIntroShown ? Time.unscaledTime + IntroDelay : 0f;
        }

        private const string MapSceneName = "MapTestScene";
        private const float IntroDelay = 0.8f; // let the map settle in first
        private float introAt;

        private void ShowIntroWhenFree()
        {
            if (introAt <= 0f || Time.unscaledTime < introAt || RunState.Current.Deck.Count == 0) return;
            if (DeckViewerPanelUI.IsOpen || PauseMenu.IsPaused || SettingsPanel.IsOpen) return;
            introAt = 0f;
            RunState.Current.SetDeckIntroShown();
            DeckViewerPanelUI.Ensure().Show("นี่คือสำรับของคุณ", RunState.Current.Deck, "ยังไม่มีการ์ดในสำรับ");
        }

        private void Update()
        {
            ShowIntroWhenFree();
            int count = RunState.Current.Deck.Count;
            bool show = inRunScene && count > 0 && !DeckViewerPanelUI.IsOpen && !PauseMenu.IsPaused && !SettingsPanel.IsOpen
                        && !MeruViewUI.AnyPanelOpen;
            if (canvas.enabled != show) canvas.enabled = show;
            if (!show) hovered = false;
            if (show && count != shownCount)
            {
                shownCount = count;
                label.text = $"สำรับ {count} ใบ";
            }
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / 0.12f);
            glow.color = Color.Lerp(glow.color, new Color(1f, 0.55f, 0.25f, hovered ? 0.75f : 0f), k);
        }

        private void Build()
        {
            var theme = UIThemeSO.Current;
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 440; // under the deck screen (450) and the card detail screen (500)
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();

            // The click area covers the pile and its label
            var pileGo = new GameObject("DeckPile", typeof(RectTransform), typeof(Image), typeof(Button));
            pileGo.transform.SetParent(transform, false);
            pile = (RectTransform)pileGo.transform;
            pile.anchorMin = pile.anchorMax = new Vector2(0.5f, 0f);
            pile.pivot = new Vector2(0.5f, 0.5f);
            pile.sizeDelta = new Vector2(CardWidth, CardWidth / CardAspect);
            var hit = pileGo.GetComponent<Image>();
            hit.color = Color.clear;
            var button = pileGo.GetComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(OpenDeck);
            var trigger = pileGo.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => hovered = true);
            AddTrigger(trigger, EventTriggerType.PointerExit, () => hovered = false);

            // A warm glow behind the pile, lit under the pointer
            glow = UiFactory.CreateImage("Glow", pile, Color.clear);
            glow.sprite = GlowSprite();
            glow.raycastTarget = false;
            var grt = glow.rectTransform;
            grt.anchorMin = new Vector2(-0.9f, -0.5f);
            grt.anchorMax = new Vector2(1.9f, 1.5f);
            grt.offsetMin = grt.offsetMax = Vector2.zero;

            // Three face-down cards, each a little offset, so it reads as a pile (as in ศาลตายาย)
            Sprite back = RewardFxConfigSO.Load()?.cardBack;
            for (int i = 0; i < 3; i++)
            {
                var card = UiFactory.CreateImage("Card" + i, pile, back != null
                    ? Color.Lerp(new Color(0.55f, 0.5f, 0.5f), Color.white, i / 2f) : new Color(0.15f, 0.1f, 0.1f));
                card.sprite = back;
                card.raycastTarget = false;
                var crt = card.rectTransform;
                crt.anchorMin = Vector2.zero;
                crt.anchorMax = Vector2.one;
                crt.offsetMin = crt.offsetMax = Vector2.zero;
                crt.anchoredPosition = new Vector2(i * 3f, i * 3f);
                crt.localEulerAngles = new Vector3(0f, 0f, (i - 1) * 3f);
            }

            label = UiFactory.CreateText(pile, "Count", "สำรับ", 26f, new Color(0.96f, 0.92f, 0.82f),
                TextAlignmentOptions.MidlineLeft, theme != null ? theme.bodyFont : null);
            label.raycastTarget = true; // the label is part of the click area
            var lrt = label.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(1f, 0.5f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.sizeDelta = new Vector2(170f, 44f);
            lrt.anchoredPosition = new Vector2(16f, 0f);

            canvas.enabled = false;
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private static Sprite glowSprite;

        // A soft round glow, drawn once
        private static Sprite GlowSprite()
        {
            if (glowSprite != null) return glowSprite;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                px[y * size + x] = new Color(1f, 1f, 1f, a * a * a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return glowSprite;
        }
    }
}
