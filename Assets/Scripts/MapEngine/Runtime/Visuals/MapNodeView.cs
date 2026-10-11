using System;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace TawanOS.MapEngine
{
    public class MapNodeView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public SpriteRenderer iconRenderer;
        public SpriteRenderer backgroundRenderer;
        [SerializeField] private Vector3 baseScale = Vector3.one;
        [Tooltip("Icon width in world units, independent of PNG resolution. FBX pedestal disc is 1.4.")]
        [SerializeField] private float iconWorldSize = 1.2f;

        [Header("Hover ring (a thin black circle around a node the player can go to)")]
        [Tooltip("Ring diameter as a multiple of the icon size.")]
        [SerializeField] private float hoverRingSize = 1.45f;
        [Tooltip("Line width as a fraction of the ring's radius.")]
        [Range(0.01f, 0.3f)] [SerializeField] private float hoverRingThickness = 0.06f;
        [SerializeField] private Color hoverRingColor = new Color(0f, 0f, 0f, 0.9f);
        [Tooltip("Seconds the ring takes to show or hide.")]
        [SerializeField] private float hoverRingFade = 0.15f;

        public NodeBlueprint NodeData { get; private set; }
        public NodeProfileSO Profile { get; private set; }

        public event Action<MapNodeView> OnNodeClicked;
        public event Action<MapNodeView> OnNodeHoverEnter;
        public event Action<MapNodeView> OnNodeHoverExit;

        private Tween ringTween;
        private SpriteRenderer hoverRing;

        private void Awake()
        {
            transform.localScale = baseScale;
        }

        private void OnDestroy()
        {
            KillTweens();
        }

        private void KillTweens()
        {
            if (ringTween != null && ringTween.IsActive()) ringTween.Kill();
        }

        public void Setup(NodeBlueprint nodeData, NodeProfileSO profile, Vector3 worldPosition)
        {
            this.NodeData = nodeData;
            this.Profile = profile;
            transform.position = worldPosition;
            transform.rotation = Quaternion.Euler(90f, 180f, 0f); // Lie flat on paper board facing flipped camera

            baseScale = (nodeData.type == NodeType.Boss) ? new Vector3(1.3f, 1.3f, 1.3f) : Vector3.one;
            transform.localScale = baseScale;

            Sprite circleSprite = GetFallbackCircleSprite();

            if (backgroundRenderer != null)
            {
                // In 3D paper board mode, hide circular disc to reveal authentic paper texture
                backgroundRenderer.enabled = false;
            }

            if (iconRenderer != null)
            {
                iconRenderer.sortingOrder = 1;
                iconRenderer.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                iconRenderer.color = Color.white;

                if (profile != null && profile.icon != null)
                {
                    iconRenderer.sprite = profile.icon;
                }
                else if (iconRenderer.sprite == null)
                {
                    iconRenderer.sprite = circleSprite;
                }

                // Icons ship at different PNG sizes (600-855px); fit the larger side to iconWorldSize. Boss gets +30% via baseScale.
                Vector2 spriteSize = iconRenderer.sprite.bounds.size;
                float fit = iconWorldSize / Mathf.Max(spriteSize.x, spriteSize.y, 0.001f);
                iconRenderer.transform.localScale = new Vector3(fit, fit, 1f);
            }

            SetupHoverRing();

            UpdateVisualState();
        }

        private static Sprite fallbackCircleSprite;
        private static Sprite GetFallbackCircleSprite()
        {
            if (fallbackCircleSprite == null)
            {
                int res = 64;
                Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
                Color[] colors = new Color[res * res];
                float radius = res * 0.45f;
                Vector2 center = new Vector2(res * 0.5f, res * 0.5f);

                for (int y = 0; y < res; y++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), center);
                        colors[y * res + x] = dist <= radius ? Color.white : Color.clear;
                    }
                }
                tex.SetPixels(colors);
                tex.Apply();
                fallbackCircleSprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 100f);
            }
            return fallbackCircleSprite;
        }

        public void UpdateVisualState()
        {
            if (NodeData == null) return;

            KillTweens();
            Color targetColor = Profile != null ? Profile.baseColor : Color.white;
            Color bgColor = new Color(0.12f, 0.12f, 0.16f, 0.95f);

            switch (NodeData.status)
            {
                case NodeStatus.Attainable:
                    targetColor = Profile != null ? Profile.hoverColor : new Color(1f, 0.85f, 0.4f, 1f);
                    bgColor = targetColor * 0.4f;
                    bgColor.a = 1f;

                    // Still, full-strength icon (Game Designer, 11 Oct: no breathing / glow loop). Pointing at it shows
                    // the hover ring; the locked nodes around it are faded, so it stands out without moving.
                    transform.localScale = baseScale;
                    if (iconRenderer != null) iconRenderer.color = Color.white;
                    break;
                case NodeStatus.Visited:
                    targetColor = Profile != null ? Profile.visitedColor : new Color(0.9f, 0.75f, 0.2f);
                    bgColor = targetColor * 0.3f;
                    bgColor.a = 1f;
                    transform.localScale = baseScale;
                    if (iconRenderer != null) iconRenderer.color = targetColor;
                    break;
                case NodeStatus.Disabled:
                case NodeStatus.Locked:
                    targetColor = Profile != null 
                        ? new Color(Profile.baseColor.r, Profile.baseColor.g, Profile.baseColor.b, 0.35f) 
                        : new Color(0.353f, 0.094f, 0.063f, 0.35f);
                    bgColor = new Color(0.1f, 0.1f, 0.14f, 0.2f);
                    transform.localScale = baseScale * 0.9f;
                    if (iconRenderer != null) iconRenderer.color = targetColor;
                    break;
            }

            if (backgroundRenderer != null) backgroundRenderer.color = bgColor;
            ShowHoverRing(false, instant: true);
        }

        private void OnMouseEnter()
        {
            if (CardEngine.DeckViewerPanelUI.BlocksInput) return; // the deck screen is over the map
            if (NodeData != null && NodeData.status == NodeStatus.Attainable)
            {
                ShowHoverRing(true);
                OnNodeHoverEnter?.Invoke(this);
            }
        }

        private void OnMouseExit()
        {
            ShowHoverRing(false);
            if (NodeData != null && NodeData.status == NodeStatus.Attainable)
            {
                OnNodeHoverExit?.Invoke(this);
            }
        }

        // A thin black ring lying on the paper around the icon, hidden until the pointer is on a node the player can go to
        private void SetupHoverRing()
        {
            if (hoverRing == null)
            {
                var go = new GameObject("HoverRing");
                go.transform.SetParent(transform, false);
                hoverRing = go.AddComponent<SpriteRenderer>();
            }
            hoverRing.sprite = GetRingSprite(hoverRingThickness);
            hoverRing.sortingOrder = 2;
            hoverRing.transform.localPosition = new Vector3(0f, 0f, -0.012f);
            hoverRing.transform.localRotation = Quaternion.identity;
            float size = iconWorldSize * hoverRingSize / Mathf.Max(hoverRing.sprite.bounds.size.x, 0.001f);
            hoverRing.transform.localScale = new Vector3(size, size, 1f);
            ShowHoverRing(false, instant: true);
        }

        private void ShowHoverRing(bool show, bool instant = false)
        {
            if (hoverRing == null) return;
            if (ringTween != null && ringTween.IsActive()) ringTween.Kill();
            var target = hoverRingColor;
            if (!show) target.a = 0f;
            if (instant) hoverRing.color = target;
            else ringTween = hoverRing.DOColor(target, hoverRingFade).SetLink(gameObject);
        }

        private static Sprite ringSprite;
        private static float ringSpriteThickness = -1f;

        private static Sprite GetRingSprite(float thickness)
        {
            if (ringSprite != null && Mathf.Approximately(ringSpriteThickness, thickness)) return ringSprite;
            const int res = 256;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var colors = new Color[res * res];
            float outer = res * 0.5f - 2f;
            float inner = outer * (1f - thickness);
            var center = new Vector2(res * 0.5f, res * 0.5f);
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    // Soft one-pixel edges on both sides of the line
                    float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                    colors[y * res + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            ringSprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 100f);
            ringSpriteThickness = thickness;
            return ringSprite;
        }

        private void OnMouseDown()
        {
            if (CardEngine.DeckViewerPanelUI.BlocksInput) return;
            if (NodeData != null && NodeData.status == NodeStatus.Attainable)
            {
                OnNodeClicked?.Invoke(this);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (NodeData != null && NodeData.status == NodeStatus.Attainable)
            {
                OnNodeClicked?.Invoke(this);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnMouseEnter();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnMouseExit();
        }
    }
}
