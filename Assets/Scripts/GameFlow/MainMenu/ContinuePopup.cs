using System;
using TawanOS.MapEngine;
using TawanOS.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// The window the main menu shows before it continues a saved run: where the run stands (floor and node),
    /// Khwan, incense and deck size, and the mode, so the player knows what they are resuming. Built in code with
    /// the theme and the same border-plus-panel look as the map HUD. Enter continues, Esc cancels.
    /// </summary>
    public class ContinuePopup : MonoBehaviour
    {
        public const int SortingOrder = 450;

        private static ContinuePopup current;
        public static bool IsOpen => current != null;

        private Action onContinue;
        private Action onCancel;

        public static void Show(RunState.SaveSummary summary, Action onContinue, Action onCancel = null)
        {
            if (current != null) return;
            var canvas = UiFactory.CreateOverlayCanvas("ContinuePopup", SortingOrder);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var group = canvas.GetComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            group.interactable = true;
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(canvas.transform, false);
            }
            current = canvas.gameObject.AddComponent<ContinuePopup>();
            current.onContinue = onContinue;
            current.onCancel = onCancel;
            current.Build(summary);
        }

        private void OnDestroy()
        {
            if (current == this) current = null;
        }

        private void Update()
        {
            if (EscapeKey.Use()) Cancel();
        }

        private void Cancel()
        {
            var cb = onCancel;
            Destroy(gameObject);
            cb?.Invoke();
        }

        private void Confirm()
        {
            var cb = onContinue;
            Destroy(gameObject);
            cb?.Invoke();
        }

        // ---------------------------------------------------------------- text

        /// <summary>Where the run stands, in words ("ชั้นที่ 3 จาก 7 · เมรุ").</summary>
        public static string DescribePosition(RunState.SaveSummary s)
        {
            if (!s.mapKnown || s.floor < 0) return "จุดเริ่มต้นของแผนที่ ยังไม่ได้เดินไปโหนดแรก";
            if (s.atBoss) return "ด่านสุดท้าย · " + NodeName(s.nodeType);
            return $"ชั้นที่ {s.floor + 1} จาก {s.totalFloors} · {NodeName(s.nodeType)}";
        }

        /// <summary>What happens first after Continue (Continue re-enters the node the player quit inside).</summary>
        public static string DescribeResume(RunState.SaveSummary s)
        {
            switch (s.resume)
            {
                case ResumeKind.Combat: return "กลับเข้าการต่อสู้ที่ค้างไว้ · " + NodeName(s.resumeNodeType);
                case ResumeKind.Event: return "กลับเข้า" + NodeName(s.resumeNodeType) + "ที่ค้างไว้";
                case ResumeKind.Shop: return "กลับเข้าศาลที่ค้างไว้";
                case ResumeKind.Meru: return "กลับเข้าเมรุที่ค้างไว้";
                default: return "กลับไปที่แผนที่";
            }
        }

        public static string NodeName(NodeType type)
        {
            switch (type)
            {
                case NodeType.MinorEnemy: return "ศัตรูทั่วไป";
                case NodeType.EliteEnemy: return "ศัตรูพิเศษ";
                case NodeType.RestSite: return "เมรุ";
                case NodeType.Treasure: return "กองของเซ่น";
                case NodeType.Store: return "ศาล";
                case NodeType.Boss: return "บอส";
                case NodeType.Event: return "หมอกดำ";
                default: return type.ToString();
            }
        }

        // ---------------------------------------------------------------- layout (1920x1080 reference)

        private void Build(RunState.SaveSummary s)
        {
            var theme = UIThemeSO.Current;

            var dim = UiFactory.CreateImage("Dim", transform, new Color(0f, 0f, 0f, 0.75f));
            UiFactory.Stretch(dim.rectTransform, 0f);
            dim.raycastTarget = true; // blocks clicks on the menu behind

            var border = UiFactory.CreateImage("Border", transform, theme.crimson);
            Place(border.rectTransform, Vector2.zero, new Vector2(808f, 608f));
            var window = UiFactory.CreateImage("Window", transform, theme.panel);
            window.raycastTarget = true;
            var w = window.rectTransform;
            Place(w, Vector2.zero, new Vector2(800f, 600f));

            var title = UiFactory.CreateText(w, "Title", "เล่นต่อจากเกมที่บันทึกไว้", theme.titleSize, theme.accent, TextAlignmentOptions.Center, theme.titleFont);
            Place(title.rectTransform, new Vector2(0f, 240f), new Vector2(740f, 70f));

            Row(w, theme, 150f, "ตำแหน่ง", DescribePosition(s));
            Row(w, theme, 80f, "ขวัญ", $"{s.currentHp}/{s.maxHp}");
            Row(w, theme, 10f, "ธูป", s.incense.ToString());
            Row(w, theme, -60f, "สำรับ", $"{s.deckCards} ใบ");
            Row(w, theme, -130f, "โหมด", s.shortMode ? "เล่นสั้น 4 ชั้น" : "เล่นเต็ม 7 ชั้น");

            var next = UiFactory.CreateText(w, "Next", "เมื่อเริ่ม: " + DescribeResume(s), theme.labelSize, theme.text, TextAlignmentOptions.Center, theme.bodyFont);
            Place(next.rectTransform, new Vector2(0f, -195f), new Vector2(740f, 36f));

            var yes = MakeButton(w, theme, "ContinueButton", "เล่นต่อ", new Vector2(-150f, -250f), new Vector2(260f, 60f), Confirm);
            MakeButton(w, theme, "CancelButton", "ยกเลิก", new Vector2(150f, -250f), new Vector2(260f, 60f), Cancel);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(yes.gameObject); // Enter / Space confirms
        }

        private static void Row(RectTransform parent, UIThemeSO theme, float y, string label, string value)
        {
            var l = UiFactory.CreateText(parent, "Label_" + label, label, theme.bodySize, theme.text, TextAlignmentOptions.MidlineLeft, theme.bodyFont);
            Place(l.rectTransform, new Vector2(-250f, y), new Vector2(200f, 56f));
            var v = UiFactory.CreateText(parent, "Value_" + label, value, theme.bodySize * 1.1f, theme.accent, TextAlignmentOptions.MidlineLeft, theme.bodyFont);
            Place(v.rectTransform, new Vector2(100f, y), new Vector2(500f, 56f));
        }

        // Border (the button's graphic) around a panel-coloured inner with an accent label, like the map HUD buttons
        private static Button MakeButton(RectTransform parent, UIThemeSO theme, string name, string label, Vector2 position, Vector2 size, Action onClick)
        {
            var image = UiFactory.CreateImage(name, parent, Color.white);
            image.raycastTarget = true;
            Place(image.rectTransform, position, size);
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = theme.crimson;
            colors.highlightedColor = Color.Lerp(theme.crimson, theme.accent, 0.35f);
            colors.selectedColor = theme.accent; // keyboard focus
            colors.pressedColor = Color.Lerp(theme.crimson, theme.accent, 0.6f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());

            var inner = UiFactory.CreateImage("Inner", image.rectTransform, theme.panel);
            UiFactory.Stretch(inner.rectTransform, 2f);
            var text = UiFactory.CreateText(inner.rectTransform, "Label", label, theme.bodySize * 1.15f, theme.accent, TextAlignmentOptions.Center, theme.bodyFont);
            UiFactory.Stretch(text.rectTransform, 0f);
            return button;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
    }
}
