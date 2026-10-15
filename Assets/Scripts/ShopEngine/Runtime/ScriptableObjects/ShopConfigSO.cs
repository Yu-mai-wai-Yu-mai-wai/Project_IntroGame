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

    /// <summary>A blessing from the guardian spirit: raises the run's max khwan or max กุศล. Fixed price, once per visit.</summary>
    [Serializable]
    public class ShopBlessing
    {
        public ShopBlessingKind kind;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;
        [Min(1)] public int amount = 1;
        [Min(0)] public int price = 50;
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
        [Min(0)] public int cardBasePrice = 20;
        [Min(0)] public int cardPricePerMerit = 8;
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
            new ShopBlessing { kind = ShopBlessingKind.MaxKhwan, displayName = "พรขวัญ", description = "ขวัญสูงสุด +3", amount = 3, price = 25 },
            new ShopBlessing { kind = ShopBlessingKind.MaxMerit, displayName = "พรกุศล", description = "กุศลสูงสุด +1", amount = 1, price = 70 },
        };

        // No services (Game Designer, 11 Oct): the shop sells cards only. Khwan is healed at the วัด node,
        // and cards leave the deck at the เมรุ.

        public int RollCardPrice(CardDataSO card)
        {
            int basePrice = cardBasePrice + cardPricePerMerit * card.meritCost;
            return Vary(basePrice);
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
