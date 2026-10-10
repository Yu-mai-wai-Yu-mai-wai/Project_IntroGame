using UnityEngine;

namespace TawanOS.VFX
{
    /// <summary>
    /// Eyes watching from the dark: each pair fades in, holds (blinking now and then), fades out, waits, and
    /// comes back. Only a few are open at once, at random, so the forest never looks the same twice.
    /// Pairs placed behind a tree layer show only through its gaps, and drift against it with the camera.
    /// </summary>
    public class GlowingEyes : MonoBehaviour
    {
        public SpriteRenderer[] eyes = new SpriteRenderer[0];
        [Tooltip("Most pairs open at the same time.")]
        [Min(1)] public int maxOpen = 3;
        [Tooltip("Seconds a pair stays open (min, max).")]
        public Vector2 openTime = new Vector2(3f, 7f);
        [Tooltip("Seconds a pair stays hidden before it may open again (min, max).")]
        public Vector2 hiddenTime = new Vector2(2f, 6f);
        [Min(0.05f)] public float fadeTime = 0.8f;
        [Tooltip("Strongest opacity of an open pair.")]
        [Range(0f, 1f)] public float brightness = 0.95f;
        [Tooltip("How far (world units) each pair leans toward the mouse, as if watching the player.")]
        [Range(0f, 1f)] public float followMouse = 0.25f;
        [Tooltip("Seconds the eyes take to follow the mouse.")]
        [Range(0.05f, 2f)] public float followSmooth = 0.5f;

        private Vector2 look;
        private Vector2 lookVelocity;

        private enum State { Hidden, Opening, Open, Closing }

        private class Pair
        {
            public SpriteRenderer renderer;
            public Vector3 baseScale;
            public Vector3 basePosition;
            public State state;
            public float timer;
            public float blinkIn;
            public float blink; // 0 = eyes open .. 1 = shut
        }

        private Pair[] pairs = new Pair[0];

        private void Start()
        {
            pairs = new Pair[eyes.Length];
            for (int i = 0; i < eyes.Length; i++)
            {
                pairs[i] = new Pair
                {
                    renderer = eyes[i],
                    baseScale = eyes[i] != null ? eyes[i].transform.localScale : Vector3.one,
                    basePosition = eyes[i] != null ? eyes[i].transform.localPosition : Vector3.zero,
                    state = State.Hidden,
                    timer = Random.Range(0f, hiddenTime.y),
                };
                SetAlpha(pairs[i], 0f);
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Every pair leans the same way: toward the cursor, as if the dark is watching the player
            Vector2 target = Vector2.zero;
            if (Application.isFocused && Screen.width > 0 && Screen.height > 0)
            {
                Vector3 m = Input.mousePosition;
                target = new Vector2(Mathf.Clamp(m.x / Screen.width * 2f - 1f, -1f, 1f), Mathf.Clamp(m.y / Screen.height * 2f - 1f, -1f, 1f));
            }
            look = Vector2.SmoothDamp(look, target, ref lookVelocity, followSmooth, Mathf.Infinity, dt);

            int open = 0;
            foreach (var p in pairs) if (p.state != State.Hidden) open++;

            foreach (var p in pairs)
            {
                if (p.renderer == null) continue;
                p.timer -= dt;

                switch (p.state)
                {
                    case State.Hidden:
                        if (p.timer <= 0f && open < maxOpen)
                        {
                            p.state = State.Opening;
                            p.timer = fadeTime;
                            p.blinkIn = Random.Range(0.8f, 2.5f);
                            open++;
                        }
                        break;

                    case State.Opening:
                        SetAlpha(p, brightness * (1f - Mathf.Clamp01(p.timer / fadeTime)));
                        if (p.timer <= 0f) { p.state = State.Open; p.timer = Random.Range(openTime.x, openTime.y); }
                        break;

                    case State.Open:
                        SetAlpha(p, brightness);
                        if (p.timer <= 0f) { p.state = State.Closing; p.timer = fadeTime; }
                        break;

                    case State.Closing:
                        SetAlpha(p, brightness * Mathf.Clamp01(p.timer / fadeTime));
                        if (p.timer <= 0f) { p.state = State.Hidden; p.timer = Random.Range(hiddenTime.x, hiddenTime.y); }
                        break;
                }

                // A quick blink every couple of seconds while visible
                if (p.state == State.Open || p.state == State.Opening)
                {
                    p.blinkIn -= dt;
                    if (p.blinkIn <= 0f)
                    {
                        p.blink = 1f;
                        p.blinkIn = Random.Range(1.5f, 4f);
                    }
                }
                p.blink = Mathf.MoveTowards(p.blink, 0f, dt * 6f);
                float lid = 1f - Mathf.Sin(p.blink * Mathf.PI * 0.5f) * 0.9f;
                p.renderer.transform.localScale = new Vector3(p.baseScale.x, p.baseScale.y * lid, p.baseScale.z);
                p.renderer.transform.localPosition = p.basePosition + new Vector3(look.x, look.y, 0f) * followMouse;
            }
        }

        private static void SetAlpha(Pair p, float a)
        {
            var c = p.renderer.color;
            c.a = a;
            p.renderer.color = c;
        }
    }
}
