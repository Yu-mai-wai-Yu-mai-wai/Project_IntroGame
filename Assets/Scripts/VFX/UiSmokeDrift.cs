using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.VFX
{
    /// <summary>
    /// Faint smoke puffs drifting left to right across a UI rect, forever, so a flat background does not look
    /// empty. Each puff is a RawImage of a soft smoke texture that bobs, breathes and turns slowly; one that
    /// leaves the right edge comes back in from the left with a new size, height and strength.
    /// Add it to a full-screen RectTransform placed just above the background image, then call <see cref="Build"/>.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UiSmokeDrift : MonoBehaviour
    {
        public Texture texture;
        [Range(0, 16)] public int count = 7;
        public Vector2 alphaRange = new Vector2(0.04f, 0.11f);
        public Vector2 sizeRange = new Vector2(700f, 1300f);
        public Vector2 speedRange = new Vector2(18f, 45f);
        public Color tint = new Color(0.78f, 0.72f, 0.70f, 1f);

        private class Puff
        {
            public RectTransform rt;
            public RawImage image;
            public float speed;
            public float baseY;
            public float bobAmplitude;
            public float bobSpeed;
            public float spin;
            public float phase;
            public float alpha;
            public float width;
        }

        private readonly List<Puff> puffs = new List<Puff>();
        private RectTransform area;

        /// <summary>(Re)creates the puffs, spread over the whole area so the screen opens with smoke already in it.</summary>
        public void Build()
        {
            area = (RectTransform)transform;
            foreach (var p in puffs) if (p.rt != null) Destroy(p.rt.gameObject);
            puffs.Clear();
            if (texture == null) return;

            Rect r = area.rect;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Smoke", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(transform, false);
                var puff = new Puff { rt = (RectTransform)go.transform, image = go.GetComponent<RawImage>() };
                puff.image.texture = texture;
                puff.image.raycastTarget = false;

                // Evenly spaced start positions with some jitter, so the first frame is not clumped
                float x = Mathf.Lerp(r.xMin, r.xMax, (i + Random.Range(0.1f, 0.9f)) / Mathf.Max(1, count));
                Respawn(puff, x);
                puffs.Add(puff);
            }
        }

        private void Respawn(Puff p, float x)
        {
            Rect r = area.rect;
            p.width = Random.Range(sizeRange.x, sizeRange.y);
            p.rt.sizeDelta = new Vector2(p.width, p.width * Random.Range(0.45f, 0.7f));
            p.baseY = Random.Range(r.yMin + r.height * 0.1f, r.yMax - r.height * 0.1f);
            p.speed = Random.Range(speedRange.x, speedRange.y);
            p.bobAmplitude = Random.Range(10f, 40f);
            p.bobSpeed = Random.Range(0.15f, 0.35f);
            p.spin = Random.Range(-3f, 3f);
            p.phase = Random.Range(0f, Mathf.PI * 2f);
            p.alpha = Random.Range(alphaRange.x, alphaRange.y);
            p.rt.anchoredPosition = new Vector2(x, p.baseY);
            p.rt.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));
            p.image.color = new Color(tint.r, tint.g, tint.b, p.alpha);
        }

        private void Update()
        {
            if (area == null || puffs.Count == 0) return;

            Rect r = area.rect;
            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;
            foreach (var p in puffs)
            {
                Vector2 pos = p.rt.anchoredPosition;
                pos.x += p.speed * dt;
                pos.y = p.baseY + Mathf.Sin(t * p.bobSpeed + p.phase) * p.bobAmplitude;

                // Fully past the right edge: come back in from the left as a fresh puff
                if (pos.x - p.width * 0.5f > r.xMax)
                {
                    Respawn(p, r.xMin - p.width * 0.5f);
                    continue;
                }
                p.rt.anchoredPosition = pos;
                p.rt.Rotate(0f, 0f, p.spin * dt);

                // Slow breathing in strength, so the smoke looks alive
                float breathe = 0.75f + 0.25f * Mathf.Sin(t * p.bobSpeed * 1.7f + p.phase * 2f);
                p.image.color = new Color(tint.r, tint.g, tint.b, p.alpha * breathe);
            }
        }
    }
}
