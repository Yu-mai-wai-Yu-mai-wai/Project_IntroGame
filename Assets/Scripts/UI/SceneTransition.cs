using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.UI
{
    /// <summary>
    /// Scene change with a fade: the screen fades to black, the next scene loads while it is black, and the screen
    /// fades back in once the scene is ready. Ready means the scene has loaded and its first frames run smoothly:
    /// right after a scene starts, Unity is still sending its textures and meshes to the graphics card and compiling
    /// shaders, which shows as stutter, so the screen stays black until a few frames in a row are quick (or a time
    /// limit passes). "กำลังโหลด" sits at the bottom right while the screen is black, its dots counting up
    /// (กำลังโหลด, กำลังโหลด., กำลังโหลด.., กำลังโหลด...) so the player sees the game has not frozen (Game Designer, 11 Oct).
    /// One instance lives across scenes; it runs on unscaled time, so a paused game (timeScale 0) still transitions.
    /// </summary>
    public class SceneTransition : MonoBehaviour
    {
        private const int SortingOrder = 1000; // above every in-game canvas, the card book (500) included

        [Header("Fade (seconds)")]
        public float fadeToBlack = 0.45f;
        public float fadeFromBlack = 0.6f;
        [Tooltip("Shortest time the screen stays black, so a scene that loads at once still reads as a cut.")]
        public float minBlack = 0.25f;

        [Header("Wait for the new scene to settle")]
        [Tooltip("A frame quicker than this counts as smooth (seconds; 0.05 = 20 frames per second).")]
        public float smoothFrameTime = 0.05f;
        [Tooltip("This many smooth frames in a row and the scene is ready.")]
        public int smoothFramesNeeded = 5;
        [Tooltip("Longest wait for smooth frames after the scene has loaded, so a slow computer never stays black.")]
        public float maxSettleTime = 4f;

        [Header("Loading label")]
        [Tooltip("Shown without dots; the dots are added one by one.")]
        public string loadingLabel = "กำลังโหลด";
        [Tooltip("Seconds per step of the dots (none, one, two, three, then again).")]
        public float dotStep = 0.35f;

        private static SceneTransition instance;

        private CanvasGroup blackGroup;
        private TextMeshProUGUI loadingText;

        /// <summary>True from the start of a fade to black until the next scene is showing.</summary>
        public static bool IsLoading { get; private set; }

        private static SceneTransition Ensure()
        {
            if (instance != null) return instance;
            var go = new GameObject("SceneTransition");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SceneTransition>();
            instance.Build();
            return instance;
        }

        /// <summary>
        /// Fades to black, loads <paramref name="sceneName"/>, then fades back in. A request while another scene is
        /// loading is ignored.
        /// </summary>
        public static void Load(string sceneName)
        {
            if (!Application.isPlaying)
            {
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                return;
            }
            if (IsLoading)
            {
                Debug.LogWarning($"[SceneTransition] Already loading a scene - ignored the request for {sceneName}.");
                return;
            }
            Ensure().StartCoroutine(instance.LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            IsLoading = true;

            // Fade to black; the black screen also takes the clicks, so nothing in the old scene reacts meanwhile
            blackGroup.blocksRaycasts = true;
            yield return blackGroup.DOFade(1f, fadeToBlack).SetEase(Ease.InQuad).SetUpdate(true).WaitForCompletion();

            float blackSince = Time.unscaledTime;
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError($"[SceneTransition] Scene {sceneName} could not be loaded (is it in Build Settings?).");
                yield return FadeIn();
                IsLoading = false;
                yield break;
            }
            op.allowSceneActivation = false;
            while (op.progress < 0.9f || Time.unscaledTime - blackSince < minBlack) yield return null;
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            yield return null; // the new scene's Start runs before it is shown
            yield return WaitUntilSettled();

            IsLoading = false;
            yield return FadeIn();
        }

        // Waits for smoothFramesNeeded quick frames in a row: the first frames of a scene are slow while its assets
        // are uploaded and its shaders compiled, and the player should not see that
        private IEnumerator WaitUntilSettled()
        {
            float start = Time.unscaledTime;
            int smooth = 0;
            while (smooth < smoothFramesNeeded && Time.unscaledTime - start < maxSettleTime)
            {
                yield return null;
                smooth = Time.unscaledDeltaTime <= smoothFrameTime ? smooth + 1 : 0;
            }
            if (smooth < smoothFramesNeeded)
                Debug.LogWarning($"[SceneTransition] {SceneManager.GetActiveScene().name} still had slow frames after {maxSettleTime} s; showing it anyway.");
        }

        private IEnumerator FadeIn()
        {
            yield return blackGroup.DOFade(0f, fadeFromBlack).SetEase(Ease.OutQuad).SetUpdate(true).WaitForCompletion();
            blackGroup.blocksRaycasts = false;
        }

        private void Build()
        {
            var canvas = UiFactory.CreateOverlayCanvas("TransitionCanvas", SortingOrder, transform);
            // Clicks pass through except while the screen is black (the black's own group turns that on)
            canvas.GetComponent<CanvasGroup>().blocksRaycasts = true;
            canvas.gameObject.AddComponent<GraphicRaycaster>();

            var black = UiFactory.CreateImage("Black", canvas.transform, Color.black);
            UiFactory.Stretch(black.rectTransform, 0f);
            blackGroup = black.gameObject.AddComponent<CanvasGroup>();
            blackGroup.alpha = 0f;
            blackGroup.blocksRaycasts = false;
            black.raycastTarget = true;

            // Bottom right, on the black, so it fades in and out with it. The box is as wide as the label with all three
            // dots and the text is left-aligned in it, so the word stays still while the dots come and go.
            var theme = UIThemeSO.Current;
            loadingText = UiFactory.CreateText(black.transform, "Loading", loadingLabel, theme.bodySize, theme.text,
                TextAlignmentOptions.BottomLeft, theme.bodyFont);
            var rt = loadingText.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-40f, 32f);
            float width = loadingText.GetPreferredValues(loadingLabel + "...").x;
            rt.sizeDelta = new Vector2(Mathf.Ceil(width) + 4f, 48f);
            loadingText.textWrappingMode = TextWrappingModes.NoWrap;
            loadingText.raycastTarget = false;
        }

        // The dots count up while any of the black is showing; unscaled time, so it also runs while the game is paused
        private void Update()
        {
            if (loadingText == null || blackGroup == null || blackGroup.alpha <= 0f) return;
            int dots = Mathf.FloorToInt(Time.unscaledTime / Mathf.Max(0.05f, dotStep)) % 4;
            string text = loadingLabel + new string('.', dots);
            if (loadingText.text != text) loadingText.text = text;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                IsLoading = false;
            }
        }
    }
}
