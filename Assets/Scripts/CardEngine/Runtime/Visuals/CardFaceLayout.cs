using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace TawanOS.CardEngine
{
    // Where each piece of text sits on the card face, measured from the card design (939 x 1312) and
    // shared by the in-game 3D card (CardView3D) and the Card Data inspector preview.
    // Positions are fractions of the face: x -0.5 (left) .. 0.5 (right), y 0.5 (top) .. -0.5 (bottom).
    // Sizes are fractions of the face width / height.
    public static class CardFaceLayout
    {
        public struct Box
        {
            public Vector2 center;
            public Vector2 size;

            public Box(float x, float y, float w, float h)
            {
                center = new Vector2(x, y);
                size = new Vector2(w, h);
            }

            // Same centre, size times m (bigger text needs a bigger box)
            public Box Scaled(float m)
            {
                return new Box(center.x, center.y, size.x * m, size.y * m);
            }
        }

        // The texts on the card face whose size a Card Data can change
        public enum Text { Name, Type, Cost, Stat, Description }

        // The Card Data's size for that text (fontSizeAll x the text's own size); 1 without a Card Data
        // Attack (สะเทือนขวัญ) and khwan are drawn this much bigger than their box on every card, on top of each
        // card's own statFontSize
        public const float StatTextScale = 1.5f;

        public static float FontScale(CardDataSO card, Text text)
        {
            if (card == null) return text == Text.Stat ? StatTextScale : 1f;
            float own = text switch
            {
                Text.Name => card.nameFontSize,
                Text.Type => card.typeFontSize,
                Text.Cost => card.costFontSize,
                Text.Stat => card.statFontSize * StatTextScale,
                _ => card.descriptionFontSize,
            };
            return card.fontSizeAll * own;
        }

        public static readonly Box Cost = new Box(-0.374f, 0.397f, 0.16f, 0.1f);          // circle, top-left
        public static readonly Box Name = new Box(0.225f, 0.365f, 0.44f, 0.075f);          // top-right, above the divider
        public static readonly Box Type = new Box(0.225f, 0.3f, 0.44f, 0.035f);            // under the divider
        public static readonly Box Attack = new Box(-0.106f, -0.114f, 0.1f, 0.055f);       // left badge (skull)
        public static readonly Box Khwan = new Box(0.105f, -0.114f, 0.1f, 0.055f);         // right badge (blood drop)
        public static readonly Box Description = new Box(0f, -0.29f, 0.78f, 0.22f);        // bottom box
        public static readonly Box Artwork = new Box(0f, 0f, 1f, 1f);                      // the picture fills the whole face; the frame is see-through and drawn over it
        public static readonly Box Statuses = new Box(-0.04f, -0.01f, 0.62f, 0.07f);          // status tags over the picture's lower edge, just above the attack / Khwan badges

        // Text now sits on the full-face artwork, so every face label gets a dark outline to stay readable on bright art.
        // Set a label's outline only through here: outlineWidth outside Play Mode copies (and leaks) its material.
        public static void MakeReadable(TMP_Text text, float width = 0.22f)
        {
            if (text == null) return;
            if (Application.isPlaying)
            {
                text.outlineWidth = width;
                text.outlineColor = new Color32(0, 0, 0, 255);
            }
            else
            {
                // Edit-mode preview: outlineWidth would make TextMeshPro copy the label's material on every refresh
                // (and leak it into the scene), so the labels share one outlined copy of the font's material instead
                var shared = EditModeOutline(text.font, width);
                if (shared != null && text.fontSharedMaterial != shared) text.fontSharedMaterial = shared;
            }
            PlaceThaiMarks(text);
        }

        // One outlined copy per font material and outline width
        private static readonly Dictionary<(Material, float), Material> editModeOutlines = new Dictionary<(Material, float), Material>();

        private static Material EditModeOutline(TMP_FontAsset font, float width)
        {
            if (font == null || font.material == null) return null;
            if (editModeOutlines.TryGetValue((font.material, width), out var m) && m != null) return m;
            m = new Material(font.material) { name = font.material.name + " (Outline Preview)", hideFlags = HideFlags.DontSave };
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            m.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            editModeOutlines[(font.material, width)] = m;
            return m;
        }

        // Thai vowels above/below and tone marks sit where the font puts them (its mark / mark-to-mark positioning)
        // instead of sinking into the letter under them; the labels only had kerning on
        public static void PlaceThaiMarks(TMP_Text text)
        {
            if (text == null) return;
            var features = text.fontFeatures;
            if (features.Contains(OTL_FeatureTag.mark) && features.Contains(OTL_FeatureTag.mkmk)) return;
            text.fontFeatures = new List<OTL_FeatureTag> { OTL_FeatureTag.kern, OTL_FeatureTag.mark, OTL_FeatureTag.mkmk };
        }

        // The frame used for a card with no Card Background of its own
        public static Sprite DefaultFrame(Sprite whiteFrame, Sprite blackFrame, MagicSchool school)
        {
            return school == MagicSchool.WhiteMagic ? whiteFrame : blackFrame;
        }

        // The bound artwork (Art/Cards/FramedCard, see CardArtBinder) is a finished face with its own frame, name and
        // type line printed on it: no frame is drawn over it, and only cost, attack, khwan and the ability text are
        // written on it.
        public static bool ArtCarriesFrame(Sprite artwork)
        {
            return artwork != null;
        }

        // What goes behind / over the artwork: the printed card PNG, else nothing when the artwork has its own frame,
        // else the card's background or the default frame of its school
        public static Sprite FaceFrame(Sprite cardImage, Sprite cardBackground, Sprite artwork, Sprite whiteFrame, Sprite blackFrame, MagicSchool school)
        {
            if (cardImage != null) return cardImage;
            if (ArtCarriesFrame(artwork)) return null;
            return cardBackground != null ? cardBackground : DefaultFrame(whiteFrame, blackFrame, school);
        }
    }
}
