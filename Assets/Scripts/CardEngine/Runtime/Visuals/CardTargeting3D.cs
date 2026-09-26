using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    // Lets the player pick the card an incantation acts on (บทแผ่เมตตา, คาถาสะกด, ...).
    // Valid targets on the board pulse, an arrow follows the mouse from the played card, the hovered
    // target lifts. Left-click a valid target to confirm; right-click or Esc cancels. Created on demand.
    public class CardTargeting3D : MonoBehaviour
    {
        public static CardTargeting3D Instance { get; private set; }

        // True while choosing, and for the frame the choice ends, so that click does not also
        // start dragging or hovering another card
        public static bool BlocksInput => Instance != null && (Instance.active || Time.frameCount <= Instance.endedFrame);

        [Header("Look")]
        public Color arrowColor = new Color(1f, 0.85f, 0.3f);
        public float arrowWidth = 0.06f;
        public float pulseScale = 1.08f;
        public float pulseDuration = 0.45f;
        public float hoverLift = 0.25f;

        private bool active;
        private int endedFrame = -1;
        private CardView3D source;
        private readonly List<CardView3D> targetViews = new List<CardView3D>();
        private readonly Dictionary<CardView3D, Vector3> baseScales = new Dictionary<CardView3D, Vector3>();
        private CardView3D hovered;
        private Action<CardInstance> onChosen;
        private Action onCancelled;
        private LineRenderer arrow;
        private Camera cam;

        public static CardTargeting3D Ensure()
        {
            if (Instance == null) new GameObject("CardTargeting3D").AddComponent<CardTargeting3D>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            arrow = gameObject.AddComponent<LineRenderer>();
            arrow.positionCount = 2;
            arrow.startWidth = arrowWidth;
            arrow.endWidth = arrowWidth * 0.4f;
            arrow.material = new Material(Shader.Find("Sprites/Default"));
            arrow.startColor = arrowColor;
            arrow.endColor = arrowColor;
            arrow.enabled = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // targets = the cards the ability may act on; their views on the board are highlighted
        public void Begin(CardView3D sourceView, List<CardInstance> targets, Action<CardInstance> chosen, Action cancelled)
        {
            if (active) Cancel();

            source = sourceView;
            onChosen = chosen;
            onCancelled = cancelled;
            cam = Camera.main;
            active = true;

            foreach (var view in FindObjectsByType<CardView3D>(FindObjectsSortMode.None))
            {
                if (view == sourceView || view.CardData == null || !targets.Contains(view.CardData)) continue;
                targetViews.Add(view);
                baseScales[view] = view.transform.localScale;
                view.transform.DOKill();
                view.transform.DOScale(view.transform.localScale * pulseScale, pulseDuration)
                    .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            }

            arrow.enabled = true;
            Debug.Log($"[Targeting] {sourceView.CardData.cardNameThai}: choose one of {targetViews.Count} cards (right-click / Esc to cancel)");
        }

        private void Update()
        {
            if (!active) return;
            if (source == null)
            {
                Cancel();
                return;
            }

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                Cancel();
                return;
            }

            Vector3 aimPoint;
            var over = TargetUnderMouse(out aimPoint);
            SetHovered(over);

            arrow.SetPosition(0, source.transform.position);
            arrow.SetPosition(1, over != null ? over.transform.position : aimPoint);

            if (Input.GetMouseButtonDown(0) && over != null)
            {
                var callback = onChosen;
                var chosen = over.CardData;
                End();
                callback?.Invoke(chosen);
            }
        }

        private CardView3D TargetUnderMouse(out Vector3 aimPoint)
        {
            if (cam == null) cam = Camera.main;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            aimPoint = ray.GetPoint(8f);

            var hits = Physics.RaycastAll(ray, 200f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var view = hit.collider.GetComponentInParent<CardView3D>();
                if (view == null || view == source) continue;
                aimPoint = hit.point;
                if (targetViews.Contains(view)) return view;
            }
            return null;
        }

        private void SetHovered(CardView3D view)
        {
            if (hovered == view) return;
            if (hovered != null) hovered.transform.DOLocalMove(Vector3.zero, 0.12f);
            hovered = view;
            if (hovered != null) hovered.transform.DOLocalMove(Vector3.up * hoverLift, 0.12f);
        }

        public void Cancel()
        {
            if (!active) return;
            var callback = onCancelled;
            End();
            callback?.Invoke();
        }

        // Puts every highlighted card back exactly as it was
        private void End()
        {
            active = false;
            endedFrame = Time.frameCount;
            arrow.enabled = false;

            foreach (var view in targetViews)
            {
                if (view == null) continue;
                view.transform.DOKill();
                view.transform.localScale = baseScales[view];
                view.transform.localPosition = Vector3.zero; // board cards rest on their slot's centre
            }
            targetViews.Clear();
            baseScales.Clear();
            hovered = null;
            source = null;
            onChosen = null;
            onCancelled = null;
        }
    }
}
