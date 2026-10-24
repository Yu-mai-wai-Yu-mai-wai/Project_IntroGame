using System.Collections;
using DG.Tweening;
using TawanOS.CardEngine;
using TawanOS.Settings;
using TawanOS.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// The เมรุ as a 3D place (Game Designer, 14 Oct; 24 Oct: no outside, upgrade only). The scene opens inside, at eye
    /// height, looking straight at the furnace door. Pointing at the door cracks it open, the fire glowing through;
    /// clicking it swings it open and brings up the upgrade panel of <see cref="MeruViewUI"/> from the bottom of the
    /// screen; going back shuts it. Once a card is upgraded it rises from the bottom of the screen as if held, is carried
    /// in through the door and laid on the cart, the door shuts, and after a moment the fire (<see cref="MeruFireFx"/>)
    /// jets out of the gaps round the door as it rattles, then smoke rolls up. The upgraded card comes back out of the
    /// reopened door and shows with the way on.
    /// Placed by Tools/TawanOS/Meru/Setup Meru Stage.
    /// </summary>
    public class MeruStageView : MonoBehaviour
    {
        [Header("Scene")]
        public Camera viewCamera;
        [Tooltip("Where the camera stands: eye height, looking straight at the furnace door. Move it to tune.")]
        public Transform insideView;
        public float insideFov = 45f;
        public MeruViewUI view;

        [Header("Door")]
        public Transform door;
        [Tooltip("A point on the door's hinge line (world space); the door turns about the upright line through it.")]
        public Vector3 doorHinge;
        [Tooltip("How far the door swings open, in degrees (positive swings it toward the camera).")]
        public float doorOpenAngle = 105f;
        [Min(0.05f)] public float doorSeconds = 0.9f;
        [Tooltip("How far the door cracks open while the pointer is on it (0..1 of fully open).")]
        [Range(0f, 0.5f)] public float doorHoverOpen = 0.12f;

        [Header("Fire")]
        public MeruFireFx fire;
        public Light fireLight;
        [Tooltip("The fire light while the door is shut (a glow round its edges), open, and as the fire bursts out.")]
        public float fireLightShut = 0.6f;
        public float fireLightOpen = 9f;
        public float fireLightBurst = 20f;
        [Range(0f, 1f)] public float flicker = 0.18f;

        [Header("Offering")]
        [Tooltip("Top of the cart in the furnace (world space), where the card is laid.")]
        public Vector3 cartTop;
        [Tooltip("Height (world y) of the cart's rim; the card stays above it until it is over its place.")]
        public float cartRimY;
        [Tooltip("Width of the card in the scene, in world units.")]
        public float cardWorldWidth = 1.3f;
        [Min(0.05f)] public float cardRiseSeconds = 0.5f;
        [Min(0.05f)] public float cardPlaceSeconds = 1.1f;
        [Tooltip("The wait between the door shutting and the fire bursting out.")]
        [Min(0f)] public float burstDelay = 0.6f;
        [Min(0.1f)] public float burstSeconds = 1.6f;
        public float burstShake = 0.6f;
        [Tooltip("How far the shut door rattles open as the fire bursts against it, in degrees.")]
        public float doorRattle = 4f;

        [Header("Hint")]
        public string doorHint = "คลิกที่ประตูเตาเพื่อชำระการ์ด";

        private bool offering;
        private Collider doorClick;
        private bool doorHovered;
        private TextMeshProUGUI hintText;
        private GameObject hintBand;
        private float lightLevel;
        private float lightTarget;
        private Vector3 doorPos0;
        private Quaternion doorRot0;
        private Vector3 doorCenter;
        private float doorOpen;
        private Tween doorTween;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (view != null) view.stage = this; // before MeruManager.Start shows the screen
            if (door != null)
            {
                doorPos0 = door.position;
                doorRot0 = door.rotation;
                var r = door.GetComponentInChildren<Renderer>();
                doorCenter = r != null ? r.bounds.center : door.position;
                doorClick = door.GetComponentInChildren<Collider>();
            }
            BuildOverlay();
        }

        private void Start()
        {
            PlaceCamera(insideView, insideFov);
            lightLevel = lightTarget = fireLightShut;
        }

        private void Update()
        {
            UpdateFireLight();
            if (shift != 0f) ApplyShift(shift); // the screen's shape may have changed
            bool canOpen = !offering && view != null && view.CanOpenPanel;
            if (hintBand != null && hintBand.activeSelf != canOpen) hintBand.SetActive(canOpen);
            if (PauseMenu.IsPaused || !canOpen)
            {
                if (doorHovered) HoverDoor(false);
                return;
            }

            bool over = doorClick != null && PointerOn(doorClick);
            if (over) TawanOS.UI.PointerCursor.Hover();
            if (over != doorHovered) HoverDoor(over);
            if (over && Input.GetMouseButtonDown(0)) OpenDoor();
        }

        // The door cracks open under the pointer, the fire glowing through
        private void HoverDoor(bool on)
        {
            doorHovered = on;
            if (!offering && (view == null || !view.PanelOpen)) SwingDoor(on ? doorHoverOpen : 0f, 0.3f);
            if (hintText != null) hintText.DOFade(on ? 1f : 0.8f, 0.2f).SetLink(hintText.gameObject);
        }

        // Clicked: the door swings open and the upgrade panel comes up from the bottom
        private void OpenDoor()
        {
            doorHovered = false;
            view.OpenPanel();
            SwingDoor(1f, doorSeconds);
            // The furnace slides left into the space beside the panel (Game Designer, 25 Oct)
            SlideView((1f - view.PanelWidth) * 0.5f - DoorViewportX());
            TawanOS.Audio.AudioManager.Instance?.PlaySfx("sfx_fire_on");
        }

        private bool PointerOn(Collider target)
        {
            if (viewCamera == null) return false;
            var ray = viewCamera.ScreenPointToRay(Input.mousePosition);
            return target.Raycast(ray, out _, 5000f);
        }

        /// <summary>The upgrade panel went back down: the door shuts.</summary>
        public void OnBack()
        {
            SwingDoor(0f, doorSeconds * 0.8f);
            SlideView(0f);
        }

        // ---- sliding the picture sideways ----

        [Tooltip("How long the view takes to slide the furnace aside when the panel comes in, and back.")]
        [Min(0.05f)] public float cameraSlideSeconds = 0.6f;
        private float shift;   // lens shift across the screen, in viewport widths (0.3 = the picture moved 30% right)
        private Tween shiftTween;

        private void SlideView(float target)
        {
            if (viewCamera == null) return;
            shiftTween?.Kill();
            shiftTween = DOTween.To(() => shift, ApplyShift, target, cameraSlideSeconds).SetEase(Ease.InOutCubic).SetLink(gameObject);
        }

        // Off-centre projection: the camera's picture moves `value` of the screen's width sideways; the camera itself
        // stays where it is, so the furnace is seen from the same spot
        private void ApplyShift(float value)
        {
            shift = value;
            if (viewCamera == null) return;
            if (Mathf.Abs(value) < 0.0001f)
            {
                shift = 0f;
                viewCamera.ResetProjectionMatrix();
                return;
            }
            var projection = Matrix4x4.Perspective(viewCamera.fieldOfView, viewCamera.aspect, viewCamera.nearClipPlane, viewCamera.farClipPlane);
            projection.m02 = -value * 2f;
            viewCamera.projectionMatrix = projection;
        }

        // Where the door's middle is across the screen with the picture unshifted (0 = left, 1 = right)
        private float DoorViewportX()
        {
            if (viewCamera == null || insideView == null) return 0.5f;
            var local = insideView.InverseTransformPoint(doorCenter);
            if (local.z <= 0.01f) return 0.5f;
            float tanH = Mathf.Tan(insideFov * 0.5f * Mathf.Deg2Rad) * viewCamera.aspect;
            return 0.5f + 0.5f * local.x / (local.z * tanH);
        }

        /// <summary>
        /// The card has been upgraded: it flies into the furnace, the door shuts, the fire bursts out round it, then the
        /// new card comes back out.
        /// </summary>
        public void Offer(CardDataSO card, RewardCardView picked)
        {
            offering = true;
            SlideView(0f); // the furnace back in the middle for the card going in
            StartCoroutine(OfferRoutine(card, picked));
        }

        private IEnumerator OfferRoutine(CardDataSO card, RewardCardView picked)
        {
            // The panel slides down out of the way, then the card is laid on the cart inside
            yield return new WaitForSeconds(view != null ? view.slideSeconds : 0.6f);

            var held = picked != null ? WorldCard(picked) : null;
            if (held != null) yield return LayOnCart(held);

            SwingDoor(0f, doorSeconds * 0.8f);
            yield return new WaitForSeconds(doorSeconds * 0.8f + burstDelay);

            // The fire takes the card behind the shut door
            if (held != null) Destroy(held.gameObject);
            yield return Burst();

            // The new card comes back out of the fire
            SwingDoor(1f, doorSeconds);
            yield return new WaitForSeconds(doorSeconds * 0.6f);
            if (view != null) view.ShowUpgraded(card, DoorScreenPoint());
        }

        // The picked card as a flat card in the scene: a world-space canvas holding a copy of it
        private Transform WorldCard(RewardCardView picked)
        {
            var go = new GameObject("CardOnCart", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = viewCamera;
            var rt = (RectTransform)go.transform;
            var size = ((RectTransform)picked.transform).rect.size;
            if (size.x <= 0f) size = new Vector2(180f, 180f * 1312f / 939f);
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * (cardWorldWidth / size.x);

            var copy = Instantiate(picked, rt);
            var crt = (RectTransform)copy.transform;
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.offsetMin = crt.offsetMax = Vector2.zero;
            crt.localRotation = Quaternion.identity;
            crt.localScale = Vector3.one;
            if (copy.pickedHighlight != null) copy.pickedHighlight.SetActive(false);
            if (copy.button != null) copy.button.interactable = false;
            foreach (var graphic in copy.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
            var cardGroup = copy.GetComponent<CanvasGroup>();
            if (cardGroup != null) cardGroup.alpha = 1f;
            return rt;
        }

        // Up from below the screen as if lifted from the hand, a breath, then in through the door above the cart's rim,
        // tipping over until it lies face up over its place (its top toward the back of the furnace), and down onto it.
        // Kept above the rim until it is over its place, so it never passes through the cart's sides
        private IEnumerator LayOnCart(Transform card)
        {
            var cam = viewCamera.transform;
            float h = cardWorldWidth * 1312f / 939f;
            float tan = Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float d = h / (0.9f * tan);                       // close enough to fill about half the screen's height
            var heldAt = cam.position + cam.forward * d - cam.up * d * tan * 0.3f;
            var below = cam.position + cam.forward * d - cam.up * (d * tan + h * 0.6f);
            var upright = Quaternion.LookRotation(cam.forward, cam.up) * Quaternion.Euler(-10f, 0f, 0f);
            card.SetPositionAndRotation(below, upright);

            var into = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            var flat = Quaternion.LookRotation(Vector3.down, into);
            float rim = Mathf.Max(cartRimY, cartTop.y);
            // Upright in the doorway its lower edge clears the rim; flat over its place it hovers a little above it
            var doorway = new Vector3(doorCenter.x, rim + h * 0.6f, doorCenter.z) - into * 1f;
            var over = new Vector3(cartTop.x, rim + h * 0.2f, cartTop.z);
            float carry = cardPlaceSeconds * 0.75f;

            var lay = DOTween.Sequence().SetLink(card.gameObject)
                .Append(card.DOMove(heldAt, cardRiseSeconds).SetEase(Ease.OutCubic))
                .AppendInterval(0.2f)
                .Append(card.DOPath(new[] { doorway, over }, carry, PathType.CatmullRom).SetEase(Ease.InOutSine))
                .Join(card.DORotateQuaternion(flat, carry).SetEase(Ease.InOutSine))
                .Append(card.DOMove(cartTop, cardPlaceSeconds - carry).SetEase(Ease.OutQuad));
            yield return lay.WaitForCompletion();
            yield return new WaitForSeconds(0.25f);
        }

        // Fire jets out of the gaps round the shut door, which rattles against it; the light flares, the view shakes;
        // then smoke rolls up off the door as the flames die down
        private IEnumerator Burst()
        {
            TawanOS.Audio.AudioManager.Instance?.PlaySfx("sfx_fire_on");
            bool still = GameSettings.ReduceMotion;
            if (!still && viewCamera != null && burstShake > 0f)
                viewCamera.transform.DOShakePosition(burstSeconds * 0.6f, burstShake, 18).SetLink(gameObject);

            float t = 0f;
            float smokeFor = burstSeconds * 1.6f;
            while (t < smokeFor)
            {
                t += Time.deltaTime;
                float k = t / burstSeconds;
                // Up fast, hold, die down
                float blaze = k < 0.15f ? k / 0.15f : k > 0.5f ? 1f - (k - 0.5f) / 0.5f : 1f;
                blaze = Mathf.Clamp01(blaze);
                if (fire != null)
                {
                    fire.SetBurst(blaze);
                    fire.SetSmoke(Mathf.Clamp01((k - 0.35f) * 2f) * Mathf.Clamp01((smokeFor - t) / (burstSeconds * 0.4f)));
                }
                lightTarget = Mathf.Lerp(fireLightShut, fireLightBurst, blaze);
                if (!still && door != null && blaze > 0f)
                    SetDoorAngle(doorRattle * blaze * Mathf.PerlinNoise(Time.time * 18f, 0.5f));
                yield return null;
            }
            SetDoorAngle(0f);
            lightTarget = fireLightShut;
        }

        private void SwingDoor(float target, float seconds)
        {
            doorTween?.Kill();
            doorTween = DOTween.To(() => doorOpen, SetDoor, target, seconds)
                .SetEase(target > doorOpen ? Ease.OutCubic : Ease.InOutCubic).SetLink(gameObject);
        }

        private void SetDoor(float t)
        {
            doorOpen = t;
            lightTarget = Mathf.Lerp(fireLightShut, fireLightOpen, t);
            SetDoorAngle(doorOpenAngle * t);
        }

        private void SetDoorAngle(float degrees)
        {
            if (door == null) return;
            var turn = Quaternion.AngleAxis(degrees, Vector3.up);
            door.SetPositionAndRotation(doorHinge + turn * (doorPos0 - doorHinge), turn * doorRot0);
        }

        private Vector2 DoorScreenPoint()
        {
            if (viewCamera == null) return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var p = viewCamera.WorldToScreenPoint(doorCenter);
            return new Vector2(p.x, p.y);
        }

        private void PlaceCamera(Transform at, float fov)
        {
            if (viewCamera == null || at == null) return;
            viewCamera.transform.SetPositionAndRotation(at.position, at.rotation);
            viewCamera.fieldOfView = fov;
        }

        private void UpdateFireLight()
        {
            if (fireLight == null) return;
            lightLevel = Mathf.MoveTowards(lightLevel, lightTarget, Time.deltaTime * fireLightBurst * 2f);
            float wave = Mathf.PerlinNoise(Time.time * 6f, 0.37f) - 0.5f;
            fireLight.intensity = lightLevel * (1f + wave * 2f * flicker);
        }

        private void BuildOverlay()
        {
            var theme = UIThemeSO.Current;

            var hintCanvas = UiFactory.CreateOverlayCanvas("MeruHintCanvas", 5, transform);
            // A dark band behind the line keeps it readable over the lit stone
            var band = UiFactory.CreateImage("HintBand", hintCanvas.transform, new Color(0f, 0f, 0f, 0.72f));
            band.raycastTarget = false;
            var bandRect = band.rectTransform;
            bandRect.anchorMin = new Vector2(0f, 0f);
            bandRect.anchorMax = new Vector2(1f, 0f);
            bandRect.pivot = new Vector2(0.5f, 0f);
            bandRect.anchoredPosition = new Vector2(0f, 40f);
            bandRect.sizeDelta = new Vector2(0f, 76f);
            hintText = UiFactory.CreateText(band.transform, "Hint", doorHint, theme.bodySize * 1.25f, theme.text, TextAlignmentOptions.Center, theme.bodyFont);
            hintText.raycastTarget = false;
            UiFactory.Stretch(hintText.rectTransform, 0f);
            hintText.alpha = 0.8f;
            hintBand = band.gameObject;
        }
    }
}
