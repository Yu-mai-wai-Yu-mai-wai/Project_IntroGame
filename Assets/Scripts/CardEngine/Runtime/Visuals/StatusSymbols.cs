using UnityEngine;

namespace TawanOS.CardEngine
{
    // The icons that show a card's statuses (CardView3D puts them in a row from the card's bottom-right corner):
    // a round dark badge with a coloured rim and a picture of what the status does. Drawn once in code (no art files),
    // in the colours of the UI theme, so they read on any card art.
    public static class StatusSymbols
    {
        private const int Size = 128;

        private static Sprite protect, taunt, blinded, phawa, curse, hourglass, uses;

        // An amulet's turns left: an hourglass in sand gold (not a status; CardView3D draws it on amulets on the board)
        public static Sprite Hourglass => hourglass != null ? hourglass : hourglass = MakeBadge(HourglassShape, new Color(0.95f, 0.78f, 0.45f));
        // The uses left on an amulet that breaks after a few: a power (shutdown) symbol, in blood red, beside the hourglass
        public static Sprite Uses => uses != null ? uses : uses = MakeBadge(PowerShape, new Color(0.9f, 0.32f, 0.28f));

        // เกราะคุ้มภัย: a shield (the theme's accent pushed to gold)
        public static Sprite Protect => protect != null ? protect : protect = MakeBadge(ShieldShape, new Color(0.97f, 0.7f, 0.36f));
        // ยั่วยุ: a target (attacks are drawn to it)
        public static Sprite Taunt => taunt != null ? taunt : taunt = MakeBadge(TargetShape, new Color(0.95f, 0.47f, 0.28f));
        // ผีบังตา: a cross (its ability does nothing)
        public static Sprite Blinded => blinded != null ? blinded : blinded = MakeBadge(CrossShape, new Color(0.85f, 0.2f, 0.2f));
        // ผวา: an arrow down (its attack drops)
        public static Sprite Phawa => phawa != null ? phawa : phawa = MakeBadge(ArrowDownShape, new Color(0.74f, 0.6f, 0.86f));
        // สาป: a skull in bone white (Game Designer, 25 Oct); its stacks hang under it as a number (CardView3D)
        public static Sprite Curse => curse != null ? curse : curse = MakeBadge(SkullShape, new Color(0.93f, 0.86f, 0.74f));

        public static Sprite For(CardStatusType type)
        {
            switch (type)
            {
                case CardStatusType.Protect: return Protect;
                case CardStatusType.Taunt: return Taunt;
                case CardStatusType.Blinded: return Blinded;
                case CardStatusType.Phawa: return Phawa;
                case CardStatusType.DoneKhong: return Curse;
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

        // A skull: a round cranium over a square jaw, two eye holes, a nose hole and gaps between the teeth
        private static float SkullShape(float x, float y)
        {
            float cranium = 0.31f - Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.6f) * (y - 0.6f));
            float jaw = Mathf.Min(Mathf.Min(x - 0.33f, 0.67f - x), Mathf.Min(y - 0.16f, 0.45f - y));
            float body = Mathf.Max(cranium, jaw);

            float eyeL = 0.09f - Mathf.Sqrt((x - 0.385f) * (x - 0.385f) + (y - 0.57f) * (y - 0.57f));
            float eyeR = 0.09f - Mathf.Sqrt((x - 0.615f) * (x - 0.615f) + (y - 0.57f) * (y - 0.57f));
            float nose = 0.04f - Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.42f) * (y - 0.42f));
            float teeth = -1f;
            if (y < 0.31f)
                foreach (float gap in new[] { 0.415f, 0.5f, 0.585f }) teeth = Mathf.Max(teeth, 0.014f - Mathf.Abs(x - gap));
            float holes = Mathf.Max(Mathf.Max(eyeL, eyeR), Mathf.Max(nose, teeth));
            return Mathf.Min(body, -holes);
        }

        // An hourglass: two triangles meeting at the middle, a bar across the top and the bottom
        private static float HourglassShape(float x, float y)
        {
            float cx = Mathf.Abs(x - 0.5f), cy = Mathf.Abs(y - 0.5f);
            float glass = Mathf.Min(0.05f + cy * 0.85f - cx, 0.38f - cy);
            float bars = Mathf.Min(0.36f - cx, 0.05f - Mathf.Abs(cy - 0.4f));
            return Mathf.Max(glass, bars);
        }

        // A power (shutdown) symbol: a ring broken at the top with an upright bar through the gap
        private static float PowerShape(float x, float y)
        {
            float dx = x - 0.5f, dy = y - 0.46f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            // The gap: the ring is cut where it points within about 40 degrees of straight up
            float gap = dy > 0f ? (Mathf.Abs(dx) / Mathf.Max(r, 0.0001f) - 0.62f) * 0.4f : 1f;
            float ring = Mathf.Min(0.065f - Mathf.Abs(r - 0.32f), gap);
            float bar = Mathf.Min(0.065f - Mathf.Abs(dx), Mathf.Min(y - 0.5f, 0.92f - y));
            return Mathf.Max(ring, bar);
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
