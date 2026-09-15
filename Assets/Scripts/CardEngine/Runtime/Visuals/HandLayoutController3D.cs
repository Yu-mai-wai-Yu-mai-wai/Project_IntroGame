using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    public class HandLayoutController3D : MonoBehaviour
    {
        [Header("Prefab & Hierarchy")]
        public CardView3D cardPrefab;
        public Transform handContainer;

        [Header("Arc Layout Settings")]
        public float cardSpacing = 0.9f;
        public float maxTotalWidth = 6f;
        public float arcAngle = 5f;
        public float curveDepth = 0.15f;

        private readonly List<CardView3D> activeViews = new List<CardView3D>();

        private void Start()
        {
            if (handContainer == null) handContainer = transform;

            if (CardManager.Instance != null)
            {
                CardManager.Instance.OnCardDrawn += HandleCardDrawn;
                CardManager.Instance.OnCardPlayed += HandleCardPlayed;
                CardManager.Instance.OnCardDiscarded += HandleCardDiscarded;
            }
        }

        private void OnDestroy()
        {
            if (CardManager.Instance != null)
            {
                CardManager.Instance.OnCardDrawn -= HandleCardDrawn;
                CardManager.Instance.OnCardPlayed -= HandleCardPlayed;
                CardManager.Instance.OnCardDiscarded -= HandleCardDiscarded;
            }
        }

        private void HandleCardDrawn(CardInstance card)
        {
            if (cardPrefab == null || handContainer == null) return;

            CardView3D view = Instantiate(cardPrefab, handContainer);
            view.Bind(card);
            activeViews.Add(view);
            UpdateHandLayout();
        }

        private void HandleCardPlayed(CardInstance card)
        {
            // Amulet/Familiar cards are taken over by CardView3D.SnapToBoardSlot (it reparents the
            // same GameObject onto the board), so the hand must only stop tracking them here, never
            // destroy them - destroying it out from under the in-progress snap causes DOTween to
            // keep animating a null/destroyed Transform next frame.
            bool staysOnBoard = card.cardType == CardType.Amulet || card.cardType == CardType.Familiar;
            RemoveFromHand(card, destroyView: !staysOnBoard);
        }

        private void HandleCardDiscarded(CardInstance card)
        {
            RemoveFromHand(card, destroyView: true);
        }

        private void RemoveFromHand(CardInstance card, bool destroyView)
        {
            for (int i = activeViews.Count - 1; i >= 0; i--)
            {
                if (activeViews[i] == null || activeViews[i].CardData == card)
                {
                    if (activeViews[i] != null && destroyView)
                    {
                        activeViews[i].transform.DOKill();
                        Destroy(activeViews[i].gameObject);
                    }
                    activeViews.RemoveAt(i);
                }
            }
            UpdateHandLayout();
        }

        public void UpdateHandLayout()
        {
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
