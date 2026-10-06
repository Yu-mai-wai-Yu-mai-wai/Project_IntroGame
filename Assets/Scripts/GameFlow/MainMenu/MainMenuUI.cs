using DG.Tweening;
using TawanOS.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Title screen: New Game (asks before overwriting a saved run), Continue (only when a save
    /// exists, with a one-line summary of it), Settings and Quit.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Title")]
        public TextMeshProUGUI titleText;
        [Tooltip("Empty = Application.productName")]
        public string gameTitle;

        [Header("Buttons")]
        public Button newGameButton;
        public Button continueButton;
        public TextMeshProUGUI continueInfoText;
        public Button settingsButton;
        public Button quitButton;

        [Header("Overwrite Confirmation")]
        public GameObject confirmPanel;
        public Button confirmYesButton;
        public Button confirmNoButton;

        [Header("Mode Selection")]
        public GameObject modePanel;
        public Button fullModeButton;
        public Button shortModeButton;
        public Button modeCancelButton;

        [Header("Intro")]
        public CanvasGroup menuGroup;

        private void Awake()
        {
            if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGame);
            if (continueButton != null) continueButton.onClick.AddListener(OnContinue);
            if (settingsButton != null) settingsButton.onClick.AddListener(() => SettingsPanel.Open());
            if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
            if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmYes);
            if (confirmNoButton != null) confirmNoButton.onClick.AddListener(() => confirmPanel.SetActive(false));
            if (fullModeButton != null) fullModeButton.onClick.AddListener(() => StartGameWithMode(7));
            if (shortModeButton != null) shortModeButton.onClick.AddListener(() => StartGameWithMode(4));
            if (modeCancelButton != null) modeCancelButton.onClick.AddListener(() => { if (modePanel != null) modePanel.SetActive(false); });
        }

        private void Start()
        {
            if (titleText != null) titleText.text = string.IsNullOrEmpty(gameTitle) ? Application.productName : gameTitle;
            if (confirmPanel != null) confirmPanel.SetActive(false);
            if (modePanel != null) modePanel.SetActive(false);

            string save = RunState.DescribeSave();
            if (continueButton != null) continueButton.interactable = save != null;
            if (continueInfoText != null) continueInfoText.text = save ?? "ยังไม่มีเกมที่เล่นค้างไว้";

            if (menuGroup != null)
            {
                menuGroup.alpha = 0f;
                menuGroup.DOFade(1f, 0.8f);
            }
        }

        private void OnNewGame()
        {
            if (RunState.HasSave && confirmPanel != null)
            {
                confirmPanel.SetActive(true);
                return;
            }
            OpenModePanelOrStart();
        }

        private void OnConfirmYes()
        {
            if (confirmPanel != null) confirmPanel.SetActive(false);
            OpenModePanelOrStart();
        }

        private void OpenModePanelOrStart()
        {
            if (modePanel != null)
            {
                modePanel.SetActive(true);
            }
            else
            {
                StartGameWithMode(RunState.DefaultTotalFloors);
            }
        }

        public void StartGameWithMode(int totalFloors)
        {
            SetInteractable(false);
            GameFlowManager.Instance.StartNewGame(totalFloors);
        }

        private void OnContinue()
        {
            SetInteractable(false);
            GameFlowManager.Instance.ContinueGame();
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetInteractable(bool value)
        {
            if (menuGroup != null) menuGroup.interactable = value;
            if (confirmPanel != null) confirmPanel.SetActive(false);
            if (modePanel != null) modePanel.SetActive(false);
        }
    }
}
