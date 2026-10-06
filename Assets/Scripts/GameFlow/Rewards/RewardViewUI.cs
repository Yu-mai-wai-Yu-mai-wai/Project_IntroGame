using System.Collections.Generic;
using DG.Tweening;
using TawanOS.CardEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.GameFlow
{
    /// <summary>Victory screen: incense earned, the card choices and a skip button.</summary>
    public class RewardViewUI : MonoBehaviour
    {
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI incenseText;
        public TextMeshProUGUI promptText;
        public TextMeshProUGUI deckText;
        public RectTransform cardRow;
        public RewardCardView cardTemplate;
        public Button skipButton;
        public TextMeshProUGUI skipLabel;

        private RewardManager rewards;
        private readonly List<RewardCardView> cardViews = new List<RewardCardView>();

        private void Awake()
        {
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
            if (skipButton != null) skipButton.onClick.AddListener(() => { if (rewards != null) rewards.Skip(); });
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

        public void Show(RewardManager manager, int incenseEarned, RewardTier tier)
        {
            rewards = manager;
            var config = manager.config;

            if (titleText != null)
            {
                titleText.text = tier switch
                {
                    RewardTier.Boss => "ชัยชนะครั้งยิ่งใหญ่!",
                    RewardTier.Elite => "ชัยชนะเหนือผีร้าย!",
                    RewardTier.Offering => "ของจากกองเซ่น",
                    _ => "ชัยชนะ",
                };
            }
            if (incenseText != null)
            {
                incenseText.text = incenseEarned > 0 ? $"+{incenseEarned} ธูป" : string.Empty;
                incenseText.transform.localScale = Vector3.one * 1.3f;
                incenseText.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
            }
            if (promptText != null) promptText.text = manager.Choices.Count > 0 ? "เลือกการ์ด 1 ใบเข้าสำรับ" : "ไม่มีการ์ดให้เลือก";

            if (skipButton != null) skipButton.gameObject.SetActive(config.allowSkip || manager.Choices.Count == 0);
            if (skipLabel != null)
            {
                skipLabel.text = manager.Choices.Count == 0 ? "ไปต่อ"
                    : config.skipIncense > 0 ? $"ไม่รับการ์ด  (+{config.skipIncense} ธูป)" : "ไม่รับการ์ด";
            }

            while (cardViews.Count < manager.Choices.Count) cardViews.Add(Instantiate(cardTemplate, cardRow));
            for (int i = 0; i < cardViews.Count; i++)
            {
                bool used = i < manager.Choices.Count;
                cardViews[i].gameObject.SetActive(used);
                if (!used) continue;

                var card = manager.Choices[i];
                cardViews[i].Setup(card, () => manager.Choose(card));
                var group = cardViews[i].GetComponent<CanvasGroup>();
                if (group != null)
                {
                    group.alpha = 0f;
                    group.DOFade(1f, 0.3f).SetDelay(0.1f * i);
                }
            }
        }

        public void ShowPicked(CardDataSO card)
        {
            for (int i = 0; i < cardViews.Count && i < rewards.Choices.Count; i++)
                cardViews[i].SetResult(rewards.Choices[i] == card);
            if (promptText != null) promptText.text = $"\"{card.cardNameThai}\" เข้าสำรับแล้ว";
            if (skipButton != null) skipButton.interactable = false;
        }

        private void RefreshDeckCount()
        {
            if (deckText != null) deckText.text = $"สำรับ {RunState.Current.Deck.Count} ใบ   •   ธูป {RunState.Current.Incense}";
        }
    }
}
