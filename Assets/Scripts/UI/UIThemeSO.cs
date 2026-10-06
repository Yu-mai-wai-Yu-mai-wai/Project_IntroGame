using TMPro;
using UnityEngine;

namespace TawanOS.UI
{
    /// <summary>
    /// The game's CI palette, Thai fonts and text sizes in one place (plan task G1). Every runtime UI reads from
    /// here instead of hard-coding colors. Loaded from Resources/UITheme.asset; when the asset is missing a
    /// default instance with the CI colors is used (without fonts, so Thai would not render: create the asset
    /// with Tools/TawanOS/UI/Create UI Theme).
    ///
    /// Colors were read from the artist's swatch images. Crimson on black is only 1.5:1, so crimson is for
    /// fills and borders, never for text on a dark background.
    /// </summary>
    [CreateAssetMenu(fileName = "UITheme", menuName = "TawanOS/UI/Theme")]
    public class UIThemeSO : ScriptableObject
    {
        public const string ResourceName = "UITheme";

        [Header("CI palette")]
        public Color black = Hex("#110E0E");     // scene and screen background
        public Color panel = Hex("#2A2222");     // panels
        public Color crimson = Hex("#572121");   // fills, borders, bars (not text)
        public Color text = Hex("#C2BEBE");     // main text
        public Color accent = Hex("#F0AD7B");   // highlights, focus, important numbers

        [Header("Fonts (Thai)")]
        public TMP_FontAsset bodyFont;
        public TMP_FontAsset titleFont;

        [Header("Text sizes at 1920x1080 reference (no text smaller than labelSize)")]
        public float bodySize = 24f;
        public float labelSize = 20f;
        public float titleSize = 36f;
        public float numberSize = 32f;

        private static UIThemeSO cached;

        public static UIThemeSO Current
        {
            get
            {
                if (cached == null) cached = Resources.Load<UIThemeSO>(ResourceName);
                if (cached == null)
                {
                    cached = CreateInstance<UIThemeSO>();
                    cached.hideFlags = HideFlags.HideAndDontSave;
                }
                return cached;
            }
        }

        public static void ResetCache()
        {
            cached = null;
        }

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }

        // WCAG 2.x contrast ratio between two opaque colors
        public static float Contrast(Color a, Color b)
        {
            float la = Luminance(a);
            float lb = Luminance(b);
            float hi = Mathf.Max(la, lb);
            float lo = Mathf.Min(la, lb);
            return (hi + 0.05f) / (lo + 0.05f);
        }

        private static float Luminance(Color c)
        {
            return 0.2126f * Channel(c.r) + 0.7152f * Channel(c.g) + 0.0722f * Channel(c.b);
        }

        private static float Channel(float v)
        {
            return v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
        }
    }
}
