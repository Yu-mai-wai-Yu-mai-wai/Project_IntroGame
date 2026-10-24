using System.Collections;
using System.IO;
using TawanOS.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TawanOS.GameFlow
{
    /// <summary>What the Game Over page tells the player about the run that just ended.</summary>
    public struct GameOverSummary
    {
        public string enemyName;
        public int incense;
        public int deckSize;
        public int totalFloors;
        public int maxHp;
        public float playSeconds;

        public string ToText()
        {
            string by = string.IsNullOrEmpty(enemyName) ? "ศัตรู" : enemyName;
            return $"พ่ายแพ้ต่อ {by}\n\nธูปที่สะสม {incense}   •   สำรับ {deckSize} ใบ\nขวัญสูงสุด {maxHp}   •   เวลาที่ใช้ {RunState.FormatPlayTime(playSeconds)}";
        }
    }

    /// <summary>
    /// Game Over page (plan B3, P0-2):
    /// 1. Defeat sequence: character art (IMG_7920) + 3 lines of dialogue ([ชั่วคราว]).
    /// 2. Fade to black with full-screen procedural blood overlay.
    /// 3. Stats panel (Title RueangLao, summary + buttons EkkamaiVibe) with Restart and Main Menu.
    /// </summary>
    public class GameOverScreen : MonoBehaviour
    {
        public static GameOverScreen Instance { get; private set; }
        public static bool IsShowing => Instance != null;

        public static readonly string[] DefeatDialogueLines = new string[]
        {
            "[ชั่วคราว] เปลวเทียนมอดดับ... ความมืดมิดกลืนกินสติสัมปชัญญะ",
            "[ชั่วคราว] เสียงเพรียกจากความตายดังก้องในโสตประสาท",
            "[ชั่วคราว] ขวัญของเจ้า... หลุดลอยไปเสียแล้ว"
        };

        public Button restartButton;
        public Button menuButton;
        public TextMeshProUGUI summaryText;
        public TextMeshProUGUI titleText;
        public Image characterImage;
        public TextMeshProUGUI dialogueText;
        public Image bloodOverlay;
        public CanvasGroup cutsceneGroup;
        public CanvasGroup statsGroup;

        private int totalFloors;
        private bool skipDialogueRequested;
        private Coroutine sequenceCoroutine;

        private static Sprite characterArtSprite;
        private static Sprite bloodOverlaySprite;

        public static Sprite LoadCharacterArt()
        {
            if (characterArtSprite != null) return characterArtSprite;

#if UNITY_EDITOR
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Characters/IMG_7920.JPG");
            if (tex != null)
            {
                characterArtSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                return characterArtSprite;
            }
#endif

            string fullPath = Path.Combine(Application.dataPath, "Art/Characters/IMG_7920.JPG");
            if (File.Exists(fullPath))
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                var runtimeTex = new Texture2D(2, 2);
                if (runtimeTex.LoadImage(bytes))
                {
                    characterArtSprite = Sprite.Create(runtimeTex, new Rect(0, 0, runtimeTex.width, runtimeTex.height), new Vector2(0.5f, 0.5f));
                    return characterArtSprite;
                }
            }

            var fallback = Texture2D.whiteTexture;
            characterArtSprite = Sprite.Create(fallback, new Rect(0, 0, fallback.width, fallback.height), new Vector2(0.5f, 0.5f));
            return characterArtSprite;
        }

        public static Sprite GetBloodOverlaySprite()
        {
            if (bloodOverlaySprite != null) return bloodOverlaySprite;

            int width = 256;
            int height = 256;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] colors = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);

                    float dx = (u - 0.5f) * 2f;
                    float dy = (v - 0.5f) * 2f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float wobble = Mathf.Sin(u * 20f) * 0.08f + Mathf.Cos(v * 20f) * 0.08f;
                    float drip = (1f - v) * (Mathf.Sin(u * 30f) > 0.35f ? 0.22f : 0f);

                    float totalDist = dist + wobble + drip;
                    float alpha = Mathf.Clamp01((totalDist - 0.35f) / 0.65f);
                    alpha = Mathf.Pow(alpha, 1.8f);

                    colors[y * width + x] = new Color(0.6f, 0.03f, 0.03f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            bloodOverlaySprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
            return bloodOverlaySprite;
        }

        public static GameOverScreen Show(GameOverSummary summary)
        {
            if (Instance != null)
            {
                if (Application.isPlaying) Destroy(Instance.gameObject);
                else DestroyImmediate(Instance.gameObject);
            }

            var theme = UIThemeSO.Current;
            var canvas = UiFactory.CreateOverlayCanvas("GameOverCanvas", 600);
            var group = canvas.GetComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            group.interactable = true;
            canvas.gameObject.AddComponent<GraphicRaycaster>();

            var screen = canvas.gameObject.AddComponent<GameOverScreen>();
            screen.totalFloors = summary.totalFloors;
            Instance = screen;

            // 1. Full-screen dark background
            var dim = UiFactory.CreateImage("Dim", canvas.transform, theme.black);
            dim.raycastTarget = true;
            UiFactory.Stretch(dim.rectTransform, 0f);

            // 1b. The defeat picture behind everything (Game Designer, 25 Oct), rage lines boiling round the head, a vignette
            var artSo = GameOverArtSO.Load();
            if (artSo != null && artSo.defeatArt != null) screen.BuildDefeatArt(canvas.transform, artSo.defeatArt);

            // 2. Cutscene group (Character art + dialogue)
            var cutsceneGo = new GameObject("CutsceneGroup", typeof(RectTransform), typeof(CanvasGroup));
            cutsceneGo.transform.SetParent(canvas.transform, false);
            UiFactory.Stretch(cutsceneGo.GetComponent<RectTransform>(), 0f);
            screen.cutsceneGroup = cutsceneGo.GetComponent<CanvasGroup>();
            // The cutscene is look-only: it must never catch the clicks meant for the buttons
            screen.cutsceneGroup.blocksRaycasts = false;
            screen.cutsceneGroup.interactable = false;

            // Character art removed for now (Tawan, 15 Oct): the cutscene is the dialogue only

            // Dialogue Panel
            var dialoguePanelGo = new GameObject("DialoguePanel", typeof(RectTransform), typeof(Image));
            dialoguePanelGo.transform.SetParent(cutsceneGo.transform, false);
            var diagRect = dialoguePanelGo.GetComponent<RectTransform>();
            Place(diagRect, 0.15f, 0.85f, 0.08f, 0.26f);
            dialoguePanelGo.GetComponent<Image>().color = new Color(0.12f, 0.09f, 0.09f, 0.85f);

            screen.dialogueText = UiFactory.CreateText(dialoguePanelGo.transform, "DialogueText", "",
                theme.bodySize * 1.15f, theme.accent, TextAlignmentOptions.Center, theme.bodyFont);
            UiFactory.Stretch(screen.dialogueText.rectTransform, 16f);

            // 3. Full-screen Red Blood Overlay
            var bloodGo = new GameObject("BloodOverlay", typeof(RectTransform), typeof(Image));
            bloodGo.transform.SetParent(canvas.transform, false);
            UiFactory.Stretch(bloodGo.GetComponent<RectTransform>(), 0f);
            screen.bloodOverlay = bloodGo.GetComponent<Image>();
            screen.bloodOverlay.sprite = GetBloodOverlaySprite();
            screen.bloodOverlay.color = new Color(0.6f, 0.04f, 0.04f, 0f);
            screen.bloodOverlay.raycastTarget = false;

            // 4. Stats Group (Title, summary, buttons)
            var statsGo = new GameObject("StatsGroup", typeof(RectTransform), typeof(CanvasGroup));
            statsGo.transform.SetParent(canvas.transform, false);
            UiFactory.Stretch(statsGo.GetComponent<RectTransform>(), 0f);
            screen.statsGroup = statsGo.GetComponent<CanvasGroup>();

            // Title (Font: RueangLao)
            screen.titleText = UiFactory.CreateText(statsGo.transform, "Title", "ขวัญหลุด",
                theme.titleSize * 2f, theme.accent, TextAlignmentOptions.Center,
                theme.titleFont != null ? theme.titleFont : theme.bodyFont);
            Place(screen.titleText.rectTransform, 0.2f, 0.8f, 0.68f, 0.86f);

            // Summary text (Font: EkkamaiVibe)
            screen.summaryText = UiFactory.CreateText(statsGo.transform, "Summary", summary.ToText(),
                theme.bodySize * 1.2f, theme.text, TextAlignmentOptions.Center, theme.bodyFont);
            Place(screen.summaryText.rectTransform, 0.25f, 0.75f, 0.42f, 0.64f);
            PlaytestCaptureBanner.Add(statsGo.transform); // appears with the stats

            // Buttons (Font: EkkamaiVibe)
            screen.restartButton = UiFactory.CreateButton(statsGo.transform, "RestartButton", "เริ่มใหม่", theme,
                new Vector2(0.30f, 0.24f), new Vector2(0.48f, 0.34f));
            screen.menuButton = UiFactory.CreateButton(statsGo.transform, "MainMenuButton", "เมนูหลัก", theme,
                new Vector2(0.52f, 0.24f), new Vector2(0.70f, 0.34f));
            // Old paper ยันต์ (Game Designer, 24 Oct)
            // Plain words, not ยันต์ (Game Designer, 25 Oct); เริ่มใหม่ trembles
            PlainTextButton(screen.restartButton, theme);
            PlainTextButton(screen.menuButton, theme);
            screen.restartButton.GetComponentInChildren<TMP_Text>().gameObject.AddComponent<TawanOS.VFX.ShakyLabel>();
            screen.restartButton.onClick.AddListener(screen.Restart);
            screen.menuButton.onClick.AddListener(screen.GoToMainMenu);

            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            if (Application.isPlaying)
            {
                screen.statsGroup.alpha = 0f;
                screen.statsGroup.interactable = false;
                screen.statsGroup.blocksRaycasts = false;
                screen.sequenceCoroutine = screen.StartCoroutine(screen.PlayDefeatSequence(summary));
            }
            else
            {
                // In EditMode, reveal everything immediately for tests
                screen.CompleteCutsceneImmediately();
            }

            return screen;
        }

        private IEnumerator PlayDefeatSequence(GameOverSummary summary)
        {
            // --- Part 1: the defeat dialogue is no longer played (Game Designer, 25 Oct: straight to "ขวัญหลุด").
            // Its panel and lines stay built, hidden, for whoever writes a new one.
            cutsceneGroup.alpha = 0f;
            if (artGroup != null) artGroup.DOFade(1f, 0.6f).SetUpdate(true);

            // --- Part 2: Fade to black + red blood overlay (light over the picture, so it shows through) ---
            bloodOverlay.DOColor(BloodColor, 0.8f);
            yield return new WaitForSeconds(0.8f);

            // --- Part 3: Show Stats Panel & Buttons ---
            cutsceneGroup.blocksRaycasts = false;
            statsGroup.transform.SetAsLastSibling();
            statsGroup.DOFade(1f, 0.6f).SetUpdate(true);
            statsGroup.interactable = true;
            statsGroup.blocksRaycasts = true;

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null); // a selected button ignores the pointer's highlight
        }

        // With the defeat picture the old blood overlay is gone (Game Designer, 25 Oct: the picture alone)
        private Color BloodColor => new Color(0.6f, 0.04f, 0.04f, artGroup != null ? 0f : 0.95f);

        private CanvasGroup artGroup;

        // No box: just the word, which brightens under the pointer
        private static void PlainTextButton(Button button, UIThemeSO theme)
        {
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
        }

        // The picture fills the screen (cropped, never stretched); the lines sit on it, so they keep to the head
        private void BuildDefeatArt(Transform parent, Sprite sprite)
        {
            var root = new GameObject("DefeatArt", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            UiFactory.Stretch((RectTransform)root.transform, 0f);
            artGroup = root.GetComponent<CanvasGroup>();
            artGroup.alpha = 0f;
            artGroup.blocksRaycasts = artGroup.interactable = false;

            var picture = UiFactory.CreateImage("Picture", root.transform, new Color(0.42f, 0.42f, 0.42f)); // darker, so the words read over it
            picture.sprite = sprite;
            picture.raycastTarget = false;
            var prt = picture.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            var fit = picture.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);

            var linesGo = new GameObject("RageLines", typeof(RectTransform), typeof(CanvasRenderer));
            linesGo.transform.SetParent(prt, false);
            UiFactory.Stretch((RectTransform)linesGo.transform, 0f);
            var lines = linesGo.AddComponent<TawanOS.VFX.ScribbleLines>();
            lines.color = new Color(0.1f, 0.01f, 0.01f, 0.92f);
            lines.raycastTarget = false;
            foreach (var stroke in RageStrokes) lines.strokes.Add(stroke);

            var vignette = UiFactory.CreateImage("Vignette", root.transform, new Color(0f, 0f, 0f, 0.85f));
            vignette.sprite = VignetteSprite();
            vignette.raycastTarget = false;
            UiFactory.Stretch(vignette.rectTransform, 0f);
        }

        // Zigzags round the head, spread over the red so no side is bare (picture space: x right, y up, 0..1)
        private static readonly Vector2[][] RageStrokes =
        {
            // upper left, the long one from the reference, and one under it
            new[] { new Vector2(0.093f, 0.970f), new Vector2(0.150f, 0.910f), new Vector2(0.165f, 0.990f), new Vector2(0.275f, 0.810f), new Vector2(0.300f, 0.895f), new Vector2(0.335f, 0.810f) },
            new[] { new Vector2(0.015f, 0.860f), new Vector2(0.070f, 0.800f), new Vector2(0.082f, 0.865f), new Vector2(0.150f, 0.745f), new Vector2(0.168f, 0.805f), new Vector2(0.205f, 0.745f) },
            // in the red between the left arms
            new[] { new Vector2(0.200f, 0.560f), new Vector2(0.245f, 0.505f), new Vector2(0.258f, 0.565f), new Vector2(0.305f, 0.475f), new Vector2(0.322f, 0.530f), new Vector2(0.352f, 0.478f) },
            // over the head, left and right of the hair
            new[] { new Vector2(0.345f, 0.930f), new Vector2(0.372f, 0.985f), new Vector2(0.392f, 0.925f), new Vector2(0.420f, 0.992f) },
            new[] { new Vector2(0.640f, 0.990f), new Vector2(0.668f, 0.935f), new Vector2(0.690f, 0.990f), new Vector2(0.715f, 0.925f) },
            // upper right, the two from the reference
            new[] { new Vector2(0.700f, 0.750f), new Vector2(0.718f, 0.870f), new Vector2(0.760f, 0.856f), new Vector2(0.807f, 0.980f), new Vector2(0.812f, 0.900f), new Vector2(0.885f, 0.995f) },
            new[] { new Vector2(0.730f, 0.640f), new Vector2(0.780f, 0.700f), new Vector2(0.807f, 0.645f), new Vector2(0.917f, 0.790f), new Vector2(0.904f, 0.695f), new Vector2(0.977f, 0.790f) },
            // top right corner, and the red between the head and the right arm
            new[] { new Vector2(0.900f, 0.975f), new Vector2(0.930f, 0.915f), new Vector2(0.950f, 0.980f), new Vector2(0.990f, 0.900f) },
            new[] { new Vector2(0.745f, 0.500f), new Vector2(0.795f, 0.555f), new Vector2(0.812f, 0.495f), new Vector2(0.878f, 0.600f), new Vector2(0.872f, 0.530f), new Vector2(0.930f, 0.618f) },
        };

        // 0 below a, 1 above b, smooth between (Mathf.SmoothStep interpolates a..b instead, which is not this)
        private static float Ramp(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        // Clear in the middle, dark at the edges and corners
        private static Sprite vignetteSprite;
        private static Sprite VignetteSprite()
        {
            if (vignetteSprite != null) return vignetteSprite;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx * 0.85f + dy * dy);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255f * Ramp(0.55f, 1.35f, d)));
                }
            tex.SetPixels32(px);
            tex.Apply();
            vignetteSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return vignetteSprite;
        }

        public void CompleteCutsceneImmediately()
        {
            if (sequenceCoroutine != null)
            {
                StopCoroutine(sequenceCoroutine);
                sequenceCoroutine = null;
            }
            if (cutsceneGroup != null) cutsceneGroup.alpha = 0f;
            if (dialogueText != null) dialogueText.text = DefeatDialogueLines[DefeatDialogueLines.Length - 1];
            if (bloodOverlay != null) bloodOverlay.color = BloodColor;
            if (artGroup != null) artGroup.alpha = 1f;
            if (statsGroup != null)
            {
                statsGroup.alpha = 1f;
                statsGroup.interactable = true;
                statsGroup.blocksRaycasts = true;
            }
            if (EventSystem.current != null && restartButton != null)
            {
                EventSystem.current.SetSelectedGameObject(null); // a selected button ignores the pointer's highlight
            }
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                skipDialogueRequested = true;
            }
        }

        private static void Place(RectTransform rect, float x0, float x1, float y0, float y1)
        {
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>True when a Game Over that was waiting on a timer should still appear: the scene where the
        /// fight was lost is loaded and still the active one.</summary>
        public static bool ShouldShow(Scene lostIn, Scene active)
        {
            return lostIn.IsValid() && lostIn.isLoaded && active.IsValid() && lostIn.handle == active.handle;
        }

        // Set when the page is told to go away; visible to tests because Destroy is deferred
        public bool destroyRequested;

        private void OnEnable() => SceneManager.activeSceneChanged += OnActiveSceneChanged;
        private void OnDisable() => SceneManager.activeSceneChanged -= OnActiveSceneChanged;

        // The page belongs to the scene it was shown in; it must never stay on top of another scene
        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            destroyRequested = true;
            Time.timeScale = 1f;
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);   // edit-mode tests
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Restart()
        {
            Time.timeScale = 1f;
            int floors = totalFloors > 0 ? totalFloors : RunState.DefaultTotalFloors;
            Destroy(gameObject);
            if (GameFlowManager.Instance != null) GameFlowManager.Instance.StartNewGame(floors);
        }

        private void GoToMainMenu()
        {
            Time.timeScale = 1f;
            Destroy(gameObject);
            if (Application.CanStreamedLevelBeLoaded(GameFlowManager.MainMenuSceneName))
                SceneTransition.Load(GameFlowManager.MainMenuSceneName);
        }
    }
}
