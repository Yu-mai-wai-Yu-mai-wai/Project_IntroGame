using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TawanOS.CardEngine;
using TawanOS.Settings;
using TawanOS.UI;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TawanOS.StoryEngine
{
    /// <summary>
    /// Visual-novel player for a <see cref="StoryDataSO"/>: the background (picture or looping video)
    /// cross-fades when a page changes it, the line types out in the text box. Click anywhere (or Space / Enter) to finish
    /// typing, again for the next page. Esc or the skip button ends the story. Raises
    /// <see cref="OnStoryFinished"/> after the last page.
    /// </summary>
    public class StoryPlayer : MonoBehaviour
    {
        /// <summary>Two stacked images; the hidden one fades in over the shown one.</summary>
        [Serializable]
        public class CrossFadeLayer
        {
            public Image a;
            public Image b;

            private Image front;
            private Image back;

            public void Init()
            {
                front = a;
                back = b;
                foreach (var img in new[] { a, b })
                {
                    if (img == null) continue;
                    img.color = new Color(1f, 1f, 1f, 0f);
                    img.preserveAspect = true;
                }
            }

            private bool visible;

            public bool IsShowing(Sprite sprite) => visible && front.sprite == sprite;

            public void Show(Sprite sprite, float seconds)
            {
                front.DOKill();
                back.DOKill();
                back.sprite = sprite;
                back.color = new Color(1f, 1f, 1f, 0f);
                back.transform.SetAsLastSibling();
                back.DOFade(1f, seconds);
                front.DOFade(0f, seconds);
                (front, back) = (back, front);
                visible = true;
            }

            public void Hide(float seconds)
            {
                visible = false;
                // Both layers: a quick click can leave the back one mid-fade too
                foreach (var img in new[] { a, b })
                {
                    img.DOKill();
                    img.DOFade(0f, seconds);
                }
            }
        }

        /// <summary>
        /// Two looping video players, each drawing into its own RenderTexture (sized to the clip) shown
        /// by a RawImage. The incoming video fades in only once it is prepared, so no stale frame flashes.
        /// </summary>
        [Serializable]
        public class VideoLayer
        {
            public RawImage a;
            public RawImage b;

            private RawImage front;
            private bool visible;

            public bool HasLayers => a != null && b != null;

            public void Init()
            {
                if (!HasLayers) return;
                front = a;
                foreach (var raw in new[] { a, b })
                {
                    raw.color = new Color(1f, 1f, 1f, 0f);
                    var vp = Player(raw);
                    vp.playOnAwake = false;
                    vp.isLooping = true;
                    vp.renderMode = VideoRenderMode.RenderTexture;
                    // Muted: story videos are visual loops; music and ambience belong to the audio system
                    vp.audioOutputMode = VideoAudioOutputMode.None;
                }
            }

            public void Release()
            {
                if (!HasLayers) return;
                foreach (var raw in new[] { a, b })
                {
                    if (raw.texture is RenderTexture rt) UnityEngine.Object.Destroy(rt);
                }
            }

            public bool IsShowing(VideoClip clip) => visible && Player(front).clip == clip;

            public IEnumerator Show(VideoClip clip, float seconds)
            {
                // A click during an unfinished Show leaves both layers mid-fade: reuse the fainter one
                var incoming = a.color.a <= b.color.a ? a : b;
                var outgoing = incoming == a ? b : a;
                front = incoming;
                visible = true;

                var vp = Player(incoming);
                incoming.DOKill();
                incoming.color = new Color(1f, 1f, 1f, 0f);
                vp.Stop();
                vp.clip = clip;
                vp.targetTexture = TextureFor(incoming, (int)clip.width, (int)clip.height);
                vp.Prepare();
                while (!vp.isPrepared) yield return null;
                vp.Play();

                var fitter = incoming.GetComponent<AspectRatioFitter>();
                if (fitter != null) fitter.aspectRatio = (float)clip.width / clip.height;

                incoming.transform.SetAsLastSibling();
                incoming.DOFade(1f, seconds);
                outgoing.DOKill();
                outgoing.DOFade(0f, seconds).OnComplete(() => Player(outgoing).Stop());
            }

            public void Hide(float seconds)
            {
                if (!visible || !HasLayers) return;
                visible = false;
                foreach (var raw in new[] { a, b })
                {
                    raw.DOKill();
                    raw.DOFade(0f, seconds).OnComplete(() => Player(raw).Stop());
                }
            }

            private static VideoPlayer Player(RawImage raw) => raw.GetComponent<VideoPlayer>();

            // Reuses the layer's texture when the next clip has the same size
            private static RenderTexture TextureFor(RawImage raw, int width, int height)
            {
                if (raw.texture is RenderTexture current)
                {
                    if (current.width == width && current.height == height) return current;
                    UnityEngine.Object.Destroy(current);
                }
                var created = new RenderTexture(Mathf.Max(1, width), Mathf.Max(1, height), 0);
                raw.texture = created;
                return created;
            }
        }

        public static StoryPlayer Instance { get; private set; }

        public event Action OnStoryFinished;

        /// <summary>
        /// A page asks for a tutorial fight (<see cref="StoryPage.tutorialFightEnemy"/>): that page, and the page to
        /// continue from afterwards. Without a listener (the story scene played on its own) the story just goes on.
        /// </summary>
        public event Action<StoryPage, int> OnTutorialFightRequested;

        public StoryDataSO CurrentStory => story;
        public int PageIndex => pageIndex;

        [Tooltip("Played when nothing calls Begin() before Start (the intro).")]
        public StoryDataSO defaultStory;

        [Header("Pictures")]
        public CrossFadeLayer background = new CrossFadeLayer();
        [Tooltip("Drawn above the background pictures; used by pages that set a background video.")]
        public VideoLayer backgroundVideo = new VideoLayer();
        [Min(0f)] public float fadeSeconds = 0.6f;

        [Header("Text")]
        public GameObject speakerPlate;
        public TextMeshProUGUI speakerText;
        public TextMeshProUGUI bodyText;
        public Graphic nextIndicator;
        [Min(1f)] public float charactersPerSecond = 40f;

        [Header("Input")]
        [Tooltip("Full-screen transparent button: clicking anywhere advances.")]
        public Button advanceButton;
        public Button skipButton;
        public CanvasGroup rootGroup;

        private StoryDataSO story;
        private int pageIndex = -1;
        private Coroutine typingRoutine;
        private Coroutine videoRoutine;
        private bool isTyping;
        private bool finished;

        private void Awake()
        {
            Instance = this;
            background.Init();
            backgroundVideo.Init();
            EnableThaiMarkPositioning(bodyText);
            EnableThaiMarkPositioning(speakerText);
            if (advanceButton != null) advanceButton.onClick.AddListener(Advance);
            if (skipButton != null) skipButton.onClick.AddListener(Skip);
            if (nextIndicator != null)
            {
                nextIndicator.gameObject.SetActive(false);
                nextIndicator.DOFade(0.2f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetLink(nextIndicator.gameObject);
            }
        }

        // The project's TMP default is 'kern' only. Without 'mark'/'mkmk' a tone mark above an upper vowel
        // is not lifted: in Sarabun the ่ of "ที่" lands on the stroke of ี and disappears.
        private static void EnableThaiMarkPositioning(TMP_Text text)
        {
            if (text == null) return;
            text.fontFeatures = new List<OTL_FeatureTag> { OTL_FeatureTag.kern, OTL_FeatureTag.mark, OTL_FeatureTag.mkmk };
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            backgroundVideo.Release();
        }

        private void Start()
        {
            if (story == null) Begin(defaultStory);
        }

        private void Update()
        {
            if (finished) return;
            if (askingSkip)
            {
                if (TawanOS.UI.EscapeKey.Use()) CloseSkipQuestion();
                return;
            }
            if (Time.frameCount <= skipQuestionClosedFrame) return; // the click / key that answered the question
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) Advance();
            else if (TawanOS.UI.EscapeKey.Use()) Skip();
        }

        /// <summary>Starts a story from its first page (or from <paramref name="startPage"/>, after a tutorial fight). An empty story finishes at once.</summary>
        public void Begin(StoryDataSO storyToPlay, int startPage = 0)
        {
            story = storyToPlay;
            pageIndex = -1;
            finished = false;

            if (rootGroup != null)
            {
                rootGroup.DOKill();
                rootGroup.alpha = 0f;
                rootGroup.interactable = true;
                rootGroup.DOFade(1f, 0.5f);
            }

            if (story == null || story.pages.Count == 0)
            {
                Debug.LogWarning("[StoryPlayer] No story to play - skipping.");
                Finish();
                return;
            }
            if (startPage >= story.pages.Count)
            {
                Finish();
                return;
            }
            ShowPage(Mathf.Max(0, startPage));
        }

        /// <summary>Click / Space: finish typing the current line, or go to the next page.</summary>
        public void Advance()
        {
            if (finished || askingSkip) return;
            if (isTyping)
            {
                CompleteTyping();
                return;
            }

            var page = story.pages[pageIndex];
            if (page.tutorialFightEnemy != null && OnTutorialFightRequested != null)
            {
                LeaveForFight(page, pageIndex + 1);
                return;
            }

            if (pageIndex + 1 < story.pages.Count) ShowPage(pageIndex + 1);
            else Finish();
        }

        private void ShowPage(int index)
        {
            pageIndex = index;
            var page = story.pages[index];

            ShowBackground(page);

            bool hasSpeaker = !string.IsNullOrEmpty(page.speaker);
            if (speakerPlate != null) speakerPlate.SetActive(hasSpeaker);
            if (speakerText != null) speakerText.text = hasSpeaker ? page.speaker : string.Empty;

            if (typingRoutine != null) StopCoroutine(typingRoutine);
            typingRoutine = StartCoroutine(TypeText(page.text ?? string.Empty));
        }

        private void ShowBackground(StoryPage page)
        {
            if (page.backgroundVideo != null && !backgroundVideo.HasLayers)
            {
                Debug.LogWarning("[StoryPlayer] Page has a background video but the scene has no video layer - re-run Tools > TawanOS > Story > Setup Story Scene & Intro.");
            }

            if (page.blackScreen)
            {
                StopVideoRoutine();
                backgroundVideo.Hide(fadeSeconds);
                background.Hide(fadeSeconds);
            }
            else if (page.backgroundVideo != null && backgroundVideo.HasLayers)
            {
                if (!backgroundVideo.IsShowing(page.backgroundVideo))
                {
                    StopVideoRoutine();
                    videoRoutine = StartCoroutine(backgroundVideo.Show(page.backgroundVideo, fadeSeconds));
                }
                background.Hide(fadeSeconds);
            }
            else if (page.background != null)
            {
                StopVideoRoutine();
                backgroundVideo.Hide(fadeSeconds);
                if (!background.IsShowing(page.background)) background.Show(page.background, fadeSeconds);
            }
            // Neither set: keep whatever the previous page showed
        }

        private void StopVideoRoutine()
        {
            if (videoRoutine != null) StopCoroutine(videoRoutine);
            videoRoutine = null;
        }

        private IEnumerator TypeText(string text)
        {
            isTyping = true;
            if (nextIndicator != null) nextIndicator.gameObject.SetActive(false);

            bodyText.text = text;
            bodyText.maxVisibleCharacters = 0;
            bodyText.ForceMeshUpdate();
            int total = bodyText.textInfo.characterCount;

            float shown = 0f;
            while (shown < total)
            {
                shown += GameSettings.CharactersPerSecond(charactersPerSecond) * Time.deltaTime;
                bodyText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
                yield return null;
            }
            FinishTyping();
        }

        private void CompleteTyping()
        {
            if (typingRoutine != null) StopCoroutine(typingRoutine);
            FinishTyping();
        }

        private void FinishTyping()
        {
            typingRoutine = null;
            isTyping = false;
            bodyText.maxVisibleCharacters = int.MaxValue;
            if (nextIndicator != null) nextIndicator.gameObject.SetActive(true);
        }

        // ---------------------------------------------------------------- skipping

        // "Skip the tutorial too?" is up (built in code on first use, over the story's own canvas)
        private bool askingSkip;
        private int skipQuestionClosedFrame = -1;
        private GameObject skipQuestion;
        private int fightPageAhead = -1;

        public bool IsAskingSkip => askingSkip;

        /// <summary>
        /// Skip button / Esc. With a tutorial fight still ahead, asks whether to skip the tutorial as well
        /// (<see cref="AnswerSkip"/>); otherwise the story ends and the game starts.
        /// </summary>
        public void Skip()
        {
            if (finished || story == null || askingSkip) return;

            fightPageAhead = -1;
            if (OnTutorialFightRequested != null)
                fightPageAhead = story.pages.FindIndex(Mathf.Max(0, pageIndex), p => p.tutorialFightEnemy != null);

            if (fightPageAhead < 0)
            {
                Finish();
                return;
            }

            BuildSkipQuestion();
            askingSkip = true;
            skipQuestion.SetActive(true);
        }

        /// <summary>true = skip the tutorial too and start the game; false = skip the text and play the tutorial.</summary>
        public void AnswerSkip(bool skipTutorial)
        {
            if (!askingSkip) return;
            CloseSkipQuestion();
            if (typingRoutine != null) StopCoroutine(typingRoutine);
            isTyping = false;

            if (skipTutorial) Finish();
            else LeaveForFight(story.pages[fightPageAhead], fightPageAhead + 1);
        }

        private void CloseSkipQuestion()
        {
            askingSkip = false;
            skipQuestionClosedFrame = Time.frameCount;
            if (skipQuestion != null) skipQuestion.SetActive(false);
        }

        private void BuildSkipQuestion()
        {
            if (skipQuestion != null) return;
            var theme = UIThemeSO.Current;

            var canvas = UiFactory.CreateOverlayCanvas("SkipQuestionCanvas", 600, transform);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var group = canvas.GetComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            group.interactable = true;
            skipQuestion = canvas.gameObject;

            var dim = UiFactory.CreateImage("Dim", canvas.transform, new Color(0f, 0f, 0f, 0.7f));
            dim.raycastTarget = true; // the click-anywhere-to-advance button behind it is not reached
            UiFactory.Stretch(dim.rectTransform, 0f);

            var panel = UiFactory.CreateImage("Panel", canvas.transform, theme.panel);
            panel.raycastTarget = true;
            var rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(720f, 0f);
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = theme.accent;
            outline.effectDistance = new Vector2(2f, -2f);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 26, 26);
            layout.spacing = 16f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            UiFactory.CreateText(rt, "Title", "ต้องการข้ามบทสอนเล่นด้วยไหม?", theme.titleSize, theme.accent, TextAlignmentOptions.Center,
                theme.titleFont != null ? theme.titleFont : theme.bodyFont);
            var body = UiFactory.CreateText(rt, "Body",
                "ข้ามบทสอน: เริ่มการเดินทางบนแผนที่ทันที\nเล่นบทสอน: ข้ามเนื้อเรื่องไปสู้ในฝัน แล้วค่อยเล่าเรื่องต่อ",
                26f, theme.text, TextAlignmentOptions.Center, theme.bodyFont);
            body.lineSpacing = 12f;

            var row = UiFactory.CreateRect("Buttons", rt);
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 20f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            SkipButton(row, "ยกเลิก (Esc)", theme.black, CloseSkipQuestion);
            SkipButton(row, "เล่นบทสอน", theme.crimson, () => AnswerSkip(false));
            SkipButton(row, "ข้ามบทสอน", theme.crimson, () => AnswerSkip(true));

            skipQuestion.SetActive(false);
        }

        private static void SkipButton(Transform parent, string label, Color fill, Action onClick)
        {
            var theme = UIThemeSO.Current;
            var image = UiFactory.CreateImage(label, parent, fill);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());
            var size = image.gameObject.AddComponent<LayoutElement>();
            size.minHeight = 56f;
            size.minWidth = 190f;
            var text = UiFactory.CreateText(image.transform, "Label", label, 24f, theme.text, TextAlignmentOptions.Center, theme.bodyFont);
            UiFactory.Stretch(text.rectTransform, 8f);
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = theme.accent;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        // Fades out like the end of the story, then hands over to the fight; GameFlowManager brings the story back
        private void LeaveForFight(StoryPage page, int resumePage)
        {
            finished = true;
            if (rootGroup == null)
            {
                OnTutorialFightRequested?.Invoke(page, resumePage);
                return;
            }
            rootGroup.interactable = false;
            rootGroup.DOKill();
            rootGroup.DOFade(0f, 0.5f).OnComplete(() => OnTutorialFightRequested?.Invoke(page, resumePage));
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            if (typingRoutine != null) StopCoroutine(typingRoutine);
            isTyping = false;

            if (rootGroup == null)
            {
                OnStoryFinished?.Invoke();
                return;
            }
            rootGroup.interactable = false;
            rootGroup.DOKill();
            rootGroup.DOFade(0f, 0.5f).OnComplete(() => OnStoryFinished?.Invoke());
        }
    }
}
