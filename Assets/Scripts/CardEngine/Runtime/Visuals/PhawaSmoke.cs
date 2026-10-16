using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // ผวา on a card (CardView3D adds it): wisps of pale grey smoke that waver as they sink down the picture and fade,
    // like the card's nerve draining away. More stacks, more wisps. It runs on its own because a card on the board
    // turns its CardView3D off. Positions are fractions of the card face (the cube's local -0.5..0.5 square).
    public class PhawaSmoke : MonoBehaviour
    {
        private static readonly Color Tint = new Color(0.78f, 0.7f, 0.72f); // ghostly grey with a touch of the theme's crimson
        private const float Top = 0.36f, Bottom = -0.1f; // from the top of the picture down past the attack badge
        private const float Period = 2.4f;               // seconds for one wisp to sink all the way
        private const float Width = 0.26f, Height = 0.3f;

        public float faceZ = -0.508f;
        public int sortingOrder = 4;

        private readonly List<SpriteRenderer> wisps = new List<SpriteRenderer>();
        private int count;
        private float offset;

        private void Awake() => offset = (GetInstanceID() & 0xFF) * 0.053f;

        // 1 stack: 3 wisps, then one more per stack up to 6
        public void SetStacks(int stacks)
        {
            count = Mathf.Clamp(2 + stacks, 3, 6);
            while (wisps.Count < count)
            {
                var go = new GameObject("PhawaWisp");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = StatusSymbols.Smoke;
                sr.sortingOrder = sortingOrder;
                wisps.Add(sr);
            }
            for (int i = 0; i < wisps.Count; i++) wisps[i].gameObject.SetActive(i < count);
        }

        private void LateUpdate()
        {
            for (int i = 0; i < count && i < wisps.Count; i++)
            {
                // Each wisp has its own lane and starts at its own point in the cycle
                float lane = count == 1 ? 0f : Mathf.Lerp(-0.27f, 0.27f, i / (count - 1f));
                float t = Time.time + offset + i * (Period / count) * 1.7f;
                float cycle = Mathf.Repeat(t, Period) / Period;

                float y = Mathf.Lerp(Top, Bottom, cycle * cycle * 0.4f + cycle * 0.6f); // a slow start, then it sinks
                float x = lane + Mathf.Sin(t * 2.3f + i * 1.9f) * 0.035f;
                float fade = Mathf.Clamp01(cycle / 0.2f) * Mathf.Clamp01((1f - cycle) / 0.35f);

                var sr = wisps[i];
                var c = Tint;
                c.a = 0.75f * fade;
                sr.color = c;
                var tr = sr.transform;
                tr.localPosition = new Vector3(x, y, faceZ);
                tr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.7f + i) * 8f);
                tr.localScale = new Vector3(Width * (1f + 0.25f * cycle), Height * (0.85f + 0.4f * cycle), 1f); // it spreads as it sinks
            }
        }
    }
}
