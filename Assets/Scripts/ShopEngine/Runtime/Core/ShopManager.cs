using System;
using System.Collections.Generic;
using TawanOS.CardEngine;
using TawanOS.GameFlow;
using UnityEngine;

namespace TawanOS.ShopEngine
{
    /// <summary>One slot on the shrine's shelf: a card or an amulet with its incense price.</summary>
    public class ShopOffer
    {
        public CardDataSO card;
        public ShopAmulet amulet;
        public int price;
        public bool sold;
    }

    /// <summary>
    /// Spirit-house shop (ศาลพระภูมิ): rolls the stock for this visit and handles purchases with
    /// incense (ธูป) from the <see cref="RunState"/>. Raises <see cref="OnShopClosed"/> when the
    /// player bows out (กราบลา).
    /// </summary>
    public class ShopManager : MonoBehaviour
    {
        public static ShopManager Instance { get; private set; }

        public ShopConfigSO config;
        public ShopViewUI view;

        public event Action OnShopClosed;

        public IReadOnlyList<ShopOffer> CardOffers => cardOffers;
        public IReadOnlyList<ShopOffer> AmuletOffers => amuletOffers;
        public bool BlessingUsed { get; private set; }
        public bool RemovalUsed { get; private set; }

        public int BlessingPrice => config.blessingPrice;
        public int BlessingHealAmount => Mathf.Max(1, Mathf.RoundToInt(RunState.Current.MaxHp * config.blessingHealPercent));
        public int RemovalPrice => config.removalBasePrice + config.removalPriceStep * RunState.Current.CardRemovalsBought;

        private readonly List<ShopOffer> cardOffers = new List<ShopOffer>();
        private readonly List<ShopOffer> amuletOffers = new List<ShopOffer>();

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (config == null)
            {
                Debug.LogError("[ShopManager] ShopConfigSO is not assigned.");
                return;
            }

            RunState.Current.EnsureDeck(config.starterDeck);
            OpenShop();
        }

        private void OpenShop()
        {
            RollStock();
            BlessingUsed = false;
            RemovalUsed = false;
            if (view != null)
            {
                view.Bind(this);
                view.Say(ShopConfigSO.PickLine(config.greetingLines, "จุดธูปบอกกล่าวก่อนนะ..."));
            }
        }

        private void RollStock()
        {
            cardOffers.Clear();
            amuletOffers.Clear();

            var catalog = CardCatalogSO.Load();
            if (catalog != null)
            {
                foreach (var card in PickDistinct(catalog.BaseCards(), config.cardsForSale))
                    cardOffers.Add(new ShopOffer { card = card, price = config.RollCardPrice(card) });
            }
            else
            {
                Debug.LogWarning("[ShopManager] Resources/CardCatalog not found - no cards for sale.");
            }

            foreach (var amulet in PickDistinct(config.amulets, config.amuletsForSale))
                amuletOffers.Add(new ShopOffer { amulet = amulet, price = config.Vary(amulet.price) });
        }

        private static List<T> PickDistinct<T>(List<T> source, int count) where T : class
        {
            var pool = source.FindAll(x => x != null);
            var picked = new List<T>();
            while (picked.Count < count && pool.Count > 0)
            {
                int i = UnityEngine.Random.Range(0, pool.Count);
                picked.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return picked;
        }

        // ---------------------------------------------------------------- purchases

        public void BuyCard(ShopOffer offer)
        {
            if (!TryPay(offer)) return;
            RunState.Current.AddCard(offer.card);
            Thank($"ได้รับการ์ด \"{offer.card.cardNameThai}\"");
        }

        public void BuyAmulet(ShopOffer offer)
        {
            if (!TryPay(offer)) return;
            RunState.Current.AddRelic(offer.amulet.relicId);
            Thank($"ได้รับ \"{offer.amulet.displayName}\"");
        }

        public void BuyBlessing()
        {
            var run = RunState.Current;
            if (BlessingUsed || run.CurrentHp >= run.MaxHp) return;
            if (!run.TrySpendIncense(BlessingPrice))
            {
                Refuse();
                return;
            }
            BlessingUsed = true;
            int heal = BlessingHealAmount;
            run.Heal(heal);
            Thank($"ฟื้นฟู {heal} HP");
        }

        /// <summary>Opens the card picker; the actual removal happens in <see cref="RemoveCard"/>.</summary>
        public void BeginRemoval()
        {
            if (RemovalUsed) return;
            if (!RunState.Current.CanAfford(RemovalPrice))
            {
                Refuse();
                return;
            }
            if (view != null) view.ShowRemovalPicker(RunState.Current.Deck, RemovalPrice);
        }

        public void RemoveCard(CardDataSO card)
        {
            var run = RunState.Current;
            if (RemovalUsed || !run.TrySpendIncense(RemovalPrice)) return;
            run.RemoveCard(card, countsAsPurchase: true);
            RemovalUsed = true;
            if (view != null) view.HideRemovalPicker();
            Thank($"ถอน \"{card.cardNameThai}\" ออกจากสำรับแล้ว");
        }

        public void Leave()
        {
            if (OnShopClosed != null)
            {
                OnShopClosed.Invoke();
                return;
            }
            // Scene opened on its own: restock so designers can keep testing.
            Debug.Log("[ShopManager] Shop closed (no GameFlow listener) - restocking.");
            OpenShop();
        }

        private bool TryPay(ShopOffer offer)
        {
            if (offer == null || offer.sold) return false;
            if (!RunState.Current.TrySpendIncense(offer.price))
            {
                Refuse();
                return false;
            }
            offer.sold = true;
            return true;
        }

        private void Thank(string result)
        {
            if (view == null) return;
            view.Say(ShopConfigSO.PickLine(config.thanksLines, "เจ้าที่รับไว้แล้ว"), result);
            view.Refresh();
        }

        private void Refuse()
        {
            if (view == null) return;
            view.Say(ShopConfigSO.PickLine(config.notEnoughIncenseLines, "ธูปไม่พอหรอก..."));
            view.Refresh();
        }
    }
}
