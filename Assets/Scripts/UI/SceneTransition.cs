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
    /// fades back in once the scene is ready. An optional area title follows (<see cref="ShowTitle"/>): a big name of
    /// the place and a line saying what to do there, opening out from the centre of the screen, then fading away.
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

        [Header("Area title (seconds)")]
        [Tooltip("The title opening out from the centre to full size (ease out).")]
        public float titleOpen = 0.9f;
        [Tooltip("How long the title stays fully visible.")]
        public float titleHold = 1.1f;
        public float titleFade = 0.7f;
        [Tooltip("Width the title starts at, as a fraction of its full width, before it spreads to the sides.")]
        [Range(0.05f, 1f)] public float titleStartWidth = 0.25f;
        [Tooltip("Height the title starts at, as a fraction of its full height.")]
        [Range(0.05f, 1f)] public float titleStartHeight = 0.8f;
        public float titleSize = 150f;
        public float subtitleSize = 42f;
        public Color titleColor = new Color(0.95f, 0.92f, 0.87f);

        private static SceneTransition instance;

        private CanvasGroup blackGroup;
        private Image black;
        private RectTransform titleRoot;
        private CanvasGroup titleGroup;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI subtitleText;
        private Sequence titleSequence;

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
        /// Fades to black, loads <paramref name="sceneName"/>, fades back in, then shows the area title when
        /// <paramref name="title"/> is given. A request while another scene is loading is ignored.
        /// </summary>
        public static void Load(string sceneName, string title = null, string subtitle = null)
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
            Ensure().StartCoroutine(instance.LoadRoutine(sceneName, title, subtitle));
        }

        /// <summary>Shows the area title over the current scene, without a scene change.</summary>
        public static void ShowTitle(string title, string subtitle = null)
        {
            if (!Application.isPlaying || string.IsNullOrEmpty(title)) return;
            Ensure().PlayTitle(title, subtitle);
        }

        private IEnumerator LoadRoutine(string sceneName, string title, string subtitle)
        {
            IsLoading = true;
            KillTitle();

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
            if (!string.IsNullOrEmpty(title)) PlayTitle(title, subtitle);
        }

        private IEnumerator FadeIn()
        {
            yield return blackGroup.DOFade(0f, fadeFromBlack).SetEase(Ease.OutQuad).SetUpdate(true).WaitForCompletion();
            blackGroup.blocksRaycasts = false;
        }

        // The title opens out from the centre: it starts narrow and spreads to the sides while it grows to full size
        // (ease out), the subtitle follows a beat later, then both fade away
        private void PlayTitle(string title, string subtitle)
        {
            KillTitle();
            titleText.text = title;
            subtitleText.text = subtitle ?? string.Empty;
            subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));

            titleGroup.alpha = 1f;
            titleText.alpha = 0f;
            subtitleText.alpha = 0f;
            titleText.rectTransform.localScale = new Vector3(titleStartWidth, titleStartHeight, 1f);
            subtitleText.rectTransform.localScale = new Vector3(titleStartWidth, 1f, 1f);

            titleSequence = DOTween.Sequence().SetUpdate(true)
                .Append(titleText.rectTransform.DOScale(Vector3.one, titleOpen).SetEase(Ease.OutCubic))
                .Join(titleText.DOFade(1f, titleOpen * 0.6f).SetEase(Ease.OutQuad))
                .Insert(titleOpen * 0.3f, subtitleText.rectTransform.DOScale(Vector3.one, titleOpen).SetEase(Ease.OutCubic))
                .Insert(titleOpen * 0.3f, subtitleText.DOFade(1f, titleOpen * 0.6f).SetEase(Ease.OutQuad))
                .AppendInterval(titleHold)
                .Append(titleGroup.DOFade(0f, titleFade).SetEase(Ease.InQuad))
                .OnComplete(() => titleSequence = null);
        }

        private void KillTitle()
        {
            if (titleSequence != null) titleSequence.Kill();
            titleSequence = null;
            if (titleGroup != null) titleGroup.alpha = 0f;
        }

        private void Build()
        {
            var canvas = UiFactory.CreateOverlayCanvas("TransitionCanvas", SortingOrder, transform);
            // Clicks pass through except while the screen is black (the black's own group turns that on)
            canvas.GetComponent<CanvasGroup>().blocksRaycasts = true;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var theme = UIThemeSO.Current;

            // Area title (under the black, so a fade to black covers it)
            titleRoot = UiFactory.CreateRect("AreaTitle", canvas.transform);
            UiFactory.Stretch(titleRoot, 0f);
            titleGroup = titleRoot.gameObject.AddComponent<CanvasGroup>();
            titleGroup.alpha = 0f;
            titleGroup.blocksRaycasts = false;
            titleGroup.interactable = false;

            titleText = UiFactory.CreateText(titleRoot, "Title", string.Empty, titleSize, titleColor, TextAlignmentOptions.Center, theme.bodyFont);
            Place(titleText.rectTransform, new Vector2(0f, 40f), new Vector2(1800f, 260f));
            titleText.textWrappingMode = TextWrappingModes.NoWrap;
            subtitleText = UiFactory.CreateText(titleRoot, "Subtitle", string.Empty, subtitleSize, titleColor, TextAlignmentOptions.Center, theme.bodyFont);
            Place(subtitleText.rectTransform, new Vector2(0f, -110f), new Vector2(1600f, 70f));
            subtitleText.textWrappingMode = TextWrappingModes.NoWrap;

            // A soft dark edge keeps the white letters readable over a bright scene
            foreach (var t in new[] { titleText, subtitleText })
            {
                t.outlineWidth = 0.12f;
                t.outlineColor = new Color32(0, 0, 0, 160);
            }

            black = UiFactory.CreateImage("Black", canvas.transform, Color.black);
            UiFactory.Stretch(black.rectTransform, 0f);
            blackGroup = black.gameObject.AddComponent<CanvasGroup>();
            blackGroup.alpha = 0f;
            blackGroup.blocksRaycasts = false;
            black.raycastTarget = true;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void OnDestroy()
        {
            KillTitle();
            if (instance == this)
            {
                instance = null;
                IsLoading = false;
            }
        }
    }
}
