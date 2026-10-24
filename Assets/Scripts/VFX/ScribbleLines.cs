using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.VFX
{
    /// <summary>
    /// Hand-drawn zigzag "rage lines" that boil in place (Game Designer, 25 Oct, the defeat art): every stroke stays
    /// where it is, but its corners are redrawn a little off every <see cref="boilSeconds"/>, like an animated cartoon
    /// where an angry character has the lines shaking round its head. Each stroke is drawn twice, slightly apart,
    /// for a sketchy pencil look. Strokes are lists of points in the rect's own 0..1 space (x right, y up).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class ScribbleLines : MaskableGraphic
    {
        [Tooltip("Thickness where a stroke starts, next to the character (reference pixels).")]
        public float startThickness = 4f;
        [Tooltip("Thickness at the far end of a stroke (Game Designer, 25 Oct: thin at the body, wide at the tip).")]
        public float endThickness = 20f;
        [Tooltip("The character's middle (0..1 of the rect): each stroke starts at its end nearer to this.")]
        public Vector2 origin = new Vector2(0.53f, 0.70f);
        [Tooltip("How far a corner wanders each redraw, as a fraction of the rect's height.")]
        public float jitter = 0.006f;
        [Tooltip("Seconds between redraws (a cartoon boils at 8-12 a second).")]
        public float boilSeconds = 0.09f;
        [Tooltip("The second, sketchier pass sits this far from the first (fraction of the height).")]
        public float doubleOffset = 0.008f;

        public readonly List<Vector2[]> strokes = new List<Vector2[]>();

        private float nextBoil;
        private int seed;

        private void Update()
        {
            if (Time.unscaledTime < nextBoil) return;
            nextBoil = Time.unscaledTime + boilSeconds;
            seed++;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            if (r.width <= 0f || r.height <= 0f) return;
            var random = new System.Random(seed * 7919 + 17);
            float J() => (float)(random.NextDouble() * 2.0 - 1.0) * jitter;

            for (int s = 0; s < strokes.Count; s++)
            {
                var points = strokes[s];
                if (points == null || points.Length < 2) continue;
                for (int pass = 0; pass < 2; pass++)
                {
                    var offset = pass == 0 ? Vector2.zero : new Vector2(doubleOffset * 0.6f, -doubleOffset);
                    float scale = pass == 0 ? 1f : 0.5f;
                    // From the end nearer the character outward
                    bool reverse = (points[points.Length - 1] - origin).sqrMagnitude < (points[0] - origin).sqrMagnitude;
                    var placed = new Vector2[points.Length];
                    for (int i = 0; i < points.Length; i++)
                    {
                        var p = points[reverse ? points.Length - 1 - i : i] + offset + new Vector2(J() * r.height / r.width, J());
                        placed[i] = new Vector2(r.xMin + p.x * r.width, r.yMin + p.y * r.height);
                    }
                    float total = 0f;
                    for (int i = 0; i < placed.Length - 1; i++) total += Vector2.Distance(placed[i], placed[i + 1]);
                    float run = 0f;
                    for (int i = 0; i < placed.Length - 1; i++)
                    {
                        float len = Vector2.Distance(placed[i], placed[i + 1]);
                        float wa = Mathf.Lerp(startThickness, endThickness, total > 0f ? run / total : 0f) * scale;
                        float wb = Mathf.Lerp(startThickness, endThickness, total > 0f ? (run + len) / total : 1f) * scale;
                        Segment(vh, placed[i], placed[i + 1], wa, wb);
                        run += len;
                    }
                }
            }
        }

        // A quad along the segment, `wa` wide at a and `wb` at b, a little longer at both ends so the corners join
        private void Segment(VertexHelper vh, Vector2 a, Vector2 b, float wa, float wb)
        {
            var dir = b - a;
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();
            var perp = new Vector2(-dir.y, dir.x);
            var na = perp * (wa * 0.5f);
            var nb = perp * (wb * 0.5f);
            a -= dir * wa * 0.4f;
            b += dir * wb * 0.4f;
            int i = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.color = color;
            v.position = a - na; vh.AddVert(v);
            v.position = a + na; vh.AddVert(v);
            v.position = b + nb; vh.AddVert(v);
            v.position = b - nb; vh.AddVert(v);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
