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

        [Header("Draw Animation")]
        [Tooltip("Optional. Cards fly from this deck pile into the hand. Auto-found in the scene if empty.")]
        public DeckPileView3D deckPile;
        public float drawFlyDuration = 0.45f;
        public float drawStagger = 0.12f;

        private readonly List<CardView3D> activeViews = new List<CardView3D>();
        // Views drawn since the last layout pass; they fly in from the deck instead of sliding.
        private readonly List<CardView3D> newlyDrawn = new List<CardView3D>();
        private bool layoutDirty;
        private bool simultaneousDraw;

        // The next batch of drawn cards flies in all at once instead of staggered (opening hand)
        public void BeginSimultaneousDraw()
        {
            simultaneousDraw = true;
        }

        private void Start()
        {
            if (handContainer == null) handContainer = transform;
            if (deckPile == null) deckPile = FindFirstObjectByType<DeckPileView3D>();

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

            if (card.fromPit && DrawPitView3D.Instance != null)
            {
                // Pulled from the pit: fly out of it instead of the deck
                card.fromPit = false;
                view.transform.position = DrawPitView3D.Instance.SpawnPosition;
                view.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                newlyDrawn.Add(view);
            }
            else if (deckPile != null)
            {
                // Start lying flat on top of the deck pile; the layout pass flies it into the hand
                view.transform.position = deckPile.DrawSpawnPosition;
                view.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                newlyDrawn.Add(view);
            }

            // Several cards are drawn in the same frame at turn start: lay them out once so the
            // stagger delays are not restarted by each draw.
            layoutDirty = true;
        }

        private void LateUpdate()
        {
            if (!layoutDirty) return;
            layoutDirty = false;
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
            // Drop null views and any card that has left the hand (e.g. reparented onto a board slot),
            // so the layout never drags a placed card back to its old hand position/rotation.
            activeViews.RemoveAll(v => v == null || v.transform.parent != handContainer);
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
                int drawOrder = newlyDrawn.IndexOf(view);
                view.transform.DOKill();

                if (drawOrder >= 0)
                {
                    float delay = simultaneousDraw ? 0f : drawOrder * drawStagger;
                    view.SetRestingTransform(targetPos, targetRot, applyImmediately: false);
                    view.transform.DOLocalMove(targetPos, drawFlyDuration).SetDelay(delay).SetEase(Ease.OutCubic);
                    view.transform.DOLocalRotateQuaternion(targetRot, drawFlyDuration).SetDelay(delay).SetEase(Ease.OutCubic);
                }
                else
                {
                    view.SetRestingTransform(targetPos, targetRot);
                    view.transform.DOLocalMove(targetPos, 0.2f).SetEase(Ease.OutQuad);
                    view.transform.DOLocalRotateQuaternion(targetRot, 0.2f);
                }
            }

            if (newlyDrawn.Count > 0) simultaneousDraw = false;
            newlyDrawn.Clear();
        }
    }
}
