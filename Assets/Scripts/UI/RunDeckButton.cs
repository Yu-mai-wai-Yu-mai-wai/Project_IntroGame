using System.Collections.Generic;
using TawanOS.CardEngine;
using TawanOS.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.UI
{
    /// <summary>
    /// A "สำรับ (N)" button in the top-left corner of the screens of a run outside a fight, except the map (which has its own, in its
    /// status bar): หมอกดำ, ศาลตายาย, the temple, เมรุ, กองของเซ่น and the reward screen. It opens the
    /// same deck screen as the map's (DeckViewerPanelUI). Created by itself at start and kept across scenes; it hides
    /// while the deck screen, the pause menu or the settings are open.
    /// </summary>
    public class RunDeckButton : MonoBehaviour
    {
        // Scenes it shows in, and where (from the top-left corner, in 1920x1080 units). หมอกดำ has Khwan and incense
        // in that corner, so there it sits at the end of their row, as on the map.
        private static readonly Dictionary<string, Vector2> Places = new Dictionary<string, Vector2>
        {
            { "EventScene", new Vector2(440f, 28f) },
            { "ShopScene", new Vector2(24f, 24f) },
            { "TempleScene", new Vector2(24f, 24f) },
            { "MeruScene", new Vector2(24f, 24f) },
            { "OfferingScene", new Vector2(24f, 24f) },
            { "RewardScene", new Vector2(24f, 24f) },
        };

        private static RunDeckButton instance;

        private Canvas canvas;
        private RectTransform buttonRect;
        private TextMeshProUGUI label;
        private bool inRunScene;
        private int shownCount = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("RunDeckButton");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<RunDeckButton>();
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
            inRunScene = Places.TryGetValue(scene.name, out var place);
            if (inRunScene) buttonRect.anchoredPosition = new Vector2(place.x, -place.y);
        }

        private void Update()
        {
            int count = RunState.Current.Deck.Count;
            bool show = inRunScene && count > 0 && !DeckViewerPanelUI.IsOpen && !PauseMenu.IsPaused && !SettingsPanel.IsOpen;
            if (canvas.enabled != show) canvas.enabled = show;
            if (show && count != shownCount)
            {
                shownCount = count;
                label.text = $"สำรับ ({count})";
            }
        }

        private void OnClicked()
        {
            if (PauseMenu.IsPaused) return;
            DeckViewerPanelUI.Ensure().Show("สำรับ", RunState.Current.Deck, "ยังไม่มีการ์ดในสำรับ");
        }

        // Styled like the map's deck button (MapPlayerStatusUI): crimson box, accent text, the smoke on hover
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

            var buttonGo = new GameObject("DeckButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(transform, false);
            buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 1f);
            buttonRect.sizeDelta = new Vector2(186f, 44f);

            var image = buttonGo.GetComponent<Image>();
            image.color = theme != null ? theme.crimson : new Color(0.34f, 0.13f, 0.13f);
            var button = buttonGo.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(OnClicked);
            buttonGo.AddComponent<TawanOS.VFX.UiSmokeButton>();

            label = UiFactory.CreateText(buttonGo.transform, "Label", "สำรับ",
                theme != null ? theme.labelSize + 2f : 26f, theme != null ? theme.accent : new Color(0.94f, 0.68f, 0.48f),
                TextAlignmentOptions.Center, theme != null ? theme.bodyFont : null);
            UiFactory.Stretch(label.rectTransform, 0f);
            label.raycastTarget = false;

            canvas.enabled = false;
        }
    }
}
