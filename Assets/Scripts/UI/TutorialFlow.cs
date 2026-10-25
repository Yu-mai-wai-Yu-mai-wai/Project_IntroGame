using System;
using System.Collections;
using System.Collections.Generic;
using TawanOS.CardEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.UI
{
    /// <summary>
    /// The tutorial fight's script (Game Designer, 25 Oct): explain the hand card by card, then let the player do each
    /// thing once (place a familiar in the lane opposite the enemy's, the amulet, a black-magic familiar, cast a curse,
    /// move the camera and zoom), slow the clash down to explain it, teach the centre pit, sum up the keys, and let the
    /// player finish the fight alone. The opening hand comes from the story page (กุมารทอง, ตะกรุด, พรายน้ำ,
    /// คาถาสาปแช่ง); both sides get <see cref="TutorialExtraMerit"/> more Merit in turn 1 so every card can be played.
    /// While a step waits for the player, only what it asks for is allowed (<see cref="AllowsPlaying"/> and the rest).
    /// </summary>
    public partial class TutorialCoach
    {
        /// <summary>Merit added to both sides in the tutorial's first turn (Game Designer, 25 Oct: 4 instead of 2).</summary>
        public const int TutorialExtraMerit = 2;
        private const float ClashTimeScale = 0.6f; // the clash plays slower, lane by lane

        private const string KumanId = "s_wf05", TakrutId = "s_wa01", PraiId = "s_bf07", CurseId = "s_bi01", ShieldId = "s_wi04";
        private CardInstance enemyPlayed;
        private void NoteEnemyPlayed(CardInstance card) => enemyPlayed = card;

        // ---------------------------------------------------------------- what the player may do now

        private bool scripted;          // the steps are running; after the last one everything is allowed
        private Func<CardInstance, bool> allowPlay;
        private Func<CardInstance, BoardSlotView, bool> allowSlot;
        private Func<CardInstance, CardInstance, bool> allowTarget;
        private bool allowEndTurn, allowPit;
        private KeyCode allowKey = KeyCode.None;

        private static bool Gated => Instance != null && Instance.scripted;
        /// <summary>The player may pick up this hand card now.</summary>
        public static bool AllowsPlaying(CardInstance card) => !Gated || (Instance.allowPlay != null && Instance.allowPlay(card));
        /// <summary>A held card may go to this slot now.</summary>
        public static bool AllowsSlot(CardInstance card, BoardSlotView slot) => !Gated || Instance.allowSlot == null || Instance.allowSlot(card, slot);
        /// <summary>An incantation may be cast on this card now.</summary>
        public static bool AllowsTarget(CardInstance card, CardInstance target) => !Gated || Instance.allowTarget == null || Instance.allowTarget(card, target);
        public static bool AllowsEndTurn => !Gated || Instance.allowEndTurn;
        public static bool AllowsPitPick => !Gated || Instance.allowPit;
        public static bool AllowsCameraKey(KeyCode key) => !Gated || Instance.allowKey == key;

        private void Allow(Func<CardInstance, bool> play = null, Func<CardInstance, BoardSlotView, bool> slot = null,
            Func<CardInstance, CardInstance, bool> target = null, bool endTurn = false, bool pit = false, KeyCode key = KeyCode.None)
        {
            allowPlay = play;
            allowSlot = slot;
            allowTarget = target;
            allowEndTurn = endTurn;
            allowPit = pit;
            allowKey = key;
        }

        // ---------------------------------------------------------------- start / end

        private bool holdOpening = true, holdReached;
        private Coroutine flow;
        private CardInstance lastPlayed;
        private bool familiarHit, faceHit;
        private CardManager cards;
        private EffectResolver resolver;

        private void StartFlow()
        {
            cards = CardManager.Instance;
            resolver = EffectResolver.Instance;
            TurnPhaseController.HoldBeforeEnemyBoard = () => { holdReached = true; return holdOpening; };
            if (cards != null) cards.OnCardPlayed += NotePlayed;
            if (resolver != null)
            {
                resolver.OnFamiliarDamaged += NoteFamiliarHit;
                resolver.OnDamageDealt += NoteFaceHit;
            }
            scripted = true;
            Allow();
            flow = StartCoroutine(Run());
        }

        private void EndFlow()
        {
            CardView3D.PinnedHover = null;
            if (TurnPhaseController.HoldBeforeEnemyBoard != null) TurnPhaseController.HoldBeforeEnemyBoard = null;
            if (cards != null) cards.OnCardPlayed -= NotePlayed;
            if (resolver != null)
            {
                resolver.OnFamiliarDamaged -= NoteFamiliarHit;
                resolver.OnDamageDealt -= NoteFaceHit;
            }
            scripted = false;
            if (Time.timeScale == ClashTimeScale) Time.timeScale = 1f;
        }

        private void NotePlayed(CardInstance card) => lastPlayed = card;
        private void NoteFamiliarHit(CardInstance card, int damage) { if (InClash) familiarHit = true; }
        private void NoteFaceHit(int amount, bool toPlayer) { if (InClash && !toPlayer && amount > 0) faceHit = true; }
        private bool InClash => turns != null && turns.CurrentPhase == TurnPhase.Clash;

        // ---------------------------------------------------------------- the steps

        private IEnumerator Run()
        {
            // Turn 1 is held before the enemy's board phase until the hand has been explained
            while (!holdReached || cards == null || cards.Hand.Count < 3) yield return null;
            yield return new WaitForSecondsRealtime(0.6f);
            if (cards.Hand.Count < 4) cards.DrawCards(1); // the 4th opening card, on top of the deck (the story page's list)
            combat?.AddMerit(TutorialExtraMerit);
            combat?.AddEnemyMerit(TutorialExtraMerit);
            // Let it fly into the hand and the hand settle before anything freezes the fight
            for (float waited = 0f; waited < 3f && HandMoving(); waited += Time.unscaledDeltaTime) yield return null;
            yield return new WaitForSecondsRealtime(0.3f);

            var kuman = InHand(KumanId);
            var takrut = InHand(TakrutId);
            var prai = InHand(PraiId);
            var curse = InHand(CurseId);

            // 0: what the fight is for, both khwan bars
            yield return Explain("goalEnemy", words.enemyKhwan, () => UiRect("EnemyPanel"));
            yield return Explain("goalPlayer", words.playerKhwan, () => UiRect("PlayerPanel"));

            // 1-5: the hand, then one card in detail
            yield return Explain("hand", words.tHand, HandRect);
            yield return DoTask(words.doHoverKuman, () => CardRect(kuman), () => HoveredIs(kuman));
            var kumanView = View(kuman);
            // Keep the card raised through its three explanations, however the mouse moves to the next button
            CardView3D.PinnedHover = kumanView;
            if (kumanView != null && !kumanView.IsHovered) kumanView.Hover();
            yield return Explain("costMerit", words.tCostMerit, () => Union(LabelRect(kumanView != null ? kumanView.CostLabel : null), UiRect("MeritGauge")),
                ("cost", kuman != null ? kuman.meritCost : 1), ("merit", combat != null ? combat.CurrentMerit : 4));
            yield return Explain("attackKhwan", words.tAttackKhwan, () => Union(LabelRect(kumanView != null ? kumanView.AttackLabel : null), LabelRect(kumanView != null ? kumanView.KhwanLabel : null)));
            yield return Explain("skill", words.tSkill, () => LabelRect(kumanView != null ? kumanView.DescriptionLabel : null));
            CardView3D.PinnedHover = null;
            if (kumanView != null) kumanView.Unhover();

            // 6: the enemy lays its กุมารทอง, then the board turn
            holdOpening = false;
            yield return WaitPhase(TurnPhase.PlayerBoard);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Explain("boardTurn", words.tBoardTurn, () => SideRect(BoardSlotView.SlotSide.Enemy));
            yield return Explain("slots", words.tSlots, () => SideRect(BoardSlotView.SlotSide.Player));

            int lane = EnemyFamiliarLane();
            Allow(play: c => c == kuman, slot: (c, s) => lane < 0 ? EffectResolver.IsFamiliarSlot(s.slotIndex) : s.slotIndex == lane);
            yield return DoTask(words.doPlaceKuman, () => Holding ? SlotRect(BoardSlotView.SlotSide.Player, lane) : CardRect(kuman), () => lastPlayed == kuman);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Explain("lane", words.tLane, () => LaneRect(lane));

            Allow(play: c => c == takrut, slot: (c, s) => s.slotIndex == EffectResolver.AmuletSlot);
            yield return DoTask(words.doPlaceTakrut, () => Holding ? SlotRect(BoardSlotView.SlotSide.Player, EffectResolver.AmuletSlot) : CardRect(takrut), () => lastPlayed == takrut);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Explain("amulet", words.tAmulet, () => SlotRect(BoardSlotView.SlotSide.Player, EffectResolver.AmuletSlot));

            // 7: a black-magic familiar, and มลทิน
            Allow(play: c => c == prai);
            yield return DoTask(words.doPlacePrai, () => Holding ? SideRect(BoardSlotView.SlotSide.Player) : CardRect(prai), () => lastPlayed == prai);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Explain("corruption", words.tCorruption, () => UiRect("CorruptionGauge"),
                ("threshold", combat != null ? combat.State.corruptionThreshold : 7), ("khwanLoss", EffectResolver.CurseBackfireKhwanLoss));

            Allow(endTurn: true);
            yield return DoTask(words.doEndBoard, () => UiRect("EndTurnButton"), () => turns != null && turns.CurrentPhase != TurnPhase.PlayerBoard);

            // 8: the spell turn: the player curses the enemy's กุมารทอง (the enemy shields one later, in turn 3, so the
            // first clash shows a strike reaching the enemy)
            yield return WaitPhase(TurnPhase.PlayerSpell);
            yield return new WaitForSecondsRealtime(0.6f);
            // The curse and its target lit together, so the box goes beside them, never over the enemy's card
            var enemyKuman = EnemyFamiliar();
            yield return Explain("spellTurn", words.tEnemySpell, () => Union(CardRect(curse), CardRect(enemyKuman)));
            Allow(play: c => c == curse, target: (c, t) => enemyKuman == null || t == enemyKuman);
            yield return DoTask(words.doCast, () => Holding ? CardRect(enemyKuman) : CardRect(curse), () => lastPlayed == curse);
            yield return new WaitForSecondsRealtime(1f);
            yield return Explain("cursed", words.tCursed, () => CardRect(enemyKuman));

            // 9: the camera, up and back (the zoom is taught at the centre pit, where it is needed)
            var rig = CombatCameraRig3D.Ensure();
            yield return KeyTask(words.doUp, KeyCode.W, () => rig.IsBoardOnlyView);
            // and the zoom on the row, from up there (Game Designer, 25 Oct)
            yield return KeyTask(words.doZoom, KeyCode.Space, () => rig.ZoomedOnPlayer);
            yield return KeyTask(words.doUnzoom, KeyCode.Space, () => !rig.Zoomed);
            yield return KeyTask(words.doDown, KeyCode.S, () => !rig.IsTopView);

            // 11-12: the clash, slowed down, explained as it happens
            Allow(endTurn: true);
            yield return DoTask(words.doEndTurn, () => UiRect("EndTurnButton"), () => turns != null && turns.CurrentPhase == TurnPhase.Clash);
            Allow();
            familiarHit = faceHit = false;
            Time.timeScale = ClashTimeScale;
            yield return Explain("clash", words.tClash, () => SideRect(null));
            bool toldFamiliar = false, toldFace = false;
            while (InClash)
            {
                if (familiarHit && !toldFamiliar) { toldFamiliar = true; yield return Explain("familiarHit", words.tFamiliarHit, () => SideRect(null)); }
                if (faceHit && !toldFace) { toldFace = true; yield return Explain("faceHit", words.tFaceHit, () => UiRect("EnemyPanel")); }
                yield return null;
            }
            if (Time.timeScale == ClashTimeScale) Time.timeScale = 1f;
            // No strike reached the enemy (its shielded กุมารทอง drew them all): explain it now
            if (!toldFace && !finalQueued) yield return Explain("faceBlocked", words.tFaceBlocked, () => SideRect(null));

            // 13-14: the centre pit, zoomed in
            while (CenterPit.Instance == null || !CenterPit.Instance.WaitingForPlayer)
            {
                if (combat != null && combat.IsCombatOver) yield break;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.6f);
            yield return KeyTask(words.doUp, KeyCode.W, () => rig.IsBoardOnlyView);
            yield return KeyTask(words.doZoomPit, KeyCode.Space, () => rig.ZoomedOnPit);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Explain("pit", words.tPit, PitRect);
            Allow(pit: true);
            yield return DoTask(words.doPick, PitRect, () => !CenterPit.Instance.WaitingForPlayer);
            Allow();
            yield return new WaitForSecondsRealtime(0.8f);

            // 15: the keys, then the player is on their own
            yield return Explain("summary", words.tSummary, () => null);
            scripted = false;

            // The first time the enemy shields a familiar (turn 3), say what คุ้มกัน does
            enemyPlayed = null;
            if (combat != null && combat.EnemyCards != null) combat.EnemyCards.OnCardPlayed += NoteEnemyPlayed;
            while (!finalQueued && (enemyPlayed == null || enemyPlayed.cardId != ShieldId)) yield return null;
            if (combat != null && combat.EnemyCards != null) combat.EnemyCards.OnCardPlayed -= NoteEnemyPlayed;
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Explain("enemyShield", words.tEnemyShield, () => SideRect(BoardSlotView.SlotSide.Enemy));
            flow = null;
        }

        // ---------------------------------------------------------------- step helpers

        private IEnumerator Explain(string key, TutorialTextSO.Hint hint, Func<Rect?> rect, params (string name, int value)[] numbers)
        {
            if (finalQueued) yield break;
            Enqueue("flow:" + key, Target.None, rect, hint, false, numbers);
            while ((showing || queue.Count > 0) && !finalQueued) yield return null;
        }

        // A thing to do: the line at the top and the blinking arrow until `done`, with only what Allow lets through
        private IEnumerator DoTask(string text, Func<Rect?> arrowAt, Func<bool> done)
        {
            if (finalQueued) yield break;
            taskArrowAt = arrowAt;
            SetTask(text);
            while (!done() && !finalQueued) yield return null;
            SetTask(null);
            taskArrowAt = null;
            Allow();
        }

        private IEnumerator KeyTask(string text, KeyCode key, Func<bool> done)
        {
            Allow(key: key);
            yield return DoTask(text, () => null, done);
        }

        private IEnumerator WaitPhase(TurnPhase phase)
        {
            while (turns != null && turns.CurrentPhase != phase && !finalQueued) yield return null;
        }

        // A hand card is still flying in or sliding to its place
        private bool HandMoving()
        {
            if (cards == null) return false;
            foreach (var c in cards.Hand)
            {
                var view = View(c);
                if (view == null || !view.IsInHand || DG.Tweening.DOTween.IsTweening(view.transform)) return true;
            }
            return false;
        }

        private bool Holding =>CardPlayController3D.Instance != null && CardPlayController3D.Instance.IsHoldingAny;

        private CardInstance InHand(string cardId)
        {
            if (cards == null) return null;
            foreach (var c in cards.Hand) if (c.cardId == cardId) return c;
            return null;
        }

        private static bool HoveredIs(CardInstance card)
        {
            var view = CardView3D.HoveredInHand;
            return card != null && view != null && view.IsHovered && view.CardData == card;
        }

        private static CardView3D View(CardInstance card)
        {
            if (card == null) return null;
            foreach (var v in FindObjectsByType<CardView3D>(FindObjectsSortMode.None)) if (v.CardData == card) return v;
            return null;
        }

        private static BoardSlotView SlotOf(CardInstance card)
        {
            var view = View(card);
            return view != null && view.transform.parent != null ? view.transform.parent.GetComponent<BoardSlotView>() : null;
        }

        private CardInstance EnemyFamiliar()
        {
            if (combat == null) return null;
            foreach (var c in combat.State.enemyBoardCards) if (c != null && c.cardType == CardType.Familiar && !c.IsDead) return c;
            return null;
        }

        private int EnemyFamiliarLane()
        {
            var slot = SlotOf(EnemyFamiliar());
            return slot != null ? slot.slotIndex : -1;
        }

        // ---------------------------------------------------------------- where things are on screen (pixels)

        private Rect? HandRect()
        {
            var area = new ScreenArea();
            if (cards != null)
                foreach (var view in FindObjectsByType<CardView3D>(FindObjectsSortMode.None))
                    if (view.IsInHand && view.CardData != null && Contains(cards.Hand, view.CardData)) area.AddRenderers(view.gameObject);
            return area.found ? area.rect : (Rect?)null;
        }

        private static Rect? Union(Rect? a, Rect? b)
        {
            if (!a.HasValue) return b;
            if (!b.HasValue) return a;
            return Rect.MinMaxRect(Mathf.Min(a.Value.xMin, b.Value.xMin), Mathf.Min(a.Value.yMin, b.Value.yMin),
                Mathf.Max(a.Value.xMax, b.Value.xMax), Mathf.Max(a.Value.yMax, b.Value.yMax));
        }

        private static Rect? CardRect(CardInstance card)
        {
            var view = View(card);
            if (view == null) return null;
            var area = new ScreenArea();
            area.AddRenderers(view.gameObject);
            return area.found ? area.rect : (Rect?)null;
        }

        private static Rect? LabelRect(TMP_Text label)
        {
            if (label == null) return null;
            var area = new ScreenArea();
            label.ForceMeshUpdate();
            area.AddBounds(label.bounds.size.sqrMagnitude > 0f
                ? new Bounds(label.transform.TransformPoint(label.bounds.center), Vector3.Scale(label.bounds.size, label.transform.lossyScale))
                : new Bounds(label.transform.position, Vector3.one * 0.2f));
            return area.found ? area.rect : (Rect?)null;
        }

        private static Rect? UiRect(string objectName)
        {
            var area = new ScreenArea();
            area.AddUi(objectName);
            return area.found ? area.rect : (Rect?)null;
        }

        // One side's board row; null = both rows
        private static Rect? SideRect(BoardSlotView.SlotSide? side)
        {
            var area = new ScreenArea();
            if (side == null || side == BoardSlotView.SlotSide.Player) AddSlots(ref area, BoardSlotView.SlotSide.Player);
            if (side == null || side == BoardSlotView.SlotSide.Enemy) AddSlots(ref area, BoardSlotView.SlotSide.Enemy);
            return area.found ? area.rect : (Rect?)null;
        }

        private static Rect? SlotRect(BoardSlotView.SlotSide side, int index)
        {
            var area = new ScreenArea();
            foreach (var slot in FindObjectsByType<BoardSlotView>(FindObjectsSortMode.None))
                if (slot.side == side && slot.slotIndex == index && slot.GetComponent<DeckPileView3D>() == null && !slot.name.Contains("Deck"))
                    area.AddRenderers(slot.gameObject);
            return area.found ? area.rect : (Rect?)null;
        }

        // Both slots of one lane
        private static Rect? LaneRect(int lane)
        {
            var area = new ScreenArea();
            foreach (var slot in FindObjectsByType<BoardSlotView>(FindObjectsSortMode.None))
                if (slot.slotIndex == lane && slot.GetComponent<DeckPileView3D>() == null && !slot.name.Contains("Deck"))
                    area.AddRenderers(slot.gameObject);
            return area.found ? area.rect : (Rect?)null;
        }

        private static Rect? PitRect()
        {
            if (CenterPitView3D.Instance == null) return null;
            var area = new ScreenArea();
            area.AddRenderers(CenterPitView3D.Instance.gameObject);
            return area.found ? area.rect : (Rect?)null;
        }

        // ---------------------------------------------------------------- the task line and the arrow

        private Canvas taskCanvas;
        private RectTransform taskBar, taskArrow;
        private TextMeshProUGUI taskText;
        private Func<Rect?> taskArrowAt;

        private void BuildTask()
        {
            var theme = UIThemeSO.Current;
            // Its own canvas: the explanation canvas fades out while a task is up
            taskCanvas = UiFactory.CreateOverlayCanvas("TutorialTaskCanvas", 401, transform);
            var group = taskCanvas.GetComponent<CanvasGroup>();
            group.blocksRaycasts = group.interactable = false; // nothing here takes a click

            var bar = UiFactory.CreateImage("TaskBar", taskCanvas.transform, new Color(0.08f, 0.05f, 0.04f, 0.92f));
            bar.raycastTarget = false;
            taskBar = bar.rectTransform;
            taskBar.anchorMin = taskBar.anchorMax = taskBar.pivot = new Vector2(0.5f, 1f);
            taskBar.anchoredPosition = new Vector2(0f, -175f); // under the enemy's khwan bar
            var outline = bar.gameObject.AddComponent<Outline>();
            outline.effectColor = theme.accent;
            outline.effectDistance = new Vector2(2f, -2f);
            var fit = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            fit.padding = new RectOffset(36, 36, 16, 16);
            fit.childControlWidth = fit.childControlHeight = true;
            var size = bar.gameObject.AddComponent<ContentSizeFitter>();
            size.horizontalFit = size.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            taskText = UiFactory.CreateText(taskBar, "Task", "", 32f, theme.accent, TextAlignmentOptions.Center, theme.titleFont != null ? theme.titleFont : theme.bodyFont);
            taskText.raycastTarget = false;

            var arrow = UiFactory.CreateImage("TaskArrow", taskCanvas.transform, theme.accent);
            arrow.sprite = ArrowSprite();
            arrow.raycastTarget = false;
            taskArrow = arrow.rectTransform;
            taskArrow.anchorMin = taskArrow.anchorMax = Vector2.zero;
            taskArrow.pivot = new Vector2(0.5f, 0f); // its tip at the bottom
            taskArrow.sizeDelta = new Vector2(70f, 80f);

            SetTask(null);
        }

        private void SetTask(string text)
        {
            if (taskBar == null) return;
            taskBar.gameObject.SetActive(!string.IsNullOrEmpty(text));
            taskText.text = text ?? "";
            taskArrow.gameObject.SetActive(false);
        }

        // The arrow bobs over its target and blinks; hidden while an explanation is up or when there is no target
        private void LateUpdate()
        {
            if (taskArrow == null) return;
            Rect? at = taskArrowAt != null && taskBar.gameObject.activeSelf && !showing ? taskArrowAt() : null;
            taskArrow.gameObject.SetActive(at.HasValue);
            if (taskText != null && taskBar.gameObject.activeSelf)
                taskText.alpha = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
            float scale = taskCanvas.scaleFactor > 0f ? taskCanvas.scaleFactor : 1f;
            PlaceTaskBar(at, scale);
            if (!at.HasValue) return;

            var r = at.Value;
            float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * 18f;
            taskArrow.anchoredPosition = new Vector2(r.center.x, Mathf.Min(r.yMax, Screen.height - 90f * scale)) / scale + new Vector2(0f, 8f + bob);
            var img = taskArrow.GetComponent<Image>();
            var c = img.color;
            c.a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f));
            img.color = c;
        }

        // Under the enemy's khwan bar, or, when what the arrow points at (an enemy card) is under there, just below it
        private void PlaceTaskBar(Rect? target, float scale)
        {
            var home = new Vector2(0f, -175f);
            if (!target.HasValue || !taskBar.gameObject.activeSelf) { taskBar.anchoredPosition = home; return; }
            var t = target.Value;
            Vector2 size = taskBar.rect.size * scale;
            float top = Screen.height + home.y * scale;
            var bar = Rect.MinMaxRect((Screen.width - size.x) * 0.5f, top - size.y, (Screen.width + size.x) * 0.5f, top);
            float pad = 110f * scale; // the arrow above the target too
            bool covers = bar.xMin < t.xMax && bar.xMax > t.xMin && bar.yMin < t.yMax + pad && bar.yMax > t.yMin;
            taskBar.anchoredPosition = covers ? new Vector2(0f, -(Screen.height - t.yMin) / scale - 24f) : home;
        }

        // A downward triangle with a dark rim, drawn once
        private static Sprite arrowSprite;
        private static Sprite ArrowSprite()
        {
            if (arrowSprite != null) return arrowSprite;
            const int w = 64, h = 72;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float halfWidth = (w * 0.5f - 2f) * (y / (float)(h - 1)); // 0 at the tip (bottom), widest at the top
                for (int x = 0; x < w; x++)
                {
                    float d = halfWidth - Mathf.Abs(x + 0.5f - w * 0.5f);
                    float a = Mathf.Clamp01(d);
                    bool rim = d < 4f || y > h - 5;
                    byte v = (byte)(rim ? 70 : 255);
                    px[y * w + x] = new Color32(v, v, v, (byte)(255f * a));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            arrowSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
            return arrowSprite;
        }
    }
}
