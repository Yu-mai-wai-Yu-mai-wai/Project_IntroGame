using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.CardEngine
{
    /// <summary>
    /// Upgraded cards (a card that is another card's CardDataSO.upgradedCard, made at the เมรุ) smoulder: the fire
    /// of a dying card (same glow and char colours, from CardDeathFxConfigSO) burning along the card's frame line:
    /// a flickering glowing line scorched on both sides, with embers drifting up (Resources/Shaders/CardEmber, where
    /// the frame line is placed with _FrameInset / _FrameCorner). On the 3D card (CardView3D) and on every card screen (detail, deck,
    /// graveyard, card book, rewards).
    /// </summary>
    public static class CardEmber
    {
        public const string ShaderResource = "Shaders/CardEmber";
        private const string UiChildName = "Ember";
        // The sheet's size against the card (same centre; the shader's _Pad). 1: the fire stays on the card
        public const float Pad = 1f;
        private static readonly int PadId = Shader.PropertyToID("_Pad");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        // The ragged pattern's seed is the shader's _Seed (CardEmber.shader): another number = another pattern in the
        // same place. On: every card also gets its own pattern from its card id, the same on every screen.
        public static bool SeedPerCard = true;

        /// <summary>The seed for this card's pattern: the shader's _Seed, plus a number from its card id when SeedPerCard.</summary>
        public static float SeedFor(CardDataSO card)
        {
            float seed = Material != null ? Material.GetFloat(SeedId) : 0f;
            if (!SeedPerCard || card == null || string.IsNullOrEmpty(card.cardId)) return seed;
            return seed + StableHash(card.cardId) % 997 * 0.731f;
        }

        public static float SeedFor(CardInstance card)
        {
            return SeedFor(card != null ? card.source : null);
        }

        // The same number for the same text in every session and build (string.GetHashCode is not)
        private static uint StableHash(string text)
        {
            uint hash = 2166136261u;
            foreach (char c in text) hash = (hash ^ c) * 16777619u;
            return hash;
        }
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int CharColorId = Shader.PropertyToID("_CharColor");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");

        private static Material material;

        // TEST ONLY: every card smoulders, to look at the effect. Set back to false before the build.
        public static bool AllCardsForTest = false;

        public static bool IsUpgraded(CardDataSO card)
        {
            if (AllCardsForTest && card != null) return true;
            var catalog = CardCatalogSO.Load();
            return card != null && catalog != null && catalog.IsUpgradedVersion(card);
        }

        public static bool IsUpgraded(CardInstance card)
        {
            return card != null && IsUpgraded(card.source);
        }

        /// <summary>The shared material, in the dying-card fire's colours; null when the shader is missing.</summary>
        public static Material Material
        {
            get
            {
                if (material != null) return material;
                var shader = Resources.Load<Shader>(ShaderResource);
                if (shader == null) return null;
                material = new Material(shader) { name = "CardEmber" };
                material.SetFloat(PadId, Pad);
                var fire = TawanOS.VFX.CardDeathFxConfigSO.Load();
                if (fire != null)
                {
                    material.SetColor(GlowColorId, fire.glowColor);
                    material.SetColor(CharColorId, fire.charColor);
                }
                return material;
            }
        }

        /// <summary>
        /// Shows or hides the smouldering edge on a UI card face: an Image named "Ember" laid over <paramref name="over"/> (the
        /// face's frame or picture, same rect) and under everything after it (the text).
        /// </summary>
        public static void SetUI(Graphic over, bool on, CardDataSO card = null)
        {
            if (over == null) return;
            var parent = over.transform.parent;
            var existing = FindSibling(parent);
            if (!on || Material == null)
            {
                if (existing != null) existing.gameObject.SetActive(false);
                return;
            }

            Image sheet = existing;
            if (sheet == null)
            {
                var go = new GameObject(UiChildName, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                sheet = go.GetComponent<Image>();
                sheet.raycastTarget = false;
                sheet.material = new Material(Material);
            }
            sheet.gameObject.SetActive(true);

            var from = over.rectTransform;
            var to = sheet.rectTransform;
            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.pivot = from.pivot;
            to.localScale = from.localScale;
            // Same centre, Pad times the size
            Vector2 size = from.rect.size;
            Vector2 grow = size * (Pad - 1f) * 0.5f;
            to.offsetMin = from.offsetMin - grow;
            to.offsetMax = from.offsetMax + grow;
            sheet.transform.SetSiblingIndex(over.transform.GetSiblingIndex() + 1);

            // The card's own shape, so the fire follows its rounded outline
            sheet.material.SetFloat(AspectId, size.y > 0f ? size.x / size.y : 0.716f);
            sheet.material.SetFloat(SeedId, SeedFor(card));
        }

        private static Image FindSibling(Transform parent)
        {
            if (parent == null) return null;
            var child = parent.Find(UiChildName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        /// <summary>The aspect property id, for 3D cards that set it through a property block.</summary>
        public static int AspectProperty => AspectId;
        public static int SeedProperty => SeedId;
    }
}
