using System;
using System.Collections;
using DG.Tweening;
using TawanOS.CardEngine;
using TawanOS.VFX;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// A card offered as a reward, drawn like the card in play: frame, artwork and the face text laid out
    /// by <see cref="CardFaceLayout"/> (a finished card PNG shows as it is, with only attack / Khwan on it).
    /// Click to take it.
    /// Everything on the card lives under a runtime "Body" child, so the deal, flip, float and burn
    /// (<see cref="RewardFxConfigSO"/>) can move it while the card row's layout keeps placing the root.
    /// </summary>
    public class RewardCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
    {
        public Button button;
        public Image frame;
        public Image artwork;
        public TextMeshProUGUI costText;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI typeText;
        public TextMeshProUGUI attackText;
        public TextMeshProUGUI khwanText;
        public TextMeshProUGUI descriptionText;
        public GameObject pickedHighlight;

        [Header("Default Frames (cards without a Card Background)")]
        public Sprite defaultWhiteFrame;
        public Sprite defaultBlackFrame;

        private Action onPick;
        private CardDataSO shownCard;
        private bool ignoreClick; // this click closed the detail screen, it must not also take the card

        private RectTransform body;
        private Image back;
        private UiBurnEffect burn;
        private CanvasGroup group;

        private void Awake()
        {
            EnsureBody();
            if (button != null) button.onClick.AddListener(() =>
            {
                if (!ignoreClick) onPick?.Invoke();
                ignoreClick = false;
            });
        }

        // Moves every child of the card under one "Body" rect that the animations drive
        private void EnsureBody()
        {
            if (body != null) return;

            var go = new GameObject("Body", typeof(RectTransform));
            body = (RectTransform)go.transform;
            body.SetParent(transform, false);
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.offsetMin = body.offsetMax = Vector2.zero;
            body.pivot = new Vector2(0.5f, 0.5f);

            // Front to back, so the drawing order stays the same
            var children = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in transform) if (child != body) children.Add(child);
            foreach (var child in children) child.SetParent(body, false);

            var backGo = new GameObject("CardBack", typeof(RectTransform), typeof(Image));
            back = backGo.GetComponent<Image>();
            back.rectTransform.SetParent(body, false);
            back.rectTransform.anchorMin = Vector2.zero;
            back.rectTransform.anchorMax = Vector2.one;
            back.rectTransform.offsetMin = back.rectTransform.offsetMax = Vector2.zero;
            back.raycastTarget = false;
            back.gameObject.SetActive(false);

            burn = body.gameObject.AddComponent<UiBurnEffect>();
            group = GetComponent<CanvasGroup>();
        }

        public void Setup(CardDataSO card, Action pick)
        {
            EnsureBody();
            ResetMotion();
            onPick = pick;
            shownCard = card;
            transform.localScale = Vector3.one;

            // A finished card PNG has everything but attack / Khwan printed on it
            bool printed = card.cardImage != null;
            Sprite face = printed ? card.cardImage
                : card.cardBackground != null ? card.cardBackground
                : CardFaceLayout.DefaultFrame(defaultWhiteFrame, defaultBlackFrame, card.magicSchool);

            if (frame != null)
            {
                frame.sprite = face;
                frame.color = face != null ? Color.white : SchoolColor(card.magicSchool);
            }
            if (artwork != null)
            {
                artwork.sprite = card.artwork;
                artwork.enabled = !printed && card.artwork != null;
                artwork.preserveAspect = false;
                PlaceOnFace(artwork.rectTransform, CardFaceLayout.Artwork); // scenes built earlier still hold the old picture window
                artwork.transform.SetAsFirstSibling(); // behind the see-through frame
            }

            bool familiar = card.cardType == CardType.Familiar;
            bool hasKhwan = familiar || (card.cardType == CardType.Amulet && card.familiarHealth > 0);

            SetText(costText, printed ? "" : card.magicSchool == MagicSchool.WhiteMagic ? card.meritCost.ToString() : card.corruptionGain.ToString(),
                CardFaceLayout.Cost, card, CardFaceLayout.Text.Cost);
            SetText(nameText, printed ? "" : card.cardNameThai, CardFaceLayout.Name, card, CardFaceLayout.Text.Name);
            SetText(typeText, printed ? "" : card.GetFormattedTypeText().Replace(" • ", "  "), CardFaceLayout.Type, card, CardFaceLayout.Text.Type);
            SetText(attackText, familiar ? card.familiarDamage.ToString() : "", CardFaceLayout.Attack, card, CardFaceLayout.Text.Stat);
            SetText(khwanText, hasKhwan ? card.familiarHealth.ToString() : "", CardFaceLayout.Khwan, card, CardFaceLayout.Text.Stat);
            if (descriptionText != null) descriptionText.text = printed ? "" : FormatDescription(card);

            if (pickedHighlight != null) pickedHighlight.SetActive(false);
            if (button != null) button.interactable = true;
        }

        // Single-line face text: the Card Data's size for it grows its box (same centre), as on the 3D card
        private static void SetText(TextMeshProUGUI text, string value, CardFaceLayout.Box box, CardDataSO card, CardFaceLayout.Text part)
        {
            if (text == null) return;
            text.text = value;
            PlaceOnFace(text.rectTransform, box.Scaled(CardFaceLayout.FontScale(card, part)));
        }

        // Face boxes are fractions of the card (CardFaceLayout: x -0.5..0.5, y 0.5 top .. -0.5 bottom)
        public static void PlaceOnFace(RectTransform rt, CardFaceLayout.Box box)
        {
            Vector2 c = box.center + new Vector2(0.5f, 0.5f);
            rt.anchorMin = c - box.size * 0.5f;
            rt.anchorMax = c + box.size * 0.5f;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>The picked card grows and glows; the others burn away (<paramref name="fx"/> null = they just fade).</summary>
        public void SetResult(bool picked, RewardFxConfigSO fx = null)
        {
            SetInteractable(false);
            if (pickedHighlight != null) pickedHighlight.SetActive(picked);
            transform.DOKill();

            if (picked)
            {
                transform.DOScale(1.12f, 0.25f).SetEase(Ease.OutBack);
                return;
            }

            if (fx == null)
            {
                transform.DOScale(0.9f, 0.25f).SetEase(Ease.OutBack);
                if (group != null) group.DOFade(0.35f, 0.25f);
                return;
            }

            DOVirtual.DelayedCall(fx.burnDelay, () =>
            {
                if (burn == null) return;
                burn.Play(fx.burnMaterial, fx.burnDuration, fx.embersPerSecond, fx.glowColor, fx.charColor, fx.smokeTexture);
            }).SetTarget(transform).SetLink(gameObject);
        }

        // ---------------------------------------------------------------- deal / flip / float

        public void SetInteractable(bool on)
        {
            if (button != null) button.interactable = on;
        }

        /// <summary>Hides the card, face down, until <see cref="DealIn"/> brings it in.</summary>
        public void PrepareFaceDown(Sprite cardBack)
        {
            EnsureBody();
            SetInteractable(false);
            if (back != null)
            {
                back.sprite = cardBack;
                back.color = cardBack != null ? Color.white : new Color(0.12f, 0.08f, 0.08f);
                back.gameObject.SetActive(true);
                back.transform.SetAsLastSibling();
            }
            if (group != null) group.alpha = 0f;
        }

        /// <summary>Flies the face-down card from <paramref name="fromWorld"/> into its place in the row.</summary>
        public IEnumerator DealIn(Vector3 fromWorld, RewardFxConfigSO fx)
        {
            if (group != null) group.alpha = 1f;
            body.position = fromWorld;
            body.localEulerAngles = new Vector3(0f, 0f, fx.dealStartTilt);
            body.DOAnchorPos(Vector2.zero, fx.dealFlyDuration).SetEase(Ease.OutCubic).SetLink(gameObject);
            body.DOLocalRotate(Vector3.zero, fx.dealFlyDuration).SetEase(Ease.OutCubic).SetLink(gameObject);
            AudioHook("sfx_card_draw");
            yield return new WaitForSeconds(fx.dealFlyDuration);
        }

        /// <summary>Turns the card face up: squeezes to an edge, swaps the back for the face, opens again.</summary>
        public IEnumerator Flip(RewardFxConfigSO fx)
        {
            float half = fx.flipDuration * 0.5f;
            body.DOAnchorPosY(14f, half).SetEase(Ease.OutSine).SetLink(gameObject);
            body.DOScaleX(0f, half).SetEase(Ease.InSine).SetLink(gameObject);
            yield return new WaitForSeconds(half);

            if (back != null) back.gameObject.SetActive(false);
            AudioHook("sfx_card_play");

            body.DOAnchorPosY(0f, half).SetEase(Ease.InSine).SetLink(gameObject);
            body.DOScaleX(1f, half).SetEase(Ease.OutBack).SetLink(gameObject);
            yield return new WaitForSeconds(half);
        }

        /// <summary>Starts the endless gentle up-and-down; <paramref name="phase"/> (0..1) desyncs the cards.</summary>
        public void StartFloat(RewardFxConfigSO fx, float phase)
        {
            float half = fx.floatPeriod * 0.5f;
            body.anchoredPosition = Vector2.zero;
            body.DOAnchorPosY(fx.floatHeight, half).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                .SetDelay(phase * half).SetLink(gameObject);
            if (fx.floatSway > 0f)
            {
                body.localEulerAngles = new Vector3(0f, 0f, -fx.floatSway);
                body.DOLocalRotate(new Vector3(0f, 0f, fx.floatSway), half * 1.35f).SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo).SetDelay(phase * half * 1.35f).SetLink(gameObject);
            }
        }

        // Face up, in place, nothing burning: the card as Setup draws it
        private void ResetMotion()
        {
            DOTween.Kill(transform);
            if (body != null)
            {
                body.DOKill();
                body.anchoredPosition = Vector2.zero;
                body.localEulerAngles = Vector3.zero;
                body.localScale = Vector3.one;
            }
            if (burn != null) burn.Restore();
            if (back != null) back.gameObject.SetActive(false);
            if (group != null)
            {
                group.DOKill();
                group.alpha = 1f;
            }
        }

        // Sound goes through the AudioManager when the scene has one (same ids as the combat cards)
        private static void AudioHook(string id)
        {
            TawanOS.Audio.AudioManager.Instance?.PlaySfx(id);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            ignoreClick = CardDetailPanelUI.BlocksInput;
        }

        // Right-click: the same card detail screen as in combat
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right || shownCard == null || ignoreClick) return;
            CardDetailPanelUI.Ensure().Show(new CardInstance(shownCard), defaultWhiteFrame, defaultBlackFrame, nameText != null ? nameText.font : null);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button == null || !button.interactable) return;
            transform.DOKill();
            transform.DOScale(1.06f, 0.15f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (button == null || !button.interactable) return;
            transform.DOKill();
            transform.DOScale(1f, 0.15f);
        }

        private static Color SchoolColor(MagicSchool school)
        {
            return school == MagicSchool.WhiteMagic ? new Color(0.85f, 0.8f, 0.55f) : new Color(0.35f, 0.1f, 0.15f);
        }

        private static string FormatDescription(CardDataSO card)
        {
            try { return string.Format(card.descriptionFormat ?? string.Empty, card.baseValue); }
            catch (FormatException) { return card.descriptionFormat; }
        }
    }
}
