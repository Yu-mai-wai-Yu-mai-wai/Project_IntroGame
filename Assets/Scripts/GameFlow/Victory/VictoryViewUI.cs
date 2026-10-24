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

            if (cutsceneGroup != null && !Application.isBatchMode)
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
                titleText.text = "สู่สุขคติ";
                if (theme != null && theme.titleFont != null) titleText.font = theme.titleFont;
            }

            if (subtitleText != null)
            {
                subtitleText.text = "ชัยชนะอันสมบูรณ์เหนือวิญญาณร้าย";
                if (theme != null && theme.bodyFont != null) subtitleText.font = theme.bodyFont;
            }

            if (epilogueText != null)
            {
                epilogueText.text = DefaultEpilogue;
                if (theme != null && theme.bodyFont != null) epilogueText.font = theme.bodyFont;
            }

            if (statsText != null)
            {
                statsText.text = $"ขวัญคงเหลือ: {TawanOS.UI.KhwanDisplay.Text(khwan, maxKhwan)}   •   ธูปสะสม: {incense}   •   สำรับการ์ด: {deckCount} ใบ   •   เวลาที่ใช้: {RunState.FormatPlayTime(run != null ? run.PlaySeconds : 0f)}";
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
