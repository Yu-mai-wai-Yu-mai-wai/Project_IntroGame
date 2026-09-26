using UnityEngine;

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
        public static float FontScale(CardDataSO card, Text text)
        {
            if (card == null) return 1f;
            float own = text switch
            {
                Text.Name => card.nameFontSize,
                Text.Type => card.typeFontSize,
                Text.Cost => card.costFontSize,
                Text.Stat => card.statFontSize,
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
        public static readonly Box Artwork = new Box(0f, 0.16f, 0.84f, 0.54f);             // picture window

        // The frame used for a card with no Card Background of its own
        public static Sprite DefaultFrame(Sprite whiteFrame, Sprite blackFrame, MagicSchool school)
        {
            return school == MagicSchool.WhiteMagic ? whiteFrame : blackFrame;
        }
    }
}
