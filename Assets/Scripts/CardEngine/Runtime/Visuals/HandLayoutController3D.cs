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
        [Tooltip("Squeeze the hand so every card stays inside the camera's view, however many there are.")]
        public bool fitToCamera = true;
        [Tooltip("Space kept free at each side of the screen, as a share of the screen width.")]
        [Range(0f, 0.2f)] public float screenMargin = 0.03f;

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
        private float lastFitSpan = -1f;
        private Vector2 cardSize; // one card's width and height in the hand container's units, measured once

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
            if (EffectResolver.Instance != null) EffectResolver.Instance.OnCardSummoned += HandleCardSummoned;
        }

        private void OnDestroy()
        {
            if (CardManager.Instance != null)
            {
                CardManager.Instance.OnCardDrawn -= HandleCardDrawn;
                CardManager.Instance.OnCardPlayed -= HandleCardPlayed;
                CardManager.Instance.OnCardDiscarded -= HandleCardDiscarded;
            }
            if (EffectResolver.Instance != null) EffectResolver.Instance.OnCardSummoned -= HandleCardSummoned;
        }

        // คาถาเรียกผี put a familiar straight onto the player's board: give it a 3D card that flies into its slot
        private void HandleCardSummoned(CardInstance card, bool onPlayerSide)
        {
            if (!onPlayerSide || cardPrefab == null) return;

            CardView3D view = Instantiate(cardPrefab, handContainer);
            view.Bind(card);
            view.transform.SetParent(null, worldPositionStays: true);
            view.PlaceOnBoard();
        }

        private void HandleCardDrawn(CardInstance card)
        {
            if (cardPrefab == null || handContainer == null) return;

            CardView3D view = Instantiate(cardPrefab, handContainer);
            view.Bind(card);
            activeViews.Add(view);

            if (card.fromPit && CenterPitView3D.TryGetSpawnPosition(card, out var pitPosition))
            {
                // Taken from the center pit: fly out of its slot instead of the deck
                card.fromPit = false;
                view.transform.position = pitPosition;
                view.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                newlyDrawn.Add(view);
                TawanOS.VFX.PitCardTrail.Spawn(view.transform); // red smoke trailing it to the hand
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
            // The view got narrower or wider (window resized, camera moved to another view): lay the hand out again
            if (!layoutDirty && fitToCamera && activeViews.Count > 1)
            {
                float span = FitSpan();
                if (span > 0f && Mathf.Abs(span - lastFitSpan) > 0.05f) layoutDirty = true;
            }

            if (!layoutDirty) return;
            layoutDirty = false;
            UpdateHandLayout();
        }

        // How far apart (in the hand container's units) the first and last card's centres may be so that both
        // stay on screen, tilt included; 0 when there is no camera to fit to. A camera mid-tween (the hit zoom
        // kick) keeps the last value, so the hand does not shuffle with it.
        private float FitSpan()
        {
            var cam = Camera.main;
            if (cam == null || handContainer == null) return 0f;
            if (lastFitSpan > 0f && DOTween.IsTweening(cam)) return lastFitSpan;

            Vector3 toHand = handContainer.position - cam.transform.position;
            float depth = Vector3.Dot(toHand, cam.transform.forward);
            float scale = Mathf.Abs(handContainer.lossyScale.x);
            if (depth <= 0f || scale <= 0f) return 0f;

            float halfView = depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect;
            float side = Mathf.Abs(Vector3.Dot(toHand, cam.transform.right));
            float halfRoom = (halfView * (1f - 2f * screenMargin) - side) / scale;

            if (cardSize.x <= 0f) cardSize = MeasureCard();
            // The outermost cards are tilted the most: their corners reach out further than half a card width
            float tilt = Mathf.Min(arcAngle * activeViews.Count * 0.5f, 89f) * Mathf.Deg2Rad;
            float cardHalf = 0.5f * (cardSize.x * Mathf.Cos(tilt) + cardSize.y * Mathf.Sin(tilt));
            return Mathf.Max(0.01f, 2f * (halfRoom - cardHalf));
        }

        // A hand card's width and height in the container's units, from its renderers in its own space
        private Vector2 MeasureCard()
        {
            var view = activeViews.Find(v => v != null);
            if (view == null) return Vector2.zero;
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0f), max = -min;
            foreach (var r in view.GetComponentsInChildren<Renderer>(false))
            {
                // Only the card itself: no effects (hover glow, trails, particles) that reach past its edges
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is UnityEngine.VFX.VFXRenderer) continue;
                Bounds b = r.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1) * 2f - Vector3.one);
                    Vector3 p = view.transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }
            if (min.x > max.x) return Vector2.zero;
            Vector3 size = Vector3.Scale(max - min, view.transform.localScale);
            return new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
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
            if (fitToCamera && count > 1)
            {
                float span = FitSpan();
                lastFitSpan = span;
                if (span > 0f) effectiveSpacing = Mathf.Min(effectiveSpacing, span / (count - 1));
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

                // A held card stays at the side of the screen; it only learns where to come back to
                if (view.IsHeld)
                {
                    view.SetRestingTransform(targetPos, targetRot, applyImmediately: false);
                    continue;
                }

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
