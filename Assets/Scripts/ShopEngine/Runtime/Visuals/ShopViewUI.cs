using System.Collections.Generic;
using DG.Tweening;
using TawanOS.CardEngine;
using TawanOS.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.ShopEngine
{
    /// <summary>
    /// Spirit-house shop screen: the shrine and its guardian on the left, shelves of cards and amulets (and the
    /// guardian's blessings: max khwan, max กุศล) on the right.
    /// No services (heal, card removal) since 11 Oct: those belong to the วัด and the เมรุ.
    /// </summary>
    public class ShopViewUI : MonoBehaviour
    {
        [Header("Run Status")]
        public TextMeshProUGUI incenseText;
        public TextMeshProUGUI hpText;
        public TextMeshProUGUI deckText;

        [Header("Shrine")]
        public Image shrineImage;
        public TextMeshProUGUI shrineNameText;
        public TextMeshProUGUI guardianNameText;
        public TextMeshProUGUI speechText;
        public TextMeshProUGUI resultText;

        [Header("Shelves")]
        public RectTransform cardRow;
        public RectTransform amuletRow;
        public ShopItemView itemTemplate;
        public Button leaveButton;

        private ShopManager shop;
        // Item views are reused per shelf so a click handler never destroys its own button
        private readonly Dictionary<RectTransform, List<ShopItemView>> shelfItems = new Dictionary<RectTransform, List<ShopItemView>>();
        private readonly Dictionary<RectTransform, int> shelfUsed = new Dictionary<RectTransform, int>();
        private ShopCardShelf cardShelf; // real card faces, when the config has a card template

        private void Awake()
        {
            if (itemTemplate != null) itemTemplate.gameObject.SetActive(false);
            if (leaveButton != null) leaveButton.onClick.AddListener(() => { if (shop != null) shop.Leave(); });
        }

        private void OnEnable()
        {
            RunState.Current.OnChanged += RefreshStatus;
            RefreshStatus();
        }

        private void OnDisable()
        {
            RunState.Current.OnChanged -= RefreshStatus;
        }

        public void Bind(ShopManager manager)
        {
            shop = manager;
            var config = manager.config;
            if (shrineNameText != null) shrineNameText.text = config.shrineName;
            if (guardianNameText != null) guardianNameText.text = config.guardianName;
            if (shrineImage != null)
            {
                shrineImage.sprite = config.shrineImage;
                shrineImage.enabled = config.shrineImage != null;
                shrineImage.preserveAspect = true;
            }
            if (resultText != null) resultText.text = string.Empty;

            if (config.cardFaceTemplate != null && cardRow != null)
            {
                if (cardShelf == null) cardShelf = cardRow.gameObject.AddComponent<ShopCardShelf>();
                cardShelf.Init(manager, config.cardFaceTemplate, transform);
            }
            Refresh();
        }

        public void Say(string line, string result = null)
        {
            if (speechText != null)
            {
                speechText.text = $"\"{line}\"";
                speechText.DOKill();
                speechText.alpha = 0f;
                speechText.DOFade(1f, 0.35f);
            }
            if (resultText != null)
            {
                resultText.text = result ?? string.Empty;
                if (!string.IsNullOrEmpty(result))
                {
                    resultText.transform.DOKill(true);
                    resultText.transform.DOPunchScale(Vector3.one * 0.12f, 0.3f, 6, 0.8f);
                }
            }
        }

        public void Refresh()
        {
            if (shop == null) return;
            shelfUsed.Clear();

            var run = RunState.Current;

            if (cardShelf != null) cardShelf.Refresh();
            else foreach (var offer in shop.CardOffers)
            {
                var card = offer.card;
                var o = offer;
                Spawn(cardRow).Setup(card.cardImage != null ? card.cardImage : card.artwork != null ? card.artwork : card.cardBackground,
                    card.cardNameThai,
                    $"{card.GetFormattedTypeText()}  •  บุญ {card.meritCost}",
                    FormatDescription(card),
                    PriceLabel(offer.price), run.CanAfford(offer.price), offer.sold,
                    () => shop.BuyCard(o));
            }

            foreach (var offer in shop.AmuletOffers)
            {
                var o = offer;
                Spawn(amuletRow).Setup(offer.amulet.icon, offer.amulet.displayName, "เครื่องราง",
                    offer.amulet.description, PriceLabel(offer.price), run.CanAfford(offer.price), offer.sold,
                    () => shop.BuyAmulet(o));
            }

            // Blessings share the amulet shelf (the shop sells no amulets at the moment)
            foreach (var offer in shop.BlessingOffers)
            {
                var o = offer;
                Spawn(amuletRow).Setup(offer.blessing.icon, offer.blessing.displayName, "พรเจ้าที่",
                    offer.blessing.description, PriceLabel(offer.price), run.CanAfford(offer.price), offer.sold,
                    () => shop.BuyBlessing(o));
            }

            HideUnused(cardRow);
            HideUnused(amuletRow);
        }

        private ShopItemView Spawn(RectTransform row)
        {
            if (!shelfItems.TryGetValue(row, out var items)) shelfItems[row] = items = new List<ShopItemView>();
            shelfUsed.TryGetValue(row, out int used);
            if (used >= items.Count) items.Add(Instantiate(itemTemplate, row));
            shelfUsed[row] = used + 1;

            var item = items[used];
            item.gameObject.SetActive(true);
            return item;
        }

        private void HideUnused(RectTransform row)
        {
            if (!shelfItems.TryGetValue(row, out var items)) return;
            shelfUsed.TryGetValue(row, out int used);
            for (int i = used; i < items.Count; i++) items[i].gameObject.SetActive(false);
        }

        private void RefreshStatus()
        {
            var run = RunState.Current;
            if (incenseText != null) incenseText.text = $"ธูป {run.Incense}";
            if (hpText != null) hpText.text = $"HP {run.CurrentHp}/{run.MaxHp}";
            if (deckText != null) deckText.text = $"สำรับ {run.Deck.Count} ใบ";
            // Prices turn red/gold as incense changes
            if (shop != null && isActiveAndEnabled) Refresh();
        }

        private static string PriceLabel(int price) => $"ธูป {price}";

        private static string FormatDescription(CardDataSO card)
        {
            try { return string.Format(card.descriptionFormat ?? string.Empty, card.baseValue); }
            catch (System.FormatException) { return card.descriptionFormat; }
        }
    }
}
