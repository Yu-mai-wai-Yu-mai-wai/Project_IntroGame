using UnityEngine;
using UnityEngine.VFX;

namespace TawanOS.VFX
{
    /// <summary>
    /// Red smoke trailing a card pulled out of the หลุมจั่ว (draw pit) while it flies to a hand.
    /// Resources/DrawPit/PitCardTrail.prefab (Tools > TawanOS > VFX > Create Draw Pit Smoke) plays
    /// Art/VFX/DrawPitSmoke/PitCardTrail.vfx, which emits in world space at this object's position: following the
    /// card leaves the smoke behind it. When the card settles (or is gone) the emission stops and the trail
    /// fades out before this object is destroyed.
    /// </summary>
    public class PitCardTrail : MonoBehaviour
    {
        public const string PrefabResource = "DrawPit/PitCardTrail";

        public VisualEffect visualEffect;
        [Tooltip("The card counts as landed once it moves slower than this (world units per second).")]
        public float settledSpeed = 0.3f;
        [Tooltip("Stop trailing even if the card is still moving after this many seconds.")]
        public float maxDuration = 2.5f;
        [Tooltip("Seconds the trail keeps fading after it stops, before this object is removed.")]
        public float fadeOutTime = 1.2f;

        private Transform card;
        private Vector3 lastPosition;
        private float age;
        private float stillTime;
        private bool stopped;
        private bool moved;

        /// <summary>Starts a trail on a card that has just come out of the pit; null when the prefab is missing.</summary>
        public static PitCardTrail Spawn(Transform cardTransform)
        {
            var prefab = Resources.Load<PitCardTrail>(PrefabResource);
            if (prefab == null || cardTransform == null) return null;

            // At the scene root: the hand containers are camera-anchored and scaled
            var trail = Instantiate(prefab, cardTransform.position, Quaternion.identity);
            trail.name = prefab.name;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(trail.gameObject, cardTransform.gameObject.scene);
            trail.card = cardTransform;
            trail.lastPosition = cardTransform.position;
            return trail;
        }

        private void Reset()
        {
            visualEffect = GetComponent<VisualEffect>();
        }

        private void LateUpdate()
        {
            if (stopped) return;
            age += Time.deltaTime;

            if (card == null || !card.gameObject.activeInHierarchy)
            {
                StopTrail();
                return;
            }

            Vector3 p = card.position;
            float speed = Time.deltaTime > 0f ? (p - lastPosition).magnitude / Time.deltaTime : 0f;
            lastPosition = p;
            transform.position = p;

            // The layout pass may hold a card on the pit for a moment before it flies (stagger), so only a
            // pause after it has started moving counts as landing
            if (speed > settledSpeed * 2f) moved = true;
            stillTime = speed < settledSpeed ? stillTime + Time.deltaTime : 0f;
            if ((moved && stillTime > 0.15f) || age > maxDuration) StopTrail();
        }

        private void StopTrail()
        {
            stopped = true;
            if (visualEffect != null) visualEffect.Stop();
            Destroy(gameObject, fadeOutTime);
        }
    }
}
