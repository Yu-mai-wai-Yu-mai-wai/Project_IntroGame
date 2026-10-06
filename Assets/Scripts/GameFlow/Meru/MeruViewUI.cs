using System.Collections.Generic;
using TawanOS.CardEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// เมรุ screen: a short scene-setting text and the two choices (burn / upgrade), then the run deck as
    /// cards to pick one from (click to select, confirm to apply, right-click for details), then the result.
    /// </summary>
    public class MeruViewUI : MonoBehaviour
    {
        [Header("Intro")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI storyText;
        public TextMeshProUGUI deckText;

        [Header("Choice")]
        public GameObject choicePanel;
        public Button burnButton;
        public Button upgradeButton;
        public TextMeshProUGUI upgradeHintText;
        public Button leaveButton;

        [Header("Card Picker")]
        public GameObject pickerPanel;
        public TextMeshProUGUI pickerTitleText;
        public RectTransform cardGrid;
        public RewardCardView cardTemplate;
        public Button confirmButton;
        public TextMeshProUGUI confirmLabel;
        public Button backButton;

        [Header("Result")]
        public GameObject resultPanel;
        public TextMeshProUGUI resultText;
        public RewardCardView resultCard;
        public Button continueButton;

        private MeruManager meru;
        private MeruAction action;
        private int selected = -1;
        private readonly List<RewardCardView> cardViews = new List<RewardCardView>();

        private void Awake()
        {
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
            if (burnButton != null) burnButton.onClick.AddListener(() => OpenPicker(MeruAction.Burn));
            if (upgradeButton != null) upgradeButton.onClick.AddListener(() => OpenPicker(MeruAction.Upgrade));
            if (leaveButton != null) leaveButton.onClick.AddListener(() => meru?.Leave());
            if (backButton != null) backButton.onClick.AddListener(ShowChoice);
            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
            if (continueButton != null) continueButton.onClick.AddListener(() => meru?.Leave());
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

        private void RefreshDeckCount()
        {
            if (deckText != null) deckText.text = $"สำรับ {RunState.Current.Deck.Count} ใบ";
        }

        public void Show(MeruManager manager)
        {
            meru = manager;
            ShowChoice();
        }

        private void ShowChoice()
        {
            SetPanels(choice: true, picker: false, result: false);
            bool canUpgrade = meru != null && meru.AnyUpgradable();
            if (upgradeButton != null) upgradeButton.interactable = canUpgrade;
            if (upgradeHintText != null) upgradeHintText.text = canUpgrade ? "เปลี่ยนการ์ด 1 ใบเป็นร่างที่แกร่งขึ้น" : "ไม่มีการ์ดที่อัพเกรดได้";
            if (burnButton != null) burnButton.interactable = meru != null && meru.Deck.Count > 1;
        }

        private void OpenPicker(MeruAction pickAction)
        {
            action = pickAction;
            selected = -1;
            SetPanels(choice: false, picker: true, result: false);

            var deck = meru.Deck;
            while (cardViews.Count < deck.Count) cardViews.Add(Instantiate(cardTemplate, cardGrid));
            for (int i = 0; i < cardViews.Count; i++)
            {
                var view = cardViews[i];
                bool used = i < deck.Count;
                view.gameObject.SetActive(used);
                if (!used) continue;

                int index = i;
                view.Setup(deck[i], () => Select(index));

                // Cards this action cannot take are shown dimmed and cannot be picked
                bool allowed = meru.CanApply(action, deck[i]);
                if (view.button != null) view.button.interactable = allowed;
                var group = view.GetComponent<CanvasGroup>();
                if (group != null) group.alpha = allowed ? 1f : 0.35f;
            }
            RefreshSelection();
        }

        private void Select(int index)
        {
            selected = index;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            var deck = meru.Deck;
            for (int i = 0; i < cardViews.Count && i < deck.Count; i++)
            {
                if (cardViews[i].pickedHighlight != null) cardViews[i].pickedHighlight.SetActive(i == selected);
            }

            var card = selected >= 0 && selected < deck.Count ? deck[selected] : null;
            if (pickerTitleText != null)
            {
                pickerTitleText.text = action == MeruAction.Burn
                    ? (card != null ? $"เผา \"{card.cardNameThai}\" ทิ้งจากสำรับ?" : "เลือกการ์ดที่จะเผาทิ้ง")
                    : (card != null ? $"ชำระ \"{card.cardNameThai}\" เป็น \"{card.upgradedCard.cardNameThai}\"?" : "เลือกการ์ดที่จะอัพเกรด");
            }
            if (confirmLabel != null) confirmLabel.text = action == MeruAction.Burn ? "เผาทิ้ง" : "อัพเกรด";
            if (confirmButton != null) confirmButton.interactable = card != null;
        }

        private void Confirm()
        {
            var deck = meru.Deck;
            if (selected < 0 || selected >= deck.Count) return;
            meru.Apply(action, deck[selected]);
        }

        public void ShowResult(MeruAction doneAction, CardDataSO card)
        {
            SetPanels(choice: false, picker: false, result: true);
            if (resultText != null)
            {
                resultText.text = doneAction == MeruAction.Burn
                    ? $"เปลวไฟกลืน \"{card.cardNameThai}\" จนเหลือเพียงเถ้าถ่าน"
                    : $"\"{card.cardNameThai}\" ผ่านไฟชำระ กลายเป็น \"{card.upgradedCard.cardNameThai}\"";
            }
            if (resultCard != null)
            {
                // Burn: the card that was burnt, faded. Upgrade: the new card.
                var shown = doneAction == MeruAction.Burn ? card : card.upgradedCard;
                resultCard.gameObject.SetActive(true);
                resultCard.Setup(shown, null);
                if (resultCard.button != null) resultCard.button.interactable = false;
                var group = resultCard.GetComponent<CanvasGroup>();
                if (group != null) group.alpha = doneAction == MeruAction.Burn ? 0.4f : 1f;
            }
        }

        private void SetPanels(bool choice, bool picker, bool result)
        {
            if (choicePanel != null) choicePanel.SetActive(choice);
            if (pickerPanel != null) pickerPanel.SetActive(picker);
            if (resultPanel != null) resultPanel.SetActive(result);
            if (storyText != null) storyText.gameObject.SetActive(choice);
        }
    }
}
