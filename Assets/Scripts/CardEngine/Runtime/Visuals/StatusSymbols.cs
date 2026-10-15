using UnityEngine;

namespace TawanOS.CardEngine
{
    // The pictures that show a card's statuses at a glance (CardView3D): a shield for เกราะคุ้มภัย, a cross over the
    // ability text for ผีบังตา, a glowing frame for ยั่วยุ, an arrow down by the attack for ผวา. Drawn once in code
    // (no art files): white shapes with a black edge, tinted per status by the SpriteRenderer, so they read on any art.
    public static class StatusSymbols
    {
        private const int Size = 128;
        private static Sprite shield, cross, frame, arrow;

        public static Sprite Shield => shield != null ? shield : shield = Make(ShieldShape, 0.06f);
        public static Sprite Cross => cross != null ? cross : cross = Make(CrossShape, 0.05f);
        public static Sprite Arrow => arrow != null ? arrow : arrow = Make(ArrowShape, 0.07f);
        public static Sprite Frame => frame != null ? frame : frame = MakeFrame();

        // Signed distance-like test per shape: > 0 inside. `edge` is how wide the black rim is (in 0..1 of the picture).
        private delegate float Shape(float x, float y);

        private static Sprite Make(Shape inside, float edge)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "StatusSymbol" };
            var px = new Color32[Size * Size];
            float aa = 1.5f / Size;
            for (int j = 0; j < Size; j++)
            {
                for (int i = 0; i < Size; i++)
                {
                    float x = (i + 0.5f) / Size, y = (j + 0.5f) / Size;
                    float d = inside(x, y);                       // > 0 inside the shape
                    float outer = Mathf.Clamp01(d / aa + 0.5f);    // the whole shape, rim included
                    float fill = Mathf.Clamp01((d - edge) / aa + 0.5f); // the white part inside the rim
                    byte v = (byte)(255f * fill);
                    px[j * Size + i] = new Color32(v, v, v, (byte)(255f * outer));
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

        // An arrow pointing up (turned over for down): a shaft and a triangular head
        private static float ArrowShape(float x, float y)
        {
            float cx = Mathf.Abs(x - 0.5f);
            float shaft = Mathf.Min(0.11f - cx, Mathf.Min(y - 0.06f, 0.62f - y));
            float head = Mathf.Min(0.94f - y - cx * 1.1f, y - 0.5f);
            return Mathf.Max(shaft, Mathf.Min(head, 0.4f - cx));
        }

        // A glowing border along the picture's edge, soft on the inside (stretched over the whole card)
        private static Sprite MakeFrame()
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "StatusFrame" };
            var px = new Color32[Size * Size];
            for (int j = 0; j < Size; j++)
            {
                for (int i = 0; i < Size; i++)
                {
                    float x = (i + 0.5f) / Size, y = (j + 0.5f) / Size;
                    float d = Mathf.Min(Mathf.Min(x, 1f - x), Mathf.Min(y, 1f - y)); // distance to the edge
                    float a = d < 0.018f ? 1f : Mathf.Clamp01(1f - (d - 0.018f) / 0.05f) * 0.45f;
                    px[j * Size + i] = new Color32(255, 255, 255, (byte)(255f * a));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }
    }
}
