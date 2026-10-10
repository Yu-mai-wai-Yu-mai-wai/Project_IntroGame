using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.UI
{
    /// <summary>
    /// Scene change with a fade: the screen fades to black, the next scene loads while it is black, and the screen
    /// fades back in once the scene is ready. One instance lives across scenes; it runs on unscaled time, so a paused
    /// game (timeScale 0) still transitions.
    /// </summary>
    public class SceneTransition : MonoBehaviour
    {
        private const int SortingOrder = 1000; // above every in-game canvas, the card book (500) included

        [Header("Fade (seconds)")]
        public float fadeToBlack = 0.45f;
        public float fadeFromBlack = 0.6f;
        [Tooltip("Shortest time the screen stays black, so a scene that loads at once still reads as a cut.")]
        public float minBlack = 0.25f;

        private static SceneTransition instance;

        private CanvasGroup blackGroup;

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

            IsLoading = false;
            yield return FadeIn();
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
