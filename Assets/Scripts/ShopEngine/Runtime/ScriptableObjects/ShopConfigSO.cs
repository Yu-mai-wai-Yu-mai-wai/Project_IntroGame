using System;
using System.Collections.Generic;
using TawanOS.CardEngine;
using UnityEngine;

namespace TawanOS.ShopEngine
{
    /// <summary>An amulet the spirit house can offer. Granted to the run as a relic id.</summary>
    [Serializable]
    public class ShopAmulet
    {
        public string relicId;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;
        [Min(0)] public int price = 60;
    }

    public enum ShopBlessingKind { MaxKhwan, MaxMerit }

    /// <summary>A blessing from the guardian spirit: raises the run's max khwan or max กุศล. Base price + step * times bought.</summary>
    [Serializable]
    public class ShopBlessing
    {
        public ShopBlessingKind kind;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;
        [Min(1)] public int amount = 1;
        [Min(0)] public int price = 8;
        [Min(0)] public int priceStep = 0;

        public int BasePrice => price;
        public int GetPrice(int timesBought = 0) => price + priceStep * Mathf.Max(0, timesBought);
    }

    /// <summary>
    /// Tuning for the spirit-house shop (ศาลตายาย): what is for sale, prices in incense (ธูป)
    /// and what the guardian spirit says.
    /// </summary>
    [CreateAssetMenu(fileName = "ShopConfig", menuName = "TawanOS/ShopEngine/Shop Config")]
    public class ShopConfigSO : ScriptableObject
    {
        [Header("Spirit House")]
        public string shrineName = "ศาลตายาย";
        public string guardianName = "เจ้าที่";
        public Sprite shrineImage;
        [TextArea(1, 3)] public List<string> greetingLines = new List<string>();
        [TextArea(1, 3)] public List<string> thanksLines = new List<string>();
        [TextArea(1, 3)] public List<string> notEnoughIncenseLines = new List<string>();

        [Header("Cards (from Resources/CardCatalog)")]
        [Tooltip("Seeds the run deck if the player reaches the shop before any combat.")]
        public DeckConfigSO starterDeck;
        [Min(0)] public int cardsForSale = 5;
        // Card prices are set per card (CardDataSO.shopPrice) and never vary
        [Tooltip("How far an amulet's price may vary each visit, either way.")]
        [Range(0f, 0.5f)] public float priceVariance = 0.15f;

        [Header("Card Shelf (how cards look and move)")]
        [Tooltip("The real card face (same as the reward screen). Made by Tools > TawanOS > Shop Engine > Create Shop Card Template. Empty = the old item tiles.")]
        public TawanOS.GameFlow.RewardCardView cardFaceTemplate;
        [Tooltip("Shown on the deck pile at the bottom of the screen. Empty = Resources/RewardFx card back.")]
        public Sprite deckPileSprite;
        [Tooltip("Seconds a bought card lifts up before flying into the deck.")]
        [Min(0f)] public float buyLiftDuration = 0.18f;
        [Tooltip("Seconds a bought card takes to fly down into the deck pile.")]
        [Min(0.05f)] public float buyFlyDuration = 0.5f;
        [Tooltip("Card size when it reaches the deck (1 = shelf size).")]
        [Range(0.05f, 1f)] public float buyEndScale = 0.3f;

        [Header("Amulets")]
        public List<ShopAmulet> amulets = new List<ShopAmulet>();
        [Min(0)] public int amuletsForSale = 3;

        [Header("Blessings (shown on the amulet shelf)")]
        public List<ShopBlessing> blessings = new List<ShopBlessing>
        {
            new ShopBlessing { kind = ShopBlessingKind.MaxKhwan, displayName = "พรขวัญ", description = "ขวัญสูงสุด +5", amount = 5, price = 8, priceStep = 4 },
            new ShopBlessing { kind = ShopBlessingKind.MaxMerit, displayName = "พรกุศล", description = "กุศลสูงสุด +1", amount = 1, price = 15, priceStep = 8 },
        };

        public int RollBlessingPrice(ShopBlessing blessing, int timesBought = 0)
        {
            return blessing != null ? blessing.GetPrice(timesBought) : 0;
        }

        // No services (Game Designer, 11 Oct): the shop sells cards only. Khwan is healed at the วัด node,
        // and cards leave the deck at the เมรุ.

        // A card's price is its own shopPrice, set card by card in its Card Data and the same every visit
        // (Game Designer, 15 Oct). A card without one is priced from its casting cost the same way the starting
        // prices were made (CardPriceSetupTool): 1-2 for the cheapest, 8 for the dearest.
        public int RollCardPrice(CardDataSO card)
        {
            if (card == null) return 1;
            if (card.shopPrice > 0) return card.shopPrice;
            return PriceFromCost(card);
        }

        /// <summary>
        /// Starting price of a card from its casting cost (กุศล for white magic, มลทิน for black): cost 1 = 1, 2 = 3,
        /// 3 = 4, 4 = 6, 5 = 8; a familiar or amulet with 1 khwan is a ธูป cheaper, a familiar that hits harder than
        /// its cost a ธูป dearer; never under 1 or over 8.
        /// </summary>
        public static int PriceFromCost(CardDataSO card)
        {
            int cost = card.magicSchool == MagicSchool.WhiteMagic ? card.meritCost : card.corruptionGain;
            int[] byCost = { 1, 1, 3, 4, 6, 8 };
            int price = byCost[Mathf.Clamp(cost, 0, byCost.Length - 1)];
            bool onBoard = card.cardType == CardType.Familiar || card.cardType == CardType.Amulet;
            if (onBoard && card.familiarHealth <= 1) price--;
            if (card.cardType == CardType.Familiar && card.familiarDamage > cost) price++;
            return Mathf.Clamp(price, 1, 8);
        }

        public int Vary(int price)
        {
            float factor = 1f + UnityEngine.Random.Range(-priceVariance, priceVariance);
            return Mathf.Max(1, Mathf.RoundToInt(price * factor));
        }

        public static string PickLine(List<string> lines, string fallback)
        {
            return lines != null && lines.Count > 0 ? lines[UnityEngine.Random.Range(0, lines.Count)] : fallback;
        }
    }
}
