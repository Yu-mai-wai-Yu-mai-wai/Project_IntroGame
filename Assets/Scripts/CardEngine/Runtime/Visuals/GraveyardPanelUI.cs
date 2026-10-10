using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TawanOS.CardEngine
{
    // Graveyard screen: click the graveyard (GraveyardView3D) to dim the table and list every card in the
    // player's and the enemy's graveyard together, each marked with whose it is. Click a card for its
    // detail screen (CardDetailPanelUI). Esc, right-click, the close button or the dimmed edge closes it.
    // Built in code on first use (its own overlay canvas), so no scene setup is needed.
    public class GraveyardPanelUI : MonoBehaviour
    {
        public static GraveyardPanelUI Instance { get; private set; }

        // Open (or closed this frame): clicks should not also pick up or play cards
        public static bool BlocksInput => Instance != null && (Instance.isOpen || Time.frameCount <= Instance.closedFrame);
        public static bool IsOpen => Instance != null && Instance.isOpen;

        private const float CardAspect = 939f / 1312f;
        private const float ThumbHeight = 250f;
        private const float TagHeight = 34f;
        private const float FadeSpeed = 8f;

        private static readonly Color PlayerTagColor = new Color(0.3f, 0.55f, 0.85f);
        private static readonly Color EnemyTagColor = new Color(0.8f, 0.22f, 0.22f);

        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform content;
        private ScrollRect scroll;
        private TextMeshProUGUI titleText, emptyText;
        private TMP_FontAsset font;
        private Sprite defaultWhiteFrame, defaultBlackFrame;

        private readonly List<GameObject> entries = new List<GameObject>();
        private int shownPlayerCount = -1, shownEnemyCount = -1;

        private bool isOpen;
        private int openedFrame = -1;
        private int closedFrame = -1;
        private float targetAlpha;

        // The mouse press began while the card detail screen was up: that click only closes the detail
        // screen, so its release must not also click a card or close this screen
        private bool pressClosedDetail;

        public static GraveyardPanelUI Ensure()
        {
            if (Instance == null) new GameObject("GraveyardPanelUI").AddComponent<GraveyardPanelUI>();
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

        public void Show()
        {
            FindCardLook();
            Build();
            EnsureEventSystem();
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
                && (Input.GetMouseButtonDown(1) || TawanOS.UI.EscapeKey.Use()))
            {
                Hide();
            }

            // Cards can die while the screen is up (e.g. during the enemy's turn)
            if (isOpen && (PlayerPile().Count != shownPlayerCount || EnemyPile().Count != shownEnemyCount)) Refresh();

            group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, FadeSpeed * Time.unscaledDeltaTime);
            if (!isOpen && group.alpha <= 0f) canvas.gameObject.SetActive(false);
        }

        private bool ClickAllowed => isOpen && !pressClosedDetail && !CardDetailPanelUI.BlocksInput;

        // ---------------------------------------------------------------- content

        private static readonly List<CardInstance> Empty = new List<CardInstance>();

        private static IReadOnlyList<CardInstance> PlayerPile()
        {
            return CardManager.Instance != null ? CardManager.Instance.DiscardPile : Empty;
        }

        private static IReadOnlyList<CardInstance> EnemyPile()
        {
            var enemy = CombatManager.Instance != null ? CombatManager.Instance.EnemyCards : null;
            return enemy != null ? enemy.DiscardPile : Empty;
        }

        private void Refresh()
        {
            foreach (var e in entries) Destroy(e);
            entries.Clear();

            var player = PlayerPile();
            var enemy = EnemyPile();
            shownPlayerCount = player.Count;
            shownEnemyCount = enemy.Count;

            foreach (var card in player) entries.Add(MakeEntry(card, isPlayer: true));
            foreach (var card in enemy) entries.Add(MakeEntry(card, isPlayer: false));

            titleText.text = $"หลุมศพ  <size=28><color=#{ColorUtility.ToHtmlStringRGB(PlayerTagColor)}>ผู้เล่น {player.Count}</color>"
                + $"   <color=#{ColorUtility.ToHtmlStringRGB(EnemyTagColor)}>ศัตรู {enemy.Count}</color></size>";
            emptyText.gameObject.SetActive(entries.Count == 0);
        }

        private GameObject MakeEntry(CardInstance card, bool isPlayer)
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

            // The card
            var face = NewRect("Card", root);
            SetRect(face, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, TagHeight + 6f), Vector2.zero);

            // A finished card PNG already shows the name and cost
            bool printed = card.cardImage != null;
            Sprite frame = CardFaceLayout.FaceFrame(card.cardImage, card.cardBackground, card.artwork,
                defaultWhiteFrame, defaultBlackFrame, card.magicSchool);
            var frameImage = NewImage("Frame", face, frame != null ? Color.white : SchoolColor(card.magicSchool));
            frameImage.sprite = frame;
            frameImage.enabled = frame != null || card.artwork == null; // the framed artwork needs nothing over it
            Stretch(frameImage.rectTransform);

            if (!printed)
            {
                if (card.artwork != null)
                {
                    var art = NewImage("Artwork", face, Color.white);
                    art.sprite = card.artwork;
                    art.preserveAspect = false;
                    PlaceOnFace(art.rectTransform, CardFaceLayout.Artwork);
                    art.transform.SetAsFirstSibling(); // behind the see-through frame
                }

                var cost = FaceText("Cost", face, CardFaceLayout.Cost, 22);
                cost.text = card.magicSchool == MagicSchool.WhiteMagic ? card.meritCost.ToString() : card.corruptionGain.ToString();
                if (!CardFaceLayout.ArtCarriesFrame(card.artwork)) // the framed artwork has the name printed on it
                    FaceText("Name", face, CardFaceLayout.Name, 18).text = card.cardNameThai;

                var ability = FaceText("Description", face, CardFaceLayout.Description, 11);
                ability.textWrappingMode = TextWrappingModes.Normal;
                ability.fontSizeMin = 5f;
                string format = card.descriptionFormat ?? "";
                try { ability.text = string.Format(format, card.baseValue); } catch { ability.text = format; }
            }

            // Whose card it was
            var tag = NewImage("Owner", root, isPlayer ? PlayerTagColor : EnemyTagColor);
            SetRect(tag.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, TagHeight));
            var tagText = NewText("Text", tag.transform, 22, TextAlignmentOptions.Center);
            Stretch(tagText.rectTransform);
            tagText.text = isPlayer ? "ผู้เล่น" : "ศัตรู";

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
            CardFaceLayout.MakeReadable(t);
            return t;
        }

        // The Thai font and the default frames come from the cards on the table
        private void FindCardLook()
        {
            if (font != null && defaultWhiteFrame != null && defaultBlackFrame != null) return;
            foreach (var view in FindObjectsByType<CardView3D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (font == null && view.nameLabel != null) font = view.nameLabel.font;
                if (defaultWhiteFrame == null) defaultWhiteFrame = view.defaultWhiteFrame;
                if (defaultBlackFrame == null) defaultBlackFrame = view.defaultBlackFrame;
            }
        }

        private static Color SchoolColor(MagicSchool school)
        {
            return school == MagicSchool.WhiteMagic ? new Color(0.85f, 0.8f, 0.55f) : new Color(0.35f, 0.1f, 0.15f);
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

            var canvasGo = new GameObject("GraveyardCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450; // under the card detail screen (500)
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            group = canvasGo.GetComponent<CanvasGroup>();

            // Dimmed table: clicking it closes the screen
            var dim = NewImage("Dim", canvasGo.transform, new Color(0f, 0f, 0f, 0.72f));
            dim.raycastTarget = true;
            Stretch(dim.rectTransform);
            dim.gameObject.AddComponent<Button>().onClick.AddListener(() => { if (ClickAllowed) Hide(); });

            // Panel
            var panel = NewImage("Panel", canvasGo.transform, new Color(0.06f, 0.04f, 0.06f, 0.94f));
            panel.raycastTarget = true; // clicks between cards do not reach the dim
            var panelRt = panel.rectTransform;
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(1560f, 900f);
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.55f, 0.5f, 0.6f);
            outline.effectDistance = new Vector2(3f, -3f);

            titleText = NewText("Title", panelRt, 48, TextAlignmentOptions.Left);
            SetRect(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -90f), new Vector2(-200f, -20f));

            var close = NewImage("Close", panelRt, new Color(1f, 1f, 1f, 0.08f));
            close.raycastTarget = true;
            SetRect(close.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-170f, -80f), new Vector2(-30f, -30f));
            close.gameObject.AddComponent<Button>().onClick.AddListener(() => { if (ClickAllowed) Hide(); });
            var closeText = NewText("Text", close.transform, 26, TextAlignmentOptions.Center);
            Stretch(closeText.rectTransform);
            closeText.text = "ปิด (Esc)";

            var line = NewImage("Divider", panelRt, new Color(1f, 1f, 1f, 0.15f));
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
            grid.cellSize = new Vector2(ThumbHeight * CardAspect, ThumbHeight + TagHeight + 6f);
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
            emptyText.color = new Color(0.6f, 0.56f, 0.56f);
            emptyText.text = "ยังไม่มีการ์ดในหลุมศพ";

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
            t.color = Color.white;
            t.richText = true;
            t.raycastTarget = false;
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
