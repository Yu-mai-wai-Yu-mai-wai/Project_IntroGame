using DG.Tweening;
using TawanOS.UI;
using TMPro;
using UnityEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// The pile of face-down cards in the กองของเซ่น scene. A hint band at the bottom says to click it; pointing at
    /// the pile brightens the light over it; a click (or Enter / Space) lifts the top card up toward the camera, and
    /// when it has risen the scene moves on to the reward screen. Same feel as the incense pot at the ศาลตายาย.
    /// </summary>
    public class OfferingPileView : MonoBehaviour
    {
        [Header("Scene")]
        public OfferingManager manager;
        [Tooltip("Box around the whole pile; clicking it takes the top card.")]
        public Collider pileCollider;
        [Tooltip("The card on top of the pile, lifted when the pile is clicked.")]
        public Transform topCard;
        [Tooltip("Light over the pile, brightened while the pointer is on it.")]
        public Light pileLight;
        public Camera viewCamera;

        [Header("Look")]
        [Min(1f)] public float hoverLightBoost = 1.8f;
        public string hint = "คลิกที่กองการ์ดเพื่อหยิบของเซ่น";
        [Tooltip("Seconds the top card takes to rise before the reward screen.")]
        [Min(0.1f)] public float liftSeconds = 0.8f;
        [Tooltip("How far toward the camera the card rises, as a fraction of the distance to it.")]
        [Range(0.1f, 0.9f)] public float liftToward = 0.45f;

        private float baseIntensity;
        private bool hovering;
        private bool taking;
        private TextMeshProUGUI hintText;
        private GameObject hintBand;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (manager == null) manager = FindFirstObjectByType<OfferingManager>();
            baseIntensity = pileLight != null ? pileLight.intensity : 0f;
            BuildHint();
        }

        private void Update()
        {
            if (taking || PauseMenu.IsPaused || SceneTransition.IsLoading) return;

            bool over = PointerOnPile();
            if (over != hovering) SetHover(over);
            if ((over && Input.GetMouseButtonDown(0)) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                TakeTopCard();
        }

        private bool PointerOnPile()
        {
            if (pileCollider == null || viewCamera == null) return false;
            return pileCollider.Raycast(viewCamera.ScreenPointToRay(Input.mousePosition), out _, 1000f);
        }

        private void SetHover(bool on)
        {
            hovering = on;
            if (pileLight != null) pileLight.DOIntensity(baseIntensity * (on ? hoverLightBoost : 1f), 0.2f).SetLink(pileLight.gameObject);
            if (hintText != null) hintText.DOFade(on ? 1f : 0.85f, 0.2f).SetLink(hintText.gameObject);
        }

        // The top card rises off the pile toward the camera, turning to face it a little, then the reward screen
        private void TakeTopCard()
        {
            taking = true;
            if (hintBand != null) hintBand.SetActive(false);
            TawanOS.Audio.AudioManager.Instance?.PlaySfx("sfx_card_draw");
            if (topCard == null || viewCamera == null)
            {
                manager?.Take();
                return;
            }

            Vector3 target = Vector3.Lerp(topCard.position, viewCamera.transform.position, liftToward);
            Quaternion face = Quaternion.LookRotation(-viewCamera.transform.up, viewCamera.transform.position - target);
            DOTween.Sequence()
                .Append(topCard.DOMove(target, liftSeconds).SetEase(Ease.OutCubic))
                .Join(topCard.DORotateQuaternion(Quaternion.Slerp(topCard.rotation, face, 0.5f), liftSeconds).SetEase(Ease.OutCubic))
                .OnComplete(() => manager?.Take())
                .SetLink(gameObject);
        }

        private void BuildHint()
        {
            var theme = UIThemeSO.Current;
            var canvas = UiFactory.CreateOverlayCanvas("OfferingHintCanvas", -1, transform);
            // A dark band behind the line keeps it readable over the forest
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
