using UnityEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// How the card reward screen moves: cards dealt face down one at a time and flipped, then floating;
    /// soft smoke drifting across the background; the cards not taken burn away from the bottom-right corner.
    /// Loaded from Resources/RewardFx.asset (create it with Tools/TawanOS/Rewards/Create Reward FX Config).
    /// Every number here is presentation only and safe for the design team to tune in the Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "RewardFx", menuName = "TawanOS/Rewards/Reward FX Config")]
    public class RewardFxConfigSO : ScriptableObject
    {
        public const string ResourceName = "RewardFx";

        [Header("Art")]
        [Tooltip("Shown while a card is face down, before it flips.")]
        public Sprite cardBack;
        [Tooltip("Soft smoke texture drifting across the background.")]
        public Texture2D smokeTexture;
        [Tooltip("Material using the TawanOS/UI/BurnDissolve shader.")]
        public Material burnMaterial;

        [Header("Deal (one card at a time)")]
        [Tooltip("Wait before the first card comes in.")]
        [Min(0f)] public float dealStartDelay = 0.35f;
        [Tooltip("Seconds for a face-down card to fly into its place.")]
        [Min(0.05f)] public float dealFlyDuration = 0.45f;
        [Tooltip("Where cards fly in from: pixels below the card row, on the 1920x1080 layout.")]
        public float dealFromBelow = 700f;
        [Tooltip("Tilt (degrees) the card starts with while flying in.")]
        public float dealStartTilt = -12f;
        [Tooltip("Pause after a card lands, before it flips.")]
        [Min(0f)] public float pauseBeforeFlip = 0.12f;
        [Tooltip("Seconds for the whole flip (face down -> face up).")]
        [Min(0.05f)] public float flipDuration = 0.4f;
        [Tooltip("Pause after a flip, before the next card is dealt.")]
        [Min(0f)] public float pauseBetweenCards = 0.15f;

        [Header("Float (idle loop)")]
        [Tooltip("How far a card drifts up and down, in pixels.")]
        [Min(0f)] public float floatHeight = 10f;
        [Tooltip("Seconds for one full up-and-down.")]
        [Min(0.2f)] public float floatPeriod = 2.6f;
        [Tooltip("Small sway in degrees while floating (0 = none).")]
        [Min(0f)] public float floatSway = 0.8f;

        [Header("Smoke (background)")]
        [Range(0, 16)] public int smokeCount = 7;
        [Tooltip("Smoke opacity, from faintest to strongest puff.")]
        public Vector2 smokeAlpha = new Vector2(0.02f, 0.06f);
        [Tooltip("Puff width in pixels (min, max).")]
        public Vector2 smokeSize = new Vector2(700f, 1300f);
        [Tooltip("Drift speed in pixels per second (min, max).")]
        public Vector2 smokeSpeed = new Vector2(18f, 45f);
        public Color smokeTint = new Color(0.70f, 0.64f, 0.62f, 1f);

        [Header("Burn (cards not taken)")]
        [Tooltip("Wait after the pick before the other cards catch fire.")]
        [Min(0f)] public float burnDelay = 0.25f;
        [Tooltip("Seconds for the fire to cross a whole card.")]
        [Min(0.1f)] public float burnDuration = 1.3f;
        [Tooltip("Embers thrown up from the fire front per second.")]
        [Min(0f)] public float embersPerSecond = 28f;
        [ColorUsage(true, true)] public Color glowColor = new Color(1.6f, 0.55f, 0.12f, 1f);
        public Color charColor = new Color(0.12f, 0.05f, 0.03f, 1f);
        [Tooltip("Hold on the picked card after the burn, before leaving the screen.")]
        [Min(0f)] public float holdAfterBurn = 0.5f;

        /// <summary>Seconds from the pick until the screen may close.</summary>
        public float PickSequenceSeconds => burnDelay + burnDuration + holdAfterBurn;

        private static RewardFxConfigSO cached;

        /// <summary>The project's config, or defaults (without art) when the asset is missing.</summary>
        public static RewardFxConfigSO Load()
        {
            if (cached == null) cached = Resources.Load<RewardFxConfigSO>(ResourceName);
            if (cached == null)
            {
                Debug.LogWarning($"[RewardFx] Resources/{ResourceName}.asset is missing (Tools > TawanOS > Rewards > Create Reward FX Config) - using defaults without card back, smoke or burn art.");
                cached = CreateInstance<RewardFxConfigSO>();
                cached.hideFlags = HideFlags.HideAndDontSave;
            }
            return cached;
        }
    }
}
