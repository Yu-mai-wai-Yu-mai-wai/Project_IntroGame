using UnityEngine;
using UnityEngine.VFX;

namespace TawanOS.VFX
{
    /// <summary>
    /// Black smoke over the หลุมจั่ว (draw pit), after Docs/ref/image.png: a tight ball of black smoke on the pit,
    /// turning around its centre and ringed by glowing red strands in the black-magic cards' colours.
    /// Two versions, built by Tools > TawanOS > VFX > Create Draw Pit Smoke:
    ///  - Resources/DrawPit/DrawPitSmokeVFX.prefab: a VFX Graph (Art/VFX/DrawPitSmoke/DrawPitSmoke.vfx), used
    ///    when it exists. The graph works in local space, so turning this transform makes the smoke orbit.
    ///  - Resources/DrawPit/DrawPitSmoke.prefab: three Particle Systems (Cover, Wisps, RedStrands), otherwise.
    /// This component only keeps the prefab on the pit and sends up a gust when a card is drawn.
    /// </summary>
    public class DrawPitSmoke : MonoBehaviour
    {
        public const string PrefabResource = "DrawPit/DrawPitSmoke";
        public const string VfxPrefabResource = "DrawPit/DrawPitSmokeVFX";
        public const string BurstEvent = "Burst";
        public const string GlowProperty = "Glow";

        [Header("Pit")]
        [Tooltip("Width (x) and depth (z) of the pit you click to draw, in world units. Read by DrawPitView3D when " +
                 "this object is in the scene; shown as a red outline in the Scene view when selected.")]
        public Vector2 pitSize = new Vector2(2f, 2f);

        [Header("VFX Graph version")]
        [Tooltip("The VFX Graph smoke. When set, the Particle System layers below are not used.")]
        public VisualEffect visualEffect;
        [Tooltip("Turns per second the VFX Graph smoke orbits the pit's centre (negative = the other way).")]
        public float orbitTurnsPerSecond = 0.18f;

        [Header("Mouse over the pit")]
        [Tooltip("Red point light in the middle of the pit, lit while the mouse is over it (optional).")]
        public Light hoverLight;
        public float hoverLightIntensity = 6f;
        [Tooltip("How strong the red glow in the VFX Graph gets (its exposed float \"Glow\").")]
        public float hoverGlow = 1f;
        [Tooltip("Seconds to fade the glow in or out.")]
        public float hoverFadeTime = 0.2f;

        [Header("Layers (children of this prefab)")]
        [Tooltip("Dense smoke lying flat on the pit, hiding its middle.")]
        public ParticleSystem cover;
        [Tooltip("Curling smoke rising and turning around the pit.")]
        public ParticleSystem wisps;
        [Tooltip("Glowing red strands on the rim of the smoke.")]
        public ParticleSystem redStrands;

        [Header("Card drawn (Burst)")]
        public int burstWisps = 22;
        public int burstStrands = 4;
        [Tooltip("Upward speed of the burst, in world units per second.")]
        public float burstRise = 1.4f;

        private Transform pit;
        private bool hovered;
        private float hover; // 0..1, fading toward hovered

        /// <summary>Places the smoke prefab on the pit, or returns null when the prefab is missing.</summary>
        public static DrawPitSmoke Attach(Transform pitTransform)
        {
            var prefab = Resources.Load<DrawPitSmoke>(VfxPrefabResource);
            if (prefab == null) prefab = Resources.Load<DrawPitSmoke>(PrefabResource);
            if (prefab == null)
            {
                Debug.LogWarning($"[DrawPitSmoke] Resources/{PrefabResource}.prefab is missing (Tools > TawanOS > VFX > Create Draw Pit Smoke) - the draw pit has no smoke.");
                return null;
            }

            // At the scene root, not under the pit: the pit is a squashed cylinder and would squash the smoke.
            // Created right at the pit, because the systems prewarm (simulate a few seconds) the moment they
            // are created, in world space, and would leave that smoke wherever they were made.
            var smoke = Instantiate(prefab, pitTransform.position, Quaternion.identity);
            smoke.name = prefab.name;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(smoke.gameObject, pitTransform.gameObject.scene);
            smoke.pit = pitTransform;
            return smoke;
        }

        /// <summary>Keeps smoke already placed in the scene on the pit (the pit is created at runtime).</summary>
        public void Follow(Transform pitTransform)
        {
            pit = pitTransform;
            transform.position = pitTransform.position;
        }

        private void Reset()
        {
            visualEffect = GetComponent<VisualEffect>();
        }

        /// <summary>A card was pulled out: a gust of smoke and red strands rises from the pit.</summary>
        public void Burst()
        {
            if (visualEffect != null)
            {
                visualEffect.SendEvent(BurstEvent);
                return;
            }
            Gust(wisps, burstWisps, 0.35f, 0.7f, 1.2f);
            Gust(redStrands, burstStrands, 0.3f, 0.5f, 1f);
        }

        private void Gust(ParticleSystem ps, int count, float spread, float minRise, float maxRise)
        {
            if (ps == null) return;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            for (int i = 0; i < count; i++)
            {
                Vector2 side = Random.insideUnitCircle * spread;
                p.velocity = new Vector3(side.x, burstRise * Random.Range(minRise, maxRise), side.y);
                ps.Emit(p, 1);
            }
        }

        /// <summary>The mouse moved onto (true) or off (false) the pit: red light wells up from its middle.</summary>
        public void SetHover(bool on)
        {
            hovered = on;
        }

        private void Start()
        {
            ApplyHover();
        }

        private void Update()
        {
            float target = hovered ? 1f : 0f;
            if (Mathf.Approximately(hover, target)) return;
            hover = Mathf.MoveTowards(hover, target, Time.deltaTime / Mathf.Max(0.01f, hoverFadeTime));
            ApplyHover();
        }

        private void ApplyHover()
        {
            float eased = hover * hover * (3f - 2f * hover);
            if (visualEffect != null && visualEffect.HasFloat(GlowProperty)) visualEffect.SetFloat(GlowProperty, eased * hoverGlow);
            if (hoverLight != null)
            {
                hoverLight.intensity = eased * hoverLightIntensity;
                hoverLight.enabled = eased > 0f;
            }
        }

        private void OnDrawGizmosSelected()
        {
            // The clickable pit: an upright cylinder, so an ellipse on the table
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
            Vector3 centre = transform.position;
            Vector3 previous = centre + new Vector3(pitSize.x * 0.5f, 0f, 0f);
            for (int i = 1; i <= 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                Vector3 next = centre + new Vector3(Mathf.Cos(a) * pitSize.x * 0.5f, 0f, Mathf.Sin(a) * pitSize.y * 0.5f);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        private void LateUpdate()
        {
            if (pit != null) transform.position = pit.position;
            if (visualEffect != null) transform.Rotate(0f, orbitTurnsPerSecond * 360f * Time.deltaTime, 0f, Space.World);
        }
    }
}
