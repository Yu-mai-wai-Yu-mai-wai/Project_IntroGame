using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.UI
{
    /// <summary>
    /// Draws its Image as a solid panel whose left edge is rolling smoke (shader TawanOS/UI/SmokeEdge, the same edge
    /// as the ศาลตายาย menu). Put it on a black Image that covers part of the screen; the smoke takes the first
    /// <see cref="edge"/> of its width, so what is behind shows through the wisps.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class SmokeEdgePanel : MonoBehaviour
    {
        [Tooltip("Width of the smoky border on the left, as a fraction of this panel's width.")]
        [Range(0.01f, 0.6f)] public float edge = 0.18f;
        public float speed = 1f;
        public float scale = 4f;

        private Image image;
        private Material material;
        private Vector2 lastSize;

        private static readonly int EdgeId = Shader.PropertyToID("_Edge");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");
        private static readonly int SpeedId = Shader.PropertyToID("_Speed");
        private static readonly int ScaleId = Shader.PropertyToID("_Scale");

        private void Awake()
        {
            image = GetComponent<Image>();
            var shader = Resources.Load<Shader>("TawanOS_UISmokeEdge");
            if (shader == null) { enabled = false; return; }
            material = new Material(shader);
            material.SetFloat(EdgeId, edge);
            material.SetFloat(SpeedId, speed);
            material.SetFloat(ScaleId, scale);
            image.material = material;
        }

        // The smoke stays round whatever the panel's shape
        private void Update()
        {
            var size = image.rectTransform.rect.size;
            if (size == lastSize || size.y <= 0f) return;
            lastSize = size;
            material.SetFloat(AspectId, size.x / size.y);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
