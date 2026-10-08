using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TawanOS.CardEngine;
using TawanOS.VFX;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Victory screen: incense earned, the card choices and a skip button. The choices are dealt face down one
    /// at a time and flipped, then float over drifting smoke; after the pick the others burn away
    /// (timings and art in <see cref="RewardFxConfigSO"/>).
    /// </summary>
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
        private RewardFxConfigSO fx;
        private Coroutine dealing;

        /// <summary>Seconds from the pick until the screen may close (the burn plays out first).</summary>
        public float PickSequenceSeconds => fx != null ? fx.PickSequenceSeconds : 0f;

        private void Awake()
        {
            fx = RewardFxConfigSO.Load();
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
            if (skipButton != null) skipButton.onClick.AddListener(() => { if (rewards != null) rewards.Skip(); });
            BuildSmoke();
        }

        // Smoke sits right above the background image, behind everything else on the screen
        private void BuildSmoke()
        {
            if (fx.smokeTexture == null || fx.smokeCount <= 0) return;

            var go = new GameObject("SmokeLayer", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var background = transform.Find("Background");
            rt.SetSiblingIndex(background != null ? background.GetSiblingIndex() + 1 : 0);

            var smoke = go.AddComponent<UiSmokeDrift>();
            smoke.texture = fx.smokeTexture;
            smoke.count = fx.smokeCount;
            smoke.alphaRange = fx.smokeAlpha;
            smoke.sizeRange = fx.smokeSize;
            smoke.speedRange = fx.smokeSpeed;
            smoke.tint = fx.smokeTint;
            Canvas.ForceUpdateCanvases();
            smoke.Build();
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
                cardViews[i].PrepareFaceDown(fx.cardBack);
            }

            if (dealing != null) StopCoroutine(dealing);
            dealing = StartCoroutine(DealCards(manager.Choices.Count));
        }

        // One card at a time: fly in face down, flip, then the next. Nothing can be picked until all are up.
        private IEnumerator DealCards(int count)
        {
            if (skipButton != null) skipButton.interactable = false;

            // The row's layout must have placed the cards before they can fly to their places
            LayoutRebuilder.ForceRebuildLayoutImmediate(cardRow);
            Vector3 from = cardRow.TransformPoint(cardRow.rect.center) + Vector3.down * fx.dealFromBelow * RootScale();

            yield return new WaitForSeconds(fx.dealStartDelay);
            for (int i = 0; i < count; i++)
            {
                var view = cardViews[i];
                yield return view.DealIn(from, fx);
                yield return new WaitForSeconds(fx.pauseBeforeFlip);
                yield return view.Flip(fx);
                view.StartFloat(fx, count > 1 ? i / (float)count : 0f);
                if (i < count - 1) yield return new WaitForSeconds(fx.pauseBetweenCards);
            }

            for (int i = 0; i < count; i++) cardViews[i].SetInteractable(true);
            if (skipButton != null) skipButton.interactable = true;
            dealing = null;
        }

        // Pixels of the reference layout -> world units of this (overlay) canvas
        private float RootScale()
        {
            var canvas = GetComponentInParent<Canvas>();
            return canvas != null ? canvas.rootCanvas.transform.lossyScale.y : 1f;
        }

        public void ShowPicked(CardDataSO card)
        {
            for (int i = 0; i < cardViews.Count && i < rewards.Choices.Count; i++)
                cardViews[i].SetResult(rewards.Choices[i] == card, fx);

            // One fire sound for the whole burn, however many cards catch fire
            if (rewards.Choices.Count > 1)
            {
                DOVirtual.DelayedCall(fx.burnDelay, () => TawanOS.Audio.AudioManager.Instance?.PlaySfx("sfx_fire_on"))
                    .SetLink(gameObject);
            }
            if (promptText != null) promptText.text = $"\"{card.cardNameThai}\" เข้าสำรับแล้ว";
            if (skipButton != null) skipButton.interactable = false;
        }

        private void RefreshDeckCount()
        {
            if (deckText != null) deckText.text = $"สำรับ {RunState.Current.Deck.Count} ใบ   •   ธูป {RunState.Current.Incense}";
        }
    }
}
