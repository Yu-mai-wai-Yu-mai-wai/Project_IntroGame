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
            TawanOS.VFX.UiTalismanButton.Dress(screen.restartButton);
            TawanOS.VFX.UiTalismanButton.Dress(screen.menuButton);
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
            // --- Part 1: Character Art + Dialogue ---
            cutsceneGroup.alpha = 1f;

            for (int i = 0; i < DefeatDialogueLines.Length; i++)
            {
                string line = DefeatDialogueLines[i];
                dialogueText.text = "";

                for (int c = 0; c < line.Length; c++)
                {
                    if (skipDialogueRequested)
                    {
                        dialogueText.text = line;
                        skipDialogueRequested = false;
                        break;
                    }
                    dialogueText.text += line[c];
                    yield return new WaitForSeconds(0.03f);
                }

                // Pause between lines
                float pause = 1.0f;
                while (pause > 0f)
                {
                    if (skipDialogueRequested)
                    {
                        skipDialogueRequested = false;
                        break;
                    }
                    pause -= Time.deltaTime;
                    yield return null;
                }
            }

            // --- Part 2: Fade to black + red blood overlay ---
            cutsceneGroup.DOFade(0f, 0.7f);
            bloodOverlay.DOColor(new Color(0.6f, 0.04f, 0.04f, 0.95f), 0.8f);
            yield return new WaitForSeconds(0.8f);

            // --- Part 3: Show Stats Panel & Buttons ---
            cutsceneGroup.blocksRaycasts = false;
            statsGroup.transform.SetAsLastSibling();
            statsGroup.DOFade(1f, 0.6f).SetUpdate(true);
            statsGroup.interactable = true;
            statsGroup.blocksRaycasts = true;

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
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
            if (bloodOverlay != null) bloodOverlay.color = new Color(0.6f, 0.04f, 0.04f, 0.95f);
            if (statsGroup != null)
            {
                statsGroup.alpha = 1f;
                statsGroup.interactable = true;
                statsGroup.blocksRaycasts = true;
            }
            if (EventSystem.current != null && restartButton != null)
            {
                EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
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
