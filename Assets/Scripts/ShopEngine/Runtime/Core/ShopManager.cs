using System;
using System.Collections.Generic;
using TawanOS.CardEngine;
using TawanOS.GameFlow;
using UnityEngine;

namespace TawanOS.ShopEngine
{
    /// <summary>One slot on the shrine's shelf: a card, an amulet or a blessing with its incense price.</summary>
    public class ShopOffer
    {
        public CardDataSO card;
        public ShopAmulet amulet;
        public ShopBlessing blessing;
        public int price;
        public bool sold;
    }

    /// <summary>
    /// Spirit-house shop (ศาลตายาย): rolls the stock for this visit and handles purchases with
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
        public IReadOnlyList<ShopOffer> BlessingOffers => blessingOffers;


        private readonly List<ShopOffer> cardOffers = new List<ShopOffer>();
        private readonly List<ShopOffer> amuletOffers = new List<ShopOffer>();
        private readonly List<ShopOffer> blessingOffers = new List<ShopOffer>();

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
            blessingOffers.Clear();

            if (RunState.Current.HasSavedShopStock)
            {
                RestoreSavedStock();
                return;
            }

            var catalog = CardCatalogSO.Load();
            if (catalog != null)
            {
                var cards = config.cardTypeOdds != null ? config.cardTypeOdds.Pick(catalog.BaseCards(), config.cardsForSale)
                    : PickDistinct(catalog.BaseCards(), config.cardsForSale);
                foreach (var card in cards)
                    cardOffers.Add(new ShopOffer { card = card, price = config.RollCardPrice(card) });
            }
            else
            {
                Debug.LogWarning("[ShopManager] Resources/CardCatalog not found - no cards for sale.");
            }

            foreach (var amulet in PickDistinct(config.amulets, config.amuletsForSale))
                amuletOffers.Add(new ShopOffer { amulet = amulet, price = config.Vary(amulet.price) });

            // Every visit offers each blessing once, scaled by how many times bought this run (P0-8)
            foreach (var blessing in config.blessings)
                if (blessing != null)
                {
                    int timesBought = RunState.Current.GetBlessingPurchases(blessing.kind);
                    blessingOffers.Add(new ShopOffer { blessing = blessing, price = blessing.GetPrice(timesBought) });
                }

            SaveStockToRunState();
        }

        private void RestoreSavedStock()
        {
            var stock = RunState.Current.ShopStock;
            var catalog = CardCatalogSO.Load();

            if (stock.cards != null && catalog != null)
            {
                foreach (var saved in stock.cards)
                {
                    var card = catalog.cards.Find(c => c != null && c.cardId == saved.id);
                    if (card != null)
                        cardOffers.Add(new ShopOffer { card = card, price = saved.price, sold = saved.sold });
                }
            }

            if (stock.amulets != null && config != null && config.amulets != null)
            {
                foreach (var saved in stock.amulets)
                {
                    var amulet = config.amulets.Find(a => a != null && a.relicId == saved.id);
                    if (amulet != null)
                        amuletOffers.Add(new ShopOffer { amulet = amulet, price = saved.price, sold = saved.sold });
                }
            }

            if (stock.blessings != null && config != null && config.blessings != null)
            {
                foreach (var saved in stock.blessings)
                {
                    var blessing = config.blessings.Find(b => b != null && b.kind.ToString() == saved.id);
                    if (blessing != null)
                        blessingOffers.Add(new ShopOffer { blessing = blessing, price = saved.price, sold = saved.sold });
                }
            }
        }

        public void SaveStockToRunState()
        {
            var stock = new SavedShopStock();
            foreach (var o in cardOffers)
                if (o.card != null)
                    stock.cards.Add(new SavedShopOffer { id = o.card.cardId, price = o.price, sold = o.sold });

            foreach (var o in amuletOffers)
                if (o.amulet != null)
                    stock.amulets.Add(new SavedShopOffer { id = o.amulet.relicId, price = o.price, sold = o.sold });

            foreach (var o in blessingOffers)
                if (o.blessing != null)
                    stock.blessings.Add(new SavedShopOffer { id = o.blessing.kind.ToString(), price = o.price, sold = o.sold });

            RunState.Current.SetShopStock(stock);
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

        public void BuyBlessing(ShopOffer offer)
        {
            if (!TryPay(offer)) return;
            var blessing = offer.blessing;
            RunState.Current.RecordBlessingPurchase(blessing.kind);
            if (blessing.kind == ShopBlessingKind.MaxMerit)
            {
                RunState.Current.ChangeMaxMerit(blessing.amount);
                Thank($"กุศลสูงสุด +{blessing.amount} (เป็น {RunState.Current.MaxMerit})");
            }
            else
            {
                RunState.Current.ChangeMaxHp(blessing.amount);
                Thank($"ขวัญสูงสุด +{blessing.amount} (เป็น {RunState.Current.MaxHp})");
            }
        }

        public int GetBlessingPrice(ShopBlessing blessing)
        {
            if (blessing == null) return 0;
            return blessing.GetPrice(RunState.Current.GetBlessingPurchases(blessing.kind));
        }

        public void Leave()
        {
            RunState.Current.ClearShopStock();
            RunState.Current.ClearResume();
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
            SaveStockToRunState();
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
