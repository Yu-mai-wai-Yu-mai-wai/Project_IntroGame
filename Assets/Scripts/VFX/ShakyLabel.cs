using UnityEngine;

namespace TawanOS.VFX
{
    /// <summary>
    /// Keeps a piece of UI trembling in place (Game Designer, 25 Oct: the เริ่มใหม่ button on the defeat screen): every
    /// <see cref="stepSeconds"/> it jumps to a new small offset and tilt around where it was laid out, in real time.
    /// </summary>
    public class ShakyLabel : MonoBehaviour
    {
        [Tooltip("How far it wanders, in reference pixels.")]
        public float pixels = 3f;
        [Tooltip("How far it tilts, in degrees.")]
        public float degrees = 2.5f;
        public float stepSeconds = 0.06f;

        private RectTransform rt;
        private Vector2 home;
        private float next;

        private void OnEnable()
        {
            rt = (RectTransform)transform;
            home = rt.anchoredPosition;
        }

        private void OnDisable()
        {
            if (rt == null) return;
            rt.anchoredPosition = home;
            rt.localRotation = Quaternion.identity;
        }

        private void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + stepSeconds;
            rt.anchoredPosition = home + Random.insideUnitCircle * pixels;
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-degrees, degrees));
        }
    }
}
