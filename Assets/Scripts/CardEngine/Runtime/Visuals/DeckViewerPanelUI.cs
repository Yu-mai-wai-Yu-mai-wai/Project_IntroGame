using System;
using System.Collections.Generic;
using TawanOS.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TawanOS.CardEngine
{
    // Deck screen: a dimmed overlay listing a set of cards in a scrolling grid, sorted by school, type, cost
    // and name (so the draw pile's order is not given away). Opened by the deck button on the map (the run's
    // deck) and by clicking the draw pile in combat (DeckPileView3D). Click a card for its detail screen
    // (CardDetailPanelUI). Esc, right-click, the close button or the dimmed edge closes it.
    // Built in code on first use (its own overlay canvas), so no scene setup is needed.
    public class DeckViewerPanelUI : MonoBehaviour
    {
        public static DeckViewerPanelUI Instance { get; private set; }

        // Open (or closed this frame): clicks should not also pick up cards or enter map nodes
        public static bool BlocksInput => Instance != null && (Instance.isOpen || Time.frameCount <= Instance.closedFrame);
        public static bool IsOpen => Instance != null && Instance.isOpen;

        private const float CardAspect = 939f / 1312f;
        private const float ThumbHeight = 280f;
        private const float FadeSpeed = 8f;

        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform content;
        private ScrollRect scroll;
        private TextMeshProUGUI titleText, emptyText;
        private TMP_FontAsset font;
        private Sprite defaultWhiteFrame, defaultBlackFrame;

        private readonly List<GameObject> entries = new List<GameObject>();
        private Func<IReadOnlyList<CardInstance>> source;
        private string title;
        private int shownCount = -1;

        private bool isOpen;
        private int openedFrame = -1;
        private int closedFrame = -1;
        private float targetAlpha;

        // The mouse press began while the card detail screen was up: that click only closes the detail
        // screen, so its release must not also click a card or close this screen
        private bool pressClosedDetail;

        public static DeckViewerPanelUI Ensure()
        {
            if (Instance == null) new GameObject("DeckViewerPanelUI").AddComponent<DeckViewerPanelUI>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------- show / hide

        // A fixed list of cards, e.g. the run's deck on the map
        public void Show(string screenTitle, IEnumerable<CardDataSO> cards, string emptyMessage)
        {
            var list = new List<CardInstance>();
            if (cards != null)
                foreach (var card in cards)
                    if (card != null) list.Add(new CardInstance(card));
            Show(screenTitle, () => list, emptyMessage);
        }

        // A pile that can change while the screen is up (e.g. the draw pile in combat): re-read every frame
        public void Show(string screenTitle, Func<IReadOnlyList<CardInstance>> cards, string emptyMessage)
        {
            title = screenTitle;
            source = cards;
            FindCardLook();
            Build();
            EnsureEventSystem();
            emptyText.text = emptyMessage;
            Refresh();
            scroll.verticalNormalizedPosition = 1f;

            isOpen = true;
            openedFrame = Time.frameCount;
            targetAlpha = 1f;
            canvas.gameObject.SetActive(true);
            group.alpha = 0f;
            group.blocksRaycasts = true;
        }

        public void Hide()
        {
            if (!isOpen) return;
            isOpen = false;
            closedFrame = Time.frameCount;
            targetAlpha = 0f;
            group.blocksRaycasts = false;
        }

        private void Update()
        {
            if (canvas == null) return;

            if (Input.GetMouseButtonDown(0)) pressClosedDetail = CardDetailPanelUI.BlocksInput;

            if (isOpen && Time.frameCount > openedFrame && !CardDetailPanelUI.BlocksInput
                && (Input.GetMouseButtonDown(1) || EscapeKey.Use()))
            {
                Hide();
            }

            if (isOpen && Cards().Count != shownCount) Refresh();

            group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, FadeSpeed * Time.unscaledDeltaTime);
            if (!isOpen && group.alpha <= 0f) canvas.gameObject.SetActive(false);
        }

        private bool ClickAllowed => isOpen && !pressClosedDetail && !CardDetailPanelUI.BlocksInput;

        // ---------------------------------------------------------------- content

        private static readonly List<CardInstance> Empty = new List<CardInstance>();

        private IReadOnlyList<CardInstance> Cards()
        {
            return source?.Invoke() ?? Empty;
        }

        private void Refresh()
        {
            foreach (var e in entries) Destroy(e);
            entries.Clear();

            var cards = new List<CardInstance>(Cards());
            shownCount = cards.Count;
            cards.Sort(CompareCards);
            foreach (var card in cards) entries.Add(MakeEntry(card));

            var theme = UIThemeSO.Current;
            titleText.text = $"{title}  <size=32><color=#{ColorUtility.ToHtmlStringRGB(theme.accent)}>{cards.Count} ใบ</color></size>";
            emptyText.gameObject.SetActive(entries.Count == 0);
        }

        private static int CompareCards(CardInstance a, CardInstance b)
        {
            int c = a.magicSchool.CompareTo(b.magicSchool);
            if (c == 0) c = a.cardType.CompareTo(b.cardType);
            if (c == 0) c = Cost(a).CompareTo(Cost(b));
            if (c == 0) c = string.CompareOrdinal(a.cardNameThai, b.cardNameThai);
            return c;
        }

        private static int Cost(CardInstance card)
        {
            return card.magicSchool == MagicSchool.WhiteMagic ? card.meritCost : card.corruptionGain;
        }

        private GameObject MakeEntry(CardInstance card)
        {
            var root = NewRect(card.cardNameThai, content);
            var button = root.gameObject.AddComponent<Button>();
            var hitArea = root.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            button.targetGraphic = hitArea;
            button.onClick.AddListener(() =>
            {
                if (ClickAllowed) CardDetailPanelUI.Ensure().Show(card, defaultWhiteFrame, defaultBlackFrame, font);
            });

            // A finished card PNG already shows the name and cost
            bool printed = card.cardImage != null;
            Sprite frame = printed ? card.cardImage
                : card.cardBackground != null ? card.cardBackground
                : CardFaceLayout.DefaultFrame(defaultWhiteFrame, defaultBlackFrame, card.magicSchool);
            var frameImage = NewImage("Frame", root, frame != null ? Color.white : UIThemeSO.Current.crimson);
            frameImage.sprite = frame;
            Stretch(frameImage.rectTransform);

            if (!printed)
            {
                if (card.artwork != null)
                {
                    var art = NewImage("Artwork", root, Color.white);
                    art.sprite = card.artwork;
                    art.preserveAspect = false;
                    PlaceOnFace(art.rectTransform, CardFaceLayout.Artwork);
                    art.transform.SetAsFirstSibling(); // behind the see-through frame
                }

                var cost = FaceText("Cost", root, CardFaceLayout.Cost, 22);
                cost.text = Cost(card).ToString();
                var nameText = FaceText("Name", root, CardFaceLayout.Name, 18);
                nameText.text = card.cardNameThai;

                // The ability, small, in the description box (the detail panel shows it at reading size)
                var ability = FaceText("Description", root, CardFaceLayout.Description, 11);
                ability.textWrappingMode = TextWrappingModes.Normal;
                ability.fontSizeMin = 5f;
                string format = card.descriptionFormat ?? "";
                try { ability.text = string.Format(format, card.baseValue); } catch { ability.text = format; }
            }

            // Attack / Khwan are never printed on the finished PNG
            if (card.cardType == CardType.Familiar)
            {
                FaceText("Attack", root, CardFaceLayout.Attack, 18).text = card.familiarDamage.ToString();
                FaceText("Khwan", root, CardFaceLayout.Khwan, 18).text = card.familiarHealth.ToString();
            }

            return root.gameObject;
        }

        private TextMeshProUGUI FaceText(string name, RectTransform face, CardFaceLayout.Box box, float maxSize)
        {
            var t = NewText(name, face, maxSize, TextAlignmentOptions.Center);
            PlaceOnFace(t.rectTransform, box);
            t.enableAutoSizing = true;
            t.fontSizeMin = 6f;
            t.fontSizeMax = maxSize;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.color = Color.white; // on the card art, as on the 3D card
            CardFaceLayout.MakeReadable(t);
            return t;
        }

        // The Thai font comes from the UI theme; the default frames from the card catalog (works in any scene),
        // or from the cards on the table when the catalog has none
        private void FindCardLook()
        {
            if (font == null) font = UIThemeSO.Current.bodyFont;
            var catalog = CardCatalogSO.Load();
            if (catalog != null)
            {
                if (defaultWhiteFrame == null) defaultWhiteFrame = catalog.defaultWhiteFrame;
                if (defaultBlackFrame == null) defaultBlackFrame = catalog.defaultBlackFrame;
            }
            if (font != null && defaultWhiteFrame != null && defaultBlackFrame != null) return;
            foreach (var view in FindObjectsByType<CardView3D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (font == null && view.nameLabel != null) font = view.nameLabel.font;
                if (defaultWhiteFrame == null) defaultWhiteFrame = view.defaultWhiteFrame;
                if (defaultBlackFrame == null) defaultBlackFrame = view.defaultBlackFrame;
            }
        }

        // Buttons and scrolling need one; combat scenes normally have it already
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        // ---------------------------------------------------------------- building the UI

        private void Build()
        {
            if (canvas != null) return;
            var theme = UIThemeSO.Current;

            var canvasGo = new GameObject("DeckViewerCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450; // under the card detail screen (500)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            group = canvasGo.GetComponent<CanvasGroup>();

            // Dimmed background: clicking it closes the screen
            var dim = NewImage("Dim", canvasGo.transform, new Color(theme.black.r, theme.black.g, theme.black.b, 0.8f));
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => { if (ClickAllowed) Hide(); });

            // Panel
            var panel = NewImage("Panel", canvasGo.transform, theme.panel);
            panel.raycastTarget = true; // clicks between cards do not reach the dim
            var panelRt = panel.rectTransform;
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(1560f, 900f);
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = theme.crimson;
            outline.effectDistance = new Vector2(3f, -3f);

            titleText = NewText("Title", panelRt, 48, TextAlignmentOptions.Left);
            SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -90f), new Vector2(-200f, -20f));

            var close = NewImage("Close", panelRt, theme.crimson);
            close.raycastTarget = true;
            SetRect(close.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-170f, -80f), new Vector2(-30f, -30f));
            close.gameObject.AddComponent<Button>().onClick.AddListener(() => { if (ClickAllowed) Hide(); });
            var closeText = NewText("Text", close.transform, 26, TextAlignmentOptions.Center);
            Stretch(closeText.rectTransform);
            closeText.text = "ปิด (Esc)";

            var line = NewImage("Divider", panelRt, theme.crimson);
            SetRect(line.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -102f), new Vector2(-40f, -100f));

            // Scrolling grid of cards
            var viewport = NewImage("Viewport", panelRt, Color.clear);
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            SetRect(viewport.rectTransform, Vector2.zero, Vector2.one, new Vector2(40f, 30f), new Vector2(-40f, -120f));

            content = NewRect("Content", viewport.transform);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(ThumbHeight * CardAspect, ThumbHeight);
            grid.spacing = new Vector2(26f, 26f);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.childAlignment = TextAnchor.UpperCenter;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            emptyText = NewText("Empty", viewport.transform, 32, TextAlignmentOptions.Center);
            Stretch(emptyText.rectTransform);

            canvasGo.SetActive(false);
        }

        // Face boxes are fractions of the card (CardFaceLayout: x -0.5..0.5, y 0.5 top .. -0.5 bottom)
        private static void PlaceOnFace(RectTransform rt, CardFaceLayout.Box box)
        {
            Vector2 c = box.center + new Vector2(0.5f, 0.5f);
            rt.anchorMin = c - box.size * 0.5f;
            rt.anchorMax = c + box.size * 0.5f;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var img = NewRect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private TextMeshProUGUI NewText(string name, Transform parent, float size, TextAlignmentOptions align)
        {
            var t = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = UIThemeSO.Current.text;
            t.richText = true;
            t.raycastTarget = false;
            UiFactory.EnableThaiMarks(t);
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
