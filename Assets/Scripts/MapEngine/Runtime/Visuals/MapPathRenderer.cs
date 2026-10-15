using UnityEngine;

namespace TawanOS.MapEngine
{
    [RequireComponent(typeof(LineRenderer))]
    public class MapPathRenderer : MonoBehaviour
    {
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private int pointsCount = 20;
        [SerializeField] private float curveOffsetMagnitude = 0.5f;
        [Tooltip("Height above node points. PaperFloor top is y=0.027, nodes sit at 0.025, so paths need a lift to show.")]
        [SerializeField] private float surfaceLift = 0.02f;
        [Tooltip("Gap between a string end and the node centre (3D table). Icon half-width is 0.6.")]
        [SerializeField] private float nodeGap = 0.75f;
        [Header("Hand-drawn wobble (3D table)")]
        [Tooltip("How far the straight string strays sideways at most, in world units (its width is 0.07).")]
        [SerializeField] private float wobble = 0.035f;
        [Tooltip("How many slow sways along a string; small jitters ride on top of them.")]
        [SerializeField] private float wobbleWaves = 2.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock colorBlock;

        public Vector2Int SourcePos { get; private set; }
        public Vector2Int TargetPos { get; private set; }

        private void Awake()
        {
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }
            EnsureValidMaterial();
        }

        public void EnsureValidMaterial()
        {
            if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                lineRenderer.alignment = LineAlignment.TransformZ;
                lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lineRenderer.receiveShadows = false;
#if UNITY_EDITOR
                if (lineRenderer.sharedMaterial == null || lineRenderer.sharedMaterial.shader == null || lineRenderer.sharedMaterial.shader.name == "Hidden/InternalErrorShader")
                {
                    Material stringMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/MapPaper/String.mat");
                    if (stringMat != null)
                    {
                        lineRenderer.sharedMaterial = stringMat;
                        return;
                    }
                }
#endif
                if (lineRenderer.sharedMaterial == null || lineRenderer.sharedMaterial.shader == null || lineRenderer.sharedMaterial.shader.name == "Hidden/InternalErrorShader")
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") 
                                 ?? Shader.Find("Sprites/Default") 
                                 ?? Shader.Find("Unlit/Color")
                                 ?? Shader.Find("GUI/Text Shader");
                    if (shader != null)
                    {
                        lineRenderer.material = new Material(shader);
                    }
                }
            }
        }

        public void SetupPath(Vector3 start, Vector3 end, Vector2Int sourcePos, Vector2Int targetPos, BiomeProfileSO biome)
        {
            EnsureValidMaterial();
            this.SourcePos = sourcePos;
            this.TargetPos = targetPos;

            if (lineRenderer == null) return;

            lineRenderer.positionCount = pointsCount;
            lineRenderer.useWorldSpace = true;
            lineRenderer.alignment = LineAlignment.TransformZ;
            lineRenderer.startWidth = 0.07f;
            lineRenderer.endWidth = 0.07f;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;

            // Orient GameObject so that local +Z points directly along world +Y (Up)
            // This ensures the LineRenderer ribbon lies 100% flat on the horizontal X-Z plane
            transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            Vector3 direction = (end - start).normalized;

            // Detect whether path is on horizontal X-Z table surface or vertical X-Y canvas
            bool isHorizontalPlane = Mathf.Abs(start.y - end.y) < 0.2f && (Mathf.Abs(direction.x) > 0.01f || Mathf.Abs(direction.z) > 0.01f);

            if (isHorizontalPlane)
            {
                // 3D table: a straight string from node to node, as if drawn by hand: it sways a little to either side
                // and jitters slightly, most in the middle and not at all at its ends, which stop short of the icons
                // (Game Designer, 15 Oct: straight instead of the S-curve). The same string always wobbles the same way.
                float flatYTable = Mathf.Max(start.y, end.y) + surfaceLift;
                Vector3 a = new Vector3(start.x, flatYTable, start.z);
                Vector3 b = new Vector3(end.x, flatYTable, end.z);
                Vector3 along = b - a;
                float length = along.magnitude;
                Vector3 dir = length > 0.0001f ? along / length : Vector3.right;
                float trim = Mathf.Min(nodeGap, length * 0.4f);
                Vector3 p0 = a + dir * trim;
                Vector3 p1 = b - dir * trim;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                float seed = (sourcePos.x * 73 + sourcePos.y * 31 + targetPos.x * 17 + targetPos.y * 11) * 0.137f;

                for (int i = 0; i < pointsCount; i++)
                {
                    float t = i / (float)(pointsCount - 1);
                    float envelope = Mathf.Sin(t * Mathf.PI);
                    float sway = (Mathf.PerlinNoise(seed, t * wobbleWaves) - 0.5f) * 2f;
                    float jitter = (Mathf.PerlinNoise(seed + 41.3f, t * wobbleWaves * 6f) - 0.5f) * 2f * 0.35f;
                    lineRenderer.SetPosition(i, Vector3.Lerp(p0, p1, t) + side * (sway + jitter) * wobble * envelope);
                }

                SetColor(biome != null ? biome.pathBaseColor : new Color(0.95f, 0.93f, 0.90f, 1f));
                return;
            }

            // Vertical 2D canvas: quadratic curve bowed sideways
            Vector3 midPoint = (start + end) * 0.5f;
            Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0f);

            // Deterministic curve offset based on coordinates
            float sign = ((sourcePos.x + targetPos.y) % 2 == 0) ? 1f : -1f;
            Vector3 controlPoint = midPoint + perpendicular * (curveOffsetMagnitude * sign);

            for (int i = 0; i < pointsCount; i++)
            {
                float t = i / (float)(pointsCount - 1);
                lineRenderer.SetPosition(i, CalculateQuadraticBezierPoint(t, start, controlPoint, end));
            }

            Color pathColor = biome != null ? biome.pathBaseColor : new Color(0.95f, 0.93f, 0.90f, 0.85f);
            SetColor(pathColor);
        }

        public void SetVisited(BiomeProfileSO biome)
        {
            Color visitedColor = biome != null ? biome.pathVisitedColor : Color.gold;
            SetColor(visitedColor);
        }

        private void SetColor(Color color)
        {
            if (lineRenderer != null)
            {
                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
                // URP Lit (String.mat) ignores vertex colour; tint through _BaseColor without cloning the material.
                colorBlock ??= new MaterialPropertyBlock();
                lineRenderer.GetPropertyBlock(colorBlock);
                colorBlock.SetColor(BaseColorId, color);
                lineRenderer.SetPropertyBlock(colorBlock);
            }
        }

        private Vector3 CalculateQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            Vector3 p = uu * p0 + 2 * u * t * p1 + tt * p2;
            return p;
        }
    }
}
