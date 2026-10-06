using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using TawanOS.CardEngine;

namespace TawanOS.UI
{
    /// <summary>
    /// Top-centre banner of the combat scene: whose turn it is, which phase, and what the player can do now
    /// (plan task H1). While the camera is in the top view it also says how to get back (plan task G3).
    /// Created automatically in any scene that has a TurnPhaseController.
    /// </summary>
    public class TurnBannerView : MonoBehaviour
    {
        // Narrow enough to stay left of the enemy panel, which starts near x = 580 at 1920 wide
        private const float Width = 520f;
        private const float Height = 150f;

        private TurnPhaseController turns;
        private CanvasGroup rootGroup;
        private CanvasGroup bannerGroup;
        private Canvas canvas;
        private TextMeshProUGUI turnText;
        private TextMeshProUGUI ownerText;
        private TextMeshProUGUI phaseText;
        private TextMeshProUGUI hintText;
        private UnityEngine.UI.Image border;
        private TextMeshProUGUI topViewText;
        private GameObject topViewRoot;
        private bool combatOver;

        /// <summary>True while the "press C again" hint is on screen (read by tests and the screenshot tool).</summary>
        public bool TopViewHintVisible => topViewRoot != null && topViewRoot.activeSelf;

        private void Awake()
        {
            var theme = UIThemeSO.Current;
            canvas = UiFactory.CreateOverlayCanvas("TurnBannerCanvas", 900, transform);
            rootGroup = canvas.GetComponent<CanvasGroup>();

            // Top-left: the top centre belongs to the enemy panel and the top right to the camera button
            var root = UiFactory.CreateRect("Banner", canvas.transform);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(24f, -20f);
            root.sizeDelta = new Vector2(Width, Height);
            bannerGroup = root.gameObject.AddComponent<CanvasGroup>();

            border = UiFactory.CreateImage("Border", root, theme.crimson);
            UiFactory.Stretch(border.rectTransform, 0f);
            // Opaque: a translucent panel lets the bright border behind it show through (Linear blending makes 6% look like 17%)
            var panel = UiFactory.CreateImage("Panel", root, theme.panel);
            UiFactory.Stretch(panel.rectTransform, 4f);

            turnText = UiFactory.CreateText(root, "Turn", "", theme.labelSize, theme.text, TextAlignmentOptions.TopLeft, theme.bodyFont);
            turnText.rectTransform.anchorMin = new Vector2(0f, 1f);
            turnText.rectTransform.anchorMax = new Vector2(0f, 1f);
            turnText.rectTransform.pivot = new Vector2(0f, 1f);
            turnText.rectTransform.anchoredPosition = new Vector2(22f, -12f);
            turnText.rectTransform.sizeDelta = new Vector2(220f, 32f);

            ownerText = UiFactory.CreateText(root, "Owner", "", theme.titleSize, theme.accent, TextAlignmentOptions.Center, theme.titleFont != null ? theme.titleFont : theme.bodyFont);
            ownerText.rectTransform.anchorMin = new Vector2(0f, 1f);
            ownerText.rectTransform.anchorMax = new Vector2(1f, 1f);
            ownerText.rectTransform.pivot = new Vector2(0.5f, 1f);
            ownerText.rectTransform.anchoredPosition = new Vector2(0f, -10f);
            ownerText.rectTransform.sizeDelta = new Vector2(0f, 54f);

            phaseText = UiFactory.CreateText(root, "Phase", "", theme.bodySize, theme.text, TextAlignmentOptions.Center, theme.bodyFont);
            phaseText.rectTransform.anchorMin = new Vector2(0f, 1f);
            phaseText.rectTransform.anchorMax = new Vector2(1f, 1f);
            phaseText.rectTransform.pivot = new Vector2(0.5f, 1f);
            phaseText.rectTransform.anchoredPosition = new Vector2(0f, -64f);
            phaseText.rectTransform.sizeDelta = new Vector2(0f, 36f);

            hintText = UiFactory.CreateText(root, "Hint", "", theme.bodySize, theme.accent, TextAlignmentOptions.Center, theme.bodyFont);
            hintText.rectTransform.anchorMin = new Vector2(0f, 1f);
            hintText.rectTransform.anchorMax = new Vector2(1f, 1f);
            hintText.rectTransform.pivot = new Vector2(0.5f, 1f);
            hintText.rectTransform.anchoredPosition = new Vector2(0f, -102f);
            hintText.rectTransform.sizeDelta = new Vector2(0f, 36f);

            // Shown only while the camera is in the top view
            var hintRoot = UiFactory.CreateRect("TopViewHint", canvas.transform);
            topViewRoot = hintRoot.gameObject;
            hintRoot.anchorMin = new Vector2(0.5f, 0f);
            hintRoot.anchorMax = new Vector2(0.5f, 0f);
            hintRoot.pivot = new Vector2(0.5f, 0f);
            hintRoot.anchoredPosition = new Vector2(0f, 230f);
            hintRoot.sizeDelta = new Vector2(760f, 64f);
            var hintBorder = UiFactory.CreateImage("Border", hintRoot, theme.accent);
            UiFactory.Stretch(hintBorder.rectTransform, 0f);
            var hintPanel = UiFactory.CreateImage("Panel", hintRoot, theme.black);
            UiFactory.Stretch(hintPanel.rectTransform, 3f);
            topViewText = UiFactory.CreateText(hintRoot, "Text", "", theme.bodySize + 2f, theme.accent, TextAlignmentOptions.Center, theme.bodyFont);
            UiFactory.Stretch(topViewText.rectTransform, 10f);
            topViewRoot.SetActive(false);
        }

        private void Start()
        {
            turns = TurnPhaseController.Instance;
            if (turns == null)
            {
                Destroy(gameObject);
                return;
            }

            turns.OnPhaseChanged += Refresh;
            turns.OnTurnStarted += OnTurnStarted;
            if (CombatManager.Instance != null) CombatManager.Instance.OnCombatEnded += OnCombatEnded;
            Refresh(turns.CurrentPhase);
        }

        private void OnDestroy()
        {
            if (turns != null)
            {
                turns.OnPhaseChanged -= Refresh;
                turns.OnTurnStarted -= OnTurnStarted;
            }
            if (CombatManager.Instance != null) CombatManager.Instance.OnCombatEnded -= OnCombatEnded;
        }

        private void OnTurnStarted(int turn)
        {
            turnText.text = $"เทิร์น {turn}";
        }

        private void OnCombatEnded(bool victory)
        {
            combatOver = true;
        }

        private void Refresh(TurnPhase phase)
        {
            var theme = UIThemeSO.Current;
            bool playerPhase = phase == TurnPhase.PlayerBoard || phase == TurnPhase.PlayerSpell;
            bool enemyPhase = phase == TurnPhase.EnemyBoard || phase == TurnPhase.EnemySpell;

            turnText.text = turns.TurnNumber > 0 ? $"เทิร์น {turns.TurnNumber}" : "";
            ownerText.text = PlayBlockReasons.OwnerLabel(phase);
            ownerText.color = playerPhase ? theme.accent : theme.text;
            phaseText.text = TurnPhaseController.PhaseLabel(phase);
            hintText.text = PlayBlockReasons.HintFor(phase);
            hintText.color = enemyPhase ? theme.text : theme.accent;
            border.color = playerPhase ? theme.accent : theme.crimson;
        }

        private void Update()
        {
            // The board-only top view hides the whole UI, so only the banner hides with it; the "press C again"
            // hint must stay visible there because that is exactly where the player needs it (plan task G3)
            bannerGroup.alpha = combatOver || CombatCameraRig3D.HideOverlay ? 0f : 1f;

            var rig = CombatCameraRig3D.Instance;
            bool inTopView = rig != null && rig.IsTopView && !combatOver;
            if (topViewRoot.activeSelf != inTopView) topViewRoot.SetActive(inTopView);
            if (inTopView) topViewText.text = $"มุมบน · กด {rig.toggleKey} อีกครั้งเพื่อกลับมุมปกติ";
        }
    }

    /// <summary>Adds the turn banner to every scene that has a TurnPhaseController.</summary>
    public static class CombatFeedbackBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryCreate();
        }

        private static void TryCreate()
        {
            if (TurnPhaseController.Instance == null) return;
            if (Object.FindFirstObjectByType<TurnBannerView>() != null) return;

            new GameObject("TurnBanner").AddComponent<TurnBannerView>();
        }
    }
}
