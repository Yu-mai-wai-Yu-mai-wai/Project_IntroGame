using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TawanOS.VFX
{
    /// <summary>
    /// Swaps a button's flat panel for a ring of slowly drifting smoke (shader TawanOS/UI/SmokeButton).
    /// The button keeps its size, click area and label; only the panel behind the label changes. The smoke
    /// glows red while the cursor is over the button and fades when the button is disabled; the label
    /// changes colour with it (white at rest, red on hover).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class UiSmokeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [Tooltip("Smoke colour at rest.")]
        public Color normalColor = new Color(0.82f, 0.74f, 0.86f, 0.7f);
        [Tooltip("Smoke colour while the cursor is over the button.")]
        public Color hoverColor = new Color(1f, 0.32f, 0.26f, 1f);
        [Tooltip("Smoke colour while the button cannot be pressed.")]
        public Color disabledColor = new Color(0.6f, 0.55f, 0.65f, 0.3f);
        [Tooltip("Label colour at rest.")]
        public Color labelNormalColor = Color.white;
        [Tooltip("Label colour while the cursor is over the button.")]
        public Color labelHoverColor = new Color(1f, 0.36f, 0.3f, 1f);
        [Tooltip("Label colour while the button cannot be pressed.")]
        public Color labelDisabledColor = new Color(0.75f, 0.7f, 0.78f, 0.45f);
        [Tooltip("How far the smoke may spill past the button, as a fraction of its width (x) and height (y) on each side.")]
        public Vector2 spill = new Vector2(0.18f, 0.45f);
        [Range(0f, 3f)] public float driftSpeed = 0.8f;
        [Tooltip("Seconds the colour takes to change on hover.")]
        [Min(0.01f)] public float fadeTime = 0.2f;

        private Button button;
        private Image smoke;
        private TMP_Text label;
        private Material material;
        private bool hovered;
        private bool focused;
        private Vector2 lastSize;

        private static readonly int InnerId = Shader.PropertyToID("_Inner");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int SpeedId = Shader.PropertyToID("_Speed");

        private void Awake()
        {
            var shader = Resources.Load<Shader>("TawanOS_UISmokeButton");
            if (shader == null) { enabled = false; return; }
            button = GetComponent<Button>();

            // Hide the old panel but keep it as the click area
            if (button.targetGraphic is Image panel) panel.color = Color.clear;
            button.transition = Selectable.Transition.None;

            material = new Material(shader);
            material.SetFloat(SeedId, Random.Range(0f, 50f));
            material.SetFloat(SpeedId, driftSpeed);
            material.SetVector(InnerId, new Vector4(spill.x / (1f + 2f * spill.x), spill.y / (1f + 2f * spill.y),
                1f - spill.x / (1f + 2f * spill.x), 1f - spill.y / (1f + 2f * spill.y)));

            var rt = new GameObject("Smoke", typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.SetAsFirstSibling(); // behind the label
            rt.anchorMin = -spill;
            rt.anchorMax = Vector2.one + spill;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            smoke = rt.gameObject.AddComponent<Image>();
            smoke.material = material;
            smoke.raycastTarget = false;
            smoke.color = Target();

            label = GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.color = LabelTarget();
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        private Color Target()
        {
            if (button != null && !button.interactable) return disabledColor;
            return hovered || focused ? hoverColor : normalColor;
        }

        private Color LabelTarget()
        {
            if (button != null && !button.interactable) return labelDisabledColor;
            return hovered || focused ? labelHoverColor : labelNormalColor;
        }

        private void Update()
        {
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / fadeTime);
            smoke.color = Color.Lerp(smoke.color, Target(), k);
            if (label != null) label.color = Color.Lerp(label.color, LabelTarget(), k);

            var size = smoke.rectTransform.rect.size;
            if (size != lastSize && size.y > 0f)
            {
                lastSize = size;
                material.SetFloat(AspectId, size.x / size.y);
            }
        }

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) => hovered = false;
        // Keyboard / gamepad focus gets the same red glow as the cursor: the button transition is None, so without
        // this a Tab-focused button would show no focus at all.
        public void OnSelect(BaseEventData eventData) => focused = true;
        public void OnDeselect(BaseEventData eventData) => focused = false;
    }
}
