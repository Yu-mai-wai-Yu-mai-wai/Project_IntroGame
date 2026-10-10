using System.Collections.Generic;
using DG.Tweening;
using TawanOS.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.ShopEngine
{
    /// <summary>
    /// ศาลตายาย as a 3D place: the shop scene opens on the shrine model, and the shop menu (<see cref="ShopViewUI"/>)
    /// stays hidden until the player clicks the incense pot (or presses Enter / Space). Pointing at the pot brightens
    /// its candle lights. On opening, the menu slides in from the right while the camera draws back and over so the
    /// shrine sits in the space left of the menu; the menu is solid black and its left border is rolling smoke.
    /// Esc or a right-click slides it out again; the menu's กราบลา still leaves.
    /// Placed by Tools/TawanOS/Shop Engine/Setup Shrine Stage.
    /// </summary>
    public class ShopShrineView : MonoBehaviour
    {
        [Header("Scene")]
        public Canvas shopCanvas;
        [Tooltip("Collider around the incense pot; clicking it opens the shop menu.")]
        public Collider incensePot;
        [Tooltip("Lights by the pot, brightened while the pointer is on it.")]
        public Light[] potLights;
        public Camera viewCamera;
        [Tooltip("The parts of the shrine that must fit beside the menu (house and base).")]
        public Renderer[] shrineRenderers;
        [Tooltip("The menu's background; it is drawn as a black panel with a smoky left edge.")]
        public Image menuBackground;

        [Header("Menu open: shrine beside the menu")]
        [Tooltip("Where the middle of the shrine sits across the screen while the menu is open (0 = left, 1 = right).")]
        [Range(0f, 1f)] public float shrineScreenX = 0.16f;
        [Tooltip("How much of the screen width the shrine takes while the menu is open.")]
        [Range(0.05f, 1f)] public float shrineScreenWidth = 0.2f;
        [Min(0.05f)] public float slideSeconds = 0.6f;

        [Header("Smoke edge of the menu")]
        [Tooltip("Width of the smoky border on the left of the black menu, as a fraction of the background's width.")]
        [Range(0.01f, 0.6f)] public float smokeEdge = 0.12f;
        public float smokeSpeed = 1f;
        public float smokeScale = 4f;

        [Header("Look")]
        [Min(1f)] public float hoverLightBoost = 1.8f;
        public string hint = "คลิกที่กระถางธูปเพื่อไหว้ศาลตายาย";

        private CanvasGroup menuGroup;
        private float[] baseIntensity;
        private TextMeshProUGUI hintText;
        private GameObject hintBand;
        private bool hovering;
        private ShopViewUI view;

        private Vector3 shrineCameraPos;
        private Vector3 menuCameraPos;
        private float slide = 1f; // 0 = menu in place, 1 = pushed off to the right
        private Tween slideTween;
        private readonly List<RectTransform> menuParts = new List<RectTransform>();
        private readonly List<Vector2> menuPartHome = new List<Vector2>();

        public bool MenuOpen { get; private set; }

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            view = shopCanvas != null ? shopCanvas.GetComponent<ShopViewUI>() : null;
            if (shopCanvas != null)
            {
                menuGroup = shopCanvas.GetComponent<CanvasGroup>();
                if (menuGroup == null) menuGroup = shopCanvas.gameObject.AddComponent<CanvasGroup>();
            }
            baseIntensity = new float[potLights != null ? potLights.Length : 0];
            for (int i = 0; i < baseIntensity.Length; i++) baseIntensity[i] = potLights[i] != null ? potLights[i].intensity : 0f;
            ApplySmokeEdge();
            BuildHint();
        }

        private void Start()
        {
            // After ShopManager.Start has filled the menu (the card shelf adds its own parts to the canvas)
            if (viewCamera != null)
            {
                shrineCameraPos = viewCamera.transform.position;
                menuCameraPos = CameraPositionBesideMenu();
            }
            SetMenu(false, instant: true);
        }

        private void Update()
        {
            if (PauseMenu.IsPaused) return;

            if (MenuOpen)
            {
                // The removal picker inside the menu closes first (ShopViewUI handles its own cancel button)
                bool pickerOpen = view != null && view.removalPanel != null && view.removalPanel.activeSelf;
                if (!pickerOpen && (EscapeKey.Use() || Input.GetMouseButtonDown(1))) SetMenu(false);
                return;
            }
            if (slideTween != null && slideTween.IsActive()) return; // still sliding out

            bool over = PointerOnPot();
            if (over != hovering) SetHover(over);
            if ((over && Input.GetMouseButtonDown(0)) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                SetMenu(true);
        }

        private bool PointerOnPot()
        {
            if (incensePot == null || viewCamera == null) return false;
            var ray = viewCamera.ScreenPointToRay(Input.mousePosition);
            return incensePot.Raycast(ray, out _, 1000f);
        }

        private void SetHover(bool on)
        {
            hovering = on;
            for (int i = 0; i < baseIntensity.Length; i++)
                if (potLights[i] != null) potLights[i].DOIntensity(baseIntensity[i] * (on ? hoverLightBoost : 1f), 0.2f).SetLink(potLights[i].gameObject);
            if (hintText != null) hintText.DOFade(on ? 1f : 0.85f, 0.2f).SetLink(hintText.gameObject);
        }

        /// <summary>Slides the shop menu in from the right (and the shrine aside), or back out.</summary>
        public void SetMenu(bool open, bool instant = false)
        {
            MenuOpen = open;
            if (open && hovering) SetHover(false);
            if (hintBand != null) hintBand.SetActive(!open);
            if (menuGroup != null)
            {
                menuGroup.interactable = open;
                menuGroup.blocksRaycasts = open;
                menuGroup.alpha = 1f;
            }
            if (open || menuParts.Count == 0) CollectMenuParts();

            slideTween?.Kill();
            float target = open ? 0f : 1f;
            if (instant)
            {
                ApplySlide(target);
                return;
            }
            slideTween = DOTween.To(() => slide, ApplySlide, target, slideSeconds)
                .SetEase(open ? Ease.OutCubic : Ease.InOutCubic)
                .SetLink(gameObject);
        }

        // Every direct part of the menu canvas moves together, including those added at runtime (card shelf, deck pile)
        private void CollectMenuParts()
        {
            if (shopCanvas == null) return;
            // Put the parts back where they belong before measuring them again
            for (int i = 0; i < menuParts.Count; i++)
                if (menuParts[i] != null) menuParts[i].anchoredPosition = menuPartHome[i];
            menuParts.Clear();
            menuPartHome.Clear();
            foreach (Transform child in shopCanvas.transform)
            {
                if (!(child is RectTransform rt)) continue;
                menuParts.Add(rt);
                menuPartHome.Add(rt.anchoredPosition);
            }
        }

        private void ApplySlide(float value)
        {
            slide = value;
            float width = shopCanvas != null ? ((RectTransform)shopCanvas.transform).rect.width : 1920f;
            for (int i = 0; i < menuParts.Count; i++)
                if (menuParts[i] != null) menuParts[i].anchoredPosition = menuPartHome[i] + new Vector2(width * value, 0f);
            if (menuGroup != null && value >= 0.999f) menuGroup.alpha = 0f;
            else if (menuGroup != null) menuGroup.alpha = 1f;
            if (viewCamera != null) viewCamera.transform.position = Vector3.LerpUnclamped(menuCameraPos, shrineCameraPos, value);
        }

        // The camera keeps its angle and moves back and across until the shrine has the wanted width on screen and
        // its middle at shrineScreenX; its height on screen stays where it was
        private Vector3 CameraPositionBesideMenu()
        {
            var cam = viewCamera;
            var t = cam.transform;
            if (shrineRenderers == null || shrineRenderers.Length == 0) return t.position;
            var bounds = new Bounds();
            bool any = false;
            foreach (var r in shrineRenderers)
            {
                if (r == null) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (!any) return t.position;

            float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * cam.aspect;
            // Width of the shrine across the camera's view
            Vector3 e = bounds.extents;
            Vector3 right = t.right;
            float widthAcross = 2f * (Mathf.Abs(right.x) * e.x + Mathf.Abs(right.y) * e.y + Mathf.Abs(right.z) * e.z);
            float distance = widthAcross / (2f * tanH * shrineScreenWidth);

            Vector3 viewport = cam.WorldToViewportPoint(bounds.center);
            float across = (shrineScreenX - 0.5f) * 2f * distance * tanH;
            float up = (viewport.y - 0.5f) * 2f * distance * tanV;
            return bounds.center - t.forward * distance - t.right * across - t.up * up;
        }

        private void ApplySmokeEdge()
        {
            if (menuBackground == null) return;
            var shader = Resources.Load<Shader>("TawanOS_UISmokeEdge");
            if (shader == null) return;
            var material = new Material(shader);
            var rect = menuBackground.rectTransform.rect;
            material.SetFloat("_Edge", smokeEdge);
            material.SetFloat("_Aspect", rect.height > 0f ? rect.width / rect.height : 1f);
            material.SetFloat("_Speed", smokeSpeed);
            material.SetFloat("_Scale", smokeScale);
            menuBackground.material = material;
        }

        private void BuildHint()
        {
            var theme = UIThemeSO.Current;
            var canvas = UiFactory.CreateOverlayCanvas("ShrineHintCanvas", -1, transform);
            // A dark band behind the line keeps it readable over the lit wood of the shrine
            var band = UiFactory.CreateImage("HintBand", canvas.transform, new Color(0f, 0f, 0f, 0.72f));
            var bandRect = band.rectTransform;
            bandRect.anchorMin = new Vector2(0f, 0f);
            bandRect.anchorMax = new Vector2(1f, 0f);
            bandRect.pivot = new Vector2(0.5f, 0f);
            bandRect.anchoredPosition = new Vector2(0f, 40f);
            bandRect.sizeDelta = new Vector2(0f, 76f);
            hintText = UiFactory.CreateText(band.transform, "Hint", hint, theme.bodySize * 1.25f, theme.text, TextAlignmentOptions.Center, theme.bodyFont);
            UiFactory.Stretch(hintText.rectTransform, 0f);
            hintText.alpha = 0.85f;
            hintBand = band.gameObject;
        }
    }
}
