using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TawanOS.UI;
using TawanOS.VFX;
using TawanOS.GameFlow;
using TawanOS.CardEngine;

namespace TawanOS.MapEngine
{
    /// <summary>
    /// Map Player Status HUD (Plan Task G2, P0-10): Top-left status bar on the map scene showing the player's
    /// current Khwan (HP bar + numbers), Incense sticks (ธูป currency), and Deck count button.
    /// Synchronizes live with RunState.Current, formatted with UIThemeSO and Thai typography.
    /// Fill amount follows currentHp / maxHp proportionally.
    /// </summary>
    public class MapPlayerStatusUI : MonoBehaviour
    {
        private TextMeshProUGUI hpText;
        private RectTransform tempStrip;
        private Image hpBarFill;
        private TextMeshProUGUI incenseText;

        public Image HpBarFill => hpBarFill;
        public float FillAmount => hpBarFill != null ? hpBarFill.fillAmount : 0f;
        public TextMeshProUGUI HpText => hpText;

        public int CurrentHp => RunState.Current.CurrentHp;
        public int MaxHp => RunState.Current.MaxHp;
        public int Incense => RunState.Current.Incense;
        public int DeckCount => RunState.Current.Deck != null ? RunState.Current.Deck.Count : 0;

        private static Sprite whiteSprite;
        public static Sprite GetWhiteSprite()
        {
            if (whiteSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            return whiteSprite;
        }

        public void EnsureUI()
        {
            if (hpBarFill == null)
            {
                BuildUI();
            }
        }

        private void Awake()
        {
            EnsureUI();
        }

        public void Subscribe()
        {
            Unsubscribe();
            RunState.Current.OnChanged += UpdateDisplay;
        }

        public void Unsubscribe()
        {
            RunState.Current.OnChanged -= UpdateDisplay;
        }

        private void OnEnable()
        {
            Subscribe();
            UpdateDisplay();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Start()
        {
            UpdateDisplay();
        }

        public void UpdateDisplay()
        {
            EnsureUI();

            int hp = CurrentHp;
            int maxHp = MaxHp > 0 ? MaxHp : RunState.DefaultMaxHp;
            int incense = Incense;

            if (hpText != null)
            {
                hpText.text = KhwanDisplay.Text(hp, maxHp);
            }

            if (hpBarFill != null)
            {
                if (hpBarFill.sprite == null)
                {
                    hpBarFill.sprite = GetWhiteSprite();
                    hpBarFill.type = Image.Type.Filled;
                    hpBarFill.fillMethod = Image.FillMethod.Horizontal;
                }
                hpBarFill.fillAmount = KhwanDisplay.Fill(hp, maxHp);
                // ขวัญชั่วคราว: gold strip over the bar (found again if the bar was built in the scene)
                if (tempStrip == null)
                {
                    var track = (RectTransform)hpBarFill.transform.parent;
                    var found = track.Find("TempKhwan");
                    tempStrip = found != null ? (RectTransform)found : KhwanDisplay.AddTempStrip(track, 2f);
                }
                KhwanDisplay.SetTempStrip(tempStrip, hp, maxHp);
            }

            if (incenseText != null)
            {
                incenseText.text = $"{incense}";
            }

        }

        private void BuildUI()
        {
            // If already built in hierarchy, reuse references
            var existingFill = transform.Find("HpBarBg/HpBarFill")?.GetComponent<Image>();
            if (existingFill != null)
            {
                hpBarFill = existingFill;
                hpBarFill.color = KhwanDisplay.BarColour;
                hpBarFill.sprite = GetWhiteSprite();
                hpBarFill.type = Image.Type.Filled;
                hpBarFill.fillMethod = Image.FillMethod.Horizontal;
                hpText = transform.Find("HpText")?.GetComponent<TextMeshProUGUI>();
                incenseText = transform.Find("IncenseText")?.GetComponent<TextMeshProUGUI>();
                return;
            }

            var theme = UIThemeSO.Current;

            var rootRect = GetComponent<RectTransform>();
            if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();

            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(28f, -18f);
            // Khwan and incense; the deck is the pile at the bottom of the screen (RunDeckButton, Game Designer 24 Oct)
            rootRect.sizeDelta = new Vector2(450f, 56f);

            // Outer Border
            var border = UiFactory.CreateImage("Border", transform, theme.crimson);
            UiFactory.Stretch(border.rectTransform, 0f);
            var softBorder = border.gameObject.AddComponent<SoftEdgeImage>();
            softBorder.SetSides(0.04f, 0.04f, 0.06f, 0.06f);

            // Panel Background
            var bg = UiFactory.CreateImage("Panel", transform, theme.panel);
            UiFactory.Stretch(bg.rectTransform, 2f);
            var softBg = bg.gameObject.AddComponent<SoftEdgeImage>();
            softBg.SetSides(0.04f, 0.04f, 0.06f, 0.06f);

            // --- 1. Khwan (ขวัญ) Section ---
            var hpLabel = UiFactory.CreateText(transform, "HpLabel", "ขวัญ",
                theme.labelSize + 2f, theme.text, TextAlignmentOptions.MidlineLeft, theme.bodyFont);
            hpLabel.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            hpLabel.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            hpLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
            hpLabel.rectTransform.anchoredPosition = new Vector2(18f, 0f);
            hpLabel.rectTransform.sizeDelta = new Vector2(52f, 36f);

            // HP Bar Background
            var barBgGo = new GameObject("HpBarBg", typeof(RectTransform), typeof(Image));
            barBgGo.transform.SetParent(transform, false);
            var barBgRect = barBgGo.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0f, 0.5f);
            barBgRect.anchorMax = new Vector2(0f, 0.5f);
            barBgRect.pivot = new Vector2(0f, 0.5f);
            barBgRect.anchoredPosition = new Vector2(74f, 0f);
            barBgRect.sizeDelta = new Vector2(130f, 22f);
            barBgGo.GetComponent<Image>().color = theme.black;

            // HP Bar Fill
            var barFillGo = new GameObject("HpBarFill", typeof(RectTransform), typeof(Image));
            barFillGo.transform.SetParent(barBgGo.transform, false);
            var barFillRect = barFillGo.GetComponent<RectTransform>();
            UiFactory.Stretch(barFillRect, 2f);
            hpBarFill = barFillGo.GetComponent<Image>();
            hpBarFill.sprite = GetWhiteSprite();
            hpBarFill.type = Image.Type.Filled;
            hpBarFill.fillMethod = Image.FillMethod.Horizontal;
            hpBarFill.color = KhwanDisplay.BarColour;
            hpBarFill.fillAmount = 1f;

            // HP Text
            hpText = UiFactory.CreateText(transform, "HpText", $"{CurrentHp}/{MaxHp}",
                theme.labelSize + 2f, theme.accent, TextAlignmentOptions.MidlineLeft, theme.bodyFont);
            hpText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            hpText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            hpText.rectTransform.pivot = new Vector2(0f, 0.5f);
            hpText.rectTransform.anchoredPosition = new Vector2(212f, 0f);
            hpText.rectTransform.sizeDelta = new Vector2(90f, 36f);

            // --- 2. Incense (ธูป) Section ---
            var incenseLabel = UiFactory.CreateText(transform, "IncenseLabel", "ธูป",
                theme.labelSize + 2f, theme.text, TextAlignmentOptions.MidlineLeft, theme.bodyFont);
            incenseLabel.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            incenseLabel.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            incenseLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
            incenseLabel.rectTransform.anchoredPosition = new Vector2(310f, 0f);
            incenseLabel.rectTransform.sizeDelta = new Vector2(44f, 36f);

            incenseText = UiFactory.CreateText(transform, "IncenseText", $"{Incense}",
                theme.labelSize + 4f, theme.accent, TextAlignmentOptions.MidlineLeft, theme.bodyFont);
            incenseText.fontStyle = FontStyles.Bold;
            incenseText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            incenseText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            incenseText.rectTransform.pivot = new Vector2(0f, 0.5f);
            incenseText.rectTransform.anchoredPosition = new Vector2(360f, 0f);
            incenseText.rectTransform.sizeDelta = new Vector2(80f, 36f);
        }
    }
}
