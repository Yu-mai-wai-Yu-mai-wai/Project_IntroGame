using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    // Lets the player pick the card an incantation acts on (บทแผ่เมตตา, คาถาสะกด, ...).
    // Valid targets on the board pulse and a bobbing arrow points down at the chosen one (like the
    // cursor in Yu-Gi-Oh! GX Tag Force). The arrow follows the mouse onto a target, or Left / Right
    // (A / D) move it between targets. Left-click a target or Enter confirms; right-click or Esc
    // cancels. Created on demand.
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
        [Tooltip("Also draw a line from the played card to the mouse.")]
        public bool showAimLine = false;

        [Header("Target Pointer")]
        [Tooltip("Optional model / sprite for the pointer: faces the camera, its tip at its origin pointing down (-Y). Empty = a built-in arrow.")]
        public GameObject pointerPrefab;
        public Color pointerColor = new Color(1f, 0.85f, 0.3f);
        public Color pointerOutlineColor = new Color(0.25f, 0.1f, 0.05f);
        public float pointerSize = 0.9f;
        [Tooltip("How far above the target card (on screen) the arrow's tip sits.")]
        public float pointerHeight = 1.1f;
        public float pointerBob = 0.25f;
        public float pointerBobSpeed = 6f;
        [Tooltip("Colour multiplier for the arrow when it shows the enemy's pick.")]
        public Color enemyPointerTint = new Color(1f, 0.3f, 0.3f);
        [Tooltip("How quickly the arrow glides to a newly chosen card.")]
        public float pointerFollowSpeed = 14f;

        private bool active;
        private int endedFrame = -1;
        private CardView3D source;
        private readonly List<CardView3D> targetViews = new List<CardView3D>();
        private readonly Dictionary<CardView3D, Vector3> baseScales = new Dictionary<CardView3D, Vector3>();
        private CardView3D hovered;
        private CardView3D focused; // the card the pointer is on
        private Transform pointer;
        private float pointOnlyUntil; // PointAt: show the arrow without a choice until this time
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

            arrow.enabled = showAimLine;

            // The pointer starts on the target nearest the mouse
            focused = NearestToMouse();
            if (pointer == null) pointer = BuildPointer();
            pointOnlyUntil = 0f;
            SetPointerTint(Color.white);
            if (focused != null) pointer.SetPositionAndRotation(PointerPosition(focused), cam.transform.rotation);
            pointer.gameObject.SetActive(focused != null);

            Debug.Log($"[Targeting] {sourceView.CardData.cardNameThai}: choose one of {targetViews.Count} cards (right-click / Esc to cancel)");
        }

        // Shows the arrow on a card for `duration` seconds without asking for a choice (e.g. the card an
        // enemy incantation acts on). Ignored while the player is choosing.
        public void PointAt(CardInstance card, float duration, bool enemy)
        {
            if (active || card == null) return;

            CardView3D view = null;
            foreach (var v in FindObjectsByType<CardView3D>(FindObjectsSortMode.None))
            {
                if (v.CardData == card) { view = v; break; }
            }
            if (view == null) return;

            cam = Camera.main;
            if (cam == null) return;
            if (pointer == null) pointer = BuildPointer();
            SetPointerTint(enemy ? enemyPointerTint : Color.white);
            if (focused == null) pointer.SetPositionAndRotation(PointerPosition(view), cam.transform.rotation);
            focused = view;
            pointOnlyUntil = Time.time + duration;
            pointer.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (TawanOS.UI.PauseMenu.IsPaused) return;
            if (!active)
            {
                if (pointOnlyUntil <= 0f) return;
                if (Time.time >= pointOnlyUntil || focused == null)
                {
                    pointOnlyUntil = 0f;
                    focused = null;
                }
                MovePointer();
                return;
            }
            if (source == null)
            {
                Cancel();
                return;
            }

            if (Input.GetMouseButtonDown(1) || TawanOS.UI.EscapeKey.Use())
            {
                Cancel();
                return;
            }

            Vector3 aimPoint;
            var over = TargetUnderMouse(out aimPoint);
            if (over != null) focused = over;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Step(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Step(1);
            SetHovered(focused);
            MovePointer();

            if (showAimLine)
            {
                arrow.SetPosition(0, source.transform.position);
                arrow.SetPosition(1, over != null ? over.transform.position : aimPoint);
            }

            CardView3D pick = Input.GetMouseButtonDown(0) ? over
                : Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ? focused
                : null;
            if (pick != null)
            {
                var callback = onChosen;
                var chosen = pick.CardData;
                End();
                callback?.Invoke(chosen);
            }
        }

        // ---------------------------------------------------------------- pointer

        // Above the card on screen, a little towards the camera so the card never covers it
        private Vector3 PointerPosition(CardView3D view)
        {
            float bob = (Mathf.Sin(Time.time * pointerBobSpeed) * 0.5f + 0.5f) * pointerBob;
            Transform c = cam.transform;
            return view.transform.position + c.up * (pointerHeight + bob) - c.forward * 0.5f;
        }

        private void MovePointer()
        {
            if (pointer == null) return;
            pointer.gameObject.SetActive(focused != null);
            if (focused == null) return;

            if (cam == null) cam = Camera.main;
            pointer.position = Vector3.Lerp(pointer.position, PointerPosition(focused), 1f - Mathf.Exp(-pointerFollowSpeed * Time.deltaTime));
            pointer.rotation = cam.transform.rotation; // flat arrow, always facing the player
        }

        // Moves the pointer to the next target left (-1) or right (+1) on screen
        private void Step(int direction)
        {
            targetViews.RemoveAll(v => v == null);
            if (targetViews.Count == 0) return;
            if (cam == null) cam = Camera.main;

            var ordered = new List<CardView3D>(targetViews);
            ordered.Sort((a, b) => cam.WorldToScreenPoint(a.transform.position).x.CompareTo(cam.WorldToScreenPoint(b.transform.position).x));
            int index = focused != null ? ordered.IndexOf(focused) : -1;
            index = index < 0 ? 0 : (index + direction + ordered.Count) % ordered.Count;
            focused = ordered[index];
        }

        private CardView3D NearestToMouse()
        {
            if (cam == null) cam = Camera.main;
            CardView3D best = null;
            float bestDistance = float.MaxValue;
            foreach (var view in targetViews)
            {
                if (view == null) continue;
                float d = ((Vector2)cam.WorldToScreenPoint(view.transform.position) - (Vector2)Input.mousePosition).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = view;
                }
            }
            return best;
        }

        private void SetPointerTint(Color tint)
        {
            if (pointer == null) return;
            foreach (var r in pointer.GetComponentsInChildren<Renderer>())
            {
                var m = r.material;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
                else if (m.HasProperty("_Color")) m.color = tint;
            }
        }

        private Transform BuildPointer()
        {
            if (pointerPrefab != null)
            {
                var copy = Instantiate(pointerPrefab, transform);
                foreach (var c in copy.GetComponentsInChildren<Collider>()) Destroy(c); // never block card clicks
                return copy.transform;
            }

            var go = new GameObject("TargetPointer", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * pointerSize;
            go.GetComponent<MeshFilter>().sharedMesh = BuildArrowMesh(pointerColor, pointerOutlineColor);
            go.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            return go.transform;
        }

        // A flat arrow pointing down (tip at the origin) with a dark outline, facing the camera like a
        // UI cursor. Colours are vertex colours, drawn outline first, so it needs no lighting or sorting.
        private static Mesh BuildArrowMesh(Color fill, Color outline)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();

            void Arrow(float grow, float z, Color color)
            {
                // Head: triangle from the tip up to a wide base; shaft: rectangle above it
                const float headHeight = 0.55f, headHalf = 0.42f, shaftHeight = 0.5f, shaftHalf = 0.16f;
                var tip = new Vector3(0f, -grow * 1.6f, z);
                var baseL = new Vector3(-headHalf - grow * 1.4f, headHeight + grow * 0.6f, z);
                var baseR = new Vector3(headHalf + grow * 1.4f, headHeight + grow * 0.6f, z);
                var shaftBL = new Vector3(-shaftHalf - grow, headHeight, z);
                var shaftBR = new Vector3(shaftHalf + grow, headHeight, z);
                var shaftTL = new Vector3(-shaftHalf - grow, headHeight + shaftHeight + grow, z);
                var shaftTR = new Vector3(shaftHalf + grow, headHeight + shaftHeight + grow, z);

                foreach (var v in new[] { tip, baseL, baseR, shaftBL, shaftTL, shaftTR, shaftBL, shaftTR, shaftBR })
                {
                    vertices.Add(v);
                    colors.Add(color);
                }
            }

            Arrow(0.07f, 0.01f, outline); // behind (the camera looks along +Z)
            Arrow(0f, 0f, fill);

            var triangles = new int[vertices.Count];
            for (int i = 0; i < triangles.Length; i++) triangles[i] = i;

            var mesh = new Mesh { name = "TargetPointerArrow" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
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
            if (pointer != null) pointer.gameObject.SetActive(false);
            focused = null;

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
