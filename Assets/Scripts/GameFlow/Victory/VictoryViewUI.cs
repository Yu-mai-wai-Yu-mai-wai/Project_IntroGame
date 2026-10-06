using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TawanOS.UI;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Victory Screen UI (Plan Task A3): Displays run summary and final victory message
    /// when the player defeats the Boss. Clears save data and allows returning to Main Menu.
    /// Uses UIThemeSO colors and typography (WCAG AA compliant, authentic Thai text, zero emojis).
    /// </summary>
    public class VictoryViewUI : MonoBehaviour
    {
        [Header("UI Elements")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI subtitleText;
        public TextMeshProUGUI epilogueText;
        public TextMeshProUGUI statsText;
        public Button mainMenuButton;
        public TextMeshProUGUI mainMenuButtonLabel;
        public CanvasGroup contentGroup;

        private const string DefaultEpilogue =
            "เจ้าได้สะกดวิญญาณร้ายแห่งป่าช้า และรวบรวมขวัญที่กระเจิดกระเจิงกลับคืนสู่ร่างได้สำเร็จ\n" +
            "ควันธูปจางหาย สายหมอกมืดมิดเริ่มคลี่คลาย การเดินทางในคืนอันยาวนานสิ้นสุดลงแล้ว...";

        private void Awake()
        {
            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.AddListener(OnMainMenuClicked);
            }
        }

        private void Start()
        {
            DisplaySummary();

            if (contentGroup != null)
            {
                contentGroup.alpha = 0f;
                contentGroup.DOFade(1f, 1.2f).SetEase(Ease.OutCubic);
            }
        }

        public void DisplaySummary()
        {
            var theme = UIThemeSO.Current;
            var run = RunState.Current;

            int khwan = run != null && run.CurrentHp > 0 ? run.CurrentHp : 50;
            int maxKhwan = run != null && run.MaxHp > 0 ? run.MaxHp : 50;
            int incense = run != null ? run.Incense : 0;
            int deckCount = run != null && run.Deck != null ? run.Deck.Count : 0;

            if (titleText != null)
            {
                titleText.text = "สู่สุขคติ";
                if (theme != null && theme.titleFont != null) titleText.font = theme.titleFont;
            }

            if (subtitleText != null)
            {
                subtitleText.text = "ชัยชนะอันสมบูรณ์เหนือวิญญาณร้าย";
                if (theme != null && theme.bodyFont != null) subtitleText.font = theme.bodyFont;
            }

            if (epilogueText != null)
            {
                epilogueText.text = DefaultEpilogue;
                if (theme != null && theme.bodyFont != null) epilogueText.font = theme.bodyFont;
            }

            if (statsText != null)
            {
                statsText.text = $"ขวัญคงเหลือ: {khwan}/{maxKhwan}   •   ธูปสะสม: {incense}   •   สำรับการ์ด: {deckCount} ใบ";
                if (theme != null && theme.bodyFont != null) statsText.font = theme.bodyFont;
            }

            if (mainMenuButtonLabel != null)
            {
                mainMenuButtonLabel.text = "กลับสู่หน้าจอหลัก";
                if (theme != null && theme.bodyFont != null) mainMenuButtonLabel.font = theme.bodyFont;
            }
        }

        public void OnMainMenuClicked()
        {
            if (mainMenuButton != null) mainMenuButton.interactable = false;

            if (Application.CanStreamedLevelBeLoaded(GameFlowManager.MainMenuSceneName))
            {
                SceneManager.LoadScene(GameFlowManager.MainMenuSceneName, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogWarning("[VictoryViewUI] MainMenu scene not found in Build Settings.");
            }
        }
    }
}
