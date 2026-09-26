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
        [SerializeField] private Vector3 hoverScale = new Vector3(1.25f, 1.25f, 1.25f);
        [SerializeField] private float animationSpeed = 0.2f;
        [Tooltip("Icon width in world units, independent of PNG resolution. FBX pedestal disc is 1.4.")]
        [SerializeField] private float iconWorldSize = 1.2f;

        public NodeBlueprint NodeData { get; private set; }
        public NodeProfileSO Profile { get; private set; }

        public event Action<MapNodeView> OnNodeClicked;
        public event Action<MapNodeView> OnNodeHoverEnter;
        public event Action<MapNodeView> OnNodeHoverExit;

        private Tween scaleTween;
        private Tween pulseTween;
        private Tween colorTween;

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
            if (scaleTween != null && scaleTween.IsActive()) scaleTween.Kill();
            if (pulseTween != null && pulseTween.IsActive()) pulseTween.Kill();
            if (colorTween != null && colorTween.IsActive()) colorTween.Kill();
        }

        public void Setup(NodeBlueprint nodeData, NodeProfileSO profile, Vector3 worldPosition)
        {
            this.NodeData = nodeData;
            this.Profile = profile;
            transform.position = worldPosition;
            transform.rotation = Quaternion.Euler(90f, 180f, 0f); // Lie flat on paper board facing flipped camera

            baseScale = (nodeData.type == NodeType.Boss) ? new Vector3(1.3f, 1.3f, 1.3f) : Vector3.one;
            transform.localScale = baseScale;
            hoverScale = baseScale * 1.2f;

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

                    // Smooth breathing pulse animation
                    pulseTween = transform.DOScale(baseScale * 1.15f, 0.75f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);

                    // Luminous glow pulse for attainable icon
                    Color glowColor = Color.Lerp(targetColor, Color.white, 0.55f);
                    if (iconRenderer != null)
                    {
                        colorTween = iconRenderer.DOColor(glowColor, 0.75f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
                    }
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
        }

        private void OnMouseEnter()
        {
            if (NodeData != null && NodeData.status == NodeStatus.Attainable)
            {
                if (pulseTween != null && pulseTween.IsActive()) pulseTween.Pause();
                scaleTween = transform.DOScale(hoverScale, animationSpeed).SetEase(Ease.OutBack);
                OnNodeHoverEnter?.Invoke(this);
            }
        }

        private void OnMouseExit()
        {
            if (NodeData != null && NodeData.status == NodeStatus.Attainable)
            {
                scaleTween = transform.DOScale(baseScale, animationSpeed).OnComplete(() =>
                {
                    if (pulseTween != null && pulseTween.IsActive()) pulseTween.Play();
                });
                OnNodeHoverExit?.Invoke(this);
            }
            else
            {
                scaleTween = transform.DOScale(baseScale, animationSpeed);
            }
        }

        private void OnMouseDown()
        {
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
