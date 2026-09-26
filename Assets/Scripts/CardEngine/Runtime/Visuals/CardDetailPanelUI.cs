using System;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.CardEngine
{
    // Card detail screen (Vanguard Dear Days style): right-click a card to dim the table and show the
    // card large on the left, with its full information on the right: name, type, cost, live stats,
    // ability text, ability breakdown and the statuses currently on it. Any click or Esc closes it.
    // Built in code on first use (its own overlay canvas), so no scene setup is needed.
    public class CardDetailPanelUI : MonoBehaviour
    {
        public static CardDetailPanelUI Instance { get; private set; }

        // Open (or closed this frame): clicks should not also pick up or play cards
        public static bool BlocksInput => Instance != null && (Instance.isOpen || Time.frameCount <= Instance.closedFrame);
        public static bool IsOpen => Instance != null && Instance.isOpen;

        private const float CardHeight = 820f;
        private const float CardAspect = 939f / 1312f;
        private const float FadeSpeed = 8f;

        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform cardRoot;
        private Image frameImage, artImage;
        private TextMeshProUGUI faceCost, faceName, faceType, faceAttack, faceKhwan, faceDescription;
        private Image panelImage;
        private Outline panelOutline;
        private TextMeshProUGUI headerText, bodyText, hintText;

        private bool isOpen;
        private int openedFrame = -1;
        private int closedFrame = -1;
        private float targetAlpha;

        public static CardDetailPanelUI Ensure()
        {
            if (Instance == null) new GameObject("CardDetailPanelUI").AddComponent<CardDetailPanelUI>();
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

        // ---------------------------------------------------------------- show / hide

        public void Show(CardView3D view)
        {
            if (view == null || view.CardData == null) return;
            Build(view.nameLabel != null ? view.nameLabel.font : null);

            var card = view.CardData;
            FillCardFace(card, view);
            FillPanel(card);

            isOpen = true;
            openedFrame = Time.frameCount;
            targetAlpha = 1f;
            canvas.gameObject.SetActive(true);
            group.alpha = 0f;
            cardRoot.localScale = Vector3.one * 0.92f;
        }

        public void Hide()
        {
            if (!isOpen) return;
            isOpen = false;
            closedFrame = Time.frameCount;
            targetAlpha = 0f;
        }

        private void Update()
        {
            if (canvas == null) return;

            if (isOpen && Time.frameCount > openedFrame
                && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)))
            {
                Hide();
            }

            group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, FadeSpeed * Time.unscaledDeltaTime);
            cardRoot.localScale = Vector3.Lerp(cardRoot.localScale, Vector3.one, 12f * Time.unscaledDeltaTime);
            if (!isOpen && group.alpha <= 0f) canvas.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- content

        private void FillCardFace(CardInstance card, CardView3D view)
        {
            Sprite frame = card.cardBackground != null
                ? card.cardBackground
                : CardFaceLayout.DefaultFrame(view.defaultWhiteFrame, view.defaultBlackFrame, card.magicSchool);

            frameImage.sprite = frame;
            frameImage.color = frame != null ? Color.white : SchoolColor(card.magicSchool);
            artImage.sprite = card.artwork;
            artImage.enabled = card.artwork != null;

            bool familiar = card.cardType == CardType.Familiar;
            bool hasKhwan = familiar || card.maxKhwan > 0;

            faceCost.text = card.magicSchool == MagicSchool.WhiteMagic ? card.meritCost.ToString() : card.corruptionGain.ToString();
            faceName.text = card.cardNameThai;
            faceType.text = TypeLine(card);
            faceAttack.text = familiar ? AttackOf(card).ToString() : "";
            faceKhwan.text = hasKhwan ? card.familiarHealth.ToString() : "";
            faceDescription.text = Description(card);

            faceType.color = frame != null ? new Color(0.95f, 0.72f, 0.72f) : Color.white;

            // Text sizes from the Card Data (Face Text Sizes)
            var src = card.source;
            SizeFaceText(faceName, CardFaceLayout.Name, 64, CardFaceLayout.FontScale(src, CardFaceLayout.Text.Name), false);
            SizeFaceText(faceType, CardFaceLayout.Type, 28, CardFaceLayout.FontScale(src, CardFaceLayout.Text.Type), false);
            SizeFaceText(faceCost, CardFaceLayout.Cost, 80, CardFaceLayout.FontScale(src, CardFaceLayout.Text.Cost), false);
            SizeFaceText(faceAttack, CardFaceLayout.Attack, 44, CardFaceLayout.FontScale(src, CardFaceLayout.Text.Stat), false);
            SizeFaceText(faceKhwan, CardFaceLayout.Khwan, 44, CardFaceLayout.FontScale(src, CardFaceLayout.Text.Stat), false);
            SizeFaceText(faceDescription, CardFaceLayout.Description, 30, CardFaceLayout.FontScale(src, CardFaceLayout.Text.Description), true);
        }

        private static void SizeFaceText(TextMeshProUGUI t, CardFaceLayout.Box box, float baseMax, float mul, bool fixedBox)
        {
            PlaceOnFace(t.rectTransform, fixedBox ? box : box.Scaled(mul));
            t.fontSizeMax = baseMax * mul;
        }

        private void FillPanel(CardInstance card)
        {
            Color accent = AccentColor(card.magicSchool);
            panelOutline.effectColor = accent;
            string accentHex = ColorUtility.ToHtmlStringRGB(accent);

            // Header: name, English name, type
            var h = new StringBuilder();
            h.Append($"<size=64>{card.cardNameThai}</size>\n");
            if (!string.IsNullOrEmpty(card.cardNameEng)) h.Append($"<size=26><color=#9A9090>{card.cardNameEng}</color></size>\n");
            h.Append($"<size=30><color=#{accentHex}>{TypeLine(card)}</color></size>");
            headerText.text = h.ToString();

            // Body: cost and stats, ability text, ability breakdown, statuses
            var b = new StringBuilder();
            b.Append(card.magicSchool == MagicSchool.WhiteMagic
                ? $"<color=#{accentHex}>กุศล</color>  {card.meritCost}"
                : $"<color=#{accentHex}>มลทิน</color>  +{card.corruptionGain}");

            if (card.cardType == CardType.Familiar)
            {
                int atk = AttackOf(card);
                string atkText = atk != card.familiarDamage ? $"{atk} <size=22><color=#9A9090>(ฐาน {card.familiarDamage})</color></size>" : atk.ToString();
                b.Append($"      <color=#{accentHex}>สะเทือนขวัญ</color>  {atkText}");
            }
            if (card.cardType == CardType.Familiar || card.maxKhwan > 0)
            {
                b.Append($"      <color=#{accentHex}>ขวัญ</color>  {card.familiarHealth}/{card.maxKhwan}");
            }
            if (card.armor > 0) b.Append($"      <color=#{accentHex}>เกราะ</color>  {card.armor}");
            if (card.cardType == CardType.Amulet && card.currentDurability > 0) b.Append($"      <color=#{accentHex}>อายุขลัง</color>  {card.currentDurability}");
            b.Append("\n\n");

            b.Append($"<size=26><color=#{accentHex}>ความสามารถ</color></size>\n");
            string description = Description(card);
            b.Append(string.IsNullOrEmpty(description) ? "<color=#9A9090>ไม่มีความสามารถ</color>" : description);
            b.Append("\n");

            if (card.abilities != null && card.abilities.Count > 0)
            {
                b.Append($"\n<size=24><color=#{accentHex}>รายละเอียด</color></size>\n<size=24><color=#C8C0C0>");
                foreach (var a in card.abilities) b.Append("• ").Append(AbilityLine(a)).Append('\n');
                b.Append("</color></size>");
            }

            if (card.statuses != null && card.statuses.Count > 0)
            {
                b.Append($"\n<size=24><color=#{accentHex}>สถานะบนการ์ด</color></size>\n<size=24>");
                foreach (var s in card.statuses)
                {
                    string color = CardStatus.IsDebuff(s.type) ? "E07070" : CardStatus.IsBlessing(s.type) ? "80D090" : "C8C0C0";
                    b.Append($"<color=#{color}>• {NameOf(s.type)} x{s.stacks}  ({s.duration} เทิร์น)</color>\n");
                }
                b.Append("</size>");
            }

            bodyText.text = b.ToString();
        }

        // Effects where the value carries no meaning for the player
        private static readonly AbilityEffect[] NoValueEffects =
        {
            AbilityEffect.Taunt, AbilityEffect.Overhead, AbilityEffect.OverheadMagnet, AbilityEffect.FlipOmens,
            AbilityEffect.CleanseAll, AbilityEffect.SummonRandomFamiliar, AbilityEffect.ReturnFromGraveyard,
            AbilityEffect.GiveCardToHand, AbilityEffect.DetonateWithOpposite, AbilityEffect.ReflectDamage,
        };

        // e.g. "เมื่อลงการ์ด: มอบสถานะ ผีบังตา 1 (2 เทิร์น) → การ์ดฝั่งตรงข้าม 1 ใบ (ผู้เล่นเลือก)"
        private static string AbilityLine(CardAbility a)
        {
            string line = $"{NameOf(a.trigger)}: {NameOf(a.effect)}";
            if (a.effect == AbilityEffect.ApplyStatus) line += $" {NameOf(a.status)}";

            if (a.effect == AbilityEffect.CritChanceBonus) line += $" {a.value * 0.1f:0.#}%";
            else if (Array.IndexOf(NoValueEffects, a.effect) < 0)
            {
                bool signed = a.effect == AbilityEffect.BuffKhwan || a.effect == AbilityEffect.BuffAttack;
                line += $" {(signed && a.value > 0 ? "+" : "")}{a.value}";
            }
            if (a.duration > 0) line += $" ({a.duration} เทิร์น)";

            bool targetMatters = a.trigger != AbilityTrigger.Passive && a.target != AbilityTarget.Self;
            if (targetMatters) line += $" → {NameOf(a.target)}";
            if (a.ignoreArmor) line += " (ไม่สนเกราะ)";
            return line;
        }

        // Thai names shown in the inspector ([InspectorName] on the enums)
        private static string NameOf(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attr = field != null ? field.GetCustomAttribute<InspectorNameAttribute>() : null;
            return attr != null ? attr.displayName : value.ToString();
        }

        private static int AttackOf(CardInstance card)
        {
            return EffectResolver.Instance != null ? EffectResolver.Instance.EffectiveAttack(card) : card.familiarDamage;
        }

        private static string TypeLine(CardInstance card)
        {
            return (card.GetFormattedTypeText() ?? "").Replace(" • ", "  ").Replace("•", " ");
        }

        private static string Description(CardInstance card)
        {
            string format = card.descriptionFormat ?? "";
            try { return string.Format(format, card.baseValue); }
            catch { return format; }
        }

        private static Color AccentColor(MagicSchool school)
        {
            return school == MagicSchool.WhiteMagic ? new Color(0.88f, 0.7f, 0.38f) : new Color(0.85f, 0.25f, 0.25f);
        }

        private static Color SchoolColor(MagicSchool school)
        {
            return school == MagicSchool.WhiteMagic ? new Color(0.85f, 0.8f, 0.55f) : new Color(0.35f, 0.1f, 0.15f);
        }

        // ---------------------------------------------------------------- building the UI

        private void Build(TMP_FontAsset font)
        {
            if (canvas != null) return;

            var canvasGo = new GameObject("CardDetailCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            group = canvasGo.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            // Dimmed table
            var dim = NewImage("Dim", canvasGo.transform, new Color(0f, 0f, 0f, 0.72f));
            Stretch(dim.rectTransform);

            // The card, large, left of centre
            cardRoot = NewRect("Card", canvasGo.transform);
            cardRoot.anchorMin = cardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            cardRoot.sizeDelta = new Vector2(CardHeight * CardAspect, CardHeight);
            cardRoot.anchoredPosition = new Vector2(-360f, 0f);

            frameImage = NewImage("Frame", cardRoot, Color.white);
            Stretch(frameImage.rectTransform);
            artImage = NewImage("Artwork", cardRoot, Color.white);
            artImage.preserveAspect = true;
            PlaceOnFace(artImage.rectTransform, CardFaceLayout.Artwork);

            faceCost = FaceText("Cost", font, CardFaceLayout.Cost, 80, FontStyles.Normal);
            faceName = FaceText("Name", font, CardFaceLayout.Name, 64, FontStyles.Normal);
            faceType = FaceText("Type", font, CardFaceLayout.Type, 28, FontStyles.Normal);
            faceAttack = FaceText("Attack", font, CardFaceLayout.Attack, 44, FontStyles.Normal);
            faceKhwan = FaceText("Khwan", font, CardFaceLayout.Khwan, 44, FontStyles.Normal);
            faceDescription = FaceText("Description", font, CardFaceLayout.Description, 30, FontStyles.Normal);
            faceDescription.textWrappingMode = TextWrappingModes.Normal;

            // Information panel, right of centre
            var panel = NewRect("InfoPanel", canvasGo.transform);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(760f, 820f);
            panel.anchoredPosition = new Vector2(330f, 0f);
            panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.06f, 0.04f, 0.06f, 0.94f);
            panelOutline = panel.gameObject.AddComponent<Outline>();
            panelOutline.effectDistance = new Vector2(3f, -3f);

            headerText = NewText("Header", panel, font, 30, TextAlignmentOptions.TopLeft);
            SetRect(headerText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -190f), new Vector2(-40f, -30f));

            var line = NewImage("Divider", panel, new Color(1f, 1f, 1f, 0.15f));
            SetRect(line.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -202f), new Vector2(-40f, -200f));

            bodyText = NewText("Body", panel, font, 30, TextAlignmentOptions.TopLeft);
            bodyText.textWrappingMode = TextWrappingModes.Normal;
            SetRect(bodyText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(40f, 60f), new Vector2(-40f, -222f));

            hintText = NewText("Hint", panel, font, 22, TextAlignmentOptions.BottomRight);
            hintText.color = new Color(0.6f, 0.56f, 0.56f);
            hintText.text = "คลิก หรือ Esc เพื่อปิด";
            SetRect(hintText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 18f), new Vector2(-30f, 55f));

            canvasGo.SetActive(false);
        }

        private TextMeshProUGUI FaceText(string name, TMP_FontAsset font, CardFaceLayout.Box box, float maxSize, FontStyles style)
        {
            var t = NewText(name, cardRoot, font, maxSize, TextAlignmentOptions.Center);
            PlaceOnFace(t.rectTransform, box);
            t.enableAutoSizing = true;
            t.fontSizeMin = 8f;
            t.fontSizeMax = maxSize;
            t.fontStyle = style;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        // Face boxes are fractions of the card (CardFaceLayout: x -0.5..0.5, y 0.5 top .. -0.5 bottom)
        private static void PlaceOnFace(RectTransform rt, CardFaceLayout.Box box)
        {
            Vector2 c = box.center + new Vector2(0.5f, 0.5f);
            rt.anchorMin = c - box.size * 0.5f;
            rt.anchorMax = c + box.size * 0.5f;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var img = NewRect(name, parent).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions align)
        {
            var t = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.richText = true;
            t.raycastTarget = false;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
