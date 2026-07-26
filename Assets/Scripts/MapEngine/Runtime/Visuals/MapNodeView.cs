using System;
using System.Collections;
using UnityEngine;

namespace TawanOS.MapEngine
{
    public class MapNodeView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer iconRenderer;
        [SerializeField] private SpriteRenderer backgroundRenderer;
        [SerializeField] private Vector3 baseScale = Vector3.one;
        [SerializeField] private Vector3 hoverScale = new Vector3(1.25f, 1.25f, 1.25f);
        [SerializeField] private float animationSpeed = 10f;

        public NodeBlueprint NodeData { get; private set; }
        public NodeProfileSO Profile { get; private set; }

        public event Action<MapNodeView> OnNodeClicked;
        public event Action<MapNodeView> OnNodeHoverEnter;
        public event Action<MapNodeView> OnNodeHoverExit;

        private Vector3 targetScale;
        private Coroutine scaleCoroutine;
        private Color currentColor;

        private void Awake()
        {
            targetScale = baseScale;
            transform.localScale = baseScale;
        }

        public void Setup(NodeBlueprint nodeData, NodeProfileSO profile, Vector3 worldPosition)
        {
            this.NodeData = nodeData;
            this.Profile = profile;
            transform.position = worldPosition;

            if (iconRenderer != null && profile != null && profile.icon != null)
            {
                iconRenderer.sprite = profile.icon;
            }

            UpdateVisualState();
        }

        public void UpdateVisualState()
        {
            if (NodeData == null) return;

            Color targetColor = Profile != null ? Profile.baseColor : Color.white;

            switch (NodeData.status)
            {
                case NodeStatus.Attainable:
                    targetColor = Profile != null ? Profile.hoverColor : Color.cyan;
                    break;
                case NodeStatus.Visited:
                    targetColor = Profile != null ? Profile.visitedColor : new Color(0.9f, 0.75f, 0.2f);
                    break;
                case NodeStatus.Disabled:
                case NodeStatus.Locked:
                    targetColor = new Color(0.3f, 0.3f, 0.35f, 0.6f);
                    break;
            }

            currentColor = targetColor;
            if (iconRenderer != null) iconRenderer.color = targetColor;
            if (backgroundRenderer != null) backgroundRenderer.color = targetColor * 0.8f;
        }

        private void OnMouseEnter()
        {
            if (NodeData.status == NodeStatus.Attainable)
            {
                SetTargetScale(hoverScale);
                OnNodeHoverEnter?.Invoke(this);
            }
        }

        private void OnMouseExit()
        {
            SetTargetScale(baseScale);
            OnNodeHoverExit?.Invoke(this);
        }

        private void OnMouseDown()
        {
            if (NodeData.status == NodeStatus.Attainable)
            {
                OnNodeClicked?.Invoke(this);
            }
        }

        private void SetTargetScale(Vector3 scale)
        {
            targetScale = scale;
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(AnimateScale());
        }

        private IEnumerator AnimateScale()
        {
            while (Vector3.Distance(transform.localScale, targetScale) > 0.01f)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * animationSpeed);
                yield return null;
            }
            transform.localScale = targetScale;
        }
    }
}
