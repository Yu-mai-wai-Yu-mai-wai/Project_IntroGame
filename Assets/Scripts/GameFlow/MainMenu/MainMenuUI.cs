using DG.Tweening;
using TawanOS.CardEngine;
using TawanOS.UI;
using TawanOS.VFX;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Title screen: New Game (asks before overwriting a saved run), Continue (only when a save
    /// exists, with a one-line summary of it), the card collection (ตำราไสยเวท), Settings and Quit.
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
        public Button collectionButton;
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
            if (collectionButton != null) collectionButton.onClick.AddListener(OpenCollection);
            if (settingsButton != null) settingsButton.onClick.AddListener(() => SettingsPanel.Open());
            if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
            if (confirmYesButton != null) confirmYesButton.onClick.AddListener(OnConfirmYes);
            if (confirmNoButton != null) confirmNoButton.onClick.AddListener(() => confirmPanel.SetActive(false));
            if (fullModeButton != null) fullModeButton.onClick.AddListener(() => StartGameWithMode(7));
            if (shortModeButton != null) shortModeButton.onClick.AddListener(() => StartGameWithMode(4));
            if (modeCancelButton != null) modeCancelButton.onClick.AddListener(() => { if (modePanel != null) modePanel.SetActive(false); });

            // The stage behind the menu turns gently with the cursor (tune it by adding the component to the camera)
            var cam = Camera.main;
            if (cam != null && cam.GetComponent<MouseParallaxCamera>() == null) cam.gameObject.AddComponent<MouseParallaxCamera>();

            // The main buttons sit on drifting smoke instead of flat panels (tune it by adding the component to a button)
            foreach (var b in new[] { newGameButton, continueButton, collectionButton, settingsButton, quitButton, fullModeButton, shortModeButton })
                if (b != null && b.GetComponent<UiSmokeButton>() == null) b.gameObject.AddComponent<UiSmokeButton>();
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
            // Show where the saved run stands before resuming it; an unreadable save falls through to ContinueGame, which handles it
            var summary = RunState.ReadSaveSummary();
            if (summary.HasValue)
            {
                ContinuePopup.Show(summary.Value, StartContinue);
                return;
            }
            StartContinue();
        }

        private void StartContinue()
        {
            SetInteractable(false);
            GameFlowManager.Instance.ContinueGame();
        }

        // ตำราไสยเวท: every card in the game, the ones never seen in any run face down
        private void OpenCollection()
        {
            var catalog = CardCatalogSO.Load();
            if (catalog == null) return;
            var collection = CardCollection.Current;
            // The ตำราไสยเวท book, turned page by page; the plain list if the book's art (Resources/CardBook) is missing
            if (CardBookPanel.Available) CardBookPanel.Ensure().Open();
            else DeckViewerPanelUI.Ensure().ShowCollection(CardBookPanel.Title, catalog.cards, collection.Has, "ยังไม่มีการ์ดในเกม");
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
