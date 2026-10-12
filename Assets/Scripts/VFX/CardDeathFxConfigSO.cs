using UnityEngine;

namespace TawanOS.VFX
{
    /// <summary>
    /// How a board familiar burns away when it dies (CardBurnEffect, played by BoardClashView3D in place of the
    /// Die clip). Loaded from Resources/CardDeathFx.asset (create it with Tools/TawanOS/VFX/Create Card Death FX).
    /// Presentation only: safe for the design team to tune in the Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "CardDeathFx", menuName = "TawanOS/VFX/Card Death FX Config")]
    public class CardDeathFxConfigSO : ScriptableObject
    {
        public const string ResourceName = "CardDeathFx";

        [Tooltip("Material using the TawanOS/CardBurnDissolve shader. Empty = the card plays the Die clip instead.")]
        public Material burnMaterial;

        [Tooltip("On: the fire starts in the middle of the card and spreads out to its edges. " +
                 "Off: it crosses from the bottom-right corner to the top-left corner (like the reward cards).")]
        public bool burnFromCentre = true;
        [Tooltip("Seconds for the fire to cross the whole card.")]
        [Min(0.1f)] public float burnDuration = 1.1f;
        [Tooltip("Embers thrown up from the fire front per second.")]
        [Min(0f)] public float embersPerSecond = 40f;
        [Tooltip("Ember size in world units (min, max).")]
        public Vector2 emberSize = new Vector2(0.05f, 0.12f);
        [Tooltip("How fast the embers rise, in world units per second (min, max).")]
        public Vector2 emberRiseSpeed = new Vector2(0.6f, 1.4f);
        [ColorUsage(true, true)] public Color glowColor = new Color(1.6f, 0.55f, 0.12f, 1f);
        public Color charColor = new Color(0.12f, 0.05f, 0.03f, 1f);

        private static CardDeathFxConfigSO cached;
        private static bool warned;

        /// <summary>The project's config, or null when the asset is missing.</summary>
        public static CardDeathFxConfigSO Load()
        {
            if (cached == null) cached = Resources.Load<CardDeathFxConfigSO>(ResourceName);
            if (cached == null && !warned)
            {
                warned = true;
                Debug.LogWarning($"[CardDeathFx] Resources/{ResourceName}.asset is missing (Tools > TawanOS > VFX > Create Card Death FX) - dying cards play the Die clip.");
            }
            return cached;
        }
    }
}
