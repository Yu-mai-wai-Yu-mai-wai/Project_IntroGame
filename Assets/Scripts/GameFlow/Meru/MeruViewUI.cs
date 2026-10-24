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
    /// เมรุ screen (Game Designer, 24 Oct). The camera faces the furnace door; clicking the door
    /// (<see cref="MeruStageView"/>) slides the upgrade panel up from the bottom of the screen over its middle. In it:
    /// click a card to pick it (right-click for details), then อัพเกรด; ย้อนกลับ or Esc slides it back down. After the
    /// upgrade the stage plays the card into the furnace and the upgraded card comes back out and shows here with the
    /// way on. Without a stage the panel is up at once and the result shows straight away.
    /// </summary>
    public class MeruViewUI : MonoBehaviour
    {
        [Serializable]
        public class Column
        {
            public RectTransform root;
            [Tooltip("Dark, a little see-through, with soft edges.")]
            public Image background;
            public CanvasGroup panelGroup;
            public TextMeshProUGUI promptText;
            public TextMeshProUGUI deckText;
            public RectTransform cardGrid;
            public Button confirmButton;
            public TextMeshProUGUI confirmLabel;
            public Button backButton;

            [NonSerialized] public float hidden;  // 0 = up, 1 = below the screen
        }

        [Header("Upgrade panel")]
        public Column upgradeColumn;
        public RewardCardView cardTemplate;
        [Tooltip("Only shown when nothing can be upgraded, so the node never traps the player.")]
        public Button leaveButton;

        [Header("Result")]
        public GameObject resultPanel;
        public TextMeshProUGUI resultText;
        public RewardCardView resultCard;
        public Button continueButton;

        [Header("Look")]
        [Tooltip("Height of the upgrade panel once up, as a fraction of the screen; it sits in the middle.")]
        [Range(0.3f, 1f)] public float panelHeight = 0.8f;
        [Tooltip("Space left at each side of the panel, as a fraction of the screen width.")]
        [Range(0f, 0.3f)] public float panelSideMargin = 0.08f;
        [Tooltip("How dark the whole screen goes behind the panel while it is up (Game Designer, 24 Oct).")]
        [Range(0f, 1f)] public float screenDim = 0.75f;
        [Min(0.05f)] public float slideSeconds = 0.6f;

        // Set by MeruStageView in its Awake: it shuts the door when the panel goes back down
        [NonSerialized] public MeruStageView stage;

        private MeruManager meru;
        private int selected = -1;
        private bool canUpgrade;
        private bool open;
        private bool busy;
        private readonly List<RewardCardView> views = new List<RewardCardView>();
        private CanvasGroup group;
        private RectTransform canvasRect;
        private Vector2 resultCardHome;
        private Image dim; // darkens the whole screen behind the panel, with the panel's slide
        private RewardCardView pickedView;

        /// <summary>The upgrade panel can be brought up (something to upgrade, not up already, nothing playing).</summary>
        public bool CanOpenPanel => meru != null && canUpgrade && !open && !busy;
        public bool PanelOpen => open;
        /// <summary>A เมรุ upgrade panel is up (the run deck pile at the bottom of the screen hides meanwhile).</summary>
        public static bool AnyPanelOpen => current != null && current.open;
        private static MeruViewUI current;

        private void Awake()
        {
            current = this;
            canvasRect = (RectTransform)transform;
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
            if (resultCard != null) resultCardHome = ((RectTransform)resultCard.transform).anchoredPosition;

            // Behind everything on this canvas, so the panel and its buttons sit on a dark screen instead of floating
            var dimGo = new GameObject("ScreenDim", typeof(RectTransform));
            dimGo.transform.SetParent(transform, false);
            dimGo.transform.SetAsFirstSibling();
            var dimRt = (RectTransform)dimGo.transform;
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = dimRt.offsetMax = Vector2.zero;
            dim = dimGo.AddComponent<Image>();
            dim.color = Color.clear;
            dim.raycastTarget = false;

            if (upgradeColumn.confirmButton != null) upgradeColumn.confirmButton.onClick.AddListener(Confirm);
            if (upgradeColumn.backButton != null) upgradeColumn.backButton.onClick.AddListener(Back);
            if (leaveButton != null) leaveButton.onClick.AddListener(() => meru?.Leave());
            if (continueButton != null) continueButton.onClick.AddListener(() => meru?.Leave());
        }

        private void OnDestroy()
        {
            if (current == this) current = null;
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
            if (open && !busy && !PauseMenu.IsPaused && EscapeKey.Use()) Back();
        }

        private void RefreshDeckCount()
        {
            if (upgradeColumn.deckText != null) upgradeColumn.deckText.text = $"สำรับ {RunState.Current.Deck.Count} ใบ";
        }

        public void Show(MeruManager manager)
        {
            meru = manager;
            selected = -1;
            open = busy = false;
            if (resultPanel != null) resultPanel.SetActive(false);

            canUpgrade = !meru.Used && meru.AnyUpgradable();
            FillGrid();
            RefreshSelection();

            upgradeColumn.hidden = 1f;
            SetGroup(upgradeColumn.panelGroup, true);
            ApplyColumn();
            // The player must upgrade a card (Game Designer 10 Oct). Leaving without one is only offered when nothing
            // can be upgraded, so the node never traps the player.
            if (leaveButton != null) leaveButton.gameObject.SetActive(!canUpgrade);
            group.blocksRaycasts = group.interactable = true;

            if (stage == null) OpenPanel();
        }

        /// <summary>The furnace door was clicked: the upgrade panel slides up from the bottom.</summary>
        public void OpenPanel()
        {
            if (!CanOpenPanel) return;
            open = true;
            selected = -1;
            RefreshSelection();
            Slide(0f);
        }

        // The panel slides back down; the stage shuts the door
        private void Back()
        {
            if (!open || busy) return;
            open = false;
            selected = -1;
            RefreshSelection();
            Slide(1f);
            if (stage != null) stage.OnBack();
        }

        private void FillGrid()
        {
            if (upgradeColumn.cardGrid == null || cardTemplate == null) return;
            var deck = meru.Deck;
            while (views.Count < deck.Count) views.Add(Instantiate(cardTemplate, upgradeColumn.cardGrid));
            for (int i = 0; i < views.Count; i++)
            {
                var view = views[i];
                bool used = i < deck.Count;
                view.gameObject.SetActive(used);
                if (!used) continue;

                int index = i;
                view.Setup(deck[i], () => Select(index));

                // Cards that cannot be upgraded are shown dimmed and cannot be picked
                bool allowed = meru.CanApply(deck[i]);
                if (view.button != null) view.button.interactable = allowed;
                var cardGroup = view.GetComponent<CanvasGroup>();
                if (cardGroup != null) cardGroup.alpha = allowed ? 1f : 0.35f;
            }
        }

        private void Select(int index)
        {
            selected = index;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            var deck = meru.Deck;
            var card = selected >= 0 && selected < deck.Count ? deck[selected] : null;

            for (int i = 0; i < views.Count; i++)
                if (views[i].pickedHighlight != null) views[i].pickedHighlight.SetActive(card != null && i == selected);

            if (upgradeColumn.promptText != null)
                upgradeColumn.promptText.text = card != null
                    ? $"ชำระ \"{card.cardNameThai}\" เป็น \"{card.upgradedCard.cardNameThai}\"?"
                    : "เลือกการ์ด 1 ใบ ชำระให้แกร่งขึ้น";
            if (upgradeColumn.confirmButton != null) upgradeColumn.confirmButton.interactable = card != null;
        }

        private void Confirm()
        {
            var deck = meru.Deck;
            if (busy || selected < 0 || selected >= deck.Count) return;
            pickedView = views[selected];
            meru.Apply(deck[selected]);
        }

        /// <summary>Called by <see cref="MeruManager"/> once the card has been upgraded.</summary>
        public void ShowResult(CardDataSO card)
        {
            busy = true;
            Slide(1f);
            if (stage != null)
            {
                // The stage carries the picked card into the furnace
                group.blocksRaycasts = group.interactable = false;
                stage.Offer(card, pickedView);
                return;
            }
            ShowUpgraded(card, null);
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

        // ---- Panel layout ----

        private void Slide(float target)
        {
            var column = upgradeColumn;
            DOTween.Kill(column);
            DOTween.To(() => column.hidden, h => { column.hidden = h; ApplyColumn(); }, target, slideSeconds)
                .SetEase(target < column.hidden ? Ease.OutCubic : Ease.InOutCubic).SetId(column).SetLink(gameObject);
        }

        // In the middle of the screen, panelHeight tall, pushed below the screen by `hidden` (Game Designer, 24 Oct)
        private void ApplyColumn()
        {
            var column = upgradeColumn;
            if (column.root == null) return;
            float screenHeight = canvasRect.rect.height > 0f ? canvasRect.rect.height : 1080f;
            float bottom = (1f - panelHeight) * 0.5f, top = (1f + panelHeight) * 0.5f;
            column.root.anchorMin = new Vector2(panelSideMargin, bottom);
            column.root.anchorMax = new Vector2(1f - panelSideMargin, top);
            column.root.anchoredPosition = new Vector2(0f, -(top * screenHeight + 40f) * column.hidden);
            column.root.sizeDelta = Vector2.zero;
            if (dim != null) dim.color = new Color(0f, 0f, 0f, screenDim * (1f - Mathf.Clamp01(column.hidden)));
        }

        private static void SetGroup(CanvasGroup g, bool on)
        {
            if (g == null) return;
            g.DOKill();
            g.interactable = g.blocksRaycasts = on;
            g.alpha = on ? 1f : 0f;
        }
    }
}
