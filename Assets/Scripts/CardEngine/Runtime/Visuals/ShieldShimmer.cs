using UnityEngine;

namespace TawanOS.CardEngine
{
    // The เกราะคุ้มภัย shield on a card (CardView3D adds it next to the shield picture). It runs on its own because a
    // card on the board turns its CardView3D off: the shield breathes between see-through and bright, its halo pulses
    // with it, and a ring flashes out from it and fades. Cards are offset in time so they do not flash together.
    public class ShieldShimmer : MonoBehaviour
    {
        public static readonly Color Base = new Color(0.97f, 0.7f, 0.36f);   // the UI theme's accent (#F0AD7B) pushed towards gold
        public static readonly Color Flash = new Color(1f, 0.92f, 0.8f);
        public const float GlowScale = 1.4f;

        public SpriteRenderer shield, glow, ripple;
        private float offset;

        private void Awake() => offset = (GetInstanceID() & 0xFF) * 0.037f;

        private void LateUpdate()
        {
            if (shield == null) return;
            float t = Time.time + offset;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4.5f);
            var c = Color.Lerp(Base, Flash, pulse * 0.6f);
            c.a = 0.5f + 0.35f * pulse;
            shield.color = c;

            if (glow != null)
            {
                var g = Base;
                g.a = 0.3f + 0.45f * pulse;
                glow.color = g;
            }
            if (ripple != null)
            {
                float cycle = Mathf.Repeat(t, 1.4f) / 1.4f;
                var r = Flash;
                r.a = 0.8f * (1f - cycle) * (1f - cycle);
                ripple.color = r;
                var s = shield.transform.localScale * (GlowScale * (1f + 0.45f * cycle));
                ripple.transform.localScale = new Vector3(s.x, s.y, 1f);
            }
        }
    }
}
