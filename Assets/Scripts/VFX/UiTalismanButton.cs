using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TawanOS.VFX
{
    /// <summary>
    /// Dresses a button as an old paper ยันต์: yellowed, stained paper with torn edges, a red-ink border of double
    /// lines with diamonds between them and a seal at each end, the label in black ink (Game Designer, 24 Oct: the
    /// end-turn button). The paper and the glow are drawn in code once and shared. Hover: the ink glows red behind
    /// the paper, the label turns red and the paper lifts a little; press: it dips; disabled: it greys and fades.
    /// The button keeps its click area; its own panel is hidden.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class UiTalismanButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [Tooltip("Label (ink) colour at rest.")]
        public Color labelNormalColor = new Color(0.16f, 0.06f, 0.04f);
        [Tooltip("Label colour while the cursor is over the button.")]
        public Color labelHoverColor = new Color(0.62f, 0.05f, 0.03f);
        [Tooltip("Glow behind the paper while the cursor is over the button.")]
        public Color glowColor = new Color(1f, 0.22f, 0.12f, 0.85f);
        [Tooltip("Faint warm glow behind the paper at rest, so it stands out from the dark table (Game Designer, 24 Oct).")]
        public Color restGlowColor = new Color(1f, 0.75f, 0.4f, 0.22f);
        [Tooltip("Paper tint while the button cannot be pressed (the enemy's turn).")]
        public Color disabledTint = new Color(0.55f, 0.52f, 0.5f, 0.55f);
        [Range(1f, 1.2f)] public float hoverScale = 1.06f;
        [Min(0.01f)] public float fadeTime = 0.12f;

        private const int TexW = 640, TexH = 168;
        private static Texture2D paperTex, glowTex;
        private static Sprite paperSprite, glowSprite;

        private Button button;
        private Image paper, glow;
        private TMP_Text label;
        private RectTransform paperRt;
        private bool hovered, focused, pressed;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (button.targetGraphic is Image panel) panel.color = Color.clear; // stays the click area
            button.transition = Selectable.Transition.None;
            EnsureTextures();

            glow = MakeImage("TalismanGlow", glowSprite, new Vector2(-0.22f, -0.8f), new Vector2(1.22f, 1.8f));
            glow.color = Color.clear;
            paper = MakeImage("TalismanPaper", paperSprite, new Vector2(-0.04f, -0.16f), new Vector2(1.04f, 1.16f));
            paperRt = paper.rectTransform;
            glow.transform.SetAsFirstSibling();
            paper.transform.SetSiblingIndex(1); // behind the label, over the glow

            label = GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.color = labelNormalColor;
                // The label rides on the paper so it lifts and dips with it
                label.transform.SetParent(paperRt, true);
            }
        }

        private Image MakeImage(string name, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        private void Update()
        {
            if (paper == null) return;
            bool on = button.interactable;
            bool lit = on && (hovered || focused);
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / fadeTime);

            paper.color = Color.Lerp(paper.color, on ? Color.white : disabledTint, k);
            Color glowTarget = lit ? glowColor : on ? restGlowColor : new Color(restGlowColor.r, restGlowColor.g, restGlowColor.b, 0f);
            glow.color = Color.Lerp(glow.color, glowTarget, k);
            // A faint flicker in the glow, like the ink is alive
            if (lit) glow.color = new Color(glow.color.r, glow.color.g, glow.color.b,
                glow.color.a * (0.85f + 0.15f * Mathf.PerlinNoise(Time.unscaledTime * 3f, 0.3f)));
            if (label != null)
            {
                Color ink = lit ? labelHoverColor : labelNormalColor;
                if (!on) ink.a = 0.55f;
                label.color = Color.Lerp(label.color, ink, k);
            }

            float scale = pressed && lit ? 0.97f : lit ? hoverScale : 1f;
            paperRt.localScale = Vector3.Lerp(paperRt.localScale, Vector3.one * scale, k);
            float tilt = lit ? -1.2f : 0f;
            paperRt.localRotation = Quaternion.Slerp(paperRt.localRotation, Quaternion.Euler(0f, 0f, tilt), k);
        }

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData eventData) => pressed = true;
        public void OnPointerUp(PointerEventData eventData) => pressed = false;
        // Keyboard focus lights it like the cursor (the button transition is None)
        public void OnSelect(BaseEventData eventData) => focused = true;
        public void OnDeselect(BaseEventData eventData) => focused = false;

        // ---------------------------------------------------------------------------------------------------------
        // The paper and the glow, drawn once

        private static void EnsureTextures()
        {
            if (paperSprite != null && glowSprite != null) return;
            paperTex = DrawPaper();
            paperSprite = Sprite.Create(paperTex, new Rect(0, 0, TexW, TexH), new Vector2(0.5f, 0.5f), 100f);
            glowTex = DrawGlow(128, 64);
            glowSprite = Sprite.Create(glowTex, new Rect(0, 0, glowTex.width, glowTex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Texture2D NewTexture(int w, int h, string name)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave
            };
        }

        private static float Fbm(float x, float y)
        {
            return 0.55f * Mathf.PerlinNoise(x, y) + 0.3f * Mathf.PerlinNoise(x * 2.1f + 7.3f, y * 2.1f + 1.7f)
                 + 0.15f * Mathf.PerlinNoise(x * 4.3f + 3.1f, y * 4.3f + 9.4f);
        }

        private static Texture2D DrawPaper()
        {
            var tex = NewTexture(TexW, TexH, "TalismanPaper");
            var px = new Color32[TexW * TexH];
            var paperLight = new Color(0.97f, 0.91f, 0.70f);
            var paperDark = new Color(0.80f, 0.67f, 0.44f);
            var burn = new Color(0.30f, 0.18f, 0.08f);
            var ink = new Color(0.66f, 0.07f, 0.05f);

            float h = TexH, w = TexW;
            float edge = 10f;          // torn margin
            float outer = edge + 9f;   // outer ink line
            float inner = outer + 13f; // inner ink line
            float seal = h * 0.5f;     // seal centres sit this far in from the ends

            for (int y = 0; y < TexH; y++)
            for (int x = 0; x < TexW; x++)
            {
                // Torn edge: the paper's outline wanders with noise
                float tear = edge * (0.35f + 0.9f * Fbm(x * 0.045f, y * 0.045f + 11f));
                float dEdge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                float alpha = Mathf.Clamp01((dEdge - tear) / 2.5f);
                if (alpha <= 0f) { px[y * TexW + x] = new Color32(0, 0, 0, 0); continue; }

                // Paper: mottled, stained, darker toward the edges (burnt)
                float n = Fbm(x * 0.012f, y * 0.03f);
                float stain = Mathf.SmoothStep(0.55f, 0.75f, Fbm(x * 0.02f + 40f, y * 0.05f + 40f));
                Color c = Color.Lerp(paperDark, paperLight, n);
                c = Color.Lerp(c, paperDark * 0.85f, stain * 0.45f);
                float fibre = Mathf.PerlinNoise(x * 0.9f, y * 0.06f);
                c *= 0.96f + 0.08f * fibre;
                float edgeBurn = 1f - Mathf.Clamp01((dEdge - tear) / 14f);
                c = Color.Lerp(c, burn, edgeBurn * 0.6f);

                // Red ink: double border, diamonds between the lines along the long sides, a seal at each end
                float inkA = 0f;
                float dOuter = Mathf.Abs(RectDist(x, y, w, h, outer));
                float dInner = Mathf.Abs(RectDist(x, y, w, h, inner));
                inkA = Mathf.Max(inkA, Line(dOuter, 2.6f));
                inkA = Mathf.Max(inkA, Line(dInner, 1.3f));

                float midBand = (outer + inner) * 0.5f;
                bool onLongSide = x > inner + seal * 0.9f && x < w - inner - seal * 0.9f;
                if (onLongSide)
                {
                    float period = 22f;
                    float lx = Mathf.Repeat(x - inner, period) - period * 0.5f;
                    foreach (float by in new[] { midBand, h - 1 - midBand })
                    {
                        float diamond = Mathf.Abs(lx) + Mathf.Abs(y - by) * 1.6f;
                        inkA = Mathf.Max(inkA, Line(Mathf.Abs(diamond - 4.5f), 1.1f));
                    }
                }

                foreach (float sx in new[] { inner + seal * 0.55f, w - 1 - inner - seal * 0.55f })
                    inkA = Mathf.Max(inkA, Seal(x - sx, y - h * 0.5f, seal * 0.42f));

                // Divider lines between each seal and the label
                foreach (float lx2 in new[] { inner + seal * 1.15f, w - 1 - inner - seal * 1.15f })
                    if (y > inner && y < h - 1 - inner) inkA = Mathf.Max(inkA, Line(Mathf.Abs(x - lx2), 1.2f));

                // Hand-drawn ink: patchy where it soaked in less
                inkA *= Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0.3f, 0.6f, Fbm(x * 0.08f + 5f, y * 0.08f)));
                c = Color.Lerp(c, ink, inkA);

                c.a = alpha;
                px[y * TexW + x] = c;
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        // Signed distance (pixels) from (x, y) to the rectangle inset by `inset` from the texture's edges
        private static float RectDist(float x, float y, float w, float h, float inset)
        {
            float dx = Mathf.Max(inset - x, x - (w - 1 - inset));
            float dy = Mathf.Max(inset - y, y - (h - 1 - inset));
            return Mathf.Max(dx, dy) <= 0f ? Mathf.Max(dx, dy)
                : new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
        }

        // Ink coverage of a stroke of half-width `half`, at distance d from its centre line
        private static float Line(float d, float half) => Mathf.Clamp01(half + 0.8f - d);

        // A seal: a ring, a smaller ring, a cross through them and four dots, like the yantra marks on a ผ้ายันต์
        private static float Seal(float x, float y, float r)
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float a = Line(Mathf.Abs(d - r), 1.6f);
            a = Mathf.Max(a, Line(Mathf.Abs(d - r * 0.55f), 1.1f));
            if (d < r) a = Mathf.Max(a, Mathf.Max(Line(Mathf.Abs(x), 1f), Line(Mathf.Abs(y), 1f)));
            a = Mathf.Max(a, Line(d, 2.4f)); // centre dot
            float k = r * 0.78f;
            foreach (var p in new[] { new Vector2(k, k), new Vector2(-k, k), new Vector2(k, -k), new Vector2(-k, -k) })
                a = Mathf.Max(a, Line(new Vector2(x - p.x * 0.72f, y - p.y * 0.72f).magnitude, 1.6f));
            return a;
        }

        private static Texture2D DrawGlow(int w, int h)
        {
            var tex = NewTexture(w, h, "TalismanGlow");
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                // A soft glow: round at the ends, fading out smoothly toward every edge
                float du = Mathf.Max(Mathf.Abs(u) * 1.3f - 0.3f, 0f);
                float d = Mathf.Sqrt(du * du + v * v);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * a;
                px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
