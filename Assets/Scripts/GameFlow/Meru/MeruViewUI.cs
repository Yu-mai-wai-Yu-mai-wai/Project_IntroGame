using System.Collections.Generic;
using DG.Tweening;
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

        [Header("Upgrade Fire")]
        [Tooltip("Seconds the old card is shown before the fire takes it.")]
        [Min(0f)] public float upgradeDelay = 0.35f;
        [Tooltip("Seconds the fire takes to creep in from the card's edge to its scorch line.")]
        [Min(0.05f)] public float upgradeFlareIn = 0.7f;
        [Tooltip("Seconds the fire, now at the scorch line, takes to calm down to its usual smoulder.")]
        [Min(0.05f)] public float upgradeFlareOut = 0.9f;
        [Tooltip("How much brighter the fire burns when it reaches the scorch line.")]
        [Min(1f)] public float upgradeFlareGlow = 2f;

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
            bool canBurn = meru != null && meru.Deck.Count > 1;
            if (burnButton != null) burnButton.interactable = canBurn;
            // The player must burn or upgrade a card (Game Designer 10 Oct). Leaving without one is only offered
            // when neither is possible (one card left and nothing to upgrade), so the node never traps the player.
            if (leaveButton != null) leaveButton.gameObject.SetActive(!canBurn && !canUpgrade);
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
                // Burn: the card that was burnt goes up in flames (same burn as the reward screen).
                // Upgrade: the old card is taken by the fire of the scorch marks and comes out as the new card.
                resultCard.gameObject.SetActive(true);
                resultCard.Setup(card, null);
                if (resultCard.button != null) resultCard.button.interactable = false;
                if (doneAction == MeruAction.Burn) PlayBurn();
                else PlayUpgrade(card);
            }
        }

        // The upgraded card's fire (CardEmber) catches at the old card's edge and creeps in, flaring, to the scorch
        // line the upgraded card keeps, and stops there; as it gets there the card becomes the upgraded one and the
        // fire calms down to its usual smoulder
        private void PlayUpgrade(CardDataSO card)
        {
            var upgraded = card.upgradedCard;
            var frame = resultCard.frame;
            CardEmber.SetUI(frame, true, upgraded);
            var sheet = CardEmber.UiSheet(frame);
            if (sheet == null)
            {
                // No ember shader: just show the new card
                resultCard.Setup(upgraded, null);
                return;
            }

            float baseGlow = sheet.material.GetFloat(CardEmber.GlowBoostProperty);
            // grow: 0 = at the card's edge .. 1 = at the scorch line; flare: 0 = usual glow .. 1 = brightest
            System.Action<float, float> apply = (grow, flare) =>
            {
                // Setup keeps the same sheet, but read it again in case the card was rebuilt
                var s = CardEmber.UiSheet(frame);
                if (s == null) return;
                s.material.SetFloat(CardEmber.GrowProperty, grow);
                s.material.SetFloat(CardEmber.GlowBoostProperty, baseGlow * Mathf.Lerp(1f, upgradeFlareGlow, flare));
            };
            // The old card has no scorch yet: the fire fades up out of nothing at its edge
            System.Action<float> fadeSheet = a =>
            {
                var s = CardEmber.UiSheet(frame);
                if (s == null) return;
                var c = s.color;
                c.a = a;
                s.color = c;
            };
            apply(0f, 0f);
            fadeSheet(0f);

            float total = upgradeDelay + upgradeFlareIn + upgradeFlareOut;
            DOTween.Sequence().SetLink(gameObject)
                .AppendInterval(upgradeDelay)
                .AppendCallback(() => TawanOS.Audio.AudioManager.Instance?.PlaySfx("sfx_fire_on"))
                .Append(DOVirtual.Float(0f, 1f, upgradeFlareIn, v =>
                {
                    apply(v, v);
                    fadeSheet(Mathf.Clamp01(v * 3f));
                }).SetEase(Ease.OutQuad))
                .AppendCallback(() =>
                {
                    resultCard.Setup(upgraded, null);
                    if (resultCard.button != null) resultCard.button.interactable = false;
                    apply(1f, 1f);
                    fadeSheet(1f);
                    resultCard.transform.DOPunchScale(Vector3.one * 0.08f, 0.35f, 6, 0.6f).SetLink(gameObject);
                })
                .Append(DOVirtual.Float(1f, 0f, upgradeFlareOut, v => apply(1f, v)).SetEase(Ease.OutCubic))
                .AppendCallback(() => TawanOS.Audio.AudioManager.Instance?.PlaySfx("sfx_fire_off"));

            if (resultText != null)
            {
                resultText.alpha = 0f;
                resultText.DOKill();
                resultText.DOFade(1f, 0.5f).SetDelay(upgradeDelay + upgradeFlareIn).SetLink(gameObject);
            }
            if (continueButton != null)
            {
                continueButton.interactable = false;
                DOVirtual.DelayedCall(total, () => continueButton.interactable = true).SetLink(gameObject);
            }
        }

        // The card is shown whole, then burns from the bottom-right corner; the text and the way out come after
        private void PlayBurn()
        {
            var fx = RewardFxConfigSO.Load();
            resultCard.SetResult(false, fx);
            DOVirtual.DelayedCall(fx.burnDelay, () => TawanOS.Audio.AudioManager.Instance?.PlaySfx("sfx_fire_on"))
                .SetLink(gameObject);

            float burnEnd = fx.burnDelay + fx.burnDuration;
            if (resultText != null)
            {
                resultText.alpha = 0f;
                resultText.DOKill();
                resultText.DOFade(1f, 0.5f).SetDelay(burnEnd * 0.6f).SetLink(gameObject);
            }
            if (continueButton != null)
            {
                continueButton.interactable = false;
                DOVirtual.DelayedCall(burnEnd, () => continueButton.interactable = true).SetLink(gameObject);
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
