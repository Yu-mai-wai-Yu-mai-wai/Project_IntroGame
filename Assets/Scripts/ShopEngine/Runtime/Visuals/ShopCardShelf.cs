using System.Collections.Generic;
using DG.Tweening;
using TawanOS.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.ShopEngine
{
    /// <summary>
    /// The shop's card shelf drawn with real card faces (<see cref="RewardCardView"/>, the same card as the
    /// reward screen) and the incense price under each one. A bought card lifts, then flies down into the
    /// deck pile at the bottom centre of the screen, as if it went into the player's deck.
    /// Lives on the card row; <see cref="ShopViewUI"/> creates it when the shop config has a card template.
    /// </summary>
    public class ShopCardShelf : MonoBehaviour
    {
        private const float CardAspect = 939f / 1312f; // the card design's shape (width / height)
        private const float PriceBand = 0.12f;          // share of the slot height kept for the price

        private static readonly Color AffordableColor = new Color(0.98f, 0.84f, 0.5f);
        private static readonly Color TooExpensiveColor = new Color(0.9f, 0.4f, 0.35f);
        private static readonly Color SoldColor = new Color(0.6f, 0.55f, 0.5f, 0.8f);

        private class Slot
        {
            public RectTransform root;
            public RectTransform cardArea;
            public RewardCardView card;
            public TextMeshProUGUI price;
            public ShopOffer offer;
            public bool flown;
        }

        private readonly List<Slot> slots = new List<Slot>();
        private ShopManager shop;
        private ShopConfigSO config;
        private RewardCardView template;
        private RectTransform deckPile;
        private TextMeshProUGUI deckCount;
        private Transform flyLayer;

        /// <summary>The pile bought cards fly into (null until <see cref="Init"/>).</summary>
        public RectTransform DeckPile => deckPile;

        /// <summary>Sets the shelf up once; <paramref name="screenRoot"/> holds the deck pile and flying cards.</summary>
        public void Init(ShopManager manager, RewardCardView cardTemplate, Transform screenRoot)
        {
            shop = manager;
            config = manager.config;
            template = cardTemplate;
            flyLayer = screenRoot;

            // The row's layout sizes the slots; the cards keep their own shape inside them
            var layout = GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = true;
            }

            if (deckPile == null) BuildDeckPile(screenRoot);
        }

        /// <summary>Shows the current stock. Cards already on the shelf only update their price and sold state.</summary>
        public void Refresh()
        {
            if (shop == null || template == null) return;

            var run = RunState.Current;
            var offers = shop.CardOffers;
            while (slots.Count < offers.Count) slots.Add(BuildSlot());

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                bool used = i < offers.Count;
                slot.root.gameObject.SetActive(used);
                if (!used) continue;

                var offer = offers[i];
                if (slot.offer != offer) Fill(slot, offer); // new stock (the shop opened or restocked)

                if (offer.sold)
                {
                    if (!slot.flown) FlyToDeck(slot);
                    slot.price.text = "ถวายแล้ว";
                    slot.price.color = SoldColor;
                }
                else
                {
                    slot.price.text = $"ธูป {offer.price}";
                    slot.price.color = run.CanAfford(offer.price) ? AffordableColor : TooExpensiveColor;
                }
            }

            if (deckCount != null) deckCount.text = $"สำรับ {run.Deck.Count} ใบ";
        }

        private Slot BuildSlot()
        {
            var slot = new Slot();

            var rootGo = new GameObject("CardSlot", typeof(RectTransform));
            slot.root = (RectTransform)rootGo.transform;
            slot.root.SetParent(transform, false);

            // Card on top, price band underneath
            var areaGo = new GameObject("CardArea", typeof(RectTransform));
            slot.cardArea = (RectTransform)areaGo.transform;
            slot.cardArea.SetParent(slot.root, false);
            slot.cardArea.anchorMin = new Vector2(0f, PriceBand);
            slot.cardArea.anchorMax = Vector2.one;
            slot.cardArea.offsetMin = slot.cardArea.offsetMax = Vector2.zero;
            var fitter = areaGo.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = CardAspect;

            var priceGo = new GameObject("Price", typeof(RectTransform));
            var priceRt = (RectTransform)priceGo.transform;
            priceRt.SetParent(slot.root, false);
            priceRt.anchorMin = Vector2.zero;
            priceRt.anchorMax = new Vector2(1f, PriceBand);
            priceRt.offsetMin = priceRt.offsetMax = Vector2.zero;
            slot.price = priceGo.AddComponent<TextMeshProUGUI>();
            slot.price.font = template.descriptionText != null ? template.descriptionText.font : null;
            slot.price.enableAutoSizing = true;
            slot.price.fontSizeMin = 14f;
            slot.price.fontSizeMax = 30f;
            slot.price.alignment = TextAlignmentOptions.Center;
            slot.price.raycastTarget = false;

            return slot;
        }

        // A fresh card face for this offer (a card that flew to the deck is gone, so each stock gets new ones)
        private void Fill(Slot slot, ShopOffer offer)
        {
            if (slot.card != null) Destroy(slot.card.gameObject);

            var card = Instantiate(template, slot.cardArea);
            card.gameObject.SetActive(true);
            var rt = (RectTransform)card.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var element = card.GetComponent<LayoutElement>();
            if (element != null) element.ignoreLayout = true;

            slot.card = card;
            slot.offer = offer;
            slot.flown = false;
            card.Setup(offer.card, () => OnCardClicked(slot));

            // Each new card comes up onto the shelf
            var group = card.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 0f;
                group.DOFade(1f, 0.3f).SetDelay(0.06f * slots.IndexOf(slot)).SetLink(card.gameObject);
            }
        }

        private void OnCardClicked(Slot slot)
        {
            if (slot.offer == null || slot.offer.sold || slot.flown) return;

            // Too expensive: the card shakes "no" and the guardian says so (ShopManager.BuyCard)
            if (!RunState.Current.CanAfford(slot.offer.price))
            {
                slot.card.transform.DOKill(true);
                slot.card.transform.DOPunchPosition(new Vector3(14f, 0f, 0f), 0.35f, 12, 0.6f).SetLink(slot.card.gameObject);
            }
            shop.BuyCard(slot.offer);
        }

        // Lift, then drop into the deck pile at the bottom centre, shrinking and turning slightly
        private void FlyToDeck(Slot slot)
        {
            slot.flown = true;
            var card = slot.card;
            if (card == null) return;

            card.SetInteractable(false);
            var rt = (RectTransform)card.transform;
            rt.DOKill();

            // Keep its on-screen size while it leaves the shelf for the screen root
            Vector2 size = rt.rect.size;
            Vector3 world = rt.TransformPoint(rt.rect.center);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.SetParent(flyLayer, true);
            rt.position = world;
            rt.localScale = Vector3.one;
            rt.SetAsLastSibling();

            Vector3 target = deckPile != null ? deckPile.position : world + Vector3.down * 600f;
            float lift = config.buyLiftDuration;
            float fly = config.buyFlyDuration;
            var group = card.GetComponent<CanvasGroup>();

            TawanOS.Audio.AudioManager.Instance?.PlaySfx("sfx_card_play");
            var seq = DOTween.Sequence().SetLink(card.gameObject);
            seq.Append(rt.DOMove(world + Vector3.up * 40f * flyLayer.lossyScale.y, lift).SetEase(Ease.OutQuad));
            seq.Join(rt.DOScale(1.1f, lift).SetEase(Ease.OutQuad));
            seq.Append(rt.DOMove(target, fly).SetEase(Ease.InQuad));
            seq.Join(rt.DOScale(config.buyEndScale, fly).SetEase(Ease.InQuad));
            seq.Join(rt.DOLocalRotate(new Vector3(0f, 0f, Random.Range(-14f, 14f)), fly).SetEase(Ease.InQuad));
            if (group != null) seq.Insert(lift + fly * 0.75f, group.DOFade(0f, fly * 0.25f));
            seq.OnComplete(() =>
            {
                if (deckPile != null)
                {
                    deckPile.DOKill(true);
                    deckPile.DOPunchScale(Vector3.one * 0.18f, 0.3f, 8, 0.7f).SetLink(deckPile.gameObject);
                }
                card.gameObject.SetActive(false);
            });
        }

        // A small stack of face-down cards at the bottom centre: where bought cards go
        private void BuildDeckPile(Transform screenRoot)
        {
            Sprite back = config.deckPileSprite != null ? config.deckPileSprite : RewardFxConfigSO.Load().cardBack;

            var go = new GameObject("DeckPile", typeof(RectTransform));
            deckPile = (RectTransform)go.transform;
            deckPile.SetParent(screenRoot, false);
            deckPile.anchorMin = deckPile.anchorMax = new Vector2(0.5f, 0f);
            deckPile.pivot = new Vector2(0.5f, 0.5f);
            deckPile.sizeDelta = new Vector2(64f, 64f / CardAspect);
            deckPile.anchoredPosition = new Vector2(0f, 62f);

            // Three cards, each a little offset, so it reads as a pile
            for (int i = 0; i < 3; i++)
            {
                var cardGo = new GameObject("Card" + i, typeof(RectTransform), typeof(Image));
                var crt = (RectTransform)cardGo.transform;
                crt.SetParent(deckPile, false);
                crt.anchorMin = Vector2.zero;
                crt.anchorMax = Vector2.one;
                crt.offsetMin = crt.offsetMax = Vector2.zero;
                crt.anchoredPosition = new Vector2(i * 3f, i * 3f);
                crt.localEulerAngles = new Vector3(0f, 0f, (i - 1) * 3f);
                var img = cardGo.GetComponent<Image>();
                img.sprite = back;
                img.color = back != null ? Color.Lerp(new Color(0.55f, 0.5f, 0.5f), Color.white, i / 2f) : new Color(0.15f, 0.1f, 0.1f);
                img.raycastTarget = false;
            }

            var labelGo = new GameObject("Count", typeof(RectTransform));
            var lrt = (RectTransform)labelGo.transform;
            lrt.SetParent(deckPile, false);
            lrt.anchorMin = new Vector2(1f, 0.5f);
            lrt.anchorMax = new Vector2(1f, 0.5f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.sizeDelta = new Vector2(180f, 40f);
            lrt.anchoredPosition = new Vector2(16f, 0f);
            deckCount = labelGo.AddComponent<TextMeshProUGUI>();
            deckCount.font = template.descriptionText != null ? template.descriptionText.font : null;
            deckCount.fontSize = 24f;
            deckCount.color = new Color(0.96f, 0.92f, 0.82f);
            deckCount.alignment = TextAlignmentOptions.MidlineLeft;
            deckCount.raycastTarget = false;
        }
    }
}
