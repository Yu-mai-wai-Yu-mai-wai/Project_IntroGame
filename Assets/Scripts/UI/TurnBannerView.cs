using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using TawanOS.CardEngine;

namespace TawanOS.UI
{
    /// <summary>
    /// The combat scene's "press C again" hint, shown while the player has the C top view open (plan task G3). The turn
    /// banner that used to sit top left (turn number, whose turn, phase, what to do) is gone: the turn is now shown
    /// on the table by TableTurnMarker (Game Designer, 13 Oct). Created automatically in any scene that has a
    /// TurnPhaseController.
    /// </summary>
    public class TurnBannerView : MonoBehaviour
    {
        private TurnPhaseController turns;
        private Canvas canvas;
        private TextMeshProUGUI topViewText;
        private GameObject topViewRoot;
        private bool combatOver;

        /// <summary>True while the "press C again" hint is on screen (read by tests and the screenshot tool).</summary>
        public bool TopViewHintVisible => topViewRoot != null && topViewRoot.activeSelf;

        private void Awake()
        {
            var theme = UIThemeSO.Current;
            canvas = UiFactory.CreateOverlayCanvas("TurnBannerCanvas", 900, transform);

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

            if (CombatManager.Instance != null) CombatManager.Instance.OnCombatEnded += OnCombatEnded;
        }

        private void OnDestroy()
        {
            if (CombatManager.Instance != null) CombatManager.Instance.OnCombatEnded -= OnCombatEnded;
        }

        private void OnCombatEnded(bool victory)
        {
            combatOver = true;
        }

        private void Update()
        {
            // The board-only top view hides the whole UI, but this hint must stay visible there because that is
            // exactly where the player needs it (plan task G3). Only the view the player opened with C: the clash
            // and a held card also use the top view, but C does nothing there
            var rig = CombatCameraRig3D.Instance;
            bool inTopView = rig != null && rig.IsBoardOnlyView && !combatOver;
            if (topViewRoot.activeSelf != inTopView) topViewRoot.SetActive(inTopView);
            if (inTopView) topViewText.text = $"มุมบน · กด {rig.homeKey} เพื่อกลับมุมปกติ";
        }
    }

    /// <summary>Adds the top-view hint and the turn marker on the table to every scene that has a TurnPhaseController.</summary>
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
            if (Object.FindFirstObjectByType<TurnBannerView>() == null) new GameObject("TurnBanner").AddComponent<TurnBannerView>();
            if (Object.FindFirstObjectByType<TableTurnMarker>() == null) new GameObject("TableTurnMarker").AddComponent<TableTurnMarker>();
        }
    }
}
