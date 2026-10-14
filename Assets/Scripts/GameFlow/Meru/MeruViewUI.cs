using System;
using System.Collections.Generic;
using DG.Tweening;
using TawanOS.CardEngine;
using TawanOS.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// เมรุ screen (Game Designer, 14 Oct). In front of the furnace door two narrow black strips stand at the screen's
    /// sides, เผาการ์ด on the left and อัพเกรดการ์ด on the right. Clicking one widens it into a column holding the whole
    /// run deck while the other strip slides away (and <see cref="MeruStageView"/> opens the door and moves the camera
    /// sideways so the door sits beside the column). In the column: click a card to pick it (right-click for details),
    /// then the column's button to burn or upgrade it; ย้อนกลับ or Esc goes back to the two strips. The black has a
    /// smoky edge on its side toward the furnace. After the one action of the visit the stage plays the card into the
    /// furnace; an upgraded card comes back out and shows here with the way on.
    /// Without a stage the strips show at once and the result shows straight away.
    /// </summary>
    public class MeruViewUI : MonoBehaviour
    {
        [Serializable]
        public class Column
        {
            public RectTransform root;
            [Tooltip("Solid black, drawn with a smoky edge on the side facing the furnace.")]
            public Image background;

            [Header("Strip (before choosing)")]
            [Tooltip("What shows while the column is a narrow strip; the whole strip is its button.")]
            public CanvasGroup stripGroup;
            public Button chooseButton;
            public TextMeshProUGUI stripTitle;
            public TextMeshProUGUI stripHint;

            [Header("Column (after choosing)")]
            public CanvasGroup panelGroup;
            public TextMeshProUGUI promptText;
            public TextMeshProUGUI deckText;
            public RectTransform cardGrid;
            public Button confirmButton;
            public TextMeshProUGUI confirmLabel;
            public Button backButton;

            [NonSerialized] public float width;   // fraction of the screen
            [NonSerialized] public float hidden;  // 0 = on screen, 1 = slid off its side
            [NonSerialized] public Material smoke;
        }

        [Header("Columns")]
        public Column burnColumn;
        public Column upgradeColumn;
        public RewardCardView cardTemplate;
        [Tooltip("Only shown when nothing can be burnt or upgraded, so the node never traps the player.")]
        public Button leaveButton;

        [Header("Result")]
        public GameObject resultPanel;
        public TextMeshProUGUI resultText;
        public RewardCardView resultCard;
        public Button continueButton;

        [Header("Look")]
        [Tooltip("Width of a strip before choosing, as a fraction of the screen: a third, so burn, furnace and upgrade share it evenly (Game Designer, 14 Oct).")]
        [Range(0.05f, 0.5f)] public float stripWidth = 1f / 3f;
        [Tooltip("Width of the chosen column, as a fraction of the screen; the door sits in the rest.")]
        [Range(0.2f, 0.8f)] public float columnWidth = 0.58f;
        [Min(0.05f)] public float slideSeconds = 0.6f;
        [Tooltip("Width of the smoky edge, in reference pixels (1920 wide screen).")]
        public float smokePixels = 110f;
        public float smokeSpeed = 1f;
        public float smokeScale = 4f;
        [Tooltip("A strip's title brightens to this while the pointer is on it.")]
        public Color stripHover = new Color(1f, 0.9f, 0.6f);
        [Tooltip("How much wider a strip grows under the pointer, as a fraction of the screen.")]
        [Range(0f, 0.2f)] public float stripHoverGrow = 0.05f;
        [Tooltip("How far the other strip draws back toward its edge meanwhile, as a fraction of its own width.")]
        [Range(0f, 1f)] public float stripHoverPush = 0.15f;
        [Min(0.05f)] public float stripHoverSeconds = 1f;

        // Set by MeruStageView in its Awake: the strips wait until the camera is inside
        [NonSerialized] public MeruStageView stage;

        private MeruManager meru;
        private MeruAction selectedAction;
        private int selected = -1;
        private bool canBurn, canUpgrade;
        private bool chosen;
        private MeruAction chosenAction;
        private bool busy;
        private readonly List<RewardCardView> burnViews = new List<RewardCardView>();
        private readonly List<RewardCardView> upgradeViews = new List<RewardCardView>();
        private CanvasGroup group;
        private RectTransform canvasRect;
        private Color stripTitleColor;
        private Vector2 resultCardHome;

        private void Awake()
        {
            canvasRect = (RectTransform)transform;
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
            if (resultCard != null) resultCardHome = ((RectTransform)resultCard.transform).anchoredPosition;

            Wire(burnColumn, MeruAction.Burn);
            Wire(upgradeColumn, MeruAction.Upgrade);
            if (leaveButton != null) leaveButton.onClick.AddListener(() => meru?.Leave());
            if (continueButton != null) continueButton.onClick.AddListener(() => meru?.Leave());
        }

        private void Wire(Column column, MeruAction action)
        {
            if (column.chooseButton != null)
            {
                column.chooseButton.onClick.AddListener(() => Choose(action));
                var hover = column.chooseButton.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                AddTrigger(hover, UnityEngine.EventSystems.EventTriggerType.PointerEnter, () => HoverStrip(column, true));
                AddTrigger(hover, UnityEngine.EventSystems.EventTriggerType.PointerExit, () => HoverStrip(column, false));
            }
            if (column.confirmButton != null) column.confirmButton.onClick.AddListener(() => Confirm(action));
            if (column.backButton != null) column.backButton.onClick.AddListener(Back);
            if (column.stripTitle != null) stripTitleColor = column.stripTitle.color;
            column.smoke = SmokeMaterial(column.background);
        }

        private static void AddTrigger(UnityEngine.EventSystems.EventTrigger trigger, UnityEngine.EventSystems.EventTriggerType type, Action action)
        {
            var entry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private void OnEnable()
        {
            RunState.Current.OnChanged += RefreshDeckCount;
            RefreshDeckCount();
        }

        private void OnDisable()
        {
            RunState.Current.OnChanged -= RefreshDeckCount;
        }

        private void Update()
        {
            if (chosen && !busy && !PauseMenu.IsPaused && EscapeKey.Use()) Back();
        }

        private void RefreshDeckCount()
        {
            string text = $"สำรับ {RunState.Current.Deck.Count} ใบ";
            if (burnColumn.deckText != null) burnColumn.deckText.text = text;
            if (upgradeColumn.deckText != null) upgradeColumn.deckText.text = text;
        }

        public void Show(MeruManager manager)
        {
            meru = manager;
            selected = -1;
            chosen = busy = false;
            if (resultPanel != null) resultPanel.SetActive(false);

            canBurn = meru.Deck.Count > 1;
            canUpgrade = meru.AnyUpgradable();
            FillColumn(burnColumn, burnViews, MeruAction.Burn);
            FillColumn(upgradeColumn, upgradeViews, MeruAction.Upgrade);
            RefreshSelection();

            foreach (var column in new[] { burnColumn, upgradeColumn })
            {
                column.width = stripWidth;
                column.hidden = 1f;
                SetGroup(column.stripGroup, true, instant: true);
                SetGroup(column.panelGroup, false, instant: true);
                ApplyColumn(column);
            }
            SetStrip(burnColumn, canBurn, "นำการ์ด 1 ใบออกจากสำรับไปตลอดกาล", "สำรับเหลือใบเดียว เผาไม่ได้");
            SetStrip(upgradeColumn, canUpgrade, "ชำระการ์ด 1 ใบในไฟให้แกร่งขึ้น", "ไม่มีการ์ดที่อัพเกรดได้");
            if (leaveButton != null) leaveButton.gameObject.SetActive(false);
            group.blocksRaycasts = group.interactable = true;

            if (stage == null) ShowChoices();
        }

        private static void SetStrip(Column column, bool can, string hint, string cannot)
        {
            if (column.stripHint != null) column.stripHint.text = can ? hint : cannot;
            if (column.chooseButton != null) column.chooseButton.interactable = can;
            if (column.stripGroup != null) column.stripGroup.alpha = can ? 1f : 0.5f;
        }

        /// <summary>The two strips slide in from the sides (the camera is inside, facing the door).</summary>
        public void ShowChoices()
        {
            SlideColumn(burnColumn, 0f);
            SlideColumn(upgradeColumn, 0f);
            // The player must burn or upgrade a card (Game Designer 10 Oct). Leaving without one is only offered
            // when neither is possible (one card left and nothing to upgrade), so the node never traps the player.
            if (leaveButton != null) leaveButton.gameObject.SetActive(!canBurn && !canUpgrade);
        }

        // The chosen strip widens into a column of cards, the other slides away; the stage opens the door
        private void Choose(MeruAction action)
        {
            if (chosen || busy) return;
            chosen = true;
            chosenAction = action;
            selected = -1;
            RefreshSelection();
            var (mine, other) = action == MeruAction.Burn ? (burnColumn, upgradeColumn) : (upgradeColumn, burnColumn);
            HoverStrip(mine, false);
            WidenColumn(mine, columnWidth);
            SetGroup(mine.stripGroup, false, instant: false);
            SetGroup(mine.panelGroup, true, instant: false);
            SlideColumn(other, 1f);
            if (leaveButton != null) leaveButton.gameObject.SetActive(false);
            if (stage != null) stage.OnChoose(action, columnWidth);
        }

        private void Back()
        {
            if (!chosen || busy) return;
            chosen = false;
            selected = -1;
            RefreshSelection();
            var (mine, other) = chosenAction == MeruAction.Burn ? (burnColumn, upgradeColumn) : (upgradeColumn, burnColumn);
            WidenColumn(mine, stripWidth);
            SetGroup(mine.panelGroup, false, instant: false);
            SetGroup(mine.stripGroup, true, instant: false);
            SlideColumn(other, 0f);
            if (stage != null) stage.OnBack();
        }

        // Under the pointer a strip leans in: it widens a little and the other strip draws back toward its edge;
        // both settle back when the pointer leaves (Game Designer, 14 Oct)
        private void HoverStrip(Column column, bool on)
        {
            bool lit = on && column.chooseButton != null && column.chooseButton.interactable && !chosen && !busy;
            if (column.stripTitle != null)
            {
                column.stripTitle.DOKill();
                column.stripTitle.DOColor(lit ? stripHover : stripTitleColor, 0.2f).SetLink(column.stripTitle.gameObject);
                column.stripTitle.transform.DOScale(lit ? 1.08f : 1f, 0.2f).SetLink(column.stripTitle.gameObject);
            }
            if (chosen || busy) return;
            var other = column == burnColumn ? upgradeColumn : burnColumn;
            WidenColumn(column, lit ? stripWidth + stripHoverGrow : stripWidth, stripHoverSeconds);
            SlideColumn(other, lit ? stripHoverPush : 0f, stripHoverSeconds);
        }

        private void FillColumn(Column column, List<RewardCardView> views, MeruAction action)
        {
            if (column.cardGrid == null || cardTemplate == null) return;
            var deck = meru.Deck;
            while (views.Count < deck.Count) views.Add(Instantiate(cardTemplate, column.cardGrid));
            for (int i = 0; i < views.Count; i++)
            {
                var view = views[i];
                bool used = i < deck.Count;
                view.gameObject.SetActive(used);
                if (!used) continue;

                int index = i;
                view.Setup(deck[i], () => Select(action, index));

                // Cards this action cannot take are shown dimmed and cannot be picked
                bool allowed = meru.CanApply(action, deck[i]);
                if (view.button != null) view.button.interactable = allowed;
                var cardGroup = view.GetComponent<CanvasGroup>();
                if (cardGroup != null) cardGroup.alpha = allowed ? 1f : 0.35f;
            }
        }

        private void Select(MeruAction action, int index)
        {
            selectedAction = action;
            selected = index;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            var deck = meru.Deck;
            var card = selected >= 0 && selected < deck.Count ? deck[selected] : null;
            bool burnPicked = card != null && selectedAction == MeruAction.Burn;
            bool upgradePicked = card != null && selectedAction == MeruAction.Upgrade;

            for (int i = 0; i < burnViews.Count; i++)
                if (burnViews[i].pickedHighlight != null) burnViews[i].pickedHighlight.SetActive(burnPicked && i == selected);
            for (int i = 0; i < upgradeViews.Count; i++)
                if (upgradeViews[i].pickedHighlight != null) upgradeViews[i].pickedHighlight.SetActive(upgradePicked && i == selected);

            if (burnColumn.promptText != null)
                burnColumn.promptText.text = burnPicked ? $"เผา \"{card.cardNameThai}\" ทิ้งจากสำรับ?" : "เลือกการ์ด 1 ใบ เผาออกจากสำรับไปตลอดกาล";
            if (upgradeColumn.promptText != null)
                upgradeColumn.promptText.text = upgradePicked
                    ? $"ชำระ \"{card.cardNameThai}\" เป็น \"{card.upgradedCard.cardNameThai}\"?"
                    : "เลือกการ์ด 1 ใบ ชำระให้แกร่งขึ้น";

            if (burnColumn.confirmButton != null) burnColumn.confirmButton.interactable = burnPicked;
            if (upgradeColumn.confirmButton != null) upgradeColumn.confirmButton.interactable = upgradePicked;
        }

        private RewardCardView pickedView;

        private void Confirm(MeruAction action)
        {
            var deck = meru.Deck;
            if (busy || action != selectedAction || selected < 0 || selected >= deck.Count) return;
            pickedView = (action == MeruAction.Burn ? burnViews : upgradeViews)[selected];
            meru.Apply(action, deck[selected]);
        }

        /// <summary>Called by <see cref="MeruManager"/> once the card has been burnt or upgraded.</summary>
        public void ShowResult(MeruAction doneAction, CardDataSO card)
        {
            busy = true;
            var column = doneAction == MeruAction.Burn ? burnColumn : upgradeColumn;
            if (stage != null)
            {
                // The column slides away and the stage carries the picked card into the furnace
                SlideColumn(column, 1f);
                group.blocksRaycasts = group.interactable = false;
                stage.Offer(doneAction, card, pickedView);
                return;
            }

            // No stage: the result straight away
            SlideColumn(column, 1f);
            if (doneAction == MeruAction.Burn) meru.Leave();
            else ShowUpgraded(card, null);
        }

        /// <summary>
        /// The upgraded card shows in the middle of the screen with the way on. With a screen point it first flies
        /// out from there (the furnace door) and grows to full size.
        /// </summary>
        public void ShowUpgraded(CardDataSO card, Vector2? fromScreen)
        {
            group.blocksRaycasts = group.interactable = true;
            if (resultPanel == null) return;
            resultPanel.SetActive(true);
            var shown = card.upgradedCard;
            if (resultText != null)
            {
                resultText.text = $"\"{card.cardNameThai}\" ผ่านไฟชำระ กลายเป็น \"{shown.cardNameThai}\"";
                resultText.alpha = 0f;
                resultText.DOFade(1f, 0.5f).SetDelay(0.6f).SetLink(resultText.gameObject);
            }
            if (continueButton != null)
            {
                continueButton.interactable = false;
                DOVirtual.DelayedCall(0.9f, () => continueButton.interactable = true).SetLink(gameObject);
            }
            if (resultCard == null) return;

            resultCard.gameObject.SetActive(true);
            resultCard.Setup(shown, null);
            if (resultCard.button != null) resultCard.button.interactable = false;
            var rt = (RectTransform)resultCard.transform;
            rt.DOKill();
            rt.anchoredPosition = resultCardHome;
            rt.localScale = Vector3.one;
            if (fromScreen == null) return;

            var home = rt.position;
            rt.position = new Vector3(fromScreen.Value.x, fromScreen.Value.y, home.z);
            rt.localScale = Vector3.one * 0.12f;
            DOTween.Sequence().SetLink(rt.gameObject)
                .Append(rt.DOMove(home, 0.8f).SetEase(Ease.OutCubic))
                .Join(rt.DOScale(1f, 0.8f).SetEase(Ease.OutBack));
        }

        // ---- Column layout ----

        private void WidenColumn(Column column, float target, float seconds = -1f)
        {
            DOTween.Kill(column);
            DOTween.To(() => column.width, w => { column.width = w; ApplyColumn(column); }, target, seconds > 0f ? seconds : slideSeconds)
                .SetEase(Ease.OutCubic).SetId(column).SetLink(gameObject);
        }

        private void SlideColumn(Column column, float target, float seconds = -1f)
        {
            DOTween.Kill(column.root);
            DOTween.To(() => column.hidden, h => { column.hidden = h; ApplyColumn(column); }, target, seconds > 0f ? seconds : slideSeconds)
                .SetEase(target < column.hidden ? Ease.OutCubic : Ease.InOutCubic).SetId(column.root).SetLink(gameObject);
        }

        // Anchors from the column's width (the burn column grows rightward from the left edge, the upgrade column
        // leftward from the right edge), pushed off its side by `hidden`; the smoky edge keeps its width in pixels
        private void ApplyColumn(Column column)
        {
            if (column.root == null) return;
            bool left = column == burnColumn;
            column.root.anchorMin = new Vector2(left ? 0f : 1f - column.width, 0f);
            column.root.anchorMax = new Vector2(left ? column.width : 1f, 1f);
            float screenWidth = canvasRect.rect.width > 0f ? canvasRect.rect.width : 1920f;
            float px = column.width * screenWidth;
            column.root.anchoredPosition = new Vector2((left ? -1f : 1f) * px * column.hidden, 0f);
            column.root.sizeDelta = Vector2.zero;

            if (column.smoke != null)
            {
                float height = canvasRect.rect.height > 0f ? canvasRect.rect.height : 1080f;
                column.smoke.SetFloat("_Edge", Mathf.Clamp(smokePixels / Mathf.Max(px, 1f), 0.01f, 0.6f));
                column.smoke.SetFloat("_Aspect", px / height);
            }
        }

        private static void SetGroup(CanvasGroup g, bool on, bool instant)
        {
            if (g == null) return;
            g.DOKill();
            g.interactable = g.blocksRaycasts = on;
            if (instant) g.alpha = on ? 1f : 0f;
            else g.DOFade(on ? 1f : 0f, 0.3f).SetDelay(on ? 0.2f : 0f).SetLink(g.gameObject);
        }

        // The black column's edge toward the furnace is rolling smoke. The shader draws the smoke on the quad's left
        // side, so the burn column's background is mirrored (scale x = -1) to put it on the right.
        private Material SmokeMaterial(Image background)
        {
            if (background == null) return null;
            var shader = Resources.Load<Shader>("TawanOS_UISmokeEdge");
            if (shader == null) return null;
            var material = new Material(shader);
            material.SetFloat("_Speed", smokeSpeed);
            material.SetFloat("_Scale", smokeScale);
            background.material = material;
            return material;
        }
    }
}
