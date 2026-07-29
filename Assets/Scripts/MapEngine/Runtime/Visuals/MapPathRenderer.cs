using UnityEngine;

namespace TawanOS.MapEngine
{
    [RequireComponent(typeof(LineRenderer))]
    public class MapPathRenderer : MonoBehaviour
    {
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private int pointsCount = 20;
        [SerializeField] private float curveOffsetMagnitude = 0.5f;

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

            // Calculate Control Point P1 for Bezier Curve
            Vector3 midPoint = (start + end) * 0.5f;
            Vector3 direction = (end - start).normalized;
            Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0f);
            
            // Deterministic curve offset based on coordinates
            float sign = ((sourcePos.x + targetPos.y) % 2 == 0) ? 1f : -1f;
            Vector3 controlPoint = midPoint + perpendicular * (curveOffsetMagnitude * sign);

            // Generate Quadratic Bezier Points
            for (int i = 0; i < pointsCount; i++)
            {
                float t = i / (float)(pointsCount - 1);
                Vector3 point = CalculateQuadraticBezierPoint(t, start, controlPoint, end);
                lineRenderer.SetPosition(i, point);
            }

            Color pathColor = biome != null ? biome.pathBaseColor : new Color(0.5f, 0.5f, 0.5f, 0.6f);
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
