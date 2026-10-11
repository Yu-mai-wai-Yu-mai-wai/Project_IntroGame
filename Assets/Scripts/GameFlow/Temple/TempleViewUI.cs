using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// วัด screen: a short scene-setting text, the player's Khwan, and one choice: กราบพระ (Khwan back to full).
    /// With Khwan already full the button is greyed out and only เดินทางต่อ is offered. Built by TempleSetupTool.
    /// </summary>
    public class TempleViewUI : MonoBehaviour
    {
        [Header("Intro")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI storyText;
        public TextMeshProUGUI khwanText;

        [Header("Choice")]
        public GameObject choicePanel;
        public Button prayButton;
        public TextMeshProUGUI prayHintText;
        public Button leaveButton;

        [Header("Result")]
        public GameObject resultPanel;
        public TextMeshProUGUI resultText;
        public Button continueButton;

        private TempleManager temple;

        private void Awake()
        {
            if (prayButton != null) prayButton.onClick.AddListener(() => temple?.Pray());
            if (leaveButton != null) leaveButton.onClick.AddListener(() => temple?.Leave());
            if (continueButton != null) continueButton.onClick.AddListener(() => temple?.Leave());
        }

        private void OnEnable()
        {
            RunState.Current.OnChanged += RefreshKhwan;
            RefreshKhwan();
        }

        private void OnDisable()
        {
            RunState.Current.OnChanged -= RefreshKhwan;
        }

        private void RefreshKhwan()
        {
            if (khwanText != null) khwanText.text = $"ขวัญ {RunState.Current.CurrentHp}/{RunState.Current.MaxHp}";
        }

        public void Show(TempleManager manager)
        {
            temple = manager;
            SetPanels(choice: true);
            bool canPray = temple != null && temple.CanPray;
            if (prayButton != null) prayButton.interactable = canPray;
            if (prayHintText != null) prayHintText.text = canPray ? "ฟื้นขวัญเต็มหลอด" : "ขวัญเต็มอยู่แล้ว";
            // Nothing to do here with full Khwan: the only way on is to leave
            if (leaveButton != null) leaveButton.gameObject.SetActive(!canPray);
            RefreshKhwan();
        }

        public void ShowResult(int healed)
        {
            SetPanels(choice: false);
            var run = RunState.Current;
            if (resultText != null) resultText.text = $"ขวัญที่หลุดลอยกลับคืนสู่ตัว ({run.CurrentHp}/{run.MaxHp})";
            TawanOS.Audio.AudioManager.Instance?.PlaySfx("sfx_merit_gain");
        }

        private void SetPanels(bool choice)
        {
            if (choicePanel != null) choicePanel.SetActive(choice);
            if (resultPanel != null) resultPanel.SetActive(!choice);
            if (storyText != null) storyText.gameObject.SetActive(choice);
        }
    }
}
