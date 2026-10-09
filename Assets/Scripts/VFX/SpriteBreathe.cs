using UnityEngine;

namespace TawanOS.VFX
{
    /// <summary>
    /// A painted layer that is never quite still: a slow breath in scale (anchored at the bottom when the
    /// sprite's pivot is there) and an optional slow pulse in brightness, kept far under 3 Hz.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteBreathe : MonoBehaviour
    {
        [Tooltip("How much bigger at the top of a breath (0.004 = 0.4%).")]
        [Range(0f, 0.05f)] public float scaleAmount = 0.004f;
        [Tooltip("Seconds for one breath.")]
        [Min(0.5f)] public float period = 4.5f;
        [Tooltip("How much the brightness rises and falls (0 = never).")]
        [Range(0f, 0.5f)] public float pulseAmount = 0f;
        [Tooltip("Seconds for one brightness pulse.")]
        [Min(0.5f)] public float pulsePeriod = 3.2f;

        private SpriteRenderer sprite;
        private Vector3 baseScale;
        private Color baseColor;
        private float phase;

        private void Start()
        {
            sprite = GetComponent<SpriteRenderer>();
            baseScale = transform.localScale;
            baseColor = sprite.color;
            phase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            float breath = (Mathf.Sin(t * Mathf.PI * 2f / period + phase) + 1f) * 0.5f;
            transform.localScale = new Vector3(baseScale.x * (1f + scaleAmount * 0.4f * breath), baseScale.y * (1f + scaleAmount * breath), baseScale.z);

            if (pulseAmount > 0f)
            {
                float pulse = 1f + pulseAmount * Mathf.Sin(t * Mathf.PI * 2f / pulsePeriod + phase * 1.7f);
                sprite.color = new Color(baseColor.r * pulse, baseColor.g * pulse, baseColor.b * pulse, baseColor.a);
            }
        }
    }
}
