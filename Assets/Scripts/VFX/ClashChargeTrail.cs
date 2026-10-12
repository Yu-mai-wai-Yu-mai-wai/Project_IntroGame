using UnityEngine;

namespace TawanOS.VFX
{
    /// <summary>
    /// The tail behind a familiar lunging at its target in the board clash. A TrailRenderer that follows the card
    /// at the scene root (the card sits in a clash rig that is removed afterwards) while the card charges; when
    /// the card hits (or is gone) it stops emitting and the tail fades before this object is destroyed.
    /// Resources/ClashTrail/ChargeTrailBlack.prefab (มนต์ดำ) and ChargeTrailWhite.prefab (มนต์ขาว), built by
    /// Tools > TawanOS > VFX > Create Clash Charge Trail.
    /// </summary>
    public class ClashChargeTrail : MonoBehaviour
    {
        public const string BlackResource = "ClashTrail/ChargeTrailBlack";
        public const string WhiteResource = "ClashTrail/ChargeTrailWhite";

        public TrailRenderer trail;

        private Transform card;
        private bool stopped;

        /// <summary>Starts a tail on <paramref name="cardTransform"/>; null when the prefab or the card is missing.</summary>
        public static ClashChargeTrail Spawn(ClashChargeTrail prefab, Transform cardTransform)
        {
            if (prefab == null || cardTransform == null) return null;
            var t = Instantiate(prefab, cardTransform.position, Quaternion.identity);
            t.name = prefab.name;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(t.gameObject, cardTransform.gameObject.scene);
            t.card = cardTransform;
            if (t.trail != null)
            {
                t.trail.Clear();
                t.trail.emitting = true;
            }
            return t;
        }

        private void Reset()
        {
            trail = GetComponent<TrailRenderer>();
        }

        private void LateUpdate()
        {
            if (stopped) return;
            if (card == null || !card.gameObject.activeInHierarchy)
            {
                Stop();
                return;
            }
            transform.position = card.position;
        }

        /// <summary>Stops the tail growing; what is left fades out, then this object is removed.</summary>
        public void Stop()
        {
            if (stopped) return;
            stopped = true;
            if (trail != null) trail.emitting = false;
            Destroy(gameObject, (trail != null ? trail.time : 0f) + 0.1f);
        }
    }
}
