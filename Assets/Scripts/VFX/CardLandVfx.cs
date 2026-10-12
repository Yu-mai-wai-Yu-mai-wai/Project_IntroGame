using UnityEngine;

namespace TawanOS.VFX
{
    /// <summary>
    /// The burst under a card as it lands on a board slot, one prefab per school (Tools > TawanOS > VFX >
    /// Create Card Land VFX): Resources/CardLand/CardLandBlack.prefab (มนต์ดำ, black and red smoke) and
    /// CardLandWhite.prefab (มนต์ขาว, streaks of light). Each is a VisualEffect that plays once.
    /// </summary>
    public static class CardLandVfx
    {
        public const string BlackResource = "CardLand/CardLandBlack";
        public const string WhiteResource = "CardLand/CardLandWhite";

        /// <summary>Seconds before the effect object is removed; longer than any of its particles live.</summary>
        public const float Lifetime = 2.5f;

        private static GameObject blackPrefab, whitePrefab;

        /// <summary>Plays the landing burst under a card that has just reached its slot.</summary>
        public static void Play(Transform card, bool blackMagic)
        {
            if (card == null) return;
            var prefab = blackMagic
                ? (blackPrefab != null ? blackPrefab : blackPrefab = Resources.Load<GameObject>(BlackResource))
                : (whitePrefab != null ? whitePrefab : whitePrefab = Resources.Load<GameObject>(WhiteResource));
            if (prefab == null) return;

            // Flat on the table, turned with the card: the effect's z runs along the card
            Vector3 along = Vector3.ProjectOnPlane(card.up, Vector3.up);
            if (along.sqrMagnitude < 0.0001f) along = Vector3.forward;
            var fx = Object.Instantiate(prefab, card.position, Quaternion.LookRotation(along.normalized, Vector3.up));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fx, card.gameObject.scene);
            Object.Destroy(fx, Lifetime);
        }
    }
}
