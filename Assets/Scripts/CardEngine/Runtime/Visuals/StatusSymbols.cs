using UnityEngine;

namespace TawanOS.CardEngine
{
    // The icons that show a card's statuses (CardView3D puts them in a row from the card's bottom-right corner):
    // a round dark badge with a coloured rim and a picture of what the status does. Drawn once in code (no art files),
    // in the colours of the UI theme, so they read on any card art.
    public static class StatusSymbols
    {
        private const int Size = 128;

        private static Sprite protect, taunt, blinded, phawa;

        // เกราะคุ้มภัย: a shield (the theme's accent pushed to gold)
        public static Sprite Protect => protect != null ? protect : protect = MakeBadge(ShieldShape, new Color(0.97f, 0.7f, 0.36f));
        // ยั่วยุ: a target (attacks are drawn to it)
        public static Sprite Taunt => taunt != null ? taunt : taunt = MakeBadge(TargetShape, new Color(0.95f, 0.47f, 0.28f));
        // ผีบังตา: a cross (its ability does nothing)
        public static Sprite Blinded => blinded != null ? blinded : blinded = MakeBadge(CrossShape, new Color(0.85f, 0.2f, 0.2f));
        // ผวา: an arrow down (its attack drops)
        public static Sprite Phawa => phawa != null ? phawa : phawa = MakeBadge(ArrowDownShape, new Color(0.74f, 0.6f, 0.86f));

        public static Sprite For(CardStatusType type)
        {
            switch (type)
            {
                case CardStatusType.Protect: return Protect;
                case CardStatusType.Taunt: return Taunt;
                case CardStatusType.Blinded: return Blinded;
                case CardStatusType.Phawa: return Phawa;
                default: return null;
            }
        }

        // Signed distance-like test per shape: > 0 inside, in 0..1 of the icon's own square
        private delegate float Shape(float x, float y);

        private static readonly Color BadgeFill = new Color(0.07f, 0.05f, 0.05f, 0.92f); // the theme's black

        // A filled circle with a rim in `colour`, and the icon in `colour` inside it (the icon uses the middle 60%)
        private static Sprite MakeBadge(Shape icon, Color colour)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "StatusBadge" };
            var px = new Color32[Size * Size];
            float aa = 1.5f / Size;
            for (int j = 0; j < Size; j++)
            {
                for (int i = 0; i < Size; i++)
                {
                    float x = (i + 0.5f) / Size, y = (j + 0.5f) / Size;
                    float r = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f));
                    float disc = Mathf.Clamp01((0.48f - r) / aa + 0.5f);          // the whole badge
                    float rim = Mathf.Clamp01((r - 0.41f) / aa + 0.5f);           // the coloured ring at its edge
                    float inIcon = Mathf.Clamp01(icon(0.5f + (x - 0.5f) / 0.6f, 0.5f + (y - 0.5f) / 0.6f) / (aa / 0.6f) + 0.5f);

                    var c = Color.Lerp(BadgeFill, colour, Mathf.Max(rim, inIcon));
                    c.a = disc * (rim > 0.5f || inIcon > 0.5f ? 1f : BadgeFill.a);
                    px[j * Size + i] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }

        // A heater shield: straight top, sides curving down to a point
        private static float ShieldShape(float x, float y)
        {
            float cx = x - 0.5f;
            float top = 0.92f - y;
            float halfWidth = y > 0.55f ? 0.40f : 0.40f * Mathf.Sqrt(Mathf.Max(0f, (y - 0.06f) / 0.49f));
            float side = halfWidth - Mathf.Abs(cx);
            return Mathf.Min(top, side, y - 0.06f);
        }

        // Two thick diagonal bars
        private static float CrossShape(float x, float y)
        {
            float a = Mathf.Abs((x - 0.5f) - (y - 0.5f)) * 0.7071f;
            float b = Mathf.Abs((x - 0.5f) + (y - 0.5f)) * 0.7071f;
            float reach = 0.46f - Mathf.Max(Mathf.Abs(x - 0.5f), Mathf.Abs(y - 0.5f));
            return Mathf.Min(0.11f - Mathf.Min(a, b), reach);
        }

        // A ring with a dot in the middle
        private static float TargetShape(float x, float y)
        {
            float r = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f));
            float ring = 0.09f - Mathf.Abs(r - 0.36f);
            float dot = 0.14f - r;
            return Mathf.Max(ring, dot);
        }

        // An arrow pointing down: a shaft and a triangular head
        private static float ArrowDownShape(float x, float y)
        {
            y = 1f - y;
            float cx = Mathf.Abs(x - 0.5f);
            float shaft = Mathf.Min(0.11f - cx, Mathf.Min(y - 0.06f, 0.62f - y));
            float head = Mathf.Min(0.94f - y - cx * 1.1f, y - 0.5f);
            return Mathf.Max(shaft, Mathf.Min(head, 0.4f - cx));
        }
    }
}
