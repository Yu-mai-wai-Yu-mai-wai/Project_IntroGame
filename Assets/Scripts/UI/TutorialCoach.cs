using System;
using System.Collections.Generic;
using TawanOS.CardEngine;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.UI
{
    /// <summary>
    /// Step-by-step hints for the tutorial fight in the intro story. Each hint shows once, when the fight first
    /// reaches the moment it explains (the first enemy board phase, the first time Corruption rises, ...), and
    /// waits for "เข้าใจแล้ว" (or Enter) while the fight goes on underneath; it never forces a move.
    /// "ข้ามบทสอน" leaves the fight at once. Started by GameFlowManager; built in code, no scene setup needed.
    /// </summary>
    public class TutorialCoach : MonoBehaviour
    {
        public static TutorialCoach Instance { get; private set; }

        /// <summary>The mouse is over the hint box: a click there must not also play or pick up a card.</summary>
        public static bool PointerOver => Instance != null && Instance.IsPointerOverPanel();

        private const float FadeSpeed = 6f;

        private struct Tip
        {
            public string title;
            public string body;
            public bool final;
        }

        private readonly Queue<Tip> queue = new Queue<Tip>();
        private readonly HashSet<string> shown = new HashSet<string>();
        private Action onSkip;
        private bool showing;
        private bool finalShown;

        private CanvasGroup group;
        private RectTransform panel;
        private TextMeshProUGUI titleText, bodyText;
        private GameObject nextButton, skipButton;

        private TurnPhaseController turns;
        private CombatManager combat;

        public static TutorialCoach Begin(Action skip)
        {
            if (Instance != null) Destroy(Instance.gameObject);
            var go = new GameObject("TutorialCoach");
            // Called from sceneLoaded: make sure it lives (and dies) with the combat scene
            if (CombatManager.Instance != null) SceneManager.MoveGameObjectToScene(go, CombatManager.Instance.gameObject.scene);
            var coach = go.AddComponent<TutorialCoach>();
            coach.onSkip = skip;
            return coach;
        }

        private void Awake()
        {
            Instance = this;
            Build();
            Add("welcome", "บทสอน: ฝันร้าย",
                "เงาดำบุกเข้ามาในฝันของขวัญ! ทำให้ขวัญของศัตรูเหลือ 0 ก่อนที่ขวัญของคุณจะหมด\n" +
                "นี่เป็นแค่ฝัน แพ้ก็ไม่เสียอะไร");
        }

        private void Start()
        {
            turns = TurnPhaseController.Instance;
            combat = CombatManager.Instance;
            if (turns != null)
            {
                turns.OnPhaseChanged += HandlePhase;
                turns.OnTurnStarted += HandleTurnStarted;
            }
            if (combat != null)
            {
                combat.OnCorruptionChanged += HandleCorruption;
                combat.OnCurseBackfireTriggered += HandleBackfire;
                combat.OnCombatEnded += HandleCombatEnded;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (turns != null)
            {
                turns.OnPhaseChanged -= HandlePhase;
                turns.OnTurnStarted -= HandleTurnStarted;
            }
            if (combat != null)
            {
                combat.OnCorruptionChanged -= HandleCorruption;
                combat.OnCurseBackfireTriggered -= HandleBackfire;
                combat.OnCombatEnded -= HandleCombatEnded;
            }
        }

        // ---------------------------------------------------------------- when each hint shows

        private void HandlePhase(TurnPhase phase)
        {
            switch (phase)
            {
                case TurnPhase.EnemyBoard:
                    Add("enemyFirst", "ศัตรูลงการ์ดก่อนเสมอ",
                        "ทุกเทิร์น ศัตรูจะลงบริวารและเครื่องรางก่อน\nดูว่ามันวางการ์ดไว้ช่องไหน แล้วค่อยวางของคุณตอบโต้");
                    break;
                case TurnPhase.PlayerBoard:
                    Add("board", "ลงบริวาร / เครื่องราง",
                        "คลิกการ์ดบริวารหรือเครื่องรางในมือ แล้วคลิกช่องว่างที่สว่างขึ้นบนกระดาน\n" +
                        "คลิกขวาหรือกด Esc เพื่อเอาการ์ดกลับเข้ามือ\n" +
                        "ลงเสร็จแล้ว กด Space หรือปุ่มจบเฟส");
                    int first = combat != null ? combat.FirstTurnMerit : 2;
                    int max = combat != null ? combat.State.maxMerit : 6;
                    Add("merit", "กุศล",
                        "การ์ดสายขาวใช้กุศล (ตัวเลขมุมซ้ายบนของการ์ด)\n" +
                        $"เทิร์นแรกมีกุศล {first} แต้ม เพิ่มเทิร์นละ 1 แต้ม สูงสุด {max} แต้ม\n" +
                        "กุศลที่ไม่ได้ใช้จะไม่สะสมไปเทิร์นหน้า");
                    break;
                case TurnPhase.PlayerSpell:
                    Add("spell", "ร่ายอาคม",
                        "เฟสนี้ใช้การ์ดอาคม: คลิกการ์ด แล้วคลิกการ์ดเป้าหมาย\n" +
                        "อาคมที่ไม่มีเป้าหมาย คลิกตรงไหนก็ได้เพื่อร่าย\n" +
                        "ร่ายเสร็จแล้ว กด Space");
                    break;
                case TurnPhase.Clash:
                    Add("clash", "การ์ดตีกัน",
                        "บริวารตีกันทีละคอลัมน์ จากซ้ายไปขวา\n" +
                        "บริวารจะตีบริวารที่อยู่ช่องตรงข้าม ถ้าช่องตรงข้ามว่าง จะตีขวัญของฝ่ายตรงข้ามโดยตรง");
                    break;
            }
        }

        private void HandleTurnStarted(int turn)
        {
            if (turn == 2)
                Add("inspect", "ดูการ์ด",
                    "คลิกขวาที่การ์ดใบไหนก็ได้เพื่อดูรายละเอียด\n" +
                    "คลิกกองจั่วหรือหลุมศพเพื่อดูการ์ดข้างใน\n" +
                    "กด C เพื่อดูกระดานจากมุมบน");
        }

        private void HandleCorruption(int current, int threshold)
        {
            if (current <= 0) return;
            Add("corruption", "มลทิน",
                "การ์ดสายดำไม่ใช้กุศล แต่เพิ่มมลทิน (ตัวเลขมุมซ้ายบนของการ์ดสายดำ)\n" +
                $"ถ้ามลทินถึง {threshold} คำสาปจะย้อนเข้าตัว: ติดสถานะร้ายแบบสุ่ม แล้วมลทินกลับเป็น 0");
        }

        private void HandleBackfire()
        {
            Add("backfire", "คำสาปย้อน!",
                "มลทินเต็มแล้ว คุณจึงติดสถานะร้ายแบบสุ่ม\nระวังอย่าใช้การ์ดสายดำติดกันมากเกินไป");
        }

        private void HandleCombatEnded(bool victory)
        {
            queue.Clear();
            showing = false;
            finalShown = true;
            if (victory) Add("end", "ชนะฝันร้าย", "เงาดำสลายไป... ขวัญกำลังจะตื่น", final: true);
            else Add("end", "ขวัญแตกในฝัน", "ไม่เป็นไร นี่เป็นแค่ฝัน... ขวัญกำลังจะตื่น", final: true);
        }

        private void Add(string key, string title, string body, bool final = false)
        {
            if (!shown.Add(key)) return;
            if (finalShown && !final) return;
            queue.Enqueue(new Tip { title = title, body = body, final = final });
        }

        // ---------------------------------------------------------------- showing

        private void Update()
        {
            if (!showing && queue.Count > 0) ShowNext();

            if (showing && nextButton.activeSelf && !PauseMenu.IsPaused
                && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                Dismiss();
            }

            group.alpha = Mathf.MoveTowards(group.alpha, showing ? 1f : 0f, FadeSpeed * Time.unscaledDeltaTime);
            group.blocksRaycasts = showing;
        }

        private void ShowNext()
        {
            var tip = queue.Dequeue();
            titleText.text = tip.title;
            bodyText.text = tip.body;
            nextButton.SetActive(!tip.final);
            skipButton.SetActive(!tip.final);
            showing = true;
            group.alpha = 0f;
        }

        private void Dismiss()
        {
            showing = false;
        }

        private void Skip()
        {
            onSkip?.Invoke();
        }

        private bool IsPointerOverPanel()
        {
            return showing && panel != null && RectTransformUtility.RectangleContainsScreenPoint(panel, Input.mousePosition, null);
        }

        // ---------------------------------------------------------------- building the UI

        private void Build()
        {
            var theme = UIThemeSO.Current;
            var canvas = UiFactory.CreateOverlayCanvas("TutorialCanvas", 400, transform); // under the card screens (450+)
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            group = canvas.GetComponent<CanvasGroup>();
            group.interactable = true;
            group.alpha = 0f;

            if (EventSystem.current == null && FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            // Left middle: clear of the hand, the held card (right) and the turn banner (top)
            var box = UiFactory.CreateImage("Panel", canvas.transform, theme.panel);
            box.raycastTarget = true;
            panel = box.rectTransform;
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 0.5f);
            panel.pivot = new Vector2(0f, 0.5f);
            panel.anchoredPosition = new Vector2(40f, 60f);
            panel.sizeDelta = new Vector2(600f, 0f);
            var outline = box.gameObject.AddComponent<Outline>();
            outline.effectColor = theme.accent;
            outline.effectDistance = new Vector2(2f, -2f);

            var layout = box.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 22, 22);
            layout.spacing = 14f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            titleText = UiFactory.CreateText(panel, "Title", "", theme.titleSize, theme.accent, TextAlignmentOptions.Left,
                theme.titleFont != null ? theme.titleFont : theme.bodyFont);
            bodyText = UiFactory.CreateText(panel, "Body", "", 26f, theme.text, TextAlignmentOptions.Left, theme.bodyFont);
            bodyText.lineSpacing = 12f;

            var row = UiFactory.CreateRect("Buttons", panel);
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 16f;
            rowLayout.childAlignment = TextAnchor.MiddleRight;
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            skipButton = MakeButton(row, "ข้ามบทสอน", theme.black, theme.text, Skip);
            nextButton = MakeButton(row, "เข้าใจแล้ว (Enter)", theme.crimson, theme.text, Dismiss);
        }

        private static GameObject MakeButton(Transform parent, string label, Color fill, Color textColor, Action onClick)
        {
            var theme = UIThemeSO.Current;
            var image = UiFactory.CreateImage(label, parent, fill);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());
            var size = image.gameObject.AddComponent<LayoutElement>();
            size.minHeight = 52f;
            size.minWidth = 200f;

            var text = UiFactory.CreateText(image.transform, "Label", label, 24f, textColor, TextAlignmentOptions.Center, theme.bodyFont);
            UiFactory.Stretch(text.rectTransform, 8f);
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = theme.accent;
            outline.effectDistance = new Vector2(1f, -1f);
            return image.gameObject;
        }
    }
}
