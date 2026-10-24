using System;
using UnityEngine;

namespace TawanOS.CardEngine
{
    /// <summary>
    /// The look of the กุศล / มลทิน flame rings in the fight (FlameGaugeHUD), for the design team to tune in the
    /// Inspector (Game Designer, 13 Oct). Lives at Resources/FlameGaugeStyle.asset (Tools > TawanOS > Card Engine >
    /// Create Flame Gauge Style). Changes made while playing show at once.
    /// </summary>
    [CreateAssetMenu(fileName = "FlameGaugeStyle", menuName = "TawanOS/Card Engine/Flame Gauge Style")]
    public class FlameGaugeStyleSO : ScriptableObject
    {
        public const string ResourceName = "FlameGaugeStyle";

        [Serializable]
        public class Palette
        {
            [Tooltip("Colour of the ring and the body of the flames.")]
            public Color flame = Color.white;
            [Tooltip("Colour the flames turn toward at their hottest.")]
            public Color tip = Color.white;
            [Tooltip("Colour of the current/max number.")]
            public Color number = Color.white;
        }

        [Header("Layout (pixels on the 1920 x 1080 screen)")]
        [Tooltip("Size of each gauge's square, flames included. The ring itself is about 60% of it.")]
        [Min(40f)] public float size = 250f;
        [Tooltip("Distance from the left edge of the screen to the left side of the gauge's square.")]
        public float leftMargin = 0f;
        [Tooltip("Height of the upper gauge's centre above the middle of the screen.")]
        public float upperY = 115f;
        [Tooltip("Height of the lower gauge's centre above the middle of the screen (negative = below).")]
        public float lowerY = -105f;
        [Tooltip("On: กุศล is the upper gauge. Off: มลทิน is the upper gauge.")]
        public bool meritOnTop = false;
        [Tooltip("On: both gauges sit in a row at the top-left corner, out of the way of a raised hand card (Game " +
                 "Designer, 24 Oct); the 'upper' gauge is the left one and Upper Y / Lower Y are not used. Off: stacked " +
                 "at the left side, at Upper Y / Lower Y.")]
        public bool topLeftRow = true;
        [Tooltip("Top-left corner: the two gauges one above the other instead of side by side (Game Designer, 25 Oct). " +
                 "Keep the lower one above a raised hand card: Top Margin + Size * 2 + Row Gap about 380 or less.")]
        public bool stackVertically = true;
        [Tooltip("Top-left row: distance from the top of the screen to the top of the gauges' squares.")]
        public float topMargin = 0f;
        [Tooltip("Top-left row: space between the two gauges' squares (negative = they overlap).")]
        public float rowGap = -40f;

        [Header("Ring (fractions of the gauge's square, 0.5 = its edge)")]
        [Tooltip("Inner edge of the ring = edge of the black plate behind the number.")]
        [Range(0.05f, 0.45f)] public float innerRadius = 0.245f;
        [Tooltip("Outer edge of the ring, where the flames start.")]
        [Range(0.06f, 0.48f)] public float outerRadius = 0.3f;

        [Header("Fire")]
        [Tooltip("How tall the flames are. 1 = normal.")]
        [Range(0f, 3f)] public float flameHeight = 1.25f;
        [Tooltip("How fast the fire moves. 1 = normal.")]
        [Range(0f, 3f)] public float flameSpeed = 1f;
        [Tooltip("Speed used instead when the player turned on Reduce Motion in Settings.")]
        [Range(0f, 1f)] public float reducedMotionSpeed = 0.3f;

        [Header("Number")]
        [Min(8f)] public float numberSize = 46f;

        [Header("มลทิน about to break (warning)")]
        [Tooltip("The มลทิน ring warns when this many points or fewer are left before it breaks.")]
        [Range(0, 5)] public int warnPoints = 2;
        [Tooltip("How much the ring swells on each beat of the warning (0.1 = 10%).")]
        [Range(0f, 0.5f)] public float warnPulse = 0.08f;
        [Tooltip("Beats per second of the warning with 2 points left; with 1 left it beats twice as fast.")]
        [Range(0.2f, 5f)] public float warnBeats = 1.2f;
        [Tooltip("How much taller the flames get while warning, at 1 point left (0.6 = 60% taller).")]
        [Range(0f, 2f)] public float warnFlameBoost = 0.6f;
        public Color warnNumber = new Color(1f, 0.25f, 0.2f);

        [Header("มลทิน breaks")]
        [Tooltip("How red the whole screen flashes when the player's มลทิน breaks (0 = no flash).")]
        [Range(0f, 1f)] public float breakFlash = 0.5f;
        [Tooltip("A lightning bolt strikes the ring when the player's มลทิน breaks, and the enemy when its does.")]
        public bool breakLightning = true;
        [Tooltip("How hard the camera shakes when the player's มลทิน breaks (0 = still).")]
        [Range(0f, 1f)] public float breakShake = 0.25f;
        [Tooltip("Sparks the ring bursts into.")]
        [Range(0, 40)] public int breakSparks = 18;
        [Tooltip("Seconds the ring's fire takes to burn back up after it breaks.")]
        [Range(0.1f, 3f)] public float breakRegrow = 0.9f;
        [Tooltip("Colour of the khwan lost, floating up from the khwan bar.")]
        public Color lossNumber = new Color(1f, 0.3f, 0.25f);

        [Header("Colours")]
        public Palette merit = new Palette
        {
            flame = new Color(0.95f, 0.66f, 0.18f),
            tip = new Color(1f, 0.92f, 0.6f),
            number = new Color(1f, 0.9f, 0.62f),
        };
        public Palette corruption = new Palette
        {
            flame = new Color(0.85f, 0.2f, 0.15f),
            tip = new Color(1f, 0.55f, 0.4f),
            number = new Color(1f, 0.72f, 0.64f),
        };

        /// <summary>Raised when a value is edited in the Inspector, so a gauge on screen can redraw.</summary>
        public static event Action Changed;

        private void OnValidate()
        {
            if (outerRadius < innerRadius + 0.01f) outerRadius = innerRadius + 0.01f;
            Changed?.Invoke();
        }

        private static FlameGaugeStyleSO cached;

        /// <summary>The project's style, or the defaults above when the asset is missing.</summary>
        public static FlameGaugeStyleSO Load()
        {
            if (cached == null) cached = Resources.Load<FlameGaugeStyleSO>(ResourceName);
            if (cached == null)
            {
                Debug.LogWarning($"[FlameGauge] Resources/{ResourceName}.asset is missing (Tools > TawanOS > Card Engine > Create Flame Gauge Style) - using defaults.");
                cached = CreateInstance<FlameGaugeStyleSO>();
                cached.hideFlags = HideFlags.HideAndDontSave;
            }
            return cached;
        }
    }
}
