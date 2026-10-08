using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.VFX
{
    /// <summary>
    /// Burns a UI card away, starting at its bottom-right corner and finishing at its top-left corner.
    /// Every Image / RawImage under the card gets a copy of the TawanOS/UI/BurnDissolve material sharing one
    /// fire front; text under the card glows and fades as the front reaches it; embers rise from the front.
    /// Add it to the card's root RectTransform and call <see cref="Play"/>; <see cref="Restore"/> puts the card
    /// back exactly as it was (for screens that reuse their cards).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UiBurnEffect : MonoBehaviour
    {
        private static readonly int BurnId = Shader.PropertyToID("_Burn");
        private static readonly int CardRectId = Shader.PropertyToID("_CardRect");
        private static readonly int NoiseTexId = Shader.PropertyToID("_NoiseTex");
        private static readonly int NoiseAmountId = Shader.PropertyToID("_NoiseAmount");
        private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int CharColorId = Shader.PropertyToID("_CharColor");

        private static Texture2D noiseTexture;

        private class TextState
        {
            public TMP_Text text;
            public Color original;
            public float along; // 0 = bottom-right corner .. 1 = top-left corner
        }

        private class Ember
        {
            public RectTransform rt;
            public Graphic graphic;
            public Vector2 velocity;
            public float life;
            public float age;
        }

        private RectTransform card;
        private Material material;
        private readonly List<(Graphic graphic, Material original)> graphics = new List<(Graphic, Material)>();
        private readonly List<TextState> texts = new List<TextState>();
        private readonly List<Ember> embers = new List<Ember>();
        private RectTransform emberLayer;
        private Texture emberTexture;
        private Color glow;
        private float duration;
        private float embersPerSecond;
        private float elapsed;
        private float emberDebt;
        private bool playing;
        private Action onDone;

        public bool IsPlaying => playing;

        /// <summary>
        /// Starts the burn. <paramref name="burnMaterial"/> must use TawanOS/UI/BurnDissolve; without it the card
        /// simply fades out. <paramref name="emberTex"/> is a soft round texture for the embers (may be null).
        /// </summary>
        public void Play(Material burnMaterial, float seconds, float embersPerSec, Color glowColor, Color charColor,
            Texture emberTex, Action done = null)
        {
            Restore();
            card = (RectTransform)transform;
            duration = Mathf.Max(0.05f, seconds);
            embersPerSecond = embersPerSec;
            glow = glowColor;
            emberTexture = emberTex;
            onDone = done;
            elapsed = 0f;
            emberDebt = 0f;

            if (burnMaterial != null)
            {
                material = new Material(burnMaterial) { name = burnMaterial.name + " (Burn)" };
                material.SetTexture(NoiseTexId, NoiseTexture());
                material.SetColor(GlowColorId, glowColor);
                material.SetColor(CharColorId, charColor);
                material.SetFloat(BurnId, 0f);

                foreach (var g in GetComponentsInChildren<Graphic>(false))
                {
                    if (g is TMP_Text) continue;
                    graphics.Add((g, g.material == g.defaultMaterial ? null : g.material));
                    g.material = material;
                }
            }

            foreach (var t in GetComponentsInChildren<TMP_Text>(false))
            {
                texts.Add(new TextState { text = t, original = t.color, along = AlongOf(t.rectTransform) });
            }

            var layerGo = new GameObject("Embers", typeof(RectTransform));
            emberLayer = (RectTransform)layerGo.transform;
            emberLayer.SetParent(card, false);
            emberLayer.anchorMin = Vector2.zero;
            emberLayer.anchorMax = Vector2.one;
            emberLayer.offsetMin = emberLayer.offsetMax = Vector2.zero;
            emberLayer.SetAsLastSibling();

            playing = true;
            UpdateCardRect();
        }

        /// <summary>Puts the card back as it was before <see cref="Play"/>.</summary>
        public void Restore()
        {
            playing = false;
            foreach (var (g, original) in graphics)
            {
                if (g != null) g.material = original;
            }
            graphics.Clear();
            foreach (var s in texts)
            {
                if (s.text != null) s.text.color = s.original;
            }
            texts.Clear();
            embers.Clear();
            if (emberLayer != null) Destroy(emberLayer.gameObject);
            emberLayer = null;
            if (material != null) Destroy(material);
            material = null;
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateEmbers(dt);
            if (!playing) return;

            elapsed += dt;
            float progress = Mathf.Clamp01(elapsed / duration);
            UpdateCardRect();
            if (material != null) material.SetFloat(BurnId, progress);

            float front = FrontAt(progress);
            foreach (var s in texts)
            {
                if (s.text == null) continue;
                // 0 = untouched .. 1 = burnt: glows orange as the fire arrives, then is gone
                float burnt = Mathf.Clamp01((front - s.along + 0.05f) / 0.12f);
                Color c = Color.Lerp(s.original, new Color(glow.r, glow.g * 0.9f, glow.b, s.original.a), Mathf.Clamp01(burnt * 2f));
                c.a = s.original.a * (1f - Mathf.Clamp01(burnt * 1.4f - 0.4f));
                s.text.color = c;
            }

            if (material == null)
            {
                // No burn shader available: fade the whole card instead
                foreach (var g in GetComponentsInChildren<Graphic>(false))
                {
                    if (g is TMP_Text) continue;
                    var c = g.color;
                    c.a = 1f - progress;
                    g.color = c;
                }
            }

            SpawnEmbers(front, progress, dt);

            if (progress >= 1f)
            {
                playing = false;
                var done = onDone;
                onDone = null;
                done?.Invoke();
            }
        }

        // The fire front in the shader's terms (see UIBurnDissolve.shader)
        private float FrontAt(float progress)
        {
            float noise = material != null ? material.GetFloat(NoiseAmountId) : 0.25f;
            float edge = material != null ? material.GetFloat(EdgeWidthId) : 0.08f;
            return Mathf.Lerp(-noise * 0.5f - edge, 1f + noise * 0.5f + edge, progress);
        }

        private void UpdateCardRect()
        {
            if (material == null || card == null) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            Transform space = canvas.rootCanvas.transform;

            var corners = new Vector3[4];
            card.GetWorldCorners(corners); // bottom-left, top-left, top-right, bottom-right
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var w in corners)
            {
                Vector2 p = space.InverseTransformPoint(w);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            material.SetVector(CardRectId, new Vector4(min.x, min.y, max.x, max.y));
        }

        // Where a child sits on the fire's path: 0 at the card's bottom-right corner, 1 at its top-left corner
        private float AlongOf(RectTransform child)
        {
            Vector3 world = child.TransformPoint(child.rect.center);
            Vector2 local = card.InverseTransformPoint(world);
            Rect r = card.rect;
            float u = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
            float v = Mathf.InverseLerp(r.yMin, r.yMax, local.y);
            return ((1f - u) + v) * 0.5f;
        }

        private void SpawnEmbers(float front, float progress, float dt)
        {
            if (emberLayer == null || embersPerSecond <= 0f || progress <= 0f || progress >= 0.98f) return;

            emberDebt += embersPerSecond * dt;
            Rect r = emberLayer.rect;
            while (emberDebt >= 1f)
            {
                emberDebt -= 1f;

                // A random point on the front line: ((1 - u) + v) / 2 == front
                for (int tries = 0; tries < 6; tries++)
                {
                    float u = UnityEngine.Random.value;
                    float v = 2f * front - 1f + u;
                    if (v < 0f || v > 1f) continue;
                    SpawnEmber(new Vector2(Mathf.Lerp(r.xMin, r.xMax, u), Mathf.Lerp(r.yMin, r.yMax, v)));
                    break;
                }
            }
        }

        private void SpawnEmber(Vector2 position)
        {
            var go = new GameObject("Ember", typeof(RectTransform));
            go.transform.SetParent(emberLayer, false);
            var rt = (RectTransform)go.transform;
            float size = UnityEngine.Random.Range(6f, 16f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = position;

            Graphic graphic;
            if (emberTexture != null)
            {
                var raw = go.AddComponent<RawImage>();
                raw.texture = emberTexture;
                graphic = raw;
            }
            else
            {
                graphic = go.AddComponent<Image>();
            }
            graphic.raycastTarget = false;
            float hot = UnityEngine.Random.value;
            graphic.color = Color.Lerp(new Color(1f, 0.45f, 0.1f, 1f), new Color(1f, 0.85f, 0.4f, 1f), hot);

            embers.Add(new Ember
            {
                rt = rt,
                graphic = graphic,
                velocity = new Vector2(UnityEngine.Random.Range(-40f, 10f), UnityEngine.Random.Range(60f, 140f)),
                life = UnityEngine.Random.Range(0.5f, 1.1f),
            });
        }

        private void UpdateEmbers(float dt)
        {
            for (int i = embers.Count - 1; i >= 0; i--)
            {
                var e = embers[i];
                e.age += dt;
                if (e.rt == null || e.age >= e.life)
                {
                    if (e.rt != null) Destroy(e.rt.gameObject);
                    embers.RemoveAt(i);
                    continue;
                }
                e.velocity += new Vector2(Mathf.Sin(e.age * 9f) * 30f, -20f) * dt; // flicker, slight slow-down
                e.rt.anchoredPosition += e.velocity * dt;
                float k = e.age / e.life;
                var c = e.graphic.color;
                c.a = 1f - k * k;
                e.graphic.color = c;
                e.rt.localScale = Vector3.one * (1f - 0.6f * k);
            }
        }

        // Tileable fractal noise for the ragged fire edge, made once
        private static Texture2D NoiseTexture()
        {
            if (noiseTexture != null) return noiseTexture;

            const int size = 128;
            noiseTexture = new Texture2D(size, size, TextureFormat.R8, false)
            {
                name = "BurnNoise",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[size * size];
            float seed = UnityEngine.Random.Range(0f, 100f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = 0f, amp = 0.5f, freq = 4f, total = 0f;
                    for (int o = 0; o < 4; o++)
                    {
                        n += TileablePerlin(x / (float)size, y / (float)size, freq, seed + o * 17f) * amp;
                        total += amp;
                        amp *= 0.5f;
                        freq *= 2f;
                    }
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(n / total * 255f), 0, 255);
                    pixels[y * size + x] = new Color32(b, b, b, 255);
                }
            }
            noiseTexture.SetPixels32(pixels);
            noiseTexture.Apply(false, true);
            return noiseTexture;
        }

        // Perlin noise that wraps at the texture edge, by blending the four wrapped samples
        private static float TileablePerlin(float u, float v, float freq, float seed)
        {
            float a = Mathf.PerlinNoise(seed + u * freq, seed + v * freq);
            float b = Mathf.PerlinNoise(seed + (u - 1f) * freq, seed + v * freq);
            float c = Mathf.PerlinNoise(seed + u * freq, seed + (v - 1f) * freq);
            float d = Mathf.PerlinNoise(seed + (u - 1f) * freq, seed + (v - 1f) * freq);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }
    }
}
