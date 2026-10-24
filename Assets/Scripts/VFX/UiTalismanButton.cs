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
    /// the paper and the label turns red, nothing moves (Game Designer, 24 Oct); disabled: it greys and fades.
    /// The button keeps its click area; its own panel is hidden. The paper is 9-sliced, so the seals stay round on a
    /// button of any width, and the label is kept between them. <see cref="Dress"/> turns an existing button into one.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class UiTalismanButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [Tooltip("Label (ink) colour at rest.")]
        public Color labelNormalColor = new Color(0.08f, 0.03f, 0.02f);
        [Tooltip("Label colour while the cursor is over the button.")]
        public Color labelHoverColor = new Color(0.62f, 0.05f, 0.03f);
        [Tooltip("Glow behind the paper while the cursor is over the button.")]
        public Color glowColor = new Color(1f, 0.22f, 0.12f, 0.85f);
        [Tooltip("Faint warm glow behind the paper at rest, so it stands out from the dark table (Game Designer, 24 Oct).")]
        public Color restGlowColor = new Color(1f, 0.75f, 0.4f, 0.22f);
        [Tooltip("Paper tint at rest; under the pointer it brightens to its full colour. White = the paper as drawn.")]
        public Color paperTint = Color.white;
        [Tooltip("Paper tint while the button cannot be pressed (the enemy's turn).")]
        public Color disabledTint = new Color(0.55f, 0.52f, 0.5f, 0.55f);
        [Min(0.01f)] public float fadeTime = 0.12f;
        [Tooltip("Keep the label between the two seals (its anchors are set to fill the button, less the seals).")]
        public bool fitLabel = true;

        [Header("Shaky ink")]
        [Tooltip("How far each letter wanders, in canvas units, like ink drawn by a shaking hand (Game Designer, 24 Oct). 0 = still.")]
        [Range(0f, 3f)] public float inkShake = 0.9f;
        [Tooltip("How many times a second the letters take a new place (stepped, like hand-drawn animation).")]
        [Range(1f, 30f)] public float inkShakeRate = 10f;

        [Header("Drift")]
        [Tooltip("How far the paper drifts up and down, in canvas units, as if it hung in still air (Game Designer, 24 Oct). " +
                 "It never tilts. 0 = still.")]
        [Range(0f, 8f)] public float floatAmount = 2.5f;
        [Tooltip("Seconds for one drift up and down.")]
        [Min(0.5f)] public float floatSeconds = 3.2f;

        private const int TexW = 640, TexH = 168;
        // The slices: the ends (the seals and their dividers) and the top and bottom borders keep their shape
        private const float SliceX = 136f, SliceY = 36f;
        private static Texture2D paperTex, glowTex;
        private static Sprite paperSprite, glowSprite;

        private Button button;
        private Image paper, glow;
        private TMP_Text label;
        private bool hovered, focused;
        private Vector2 lastSize;
        private Vector2 bob;          // the drift applied to the paper, glow and label right now
        private float bobPhase = -1f; // each talisman drifts out of step with the others

        /// <summary>Turns an existing button into a ยันต์ (its smoke, if it had one, goes). Safe to call again.</summary>
        public static UiTalismanButton Dress(Button button)
        {
            if (button == null) return null;
            var talisman = button.GetComponent<UiTalismanButton>();
            if (talisman != null) return talisman;
            var smoke = button.GetComponent<UiSmokeButton>();
            if (smoke != null)
            {
                var smokeImage = button.transform.Find("Smoke");
                if (smokeImage != null) DestroyImmediate(smokeImage.gameObject);
                DestroyImmediate(smoke);
            }
            foreach (var effect in button.GetComponents<Shadow>()) DestroyImmediate(effect); // Outline is a Shadow

            // A label inside an inner panel (a border round a dark inner, as in the settings): the panels between it and
            // the button would cover the paper, so they go see-through and the label moves up onto the button itself
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null && label.transform.parent != button.transform)
            {
                for (var t = label.transform.parent; t != null && t != button.transform; t = t.parent)
                {
                    var panel = t.GetComponent<Graphic>();
                    if (panel != null) panel.color = Color.clear;
                    foreach (var effect in t.GetComponents<Shadow>()) DestroyImmediate(effect);
                }
                label.transform.SetParent(button.transform, false);
            }
            return button.gameObject.AddComponent<UiTalismanButton>();
        }

        private void Awake()
        {
            button = GetComponent<Button>();
            if (button.targetGraphic is Image panel) panel.color = Color.clear; // stays the click area
            button.transition = Selectable.Transition.None;
            EnsureTextures();

            glow = MakeImage("TalismanGlow", glowSprite, new Vector2(-0.22f, -0.8f), new Vector2(1.22f, 1.8f));
            glow.color = Color.clear;
            paper = MakeImage("TalismanPaper", paperSprite, new Vector2(-0.04f, -0.16f), new Vector2(1.04f, 1.16f));
            paper.type = Image.Type.Sliced;
            glow.transform.SetAsFirstSibling();
            paper.transform.SetSiblingIndex(1); // behind the label, over the glow

            label = GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.color = labelNormalColor;
                // Thin strokes fade into the paper: the heavy title font, in bold (Game Designer, 24 Oct)
                var theme = TawanOS.UI.UIThemeSO.Current;
                if (theme != null && theme.titleFont != null) label.font = theme.titleFont;
                label.fontStyle |= FontStyles.Bold;
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

        // The slices scale with the paper's height, and the label sits between the seals
        private void Fit()
        {
            var size = ((RectTransform)transform).rect.size;
            if (size == lastSize || size.y <= 0f) return;
            lastSize = size;
            var paperSize = paper.rectTransform.rect.size;
            // The paper's texture height fills the paper, unless that would make the seals take more than a fifth of
            // its width each (a tall button): then they are scaled to that
            float scale = Mathf.Min(paperSize.y / TexH, 0.2f * paperSize.x / SliceX);
            var canvas = paper.canvas;
            float referencePpu = canvas != null ? canvas.referencePixelsPerUnit : 100f;
            paper.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.01f, scale) * referencePpu / paperSprite.pixelsPerUnit;
            if (!fitLabel || label == null || label.transform.parent != transform) return;
            float seal = SliceX * scale - size.x * 0.04f;
            var rt = label.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(Mathf.Max(8f, seal), 0f);
            rt.offsetMax = new Vector2(-Mathf.Max(8f, seal), 0f);
            rt.anchoredPosition += bob; // keeps drifting with the paper
            label.alignment = TextAlignmentOptions.Center; // centred between the seals
            label.margin = Vector4.zero;
        }

        private void Update()
        {
            if (paper == null) return;
            Fit();
            Drift();
            bool on = button.interactable;
            bool lit = on && (hovered || focused);
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / fadeTime);

            paper.color = Color.Lerp(paper.color, !on ? disabledTint * paperTint : lit ? Color.white : paperTint, k);
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
        }

        // The paper, its glow and the label bob gently up and down (a little sideways too), never tilting
        private void Drift()
        {
            if (bobPhase < 0f) bobPhase = Random.Range(0f, Mathf.PI * 2f);
            float amount = TawanOS.Settings.GameSettings.ReduceMotion ? 0f : floatAmount;
            float t = Time.unscaledTime * Mathf.PI * 2f / floatSeconds + bobPhase;
            var next = new Vector2(Mathf.Sin(t * 0.61f + 1.7f) * amount * 0.35f, Mathf.Sin(t) * amount);
            var step = next - bob;
            if (step.sqrMagnitude < 0.0001f) return;
            bob = next;
            paper.rectTransform.anchoredPosition += step;
            glow.rectTransform.anchoredPosition += step;
            if (label != null && label.transform.parent == transform) label.rectTransform.anchoredPosition += step;
        }

        // Each letter sits a little off its place, a new place several times a second: ink that will not keep still.
        // Off when Reduce Motion is on.
        private void LateUpdate()
        {
            if (label == null || inkShake <= 0f || !label.gameObject.activeInHierarchy || TawanOS.Settings.GameSettings.ReduceMotion) return;
            label.ForceMeshUpdate();
            var info = label.textInfo;
            float step = Mathf.Floor(Time.unscaledTime * inkShakeRate);
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                var offset = new Vector3(Mathf.PerlinNoise(i * 7.13f, step * 0.73f) - 0.5f,
                                         Mathf.PerlinNoise(i * 3.91f + 40f, step * 0.73f) - 0.5f, 0f) * (2f * inkShake);
                var verts = info.meshInfo[ch.materialReferenceIndex].vertices;
                for (int v = 0; v < 4; v++) verts[ch.vertexIndex + v] += offset;
            }
            label.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) => hovered = false;
        // Keyboard focus lights it like the cursor (the button transition is None)
        public void OnSelect(BaseEventData eventData) => focused = true;
        public void OnDeselect(BaseEventData eventData) => focused = false;

        // ---------------------------------------------------------------------------------------------------------
        // The paper and the glow, drawn once

        private static void EnsureTextures()
        {
            if (paperSprite != null && glowSprite != null) return;
            paperTex = DrawPaper();
            paperSprite = Sprite.Create(paperTex, new Rect(0, 0, TexW, TexH), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(SliceX, SliceY, SliceX, SliceY));
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
