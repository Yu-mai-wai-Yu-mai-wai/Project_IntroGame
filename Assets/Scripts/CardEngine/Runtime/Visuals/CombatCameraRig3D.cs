using UnityEngine;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    // Combat camera: the normal angled view, or a top-down view zoomed so the board cards are big and clear.
    //  - C key or the button at the top-right toggles the board-only top view: UI and both hands are hidden
    //    and the camera zooms until the two board rows fill the screen
    //  - Holding a card (CardPlayController3D) switches to the top view until the card is played / put back
    //  - Right-click a card to open its detail screen (CardDetailPanelUI)
    // The zoom narrows the field of view instead of moving the camera down, and the hands (which follow
    // the camera) are scaled to match, so they keep their size and place on screen.
    // Bootstraps itself in combat scenes.
    [DefaultExecutionOrder(-100)]
    public class CombatCameraRig3D : MonoBehaviour
    {
        public static CombatCameraRig3D Instance { get; private set; }

        [Header("Controls")]
        public KeyCode toggleKey = KeyCode.C;
        public bool showToggleButton = true;

        [Header("Top View")]
        [Tooltip("Camera height above the board. Keep it above the hands' distance from the camera (about 10).")]
        public float topHeight = 13f;
        [Tooltip("How much of the screen the board rows fill while a card is held (the hands take the rest).")]
        [Range(0.3f, 1f)] public float boardScreenFraction = 0.6f;
        [Tooltip("How much of the screen the board rows fill in the C (board-only) view, where UI and hands are hidden.")]
        [Range(0.3f, 1f)] public float boardOnlyScreenFraction = 0.95f;
        [Tooltip("How long the hands take to slide off / back on screen when the board-only view opens / closes.")]
        public float handSlideDuration = 0.35f;
        public float moveDuration = 0.45f;

        private Camera cam;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private float homeFov;
        private bool hasHome;

        private bool userTop;
        private int topRequests;

        private enum View { Home, TopWithHands, BoardOnly }
        private View appliedView = View.Home;
        private float handVisibility = 1f;

        // UI canvases hidden by the board-only view, to show again afterwards
        private readonly System.Collections.Generic.List<Canvas> hiddenCanvases = new System.Collections.Generic.List<Canvas>();

        // The hands follow the camera; their base offset / scale are kept so zoom can rescale them
        private CameraAnchoredHand playerHand;
        private Vector3 playerHandOffset, playerHandScale;
        private EnemyHandView3D enemyHand;
        private Vector3 enemyHandOffset, enemyHandScale;

        public bool IsTopView => userTop || topRequests > 0;

        // The C view: only the board, no UI and no hands (a held card brings the hands back)
        public bool IsBoardOnlyView => userTop && topRequests == 0;
        public static bool HideOverlay => Instance != null && Instance.IsBoardOnlyView;

        // The card detail screen is showing (or closed this frame): clicks should not also pick up or play cards
        public static bool BlocksInput => CardDetailPanelUI.BlocksInput;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<CombatManager>() == null) return;
            Ensure();
        }

        public static CombatCameraRig3D Ensure()
        {
            if (Instance == null) new GameObject("CombatCameraRig3D").AddComponent<CombatCameraRig3D>();
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
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            CaptureHome();
        }

        private void CaptureHome()
        {
            if (hasHome) return;
            cam = Camera.main;
            if (cam == null) return;
            homePosition = cam.transform.position;
            homeRotation = cam.transform.rotation;
            homeFov = cam.fieldOfView;
            hasHome = true;
        }

        // ---------------------------------------------------------------- requests

        public void ToggleTopView()
        {
            userTop = !userTop;
            Apply();
        }

        // Temporary top view while something needs it (a held card); balanced by ReleaseTop
        public void RequestTop()
        {
            topRequests++;
            Apply();
        }

        public void ReleaseTop()
        {
            topRequests = Mathf.Max(0, topRequests - 1);
            Apply();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey) && !CardDetailPanelUI.IsOpen) ToggleTopView();

            // Right-click a card: its detail screen. A held card or a target choice uses right-click to cancel.
            if (!Input.GetMouseButtonDown(1) || CardDetailPanelUI.BlocksInput
                || CardPlayController3D.BlocksInput || CardTargeting3D.BlocksInput) return;

            var view = CardUnderMouse();
            if (view == null || view.CardData == null) return;
            if (IsBoardCard(view) || view.IsInHand) CardDetailPanelUI.Ensure().Show(view); // not the enemy's face-down hand
        }

        private CardView3D CardUnderMouse()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return null;

            var hits = Physics.RaycastAll(cam.ScreenPointToRay(Input.mousePosition), 200f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var view = hit.collider.GetComponentInParent<CardView3D>();
                if (view != null) return view;
            }
            return null;
        }

        private static bool IsBoardCard(CardView3D view)
        {
            return view != null && view.transform.parent != null && view.transform.parent.GetComponent<BoardSlotView>() != null;
        }

        // ---------------------------------------------------------------- camera

        private void Apply()
        {
            CaptureHome();
            if (cam == null) return;

            View want = IsBoardOnlyView ? View.BoardOnly : IsTopView ? View.TopWithHands : View.Home;
            if (want == appliedView) return;

            if (want == View.Home)
            {
                MoveCamera(homePosition, homeRotation, homeFov);
            }
            else
            {
                float fraction = want == View.BoardOnly ? boardOnlyScreenFraction : boardScreenFraction;
                if (!TryTopPose(fraction, out Vector3 pos, out Quaternion rot, out float fov)) return;
                MoveCamera(pos, rot, fov);
            }

            appliedView = want;
            SetUiHidden(want == View.BoardOnly);
        }

        // Every screen UI canvas except the card detail screen
        private void SetUiHidden(bool hide)
        {
            if (hide)
            {
                foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (!canvas.isRootCanvas || !canvas.enabled || canvas.renderMode == RenderMode.WorldSpace) continue;
                    if (canvas.GetComponentInParent<CardDetailPanelUI>() != null) continue;
                    canvas.enabled = false;
                    hiddenCanvases.Add(canvas);
                }
            }
            else
            {
                foreach (var canvas in hiddenCanvases)
                {
                    if (canvas != null) canvas.enabled = true;
                }
                hiddenCanvases.Clear();
            }
        }

        private void MoveCamera(Vector3 pos, Quaternion rot, float fov)
        {
            cam.transform.DOKill();
            cam.DOKill();
            cam.transform.DOMove(pos, moveDuration).SetEase(Ease.InOutQuad);
            cam.transform.DORotateQuaternion(rot, moveDuration).SetEase(Ease.InOutQuad);
            cam.DOFieldOfView(fov, moveDuration).SetEase(Ease.InOutQuad);
        }

        // Straight down over the middle of the board, the enemy's row at the top of the screen, with a
        // field of view that just fits both rows
        private bool TryTopPose(float screenFraction, out Vector3 pos, out Quaternion rot, out float fov)
        {
            pos = default;
            rot = default;
            fov = homeFov;

            Vector3 playerSum = Vector3.zero, enemySum = Vector3.zero;
            int playerCount = 0, enemyCount = 0;
            var slots = FindObjectsByType<BoardSlotView>(FindObjectsSortMode.None);
            foreach (var slot in slots)
            {
                if (!IsBoardSlot(slot)) continue;
                if (slot.side == BoardSlotView.SlotSide.Player)
                {
                    playerSum += slot.transform.position;
                    playerCount++;
                }
                else
                {
                    enemySum += slot.transform.position;
                    enemyCount++;
                }
            }
            if (playerCount == 0 || enemyCount == 0) return false;

            Vector3 p = playerSum / playerCount, e = enemySum / enemyCount;
            Vector3 center = (p + e) * 0.5f;
            Vector3 up = e - p;
            up.y = 0f;
            up = up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, up);

            // How far the slots (plus half a card) reach from the centre, along the screen's axes
            float halfUp = 0f, halfRight = 0f;
            foreach (var slot in slots)
            {
                if (!IsBoardSlot(slot)) continue;
                Vector3 d = slot.transform.position - center;
                halfUp = Mathf.Max(halfUp, Mathf.Abs(Vector3.Dot(d, up)) + 1.45f);
                halfRight = Mathf.Max(halfRight, Mathf.Abs(Vector3.Dot(d, right)) + 1f);
            }

            float aspect = cam.aspect > 0f ? cam.aspect : 16f / 9f;
            float halfHeightNeeded = Mathf.Max(halfUp, halfRight / aspect) / screenFraction;
            float tanHalf = halfHeightNeeded / topHeight;
            fov = Mathf.Clamp(2f * Mathf.Atan(tanHalf) * Mathf.Rad2Deg, 5f, homeFov);

            pos = center + Vector3.up * topHeight;
            rot = Quaternion.LookRotation(Vector3.down, up);
            return true;
        }

        private static bool IsBoardSlot(BoardSlotView slot)
        {
            return slot.GetComponent<DeckPileView3D>() == null && !slot.name.Contains("Deck");
        }

        // ---------------------------------------------------------------- hands keep their screen size

        private void LateUpdate()
        {
            if (cam == null || !hasHome) return;
            FindHands();

            // Narrower view = bigger picture; shrink the hands by the same amount (offset x/y and scale),
            // at the same distance, so they look unchanged
            float s = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(homeFov * 0.5f * Mathf.Deg2Rad);

            // The board-only view slides the hands off screen: the player's down past the bottom edge, the
            // enemy's up past the top, and back again after (their cards stay in play, just out of view)
            float targetVisibility = appliedView == View.BoardOnly ? 0f : 1f;
            float step = handSlideDuration > 0f ? Time.deltaTime / handSlideDuration : 1f;
            handVisibility = Mathf.MoveTowards(handVisibility, targetVisibility, step);
            float t = 1f - handVisibility;
            float slide = t * t * (3f - 2f * t); // smoothstep

            if (playerHand != null)
            {
                float away = SlideDistance(playerHandOffset.z) * slide;
                playerHand.offset = new Vector3(playerHandOffset.x * s, (playerHandOffset.y - away) * s, playerHandOffset.z);
                playerHand.transform.localScale = playerHandScale * s;
            }
            if (enemyHand != null && enemyHand.handAnchor == null && enemyHand.Container != null)
            {
                float away = SlideDistance(enemyHandOffset.z) * slide;
                enemyHand.offset = new Vector3(enemyHandOffset.x * s, (enemyHandOffset.y + away) * s, enemyHandOffset.z);
                enemyHand.Container.localScale = enemyHandScale * s;
            }
        }

        // A full screen height at the hand's distance (in the home view's units), enough to clear the edge
        private float SlideDistance(float depth)
        {
            return 2f * depth * Mathf.Tan(homeFov * 0.5f * Mathf.Deg2Rad);
        }

        private void FindHands()
        {
            if (playerHand == null)
            {
                playerHand = FindFirstObjectByType<CameraAnchoredHand>();
                if (playerHand != null)
                {
                    playerHandOffset = playerHand.offset;
                    playerHandScale = playerHand.transform.localScale;
                }
            }
            if (enemyHand == null)
            {
                enemyHand = FindFirstObjectByType<EnemyHandView3D>();
                if (enemyHand != null && enemyHand.Container != null)
                {
                    enemyHandOffset = enemyHand.offset;
                    enemyHandScale = enemyHand.Container.localScale;
                }
                else
                {
                    enemyHand = null; // not set up yet; try again next frame
                }
            }
        }

        private void OnGUI()
        {
            if (!showToggleButton || CardDetailPanelUI.IsOpen || IsBoardOnlyView) return;
            string label = IsTopView ? $"มุมปกติ ({toggleKey})" : $"มุมบน ({toggleKey})";
            if (GUI.Button(new Rect(Screen.width - 150, 10, 140, 30), label)) ToggleTopView();
        }
    }
}
