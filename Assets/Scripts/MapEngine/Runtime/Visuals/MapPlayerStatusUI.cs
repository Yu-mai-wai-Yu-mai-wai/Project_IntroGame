using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TawanOS.UI;
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
        private Image hpBarFill;
        private TextMeshProUGUI incenseText;
        private TextMeshProUGUI deckText;

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
            int deck = DeckCount;

            if (hpText != null)
            {
                hpText.text = $"{hp}/{maxHp}";
            }

            if (hpBarFill != null)
            {
                if (hpBarFill.sprite == null)
                {
                    hpBarFill.sprite = GetWhiteSprite();
                    hpBarFill.type = Image.Type.Filled;
                    hpBarFill.fillMethod = Image.FillMethod.Horizontal;
                }
                float ratio = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
                hpBarFill.fillAmount = ratio;
            }

            if (incenseText != null)
            {
                incenseText.text = $"{incense}";
            }

            if (deckText != null)
            {
                deckText.text = $"สำรับ ({deck})";
            }
        }

        private void BuildUI()
        {
            // If already built in hierarchy, reuse references
            var existingFill = transform.Find("HpBarBg/HpBarFill")?.GetComponent<Image>();
            if (existingFill != null)
            {
                hpBarFill = existingFill;
                hpBarFill.sprite = GetWhiteSprite();
                hpBarFill.type = Image.Type.Filled;
                hpBarFill.fillMethod = Image.FillMethod.Horizontal;
                hpText = transform.Find("HpText")?.GetComponent<TextMeshProUGUI>();
                incenseText = transform.Find("IncenseText")?.GetComponent<TextMeshProUGUI>();
                deckText = transform.Find("DeckButton/Inner/Label")?.GetComponent<TextMeshProUGUI>();
                return;
            }

            var theme = UIThemeSO.Current;

            var rootRect = GetComponent<RectTransform>();
            if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();

            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(24f, -14f);
            rootRect.sizeDelta = new Vector2(580f, 48f);

            // Outer Border
            var border = UiFactory.CreateImage("Border", transform, theme.crimson);
            UiFactory.Stretch(border.rectTransform, 0f);

            // Panel Background
            var bg = UiFactory.CreateImage("Panel", transform, theme.panel);
            UiFactory.Stretch(bg.rectTransform, 2f);

            // --- 1. Khwan (ขวัญ) Section ---
            var hpLabel = UiFactory.CreateText(transform, "HpLabel", "ขวัญ",
                theme.labelSize, theme.text, TextAlignmentOptions.MidlineLeft, theme.bodyFont);
            hpLabel.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            hpLabel.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            hpLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
            hpLabel.rectTransform.anchoredPosition = new Vector2(16f, 0f);
            hpLabel.rectTransform.sizeDelta = new Vector2(48f, 32f);

            // HP Bar Background
            var barBgGo = new GameObject("HpBarBg", typeof(RectTransform), typeof(Image));
            barBgGo.transform.SetParent(transform, false);
            var barBgRect = barBgGo.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0f, 0.5f);
            barBgRect.anchorMax = new Vector2(0f, 0.5f);
            barBgRect.pivot = new Vector2(0f, 0.5f);
            barBgRect.anchoredPosition = new Vector2(68f, 0f);
            barBgRect.sizeDelta = new Vector2(110f, 18f);
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
            hpBarFill.color = theme.crimson;
            hpBarFill.fillAmount = 1f;

            // HP Text
            hpText = UiFactory.CreateText(transform, "HpText", $"{CurrentHp}/{MaxHp}",
                theme.labelSize, theme.accent, TextAlignmentOptions.MidlineLeft, theme.bodyFont);
            hpText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            hpText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            hpText.rectTransform.pivot = new Vector2(0f, 0.5f);
            hpText.rectTransform.anchoredPosition = new Vector2(186f, 0f);
            hpText.rectTransform.sizeDelta = new Vector2(80f, 32f);

            // --- 2. Incense (ธูป) Section ---
            var incenseLabel = UiFactory.CreateText(transform, "IncenseLabel", "ธูป",
                theme.labelSize, theme.text, TextAlignmentOptions.MidlineLeft, theme.bodyFont);
            incenseLabel.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            incenseLabel.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            incenseLabel.rectTransform.pivot = new Vector2(0f, 0.5f);
            incenseLabel.rectTransform.anchoredPosition = new Vector2(276f, 0f);
            incenseLabel.rectTransform.sizeDelta = new Vector2(40f, 32f);

            incenseText = UiFactory.CreateText(transform, "IncenseText", $"{Incense}",
                theme.labelSize + 2f, theme.accent, TextAlignmentOptions.MidlineLeft, theme.bodyFont);
            incenseText.fontStyle = FontStyles.Bold;
            incenseText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            incenseText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            incenseText.rectTransform.pivot = new Vector2(0f, 0.5f);
            incenseText.rectTransform.anchoredPosition = new Vector2(322f, 0f);
            incenseText.rectTransform.sizeDelta = new Vector2(70f, 32f);

            // --- 3. Deck Button (สำรับ) Section ---
            var deckBtnGo = new GameObject("DeckButton", typeof(RectTransform), typeof(Image), typeof(Button));
            deckBtnGo.transform.SetParent(transform, false);
            var deckBtnRect = deckBtnGo.GetComponent<RectTransform>();
            deckBtnRect.anchorMin = new Vector2(0f, 0.5f);
            deckBtnRect.anchorMax = new Vector2(0f, 0.5f);
            deckBtnRect.pivot = new Vector2(0f, 0.5f);
            deckBtnRect.anchoredPosition = new Vector2(404f, 0f);
            deckBtnRect.sizeDelta = new Vector2(160f, 34f);

            var deckBtnImg = deckBtnGo.GetComponent<Image>();
            deckBtnImg.color = theme.crimson;

            var deckInner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            deckInner.transform.SetParent(deckBtnGo.transform, false);
            UiFactory.Stretch(deckInner.GetComponent<RectTransform>(), 2f);
            deckInner.GetComponent<Image>().color = theme.panel;

            deckText = UiFactory.CreateText(deckInner.transform, "Label", $"สำรับ ({DeckCount})",
                theme.labelSize, theme.accent, TextAlignmentOptions.Center, theme.bodyFont);
            UiFactory.Stretch(deckText.rectTransform, 0f);

            var btn = deckBtnGo.GetComponent<Button>();
            btn.onClick.AddListener(OnDeckClicked);
        }

        private void OnDeckClicked()
        {
            if (PauseMenu.IsPaused) return;
            DeckViewerPanelUI.Ensure().Show("สำรับ", RunState.Current.Deck, "ยังไม่มีการ์ดในสำรับ");
        }
    }
}
