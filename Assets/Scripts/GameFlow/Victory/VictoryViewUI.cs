using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using TawanOS.UI;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Victory Screen UI (Plan Task A3, P1-8): Displays dad_khwan cutscene ("ขวัญกลับคืนร่าง")
    /// and run summary when the player defeats the Boss. Clears save data and allows returning to Main Menu.
    /// Uses UIThemeSO colors and typography (WCAG AA compliant, authentic Thai text, zero emojis).
    /// </summary>
    public class VictoryViewUI : MonoBehaviour
    {
        [Header("UI Elements")]
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI subtitleText;
        public TextMeshProUGUI epilogueText;
        public TextMeshProUGUI statsText;
        public Button mainMenuButton;
        public TextMeshProUGUI mainMenuButtonLabel;
        public CanvasGroup contentGroup;

        [Header("Background")]
        [Tooltip("Forest picture behind the whole screen; the plain dark colour stays when empty.")]
        public Sprite forestBackground;

        [Header("Cutscene Sequence (P1-8)")]
        public CanvasGroup cutsceneGroup;
        public RawImage cutsceneVideoImage;
        public VideoPlayer cutsceneVideoPlayer;
        public TextMeshProUGUI cutsceneCaptionText;
        [Tooltip("Play the dad_khwan cutscene first. Off: the ending story (StoryScene) already told the end (Game Designer, 25 Oct).")]
        public bool playCutscene;

        [Header("Black Ending (Game Designer, 25 Oct)")]
        [Tooltip("On: a black screen, the ขวัญเอ้ย ขวัญมา logo fades in and out, then จบเกม / ขอบคุณที่เล่น, the stats and " +
                 "a plain กลับสู่หน้าหลัก in the middle, the thank-you picture small in the lower right. Off: the old panel.")]
        public bool blackEnding = true;
        [Tooltip("The ขวัญเอ้ย ขวัญมา logo (Art/Logo/KhwanLogo, set by the Victory setup tool).")]
        public Sprite logo;
        [Tooltip("Empty loads Resources/Victory/ThankYou.")]
        public Sprite thankYouPicture;
        public float logoFadeSeconds = 1.2f, logoHoldSeconds = 1.6f, endFadeSeconds = 1.2f;

        private bool isCutscenePlaying;
        private Coroutine cutsceneCoroutine;
        private RenderTexture videoRt;

        private const string DefaultEpilogue =
            "เจ้าได้สะกดวิญญาณร้ายแห่งป่าช้า และรวบรวมขวัญที่กระเจิดกระเจิงกลับคืนสู่ร่างได้สำเร็จ\n" +
            "ควันธูปจางหาย สายหมอกมืดมิดเริ่มคลี่คลาย การเดินทางในคืนอันยาวนานสิ้นสุดลงแล้ว...";

        private void Awake()
        {
            ApplyForestBackground();

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.AddListener(OnMainMenuClicked);
                TawanOS.VFX.UiTalismanButton.Dress(mainMenuButton); // an old paper ยันต์ (Game Designer, 24 Oct)
            }
        }

        // The scene's full-screen "Background" image shows the forest picture instead of a flat colour; the red
        // "AuraGlow" band above it is kept so the text panel still reads against the picture
        private void ApplyForestBackground()
        {
            if (forestBackground == null) return;
            var bg = transform.Find("Background");
            var image = bg != null ? bg.GetComponent<Image>() : null;
            if (image == null) return;

            image.sprite = forestBackground;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
        }

        private void Start()
        {
            DisplaySummary();

            if (blackEnding)
            {
                BuildBlackEnding();
                endingCoroutine = StartCoroutine(PlayBlackEnding());
            }
            else if (playCutscene && cutsceneGroup != null && !Application.isBatchMode)
            {
                cutsceneCoroutine = StartCoroutine(PlayVictorySequence());
            }
            else
            {
                ShowContentDirectly();
            }
        }

        private void Update()
        {
            if (isCutscenePlaying && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
            {
                SkipCutscene();
            }
            if (endingCoroutine != null && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
            {
                StopCoroutine(endingCoroutine);
                endingCoroutine = null;
                ShowEndText(0.4f);
            }
        }

        private void ShowContentDirectly()
        {
            if (cutsceneGroup != null)
            {
                cutsceneGroup.alpha = 0f;
                cutsceneGroup.blocksRaycasts = false;
            }
            if (contentGroup != null)
            {
                contentGroup.alpha = 0f;
                contentGroup.DOFade(1f, 1.2f).SetEase(Ease.OutCubic);
            }
        }

        private IEnumerator PlayVictorySequence()
        {
            isCutscenePlaying = true;
            if (contentGroup != null) contentGroup.alpha = 0f;
            if (cutsceneGroup != null) cutsceneGroup.alpha = 1f;

            if (cutsceneCaptionText != null)
            {
                cutsceneCaptionText.text = "ขวัญกลับคืนร่าง";
                var theme = UIThemeSO.Current;
                if (theme != null && theme.titleFont != null) cutsceneCaptionText.font = theme.titleFont;
            }

            if (cutsceneVideoPlayer != null && cutsceneVideoPlayer.clip != null)
            {
                if (videoRt == null)
                {
                    videoRt = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
                    videoRt.Create();
                }
                cutsceneVideoPlayer.targetTexture = videoRt;
                if (cutsceneVideoImage != null) cutsceneVideoImage.texture = videoRt;

                cutsceneVideoPlayer.Play();

                // Wait until prepared or duration elapsed
                float elapsed = 0f;
                float maxWait = (float)cutsceneVideoPlayer.clip.length;
                if (maxWait <= 0f) maxWait = 6f;

                while (elapsed < maxWait && isCutscenePlaying)
                {
                    yield return null;
                    elapsed += Time.deltaTime;
                    if (cutsceneVideoPlayer.isPlaying && (ulong)cutsceneVideoPlayer.frame >= cutsceneVideoPlayer.frameCount - 1)
                        break;
                }
            }
            else
            {
                // Brief title banner beat if no video clip assigned
                yield return new WaitForSeconds(2.5f);
            }

            FinishCutsceneTransition();
        }

        private void SkipCutscene()
        {
            if (!isCutscenePlaying) return;
            if (cutsceneCoroutine != null) StopCoroutine(cutsceneCoroutine);
            FinishCutsceneTransition();
        }

        private void FinishCutsceneTransition()
        {
            isCutscenePlaying = false;
            if (cutsceneVideoPlayer != null && cutsceneVideoPlayer.isPlaying)
            {
                cutsceneVideoPlayer.Stop();
            }

            if (cutsceneGroup != null)
            {
                cutsceneGroup.DOFade(0f, 0.6f).OnComplete(() =>
                {
                    cutsceneGroup.blocksRaycasts = false;
                });
            }

            if (contentGroup != null)
            {
                contentGroup.DOFade(1f, 1.0f).SetEase(Ease.OutCubic);
            }
        }

        private void OnDestroy()
        {
            if (videoRt != null)
            {
                if (cutsceneVideoPlayer != null) cutsceneVideoPlayer.targetTexture = null;
                if (cutsceneVideoImage != null) cutsceneVideoImage.texture = null;
                videoRt.Release();
                Destroy(videoRt);
                videoRt = null;
            }
        }

        // ---- the black ending ----

        private Coroutine endingCoroutine;
        private CanvasGroup logoGroup, endGroup;

        // Over everything in this canvas: black, the logo, the end text with the stats and a plain button, the picture
        private void BuildBlackEnding()
        {
            var theme = UIThemeSO.Current;
            if (contentGroup != null) contentGroup.gameObject.SetActive(false);
            if (cutsceneGroup != null) cutsceneGroup.gameObject.SetActive(false);

            var black = UiFactory.CreateImage("BlackEnding", transform, Color.black);
            UiFactory.Stretch(black.rectTransform, 0f);
            var root = black.transform;

            var logoSprite = logo;
            var logoImage = UiFactory.CreateImage("Logo", root, Color.white);
            logoImage.sprite = logoSprite;
            logoImage.preserveAspect = true;
            logoImage.raycastTarget = false;
            var lr = logoImage.rectTransform;
            lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 0.5f);
            lr.sizeDelta = new Vector2(640f, 720f);
            logoGroup = logoImage.gameObject.AddComponent<CanvasGroup>();
            logoGroup.alpha = 0f;

            var end = UiFactory.CreateRect("EndText", root);
            UiFactory.Stretch(end, 0f);
            endGroup = end.gameObject.AddComponent<CanvasGroup>();
            endGroup.alpha = 0f;
            endGroup.interactable = endGroup.blocksRaycasts = false;

            var title = UiFactory.CreateText(end, "Title", "จบเกม", 110f, theme.accent, TextAlignmentOptions.Center, theme.titleFont);
            Place(title.rectTransform, 0.7f, 150f);
            var thanks = UiFactory.CreateText(end, "Thanks", "ขอบคุณที่เล่น", 44f, Color.white, TextAlignmentOptions.Center, theme.bodyFont);
            Place(thanks.rectTransform, 0.595f, 70f);
            var stats = UiFactory.CreateText(end, "Stats", statsText != null ? statsText.text : "", 28f, theme.text, TextAlignmentOptions.Center, theme.bodyFont);
            stats.lineSpacing = 18f;
            Place(stats.rectTransform, 0.43f, 220f);

            // The same plain text button as the defeat screen, without the shake
            var button = UiFactory.CreateButton(end, "MainMenu", "กลับสู่หน้าหลัก", theme, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var br = (RectTransform)button.transform;
            br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 0.5f);
            br.anchoredPosition = new Vector2(0f, -0.3f * 1080f);
            br.sizeDelta = new Vector2(520f, 90f);
            var image = button.GetComponent<Image>();
            if (image != null) image.color = new Color(0f, 0f, 0f, 0f); // still takes the click
            var label = button.GetComponentInChildren<TMP_Text>();
            label.font = theme.titleFont != null ? theme.titleFont : label.font;
            label.fontSize = theme.titleSize * 1.1f;
            label.color = Color.white;
            label.outlineWidth = 0.2f;
            label.outlineColor = new Color32(0, 0, 0, 255);
            button.targetGraphic = label;
            var colors = button.colors;
            colors.normalColor = theme.text;
            colors.highlightedColor = theme.accent;
            colors.selectedColor = theme.text;
            colors.pressedColor = Color.Lerp(theme.accent, Color.white, 0.4f);
            button.colors = colors;
            button.onClick.AddListener(OnMainMenuClicked);
            mainMenuButton = button;

            var thanksSprite = thankYouPicture != null ? thankYouPicture : Resources.Load<Sprite>("Victory/ThankYou");
            if (thanksSprite != null)
            {
                var pic = UiFactory.CreateImage("ThankYou", end, Color.white);
                pic.sprite = thanksSprite;
                pic.preserveAspect = true;
                pic.raycastTarget = false;
                var pr = pic.rectTransform;
                pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(1f, 0f);
                pr.anchoredPosition = new Vector2(-30f, 24f);
                pr.sizeDelta = new Vector2(260f, 260f);
            }
        }

        // Centred across the screen at a height (0 = bottom, 1 = top), in the 1920 x 1080 layout
        private static void Place(RectTransform rt, float y, float height)
        {
            rt.anchorMin = new Vector2(0.1f, y);
            rt.anchorMax = new Vector2(0.9f, y);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, height);
        }

        private IEnumerator PlayBlackEnding()
        {
            yield return new WaitForSeconds(0.8f);
            yield return logoGroup.DOFade(1f, logoFadeSeconds).WaitForCompletion();
            yield return new WaitForSeconds(logoHoldSeconds);
            yield return logoGroup.DOFade(0f, logoFadeSeconds).WaitForCompletion();
            yield return new WaitForSeconds(0.4f);
            endingCoroutine = null;
            ShowEndText(endFadeSeconds);
        }

        private void ShowEndText(float seconds)
        {
            logoGroup.DOKill();
            logoGroup.alpha = 0f;
            endGroup.DOKill();
            endGroup.DOFade(1f, seconds).SetEase(Ease.OutCubic);
            endGroup.interactable = endGroup.blocksRaycasts = true;
        }

        public void DisplaySummary()
        {
            var theme = UIThemeSO.Current;
            var run = RunState.Current;

            int khwan = run != null && run.CurrentHp > 0 ? run.CurrentHp : 50;
            int maxKhwan = run != null && run.MaxHp > 0 ? run.MaxHp : 50;
            int incense = run != null ? run.Incense : 0;
            int deckCount = run != null && run.Deck != null ? run.Deck.Count : 0;

            if (titleText != null)
            {
                titleText.text = "จบเกม";
                if (theme != null && theme.titleFont != null) titleText.font = theme.titleFont;
            }

            if (subtitleText != null)
            {
                subtitleText.text = "ขอบคุณที่เล่น";
                if (theme != null && theme.bodyFont != null) subtitleText.font = theme.bodyFont;
            }

            if (epilogueText != null)
            {
                epilogueText.text = DefaultEpilogue;
                if (theme != null && theme.bodyFont != null) epilogueText.font = theme.bodyFont;
            }

            if (statsText != null)
            {
                statsText.text = $"ขวัญคงเหลือ: {TawanOS.UI.KhwanDisplay.Text(khwan, maxKhwan)}\nธูปสะสม: {incense}\nสำรับการ์ด: {deckCount} ใบ\nเวลาที่ใช้:{RunState.FormatPlayTime(run != null ? run.PlaySeconds : 0f)}";
                if (theme != null && theme.bodyFont != null) statsText.font = theme.bodyFont;
                PlaytestCaptureBanner.Add(statsText.canvas.rootCanvas.transform);
            }

            if (mainMenuButtonLabel != null)
            {
                mainMenuButtonLabel.text = "กลับสู่หน้าจอหลัก";
                if (theme != null && theme.bodyFont != null) mainMenuButtonLabel.font = theme.bodyFont;
            }
        }

        public void OnMainMenuClicked()
        {
            if (mainMenuButton != null) mainMenuButton.interactable = false;

            if (Application.CanStreamedLevelBeLoaded(GameFlowManager.MainMenuSceneName))
            {
                SceneTransition.Load(GameFlowManager.MainMenuSceneName);
            }
            else
            {
                Debug.LogWarning("[VictoryViewUI] MainMenu scene not found in Build Settings.");
            }
        }
    }
}
