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

        public NodeBlueprint NodeData { get; private set; }
        public NodeProfileSO Profile { get; private set; }

        public event Action<MapNodeView> OnNodeClicked;
        public event Action<MapNodeView> OnNodeHoverEnter;
        public event Action<MapNodeView> OnNodeHoverExit;

        private Tween scaleTween;
        private Tween pulseTween;

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
        }

        public void Setup(NodeBlueprint nodeData, NodeProfileSO profile, Vector3 worldPosition)
        {
            this.NodeData = nodeData;
            this.Profile = profile;
            transform.position = worldPosition;

            baseScale = (nodeData.type == NodeType.Boss) ? new Vector3(1.5f, 1.5f, 1.5f) : Vector3.one;
            transform.localScale = baseScale;
            hoverScale = baseScale * 1.25f;

            Sprite circleSprite = GetFallbackCircleSprite();

            if (backgroundRenderer != null)
            {
                backgroundRenderer.sprite = circleSprite;
                backgroundRenderer.sortingOrder = 0;
                backgroundRenderer.transform.localPosition = Vector3.zero;
                backgroundRenderer.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
                backgroundRenderer.color = profile != null ? profile.baseColor : new Color(0.12f, 0.12f, 0.16f, 0.95f);
            }

            if (iconRenderer != null)
            {
                iconRenderer.sortingOrder = 1;
                iconRenderer.transform.localPosition = new Vector3(0f, 0f, -0.1f);
                iconRenderer.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
                iconRenderer.color = Color.white;

                if (profile != null && profile.icon != null)
                {
                    iconRenderer.sprite = profile.icon;
                }
                else if (iconRenderer.sprite == null)
                {
                    iconRenderer.sprite = circleSprite;
                }
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
                    targetColor = Profile != null ? Profile.hoverColor : Color.cyan;
                    bgColor = targetColor * 0.4f;
                    bgColor.a = 1f;
                    pulseTween = transform.DOScale(baseScale * 1.15f, 0.7f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
                    break;
                case NodeStatus.Visited:
                    targetColor = Profile != null ? Profile.visitedColor : new Color(0.9f, 0.75f, 0.2f);
                    bgColor = targetColor * 0.3f;
                    bgColor.a = 1f;
                    transform.localScale = baseScale;
                    break;
                case NodeStatus.Disabled:
                case NodeStatus.Locked:
                    targetColor = new Color(0.4f, 0.4f, 0.45f, 0.6f);
                    bgColor = new Color(0.1f, 0.1f, 0.14f, 0.7f);
                    transform.localScale = baseScale * 0.9f;
                    break;
            }

            if (iconRenderer != null) iconRenderer.color = targetColor;
            if (backgroundRenderer != null) backgroundRenderer.color = bgColor;
        }

        private void OnMouseEnter()
        {
            if (NodeData.status == NodeStatus.Attainable)
            {
                if (pulseTween != null) pulseTween.Pause();
                scaleTween = transform.DOScale(hoverScale, animationSpeed).SetEase(Ease.OutBack);
                OnNodeHoverEnter?.Invoke(this);
            }
        }

        private void OnMouseExit()
        {
            if (NodeData.status == NodeStatus.Attainable)
            {
                scaleTween = transform.DOScale(baseScale, animationSpeed).OnComplete(() =>
                {
                    if (pulseTween != null) pulseTween.Play();
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
