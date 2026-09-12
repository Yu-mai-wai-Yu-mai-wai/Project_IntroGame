using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    public class HandLayoutController : MonoBehaviour
    {
        [Header("Prefab & Hierarchy")]
        public CardView cardPrefab;
        public Transform handContainer;

        [Header("Arc Layout Settings")]
        public float cardSpacing = 120f;
        public float maxTotalWidth = 800f;
        public float arcAngle = 5f;
        public float curveDepth = 15f;

        private readonly List<CardView> activeViews = new List<CardView>();

        private void Start()
        {
            if (handContainer == null) handContainer = transform;

            if (CardManager.Instance != null)
            {
                CardManager.Instance.OnCardDrawn += HandleCardDrawn;
                CardManager.Instance.OnCardPlayed += HandleCardRemoved;
                CardManager.Instance.OnCardDiscarded += HandleCardRemoved;
            }
        }

        private void OnDestroy()
        {
            if (CardManager.Instance != null)
            {
                CardManager.Instance.OnCardDrawn -= HandleCardDrawn;
                CardManager.Instance.OnCardPlayed -= HandleCardRemoved;
                CardManager.Instance.OnCardDiscarded -= HandleCardRemoved;
            }
        }

        private void HandleCardDrawn(CardInstance card)
        {
            if (cardPrefab == null || handContainer == null) return;

            CardView view = Instantiate(cardPrefab, handContainer);
            view.Bind(card);
            activeViews.Add(view);
            UpdateHandLayout();
        }

        private void HandleCardRemoved(CardInstance card)
        {
            for (int i = activeViews.Count - 1; i >= 0; i--)
            {
                if (activeViews[i] == null || activeViews[i].CardData == card)
                {
                    if (activeViews[i] != null) Destroy(activeViews[i].gameObject);
                    activeViews.RemoveAt(i);
                }
            }
            UpdateHandLayout();
        }

        public void UpdateHandLayout()
        {
            // Clean nulls
            activeViews.RemoveAll(v => v == null);
            int count = activeViews.Count;
            if (count == 0) return;

            float effectiveSpacing = cardSpacing;
            if (count * cardSpacing > maxTotalWidth)
            {
                effectiveSpacing = maxTotalWidth / count;
            }

            float startX = -(count - 1) * effectiveSpacing * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) - 0.5f : 0f; // -0.5 to +0.5
                float x = startX + i * effectiveSpacing;
                float y = -Mathf.Abs(t) * curveDepth * 2f;
                float angle = -t * arcAngle * count;

                Vector3 targetPos = new Vector3(x, y, 0);
                Quaternion targetRot = Quaternion.Euler(0, 0, angle);

                var view = activeViews[i];
                view.SetRestingTransform(targetPos, targetRot);
                view.transform.DOLocalMove(targetPos, 0.2f).SetEase(Ease.OutQuad);
                view.transform.DOLocalRotateQuaternion(targetRot, 0.2f);
            }
        }
    }
}
