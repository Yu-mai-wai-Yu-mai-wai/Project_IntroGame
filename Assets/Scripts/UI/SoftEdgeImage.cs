using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.UI
{
    /// <summary>
    /// Fades the edges of a UI picture into whatever is behind it instead of ending at a hard rectangle.
    /// Add it with Tools/TawanOS/UI/Soften Edge (or Add Component). Previews in edit mode.
    /// Swaps in the TawanOS/UI/SoftEdge shader through IMaterialModifier, so nothing extra is saved in the scene.
    /// Use it on pictures that fill their own texture (not atlas-packed sprites) and outside a Mask.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public class SoftEdgeImage : MonoBehaviour, IMaterialModifier
    {
        public const string ShaderResource = "TawanOS_UISoftEdge";
        private static readonly int FeatherId = Shader.PropertyToID("_Feather");

        [Header("How far each edge fades, as a fraction of the picture (0 = hard edge)")]
        [Range(0f, 1f)] public float left = 0.15f;
        [Range(0f, 1f)] public float right = 0.15f;
        [Range(0f, 1f)] public float top = 0.15f;
        [Range(0f, 1f)] public float bottom = 0.15f;

        /// <summary>Fade only the given sides, e.g. SetSides(right: 0.3f) for a picture next to a text panel.</summary>
        public void SetSides(float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            this.left = left;
            this.right = right;
            this.top = top;
            this.bottom = bottom;
            if (graphic != null) graphic.SetMaterialDirty();
        }

        private Graphic graphic;
        private Material material;

        private void OnEnable()
        {
            graphic = GetComponent<Graphic>();
            graphic.SetMaterialDirty();
        }

        private void OnDisable()
        {
            if (graphic != null) graphic.SetMaterialDirty();
        }

        private void OnDestroy()
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }

        private void OnValidate()
        {
            if (graphic != null) graphic.SetMaterialDirty();
        }

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            if (!isActiveAndEnabled) return baseMaterial;
            if (material == null)
            {
                var shader = Resources.Load<Shader>(ShaderResource);
                if (shader == null)
                {
                    Debug.LogWarning($"[SoftEdgeImage] Resources/{ShaderResource}.shader is missing - edges stay hard.");
                    return baseMaterial;
                }
                material = new Material(shader) { name = "Soft Edge (runtime)", hideFlags = HideFlags.HideAndDontSave };
            }
            material.SetVector(FeatherId, new Vector4(left, right, bottom, top));
            return material;
        }
    }
}
