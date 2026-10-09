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
    /// Step-by-step spotlight hints for the tutorial fight in the intro story. Each hint shows once, when the fight
    /// first reaches the moment it explains (the first enemy board phase, the first time Corruption rises, ...):
    /// the screen goes dark except a lit window around what the hint is about (the hand, the board row, the Merit
    /// panel, the draw pile, ...), with the text beside it. The fight is frozen and card input is blocked while a
    /// hint is up; a click or Enter goes to the next one. "ข้ามบทสอน" leaves the fight at once.
    /// Started by GameFlowManager; built in code, no scene setup needed.
    /// </summary>
    public class TutorialCoach : MonoBehaviour
    {
        public static TutorialCoach Instance { get; private set; }

        /// <summary>A hint is up (or closed this frame): clicks and keys must not also act on the fight.</summary>
        public static bool Blocking => Instance != null && (Instance.showing || Time.frameCount <= Instance.closedFrame);

        private const float FadeSpeed = 5f;
        private const float SettleSeconds = 0.5f;  // let cards land and the camera move before lighting a target
        private const float Padding = 0.012f;      // around the lit window, as a fraction of the screen
        private const float BorderPixels = 4f;

        private enum Target
        {
            None,
            EnemyStatus,
            PlayerStatus,
            Hand,
            PlayerBoard,
            EnemyBoard,
            BothBoards,
            Merit,
            Corruption,
            DrawPile,
            Graveyard,
            DrawPit,
        }

        private class Tip
        {
            public string title;
            public string body;
            public Target target;
            public bool final; // the end-of-fight message: no buttons, the fight is not frozen
        }

        private readonly Queue<Tip> queue = new Queue<Tip>();
        private readonly HashSet<string> shown = new HashSet<string>();
        private Action onSkip;
        private Tip current;
        private bool showing;
        private bool finalQueued;
        private int closedFrame = -1;
        private float queuedAt;

        // Time is frozen while a hint is up; restored when it closes (or if the scene goes first)
        private bool frozeTime;
        private float timeScaleBefore = 1f;

        private CanvasGroup group;
        private RectTransform shadeBottom, shadeTop, shadeLeft, shadeRight;
        private RectTransform frame;
        private Image[] frameSides;
        private RectTransform box;
        private TextMeshProUGUI titleText, bodyText, stepText;
        private GameObject buttonsRow;

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
            Add("enemyKhwan", Target.EnemyStatus, "ขวัญของศัตรู",
                "เงาดำบุกเข้ามาในฝันของขวัญ!\nทำให้ขวัญของศัตรูตรงนี้เหลือ 0 เพื่อชนะ");
            Add("playerKhwan", Target.PlayerStatus, "ขวัญของคุณ",
                "ถ้าขวัญของคุณหมดก่อน คุณจะแพ้\nนี่เป็นแค่ฝัน แพ้ก็ไม่เสียอะไร");
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
            RestoreTime();
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
                    Add("enemyFirst", Target.EnemyBoard, "ศัตรูลงการ์ดก่อนเสมอ",
                        "ทุกเทิร์น ศัตรูจะลงบริวารและเครื่องรางในแถวนี้ก่อน\nดูว่ามันวางการ์ดไว้ช่องไหน แล้วค่อยวางของคุณตอบโต้");
                    break;

                case TurnPhase.PlayerBoard:
                    CountHand(out int boardCards, out int incantations);
                    Add("hand", Target.Hand, "การ์ดในมือ",
                        $"มือนี้มีบริวาร / เครื่องราง {boardCards} ใบ และอาคม {incantations} ใบ ลองใช้ให้ครบในเทิร์นนี้\n" +
                        "ตอนนี้เป็นเฟสลงบริวาร / เครื่องราง: คลิกการ์ดบริวารหรือเครื่องรางในมือเพื่อหยิบ");
                    Add("board", Target.PlayerBoard, "กระดานของคุณ",
                        "หยิบการ์ดแล้ว คลิกช่องว่างที่สว่างขึ้นในแถวนี้เพื่อวาง\n" +
                        "คลิกขวาหรือกด Esc เพื่อเอาการ์ดกลับเข้ามือ\nลงเสร็จแล้ว กด Space เพื่อจบเฟส");
                    int first = combat != null ? combat.FirstTurnMerit : 2;
                    int max = combat != null ? combat.State.maxMerit : 6;
                    Add("merit", Target.Merit, "กุศล",
                        "การ์ดสายขาวใช้กุศล (ตัวเลขมุมซ้ายบนของการ์ด)\n" +
                        $"เทิร์นแรกมีกุศล {first} แต้ม เพิ่มเทิร์นละ 1 แต้ม สูงสุด {max} แต้ม\n" +
                        "กุศลที่ไม่ได้ใช้จะไม่สะสมไปเทิร์นหน้า");
                    break;

                case TurnPhase.PlayerSpell:
                    Add("spell", Target.Hand, "ร่ายอาคม",
                        "เฟสนี้ใช้การ์ดอาคม: คลิกการ์ด แล้วคลิกการ์ดเป้าหมาย\n" +
                        "อาคมที่ไม่มีเป้าหมาย คลิกตรงไหนก็ได้เพื่อร่าย\nร่ายเสร็จแล้ว กด Space");
                    break;

                case TurnPhase.Clash:
                    Add("clash", Target.BothBoards, "การ์ดตีกัน",
                        "บริวารตีกันทีละคอลัมน์ จากซ้ายไปขวา\n" +
                        "บริวารจะตีบริวารที่อยู่ช่องตรงข้าม ถ้าช่องตรงข้ามว่าง จะตีขวัญของฝ่ายตรงข้ามโดยตรง");
                    break;
            }
        }

        private static void CountHand(out int boardCards, out int incantations)
        {
            boardCards = incantations = 0;
            if (CardManager.Instance == null) return;
            foreach (var card in CardManager.Instance.Hand)
            {
                if (card.cardType == CardType.Incantation) incantations++;
                else boardCards++;
            }
        }

        private void HandleTurnStarted(int turn)
        {
            if (turn != 2) return;
            Add("drawPile", Target.DrawPile, "กองจั่ว",
                "การ์ดที่ยังไม่ได้จั่วอยู่ที่นี่ คลิกเพื่อดูว่าเหลือการ์ดอะไรบ้าง");
            Add("graveyard", Target.Graveyard, "หลุมศพ",
                "การ์ดที่ใช้ไปหรือถูกทำลายของทั้งสองฝ่ายมาอยู่ที่นี่ คลิกเพื่อดู");
            int pit = CardManager.Instance != null ? CardManager.Instance.NextPitCorruption : 1;
            Add("pit", Target.DrawPit, "หลุมจั่ว",
                "ในตาของคุณ คลิกเพื่อจั่วการ์ดสุ่มเพิ่ม 1 ใบ แลกกับมลทิน\n" +
                $"ครั้งต่อไปได้มลทิน {pit} แต้ม และยิ่งจั่วบ่อย มลทินยิ่งเพิ่ม");
            Add("inspect", Target.None, "ดูการ์ด",
                "คลิกขวาที่การ์ดใบไหนก็ได้เพื่อดูรายละเอียด\nกด C เพื่อดูกระดานจากมุมบน");
        }

        private void HandleCorruption(int current, int threshold)
        {
            if (current <= 0) return;
            Add("corruption", Target.Corruption, "มลทิน",
                "การ์ดสายดำไม่ใช้กุศล แต่เพิ่มมลทิน (ตัวเลขมุมซ้ายบนของการ์ดสายดำ)\n" +
                $"ถ้ามลทินถึง {threshold} คำสาปจะย้อนเข้าตัว: ติดสถานะร้ายแบบสุ่ม แล้วมลทินกลับเป็น 0");
        }

        private void HandleBackfire()
        {
            Add("backfire", Target.Corruption, "คำสาปย้อน!",
                "มลทินเต็มแล้ว คุณจึงติดสถานะร้ายแบบสุ่ม\nระวังอย่าใช้การ์ดสายดำติดกันมากเกินไป");
        }

        private void HandleCombatEnded(bool victory)
        {
            queue.Clear();
            finalQueued = true;
            if (showing) Close();
            if (victory) Add("end", Target.None, "ชนะฝันร้าย", "เงาดำสลายไป... ขวัญกำลังจะตื่น", final: true);
            else Add("end", Target.None, "ขวัญแตกในฝัน", "ไม่เป็นไร นี่เป็นแค่ฝัน... ขวัญกำลังจะตื่น", final: true);
        }

        private void Add(string key, Target target, string title, string body, bool final = false)
        {
            if (finalQueued && !final) return;
            if (!shown.Add(key)) return;
            if (queue.Count == 0 && !showing) queuedAt = Time.unscaledTime;
            queue.Enqueue(new Tip { title = title, body = body, target = target, final = final });
        }

        // ---------------------------------------------------------------- showing

        private void Update()
        {
            if (!showing && queue.Count > 0 && Time.unscaledTime - queuedAt >= SettleSeconds && !PauseMenu.IsPaused)
                ShowNext();

            if (showing && !current.final && !PauseMenu.IsPaused && Time.frameCount > closedFrame
                && (Input.GetMouseButtonDown(0) && !OverSkipButton()
                    || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                Next();
            }

            if (showing) PlaceSpotlight(current.target); // the camera or the cards may still move under it

            group.alpha = Mathf.MoveTowards(group.alpha, showing ? 1f : 0f, FadeSpeed * Time.unscaledDeltaTime);
            group.blocksRaycasts = showing && !current.final;
        }

        private void ShowNext()
        {
            current = queue.Dequeue();
            titleText.text = current.title;
            bodyText.text = current.body;
            stepText.text = current.final ? "" : "คลิกหรือกด Enter เพื่อไปต่อ";
            buttonsRow.SetActive(!current.final);
            showing = true;
            if (!current.final) FreezeTime();
            PlaceSpotlight(current.target);
        }

        // For the automated test (AutomatedTutorialFlowTest)
        public bool IsShowingHint => showing && current != null && !current.final;
        public string ShowingTitle => showing && current != null ? current.title : null;
        public bool ShowingLit { get; private set; }

        /// <summary>Same as a click on the dark screen: the next hint, or back to the fight.</summary>
        public void Next()
        {
            if (IsShowingHint) Close();
            if (queue.Count > 0) queuedAt = Time.unscaledTime - SettleSeconds;
        }

        private void Close()
        {
            showing = false;
            closedFrame = Time.frameCount;
            RestoreTime();
        }

        private void Skip()
        {
            Close();
            queue.Clear();
            onSkip?.Invoke();
        }

        private void FreezeTime()
        {
            if (frozeTime) return;
            frozeTime = true;
            timeScaleBefore = Time.timeScale;
            Time.timeScale = 0f;
        }

        private void RestoreTime()
        {
            if (!frozeTime) return;
            frozeTime = false;
            // The Pause menu keeps its own copy and puts it back when it closes
            if (!PauseMenu.IsPaused) Time.timeScale = timeScaleBefore;
        }

        private bool OverSkipButton()
        {
            var skip = buttonsRow != null ? (RectTransform)buttonsRow.transform.GetChild(0) : null;
            return skip != null && RectTransformUtility.RectangleContainsScreenPoint(skip, Input.mousePosition, null);
        }

        // ---------------------------------------------------------------- the lit window

        // Darkens everything but the target's screen rectangle and puts the text box beside it
        private void PlaceSpotlight(Target target)
        {
            bool lit = TryFindScreenRect(target, out Rect r);
            ShowingLit = lit;
            if (lit)
            {
                float w = Screen.width, h = Screen.height;
                r = Rect.MinMaxRect(
                    Mathf.Clamp01(r.xMin / w - Padding), Mathf.Clamp01(r.yMin / h - Padding),
                    Mathf.Clamp01(r.xMax / w + Padding), Mathf.Clamp01(r.yMax / h + Padding));
            }
            else
            {
                r = new Rect(0.5f, 0.5f, 0f, 0f); // no target: the whole screen dark
            }

            SetAnchors(shadeBottom, 0f, 0f, 1f, r.yMin);
            SetAnchors(shadeTop, 0f, r.yMax, 1f, 1f);
            SetAnchors(shadeLeft, 0f, r.yMin, r.xMin, r.yMax);
            SetAnchors(shadeRight, r.xMax, r.yMin, 1f, r.yMax);

            frame.gameObject.SetActive(lit);
            if (lit)
            {
                SetAnchors(frame, r.xMin, r.yMin, r.xMax, r.yMax);
                float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 4f);
                var c = UIThemeSO.Current.accent;
                foreach (var side in frameSides) side.color = new Color(c.r, c.g, c.b, pulse);
            }

            PlaceBox(lit ? r : (Rect?)null);
        }

        // Beside the lit window where there is the most room; in the middle when nothing is lit
        private void PlaceBox(Rect? lit)
        {
            if (lit == null)
            {
                box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
                box.anchoredPosition = Vector2.zero;
                return;
            }

            var r = lit.Value;
            float above = 1f - r.yMax, below = r.yMin, left = r.xMin, right = 1f - r.xMax;
            float centreX = Mathf.Clamp(r.center.x, 0.25f, 0.75f);
            float centreY = Mathf.Clamp(r.center.y, 0.25f, 0.75f);
            const float gap = 0.02f;

            if (above >= 0.3f || below >= 0.3f)
            {
                bool putAbove = above >= below;
                box.anchorMin = box.anchorMax = new Vector2(centreX, putAbove ? r.yMax + gap : r.yMin - gap);
                box.pivot = new Vector2(0.5f, putAbove ? 0f : 1f);
            }
            else
            {
                bool putRight = right >= left;
                box.anchorMin = box.anchorMax = new Vector2(putRight ? r.xMax + gap : r.xMin - gap, centreY);
                box.pivot = new Vector2(putRight ? 0f : 1f, 0.5f);
            }
            box.anchoredPosition = Vector2.zero;
        }

        private static void SetAnchors(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(Mathf.Max(xMin, xMax), Mathf.Max(yMin, yMax));
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ---------------------------------------------------------------- finding what to light (screen pixels)

        private static bool TryFindScreenRect(Target target, out Rect rect)
        {
            var area = new ScreenArea();
            switch (target)
            {
                case Target.EnemyStatus: area.AddUi("EnemyPanel"); break;
                case Target.PlayerStatus: area.AddUi("PlayerPanel"); break;
                case Target.Merit: area.AddUi("MeritPanel"); break;
                case Target.Corruption: area.AddUi("CorruptionPanel"); break;

                case Target.Hand:
                    var hand = CardManager.Instance != null ? CardManager.Instance.Hand : null;
                    if (hand == null) break;
                    foreach (var view in FindObjectsByType<CardView3D>(FindObjectsSortMode.None))
                        if (view.IsInHand && view.CardData != null && Contains(hand, view.CardData)) area.AddRenderers(view.gameObject);
                    break;

                case Target.PlayerBoard: AddSlots(ref area, BoardSlotView.SlotSide.Player); break;
                case Target.EnemyBoard: AddSlots(ref area, BoardSlotView.SlotSide.Enemy); break;
                case Target.BothBoards:
                    AddSlots(ref area, BoardSlotView.SlotSide.Player);
                    AddSlots(ref area, BoardSlotView.SlotSide.Enemy);
                    break;

                case Target.DrawPile:
                    var pile = FindFirstObjectByType<DeckPileView3D>();
                    if (pile != null) area.AddBounds(pile.PileBounds);
                    break;
                case Target.Graveyard:
                    var grave = GraveyardView3D.Instance;
                    if (grave != null) area.AddRenderers(grave.graveyardObject != null ? grave.graveyardObject : grave.gameObject);
                    break;
                case Target.DrawPit:
                    if (DrawPitView3D.Instance != null) area.AddRenderers(DrawPitView3D.Instance.gameObject);
                    break;
            }
            rect = area.rect;
            return area.found && rect.width > 1f && rect.height > 1f;
        }

        private static bool Contains(IReadOnlyList<CardInstance> list, CardInstance card)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == card) return true;
            return false;
        }

        private static void AddSlots(ref ScreenArea area, BoardSlotView.SlotSide side)
        {
            // Deck slots are BoardSlotViews too (Player_DeckSlot / Enemy_DeckSlot): only the board row is lit
            foreach (var slot in FindObjectsByType<BoardSlotView>(FindObjectsSortMode.None))
                if (slot.side == side && slot.GetComponent<DeckPileView3D>() == null && !slot.name.Contains("Deck"))
                    area.AddRenderers(slot.gameObject);
        }

        // A growing screen rectangle around UI panels and 3D objects
        private struct ScreenArea
        {
            public Rect rect;
            public bool found;

            private void Add(Vector2 p)
            {
                if (!found) { rect = new Rect(p, Vector2.zero); found = true; return; }
                rect = Rect.MinMaxRect(Mathf.Min(rect.xMin, p.x), Mathf.Min(rect.yMin, p.y), Mathf.Max(rect.xMax, p.x), Mathf.Max(rect.yMax, p.y));
            }

            // Screen Space Overlay canvases: world corners are screen pixels
            public void AddUi(string objectName)
            {
                var go = GameObject.Find(objectName);
                if (go == null || !go.activeInHierarchy || !(go.transform is RectTransform rt)) return;
                var corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                foreach (var c in corners) Add(c);
            }

            public void AddRenderers(GameObject root)
            {
                bool any = false;
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer) continue;
                    AddBounds(r.bounds);
                    any = true;
                }
                if (any) return;

                // Hidden markers (e.g. board slots whose renderer is off because the table model draws them):
                // use the mesh's own shape where it sits
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
                    if (mf.sharedMesh != null) AddLocalBounds(mf.transform, mf.sharedMesh.bounds);
            }

            private void AddLocalBounds(Transform t, Bounds local)
            {
                var cam = Camera.main;
                if (cam == null) return;
                for (int i = 0; i < 8; i++)
                {
                    var corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    AddWorldPoint(cam, t.TransformPoint(corner));
                }
            }

            private void AddWorldPoint(Camera cam, Vector3 world)
            {
                var p = cam.WorldToScreenPoint(world);
                if (p.z > 0f) Add(new Vector2(Mathf.Clamp(p.x, 0f, Screen.width), Mathf.Clamp(p.y, 0f, Screen.height)));
            }

            public void AddBounds(Bounds b)
            {
                var cam = Camera.main;
                if (cam == null) return;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    AddWorldPoint(cam, corner);
                }
            }
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

            var shade = new Color(0f, 0f, 0f, 0.8f);
            shadeBottom = Shade("ShadeBottom", canvas.transform, shade);
            shadeTop = Shade("ShadeTop", canvas.transform, shade);
            shadeLeft = Shade("ShadeLeft", canvas.transform, shade);
            shadeRight = Shade("ShadeRight", canvas.transform, shade);

            // A glowing border around the lit window
            frame = UiFactory.CreateRect("Frame", canvas.transform);
            frameSides = new[]
            {
                Side(frame, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-BorderPixels, -BorderPixels), new Vector2(BorderPixels, 0f)),
                Side(frame, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-BorderPixels, 0f), new Vector2(BorderPixels, BorderPixels)),
                Side(frame, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(-BorderPixels, 0f), new Vector2(0f, 0f)),
                Side(frame, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(BorderPixels, 0f)),
            };

            // Text box
            var boxImage = UiFactory.CreateImage("Box", canvas.transform, theme.panel);
            boxImage.raycastTarget = true;
            box = boxImage.rectTransform;
            box.sizeDelta = new Vector2(640f, 0f);
            var outline = boxImage.gameObject.AddComponent<Outline>();
            outline.effectColor = theme.accent;
            outline.effectDistance = new Vector2(2f, -2f);

            var layout = boxImage.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 22, 22);
            layout.spacing = 12f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            boxImage.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            titleText = UiFactory.CreateText(box, "Title", "", theme.titleSize, theme.accent, TextAlignmentOptions.Left,
                theme.titleFont != null ? theme.titleFont : theme.bodyFont);
            bodyText = UiFactory.CreateText(box, "Body", "", 26f, theme.text, TextAlignmentOptions.Left, theme.bodyFont);
            bodyText.lineSpacing = 12f;

            var row = UiFactory.CreateRect("Buttons", box);
            buttonsRow = row.gameObject;
            var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 16f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            MakeButton(row, "ข้ามบทสอน", theme.black, theme.text, Skip); // child 0, see OverSkipButton
            stepText = UiFactory.CreateText(row, "Next", "", theme.labelSize, theme.accent, TextAlignmentOptions.Right, theme.bodyFont);
            stepText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private static RectTransform Shade(string name, Transform parent, Color color)
        {
            var image = UiFactory.CreateImage(name, parent, color);
            image.raycastTarget = true; // clicks on the dark part do not reach the UI behind it
            return image.rectTransform;
        }

        private static Image Side(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var image = UiFactory.CreateImage("Side", parent, UIThemeSO.Current.accent);
            var rt = image.rectTransform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return image;
        }

        private static void MakeButton(Transform parent, string label, Color fill, Color textColor, Action onClick)
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
        }
    }
}
