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
    /// The เมรุ as a 3D place (Game Designer, 14 Oct). The scene opens outside on the เมรุ (the model's first camera);
    /// pointing at it lights it up, and clicking it fades to black and back in inside, at eye height, looking straight
    /// at the furnace door, with the burn and upgrade strips of <see cref="MeruViewUI"/> at the sides.
    /// Choosing a side opens the door and slides the view sideways (no step back) until the door sits beside the
    /// chosen column; going back closes it and slides back. The slide is a lens shift: the camera stays where it is
    /// and the picture moves, so the pillars near the camera never swing across the door. Once a card is burnt or
    /// upgraded it flies into the furnace, the door shuts, and after a moment the fire (<see cref="MeruFireFx"/>) jets
    /// out of the gaps round the door as it rattles, then smoke rolls up. A burnt card is gone: the screen fades and the
    /// run goes on. An upgraded card comes back out of the reopened door and shows with the way on.
    /// Placed by Tools/TawanOS/Meru/Setup Meru Stage.
    /// </summary>
    public class MeruStageView : MonoBehaviour
    {
        private enum Stage { Outside, Entering, Inside, Offering }

        [Header("Scene")]
        public Camera viewCamera;
        [Tooltip("Where the camera stands outside (the model's first camera).")]
        public Transform outsideView;
        public float outsideFov = 42f;
        [Tooltip("Where the camera stands inside: eye height, looking straight at the furnace door. Move it to tune.")]
        public Transform insideView;
        public float insideFov = 45f;
        [Tooltip("Clicking this (the เมรุ building) goes inside.")]
        public Collider meruClick;
        public MeruViewUI view;

        [Header("Highlight outside")]
        [Tooltip("A warm light on the เมรุ, off until the pointer is on it.")]
        public Light meruLight;
        public float meruLightHover = 4000f;

        [Header("Door")]
        public Transform door;
        [Tooltip("A point on the door's hinge line (world space); the door turns about the upright line through it.")]
        public Vector3 doorHinge;
        [Tooltip("How far the door swings open, in degrees (positive swings it toward the camera).")]
        public float doorOpenAngle = 105f;
        [Min(0.05f)] public float doorSeconds = 0.9f;
        [Tooltip("How long the view takes to slide sideways when a side is chosen or given up.")]
        [Min(0.05f)] public float cameraSlideSeconds = 0.6f;

        [Header("Fire")]
        public MeruFireFx fire;
        public Light fireLight;
        [Tooltip("The fire light while the door is shut (a glow round its edges), open, and as the fire bursts out.")]
        public float fireLightShut = 0.6f;
        public float fireLightOpen = 9f;
        public float fireLightBurst = 20f;
        [Range(0f, 1f)] public float flicker = 0.18f;

        [Header("Offering")]
        [Min(0.05f)] public float cardFlightSeconds = 0.7f;
        [Tooltip("The wait between the door shutting and the fire bursting out.")]
        [Min(0f)] public float burstDelay = 0.6f;
        [Min(0.1f)] public float burstSeconds = 1.6f;
        public float burstShake = 0.6f;
        [Tooltip("How far the shut door rattles open as the fire bursts against it, in degrees.")]
        public float doorRattle = 4f;

        [Header("Fade")]
        [Min(0.05f)] public float fadeSeconds = 0.45f;

        [Header("Hint")]
        public string outsideHint = "คลิกที่เมรุเพื่อเข้าไปข้างใน";

        private Stage stage;
        private Image fade;
        private TextMeshProUGUI hintText;
        private GameObject hintBand;
        private bool hovering;
        private float lightLevel;
        private float lightTarget;
        private Vector3 doorPos0;
        private Quaternion doorRot0;
        private Vector3 doorCenter;
        private float doorOpen;
        private float shift;   // lens shift across the screen, in viewport widths (0.3 = the picture moved 30% right)
        private Tween doorTween;
        private Tween shiftTween;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (view != null) view.stage = this; // before MeruManager.Start shows the strips
            if (door != null)
            {
                doorPos0 = door.position;
                doorRot0 = door.rotation;
                var r = door.GetComponent<Renderer>();
                doorCenter = r != null ? r.bounds.center : door.position;
            }
            BuildOverlay();
        }

        private void Start()
        {
            PlaceCamera(outsideView, outsideFov);
            if (meruLight != null) meruLight.intensity = 0f;
            lightLevel = lightTarget = fireLightShut;
            SetStage(Stage.Outside);
        }

        private void Update()
        {
            UpdateFireLight();
            if (shift != 0f) ApplyShift(shift); // the screen's shape may have changed
            if (PauseMenu.IsPaused || stage != Stage.Outside) return;

            bool over = meruClick != null && PointerOn(meruClick);
            if (over != hovering) SetHover(over);
            if (over && Input.GetMouseButtonDown(0)) GoInside();
        }

        private void SetStage(Stage next)
        {
            stage = next;
            if (hovering && next != Stage.Outside) SetHover(false);
            if (hintBand != null) hintBand.SetActive(next == Stage.Outside);
        }

        private bool PointerOn(Collider target)
        {
            if (viewCamera == null) return false;
            var ray = viewCamera.ScreenPointToRay(Input.mousePosition);
            return target.Raycast(ray, out _, 5000f);
        }

        // The เมรุ lights up under the pointer
        private void SetHover(bool on)
        {
            hovering = on;
            if (meruLight != null) meruLight.DOIntensity(on ? meruLightHover : 0f, 0.25f).SetLink(meruLight.gameObject);
            if (hintText != null) hintText.DOFade(on ? 1f : 0.8f, 0.2f).SetLink(hintText.gameObject);
        }

        // Fade to black, stand in front of the furnace door, fade back in; then the two strips come in
        private void GoInside()
        {
            SetStage(Stage.Entering);
            DOTween.Sequence().SetLink(gameObject)
                .Append(fade.DOFade(1f, fadeSeconds).SetEase(Ease.InQuad))
                .AppendCallback(() => PlaceCamera(insideView, insideFov))
                .AppendInterval(0.15f)
                .Append(fade.DOFade(0f, fadeSeconds * 1.3f).SetEase(Ease.OutQuad))
                .OnComplete(() =>
                {
                    SetStage(Stage.Inside);
                    if (view != null) view.ShowChoices();
                });
        }

        /// <summary>A side was chosen: the door opens and the view slides until the door sits beside the column.</summary>
        public void OnChoose(MeruAction action, float columnWidth)
        {
            float doorX = action == MeruAction.Burn ? (columnWidth + 1f) * 0.5f : (1f - columnWidth) * 0.5f;
            SwingDoor(1f, doorSeconds);
            SlideView(doorX - DoorViewportX());
            TawanOS.Audio.AudioManager.Instance?.PlaySfx("sfx_fire_on");
        }

        /// <summary>The side was given up: the door shuts and the view slides back to face it.</summary>
        public void OnBack()
        {
            SwingDoor(0f, doorSeconds * 0.8f);
            SlideView(0f);
        }

        /// <summary>
        /// The card has been burnt or upgraded: it flies into the furnace, the door shuts, the fire bursts out round
        /// it; then the run goes on (burnt) or the new card comes back out (upgraded).
        /// </summary>
        public void Offer(MeruAction action, CardDataSO card, RectTransform flyingCard)
        {
            SetStage(Stage.Offering);
            StartCoroutine(OfferRoutine(action, card, flyingCard));
        }

        private IEnumerator OfferRoutine(MeruAction action, CardDataSO card, RectTransform flying)
        {
            // Face the door again, then the card goes in through it
            SlideView(0f);
            yield return new WaitForSeconds(cameraSlideSeconds);

            if (flying != null)
            {
                var cardGroup = flying.GetComponent<CanvasGroup>();
                if (cardGroup == null) cardGroup = flying.gameObject.AddComponent<CanvasGroup>();
                Vector2 target = DoorScreenPoint();
                var flight = DOTween.Sequence().SetLink(flying.gameObject)
                    .Append(flying.DOMove(new Vector3(target.x, target.y, flying.position.z), cardFlightSeconds).SetEase(Ease.InCubic))
                    .Join(flying.DOScale(0.08f, cardFlightSeconds).SetEase(Ease.InQuad))
                    .Join(cardGroup.DOFade(0f, cardFlightSeconds * 0.35f).SetDelay(cardFlightSeconds * 0.65f))
                    .OnComplete(() => Destroy(flying.gameObject));
                yield return flight.WaitForCompletion();
            }

            SwingDoor(0f, doorSeconds * 0.8f);
            yield return new WaitForSeconds(doorSeconds * 0.8f + burstDelay);

            yield return Burst();

            if (action == MeruAction.Burn)
            {
                // The card is ash: fade out and go on
                yield return fade.DOFade(1f, fadeSeconds * 1.5f).SetEase(Ease.InQuad).SetLink(gameObject).WaitForCompletion();
                MeruManager.Instance?.Leave();
                yield break;
            }

            // The new card comes back out of the fire
            SwingDoor(1f, doorSeconds);
            yield return new WaitForSeconds(doorSeconds * 0.6f);
            if (view != null) view.ShowUpgraded(card, DoorScreenPoint());
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

        private void SlideView(float target)
        {
            if (viewCamera == null) return;
            shiftTween?.Kill();
            shiftTween = DOTween.To(() => shift, ApplyShift, target, cameraSlideSeconds).SetEase(Ease.InOutCubic).SetLink(gameObject);
        }

        // Off-centre projection: the camera's picture moves `value` of the screen's width sideways
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

        private Vector2 DoorScreenPoint()
        {
            if (viewCamera == null) return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var p = viewCamera.WorldToScreenPoint(doorCenter);
            return new Vector2(p.x, p.y);
        }

        private void PlaceCamera(Transform at, float fov)
        {
            if (viewCamera == null || at == null) return;
            ApplyShift(0f);
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
            hintText = UiFactory.CreateText(band.transform, "Hint", outsideHint, theme.bodySize * 1.25f, theme.text, TextAlignmentOptions.Center, theme.bodyFont);
            hintText.raycastTarget = false;
            UiFactory.Stretch(hintText.rectTransform, 0f);
            hintText.alpha = 0.8f;
            hintBand = band.gameObject;

            // Above every other canvas, so the walk inside is a clean cut to black
            var fadeCanvas = UiFactory.CreateOverlayCanvas("MeruFadeCanvas", 900, transform);
            fade = UiFactory.CreateImage("Fade", fadeCanvas.transform, Color.black);
            fade.raycastTarget = false;
            UiFactory.Stretch(fade.rectTransform, 0f);
            fade.color = new Color(0f, 0f, 0f, 0f);
        }
    }
}
