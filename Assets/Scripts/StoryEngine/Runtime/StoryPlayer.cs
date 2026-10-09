using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TawanOS.CardEngine;
using TawanOS.Settings;
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
        /// A page asks for a tutorial fight (<see cref="StoryPage.tutorialFightEnemy"/>): the enemy, and the page to
        /// continue from afterwards. Without a listener (the story scene played on its own) the story just goes on.
        /// </summary>
        public event Action<EnemyProfileSO, int> OnTutorialFightRequested;

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
            if (skipButton != null) skipButton.onClick.AddListener(Finish);
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
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) Advance();
            else if (TawanOS.UI.EscapeKey.Use()) Finish();
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
            if (finished) return;
            if (isTyping)
            {
                CompleteTyping();
                return;
            }

            var fightEnemy = story.pages[pageIndex].tutorialFightEnemy;
            if (fightEnemy != null && OnTutorialFightRequested != null)
            {
                LeaveForFight(fightEnemy, pageIndex + 1);
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

        // Fades out like the end of the story, then hands over to the fight; GameFlowManager brings the story back
        private void LeaveForFight(EnemyProfileSO enemy, int resumePage)
        {
            finished = true;
            if (rootGroup == null)
            {
                OnTutorialFightRequested?.Invoke(enemy, resumePage);
                return;
            }
            rootGroup.interactable = false;
            rootGroup.DOKill();
            rootGroup.DOFade(0f, 0.5f).OnComplete(() => OnTutorialFightRequested?.Invoke(enemy, resumePage));
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
