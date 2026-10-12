using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TawanOS.VFX
{
    /// <summary>
    /// Burns a 3D board card away, from its centre out to its edges (or from its bottom-right corner to its
    /// top-left corner, CardDeathFxConfigSO.burnFromCentre off): the 3D counterpart of
    /// UiBurnEffect. Every sprite and the card block get a copy of the TawanOS/CardBurnDissolve material sharing
    /// one fire front; text glows and fades as the front reaches it; embers rise from the front.
    /// Runs on its own object so the embers outlive the card. It does not destroy the card: whoever calls
    /// <see cref="Burn"/> removes it after the returned number of seconds.
    /// </summary>
    public class CardBurnEffect : MonoBehaviour
    {
        private static readonly int BurnId = Shader.PropertyToID("_Burn");
        private static readonly int CardRectId = Shader.PropertyToID("_CardRect");
        private static readonly int CardWorldToLocalId = Shader.PropertyToID("_CardWorldToLocal");
        private static readonly int NoiseTexId = Shader.PropertyToID("_NoiseTex");
        private static readonly int NoiseAmountId = Shader.PropertyToID("_NoiseAmount");
        private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int CharColorId = Shader.PropertyToID("_CharColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int FromCentreId = Shader.PropertyToID("_FromCentre");
        private static readonly int FaceSizeId = Shader.PropertyToID("_FaceSize");

        private static Sprite emberSprite;

        private class TextState
        {
            public TMP_Text text;
            public Color original;
            public float along; // 0 = bottom-right corner .. 1 = top-left corner
        }

        private class Ember
        {
            public Transform t;
            public SpriteRenderer sr;
            public Vector3 velocity;
            public float life;
            public float age;
            public float size;
        }

        private Transform card;
        private CardDeathFxConfigSO fx;
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<TextState> texts = new List<TextState>();
        private readonly List<Ember> embers = new List<Ember>();
        private Material emberMaterial;

        // World -> card face: x, y of the result are the face's axes; the rect is the face in those axes
        private int axisU, axisV;
        private Vector4 rect;
        private Vector2 faceSize; // the face's width and height in world units, so the centre burn is round

        private float elapsed;
        private float emberDebt;
        private bool playing;

        /// <summary>
        /// Starts burning <paramref name="card"/> and returns how long it takes (0 when the config has no burn
        /// material: nothing happens and the caller should fall back to its usual death).
        /// </summary>
        public static float Burn(Transform card, CardDeathFxConfigSO fx)
        {
            if (card == null || fx == null || fx.burnMaterial == null) return 0f;
            var go = new GameObject("CardBurn");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, card.gameObject.scene);
            go.AddComponent<CardBurnEffect>().Begin(card, fx);
            return fx.burnDuration;
        }

        private void Begin(Transform target, CardDeathFxConfigSO config)
        {
            card = target;
            fx = config;

            foreach (var r in card.GetComponentsInChildren<Renderer>(false))
            {
                var text = r.GetComponent<TMP_Text>();
                if (text != null)
                {
                    texts.Add(new TextState { text = text, original = text.color });
                    continue;
                }
                if (!(r is SpriteRenderer) && !(r is MeshRenderer)) continue;

                var original = r.sharedMaterial;
                var mat = new Material(fx.burnMaterial) { name = fx.burnMaterial.name + " (Burn)" };
                mat.SetTexture(NoiseTexId, UiBurnEffect.NoiseTexture());
                mat.SetColor(GlowColorId, fx.glowColor);
                mat.SetColor(CharColorId, fx.charColor);
                mat.SetFloat(BurnId, 0f);

                if (r is SpriteRenderer)
                {
                    if (emberMaterial == null) emberMaterial = original; // the sprites' own material suits the embers
                }
                else
                {
                    // The card block: keep its colour (and texture, if any) and draw it behind the face sprites
                    if (original != null)
                    {
                        if (original.HasProperty("_BaseColor")) mat.SetColor(ColorId, original.GetColor("_BaseColor"));
                        else if (original.HasProperty(ColorId)) mat.SetColor(ColorId, original.GetColor(ColorId));
                        if (original.mainTexture != null) mat.SetTexture(MainTexId, original.mainTexture);
                    }
                    r.sortingOrder = -1;
                }

                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                renderers.Add(r);
                materials.Add(mat);
            }

            FindFace();
            foreach (var s in texts) s.along = AlongOf(s.text.transform.TransformPoint(s.text.bounds.center));
            playing = true;
            Apply(0f);
        }

        // The card face is the plane of the two longest axes of the card's sprites, in the card's local space.
        // Only the sprites count when there are any: the card block is a unit cube in the card's (non-uniformly
        // scaled) local space, so it is as "thick" there as it is wide and would hide which axis is the face.
        private void FindFace()
        {
            bool hasSprites = renderers.Exists(r => r is SpriteRenderer sr && sr.sprite != null);
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = -min;
            bool any = false;
            foreach (var r in renderers)
            {
                Bounds local;
                if (r is SpriteRenderer sr && sr.sprite != null) local = sr.sprite.bounds;
                else if (!hasSprites && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null) local = mf.sharedMesh.bounds;
                else continue;

                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1) * 2f - Vector3.one);
                    Vector3 p = card.InverseTransformPoint(r.transform.TransformPoint(corner));
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                    any = true;
                }
            }
            if (!any)
            {
                min = new Vector3(-0.5f, -0.5f, -0.5f);
                max = -min;
            }

            // Thinnest local axis = the card's thickness; the other two (in order) are right and up
            Vector3 scale = card.lossyScale;
            Vector3 size = new Vector3(Mathf.Abs((max.x - min.x) * scale.x), Mathf.Abs((max.y - min.y) * scale.y), Mathf.Abs((max.z - min.z) * scale.z));
            int thin = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
            axisU = thin == 0 ? 1 : 0;
            axisV = thin == 2 ? 1 : 2;
            rect = new Vector4(min[axisU], min[axisV], max[axisU], max[axisV]);
            faceSize = new Vector2(Mathf.Max(0.0001f, size[axisU]), Mathf.Max(0.0001f, size[axisV]));
        }

        // World -> card space, with the face axes moved to x and y for the shader
        private Matrix4x4 WorldToFace()
        {
            Matrix4x4 m = card.worldToLocalMatrix;
            var face = Matrix4x4.identity;
            face.SetRow(0, m.GetRow(axisU));
            face.SetRow(1, m.GetRow(axisV));
            face.SetRow(2, Vector4.zero);
            face.SetRow(3, m.GetRow(3));
            return face;
        }

        private float AlongOf(Vector3 worldPoint)
        {
            Vector3 p = card.InverseTransformPoint(worldPoint);
            float u = Mathf.InverseLerp(rect.x, rect.z, p[axisU]);
            float v = Mathf.InverseLerp(rect.y, rect.w, p[axisV]);
            return AlongOf(u, v);
        }

        // The order the fire reaches a point on the face (u, v 0..1), 0 first .. 1 last; see the shader
        private float AlongOf(float u, float v)
        {
            if (!fx.burnFromCentre) return ((1f - u) + v) * 0.5f;
            var fromCentre = new Vector2((u - 0.5f) * faceSize.x, (v - 0.5f) * faceSize.y);
            return fromCentre.magnitude / (0.5f * faceSize).magnitude;
        }

        private Vector3 FacePoint(float u, float v)
        {
            var p = Vector3.zero;
            p[axisU] = Mathf.Lerp(rect.x, rect.z, u);
            p[axisV] = Mathf.Lerp(rect.y, rect.w, v);
            return card.TransformPoint(p);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            UpdateEmbers(dt);

            if (playing)
            {
                if (card == null)
                {
                    playing = false;
                }
                else
                {
                    elapsed += dt;
                    float progress = Mathf.Clamp01(elapsed / fx.burnDuration);
                    Apply(progress);
                    SpawnEmbers(FrontAt(progress), progress, dt);
                    if (progress >= 1f)
                    {
                        playing = false;
                        foreach (var r in renderers) if (r != null) r.enabled = false;
                        foreach (var s in texts) if (s.text != null) s.text.enabled = false;
                    }
                }
            }

            if (!playing && embers.Count == 0) Destroy(gameObject);
        }

        private void Apply(float progress)
        {
            var toFace = WorldToFace();
            foreach (var m in materials)
            {
                m.SetFloat(BurnId, progress);
                m.SetMatrix(CardWorldToLocalId, toFace);
                m.SetVector(CardRectId, rect);
                m.SetFloat(FromCentreId, fx.burnFromCentre ? 1f : 0f);
                m.SetVector(FaceSizeId, faceSize);
            }

            float front = FrontAt(progress);
            foreach (var s in texts)
            {
                if (s.text == null) continue;
                // 0 = untouched .. 1 = burnt: glows orange as the fire arrives, then is gone
                float burnt = Mathf.Clamp01((front - s.along + 0.05f) / 0.12f);
                Color glow = fx.glowColor;
                Color c = Color.Lerp(s.original, new Color(glow.r, glow.g * 0.9f, glow.b, s.original.a), Mathf.Clamp01(burnt * 2f));
                c.a = s.original.a * (1f - Mathf.Clamp01(burnt * 1.4f - 0.4f));
                s.text.color = c;
            }
        }

        // The fire front in the shader's terms (see CardBurnDissolve.shader)
        private float FrontAt(float progress)
        {
            var m = fx.burnMaterial;
            float noise = m.HasProperty(NoiseAmountId) ? m.GetFloat(NoiseAmountId) : 0.25f;
            float edge = m.HasProperty(EdgeWidthId) ? m.GetFloat(EdgeWidthId) : 0.08f;
            return Mathf.Lerp(-noise * 0.5f - edge, 1f + noise * 0.5f + edge, progress);
        }

        private void SpawnEmbers(float front, float progress, float dt)
        {
            if (fx.embersPerSecond <= 0f || progress <= 0f || progress >= 0.98f) return;

            emberDebt += fx.embersPerSecond * dt;
            while (emberDebt >= 1f)
            {
                emberDebt -= 1f;

                // A random point on the front line, inside the card
                for (int tries = 0; tries < 6; tries++)
                {
                    float u, v;
                    if (fx.burnFromCentre)
                    {
                        // A ring `front` of the way from the centre to the corners
                        Vector2 dir = Random.insideUnitCircle.normalized;
                        Vector2 offset = dir * (front * (0.5f * faceSize).magnitude);
                        u = 0.5f + offset.x / faceSize.x;
                        v = 0.5f + offset.y / faceSize.y;
                    }
                    else
                    {
                        // ((1 - u) + v) / 2 == front
                        u = Random.value;
                        v = 2f * front - 1f + u;
                    }
                    if (u < 0f || u > 1f || v < 0f || v > 1f) continue;
                    SpawnEmber(FacePoint(u, v));
                    break;
                }
            }
        }

        private void SpawnEmber(Vector3 position)
        {
            var go = new GameObject("Ember");
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = EmberSprite();
            if (emberMaterial != null) sr.sharedMaterial = emberMaterial;
            sr.sortingOrder = 10; // over the cards
            sr.color = Color.Lerp(new Color(1f, 0.45f, 0.1f, 1f), new Color(1f, 0.85f, 0.4f, 1f), Random.value);

            float size = Random.Range(fx.emberSize.x, fx.emberSize.y);
            go.transform.localScale = Vector3.one * size;
            embers.Add(new Ember
            {
                t = go.transform,
                sr = sr,
                size = size,
                velocity = new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(fx.emberRiseSpeed.x, fx.emberRiseSpeed.y), Random.Range(-0.25f, 0.25f)),
                life = Random.Range(0.5f, 1.1f),
            });
        }

        private void UpdateEmbers(float dt)
        {
            var cam = Camera.main;
            for (int i = embers.Count - 1; i >= 0; i--)
            {
                var e = embers[i];
                e.age += dt;
                if (e.t == null || e.age >= e.life)
                {
                    if (e.t != null) Destroy(e.t.gameObject);
                    embers.RemoveAt(i);
                    continue;
                }
                // Flicker sideways and slow down a little
                e.velocity += new Vector3(Mathf.Sin(e.age * 9f) * 0.8f, -0.4f, Mathf.Cos(e.age * 7f) * 0.5f) * dt;
                e.t.position += e.velocity * dt;
                if (cam != null) e.t.rotation = cam.transform.rotation; // face the camera
                float k = e.age / e.life;
                var c = e.sr.color;
                c.a = 1f - k * k;
                e.sr.color = c;
                e.t.localScale = Vector3.one * (e.size * (1f - 0.6f * k));
            }
        }

        private void OnDestroy()
        {
            foreach (var m in materials) if (m != null) Destroy(m);
        }

        // A soft round dot, made once
        private static Sprite EmberSprite()
        {
            if (emberSprite != null) return emberSprite;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Ember",
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float a = Mathf.Clamp01(1f - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            emberSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            emberSprite.hideFlags = HideFlags.DontSave;
            return emberSprite;
        }
    }
}
