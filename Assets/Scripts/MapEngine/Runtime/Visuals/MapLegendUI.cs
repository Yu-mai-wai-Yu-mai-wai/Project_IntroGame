using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TawanOS.UI;

namespace TawanOS.MapEngine
{
    /// <summary>
    /// Map Legend UI (Plan Task G2): Displays authentic Thai descriptions for all 7 node types on the map,
    /// styled cleanly with UIThemeSO (CI colors, Sarabun Thai font, WCAG AA contrast >= 4.5:1, zero emojis).
    /// Can be toggled with key 'L' or the toggle button, persisting state in PlayerPrefs.
    /// </summary>
    public class MapLegendUI : MonoBehaviour
    {
        public const string PrefsKeyVisible = "MapLegend_Visible";

        public static readonly NodeType[] AllNodeTypes = new[]
        {
            NodeType.MinorEnemy,
            NodeType.EliteEnemy,
            NodeType.RestSite,
            NodeType.Treasure,
            NodeType.Store,
            NodeType.Boss,
            NodeType.Event
        };

        public static string GetNodeTitle(NodeType type)
        {
            switch (type)
            {
                case NodeType.MinorEnemy: return "ศัตรูทั่วไป";
                case NodeType.EliteEnemy: return "ศัตรูระดับสูง";
                case NodeType.RestSite:   return "เมรุ";
                case NodeType.Treasure:   return "กองของเซ่น";
                case NodeType.Store:      return "ศาลเจ้า";
                case NodeType.Boss:       return "บอสใหญ่";
                case NodeType.Event:      return "หมอกดำ";
                default:                  return "เส้นทางลึกลับ";
            }
        }

        public static string GetNodeDescription(NodeType type)
        {
            switch (type)
            {
                case NodeType.MinorEnemy: return "การเผชิญหน้ากับบริวารผีร้าย เอาชนะเพื่อรับการ์ดรางวัล";
                case NodeType.EliteEnemy: return "มินิบอสสุดอันตราย เอาชนะเพื่อรับเครื่องรางล้ำค่า";
                case NodeType.RestSite:   return "จุดพักศักดิ์สิทธิ์ เผาทำลายการ์ดออกจากสำรับ หรือสวดชุบขวัญ";
                case NodeType.Treasure:   return "เครื่องเซ่นไหว้โบราณ บันทึกเรื่องเล่าและเลือกรับการ์ดใหม่";
                case NodeType.Store:      return "ศาลบูชาเร้นลับ แลกเปลี่ยนธูปเพื่อซื้อการ์ดและเครื่องราง";
                case NodeType.Boss:       return "เจ้าแห่งวิญญาณประจำชั้น ปราบให้สิ้นซากเพื่อผ่านด่าน";
                case NodeType.Event:      return "เหตุการณ์ลึกลับในสายหมอก การตัดสินใจจะเปลี่ยนชะตากรรม";
                default:                  return "จุดหมายที่ยังไม่มีข้อมูล";
            }
        }

        private GameObject panelRoot;
        private CanvasGroup panelGroup;
        private bool isVisible = true;

        public bool IsVisible => isVisible;

        private void Awake()
        {
            isVisible = PlayerPrefs.GetInt(PrefsKeyVisible, 1) == 1;
            BuildUI();
            UpdateVisibility();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.L) && !TawanOS.UI.PauseMenu.IsPaused)
            {
                Toggle();
            }
        }

        public void Toggle()
        {
            isVisible = !isVisible;
            PlayerPrefs.SetInt(PrefsKeyVisible, isVisible ? 1 : 0);
            PlayerPrefs.Save();
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            if (panelGroup != null)
            {
                panelGroup.alpha = isVisible ? 1f : 0f;
                panelGroup.blocksRaycasts = isVisible;
                panelGroup.interactable = isVisible;
            }
        }

        private void BuildUI()
        {
            var theme = UIThemeSO.Current;

            // The setup tool creates this object empty under the Canvas, so it has a zero-size rect at the
            // screen centre. Anchors on children resolve against that rect, which pushed the button to the
            // middle of the screen. Stretch it over the whole canvas so the anchors mean screen corners.
            var selfRect = GetComponent<RectTransform>();
            if (selfRect == null) selfRect = gameObject.AddComponent<RectTransform>();
            selfRect.anchorMin = Vector2.zero;
            selfRect.anchorMax = Vector2.one;
            selfRect.offsetMin = Vector2.zero;
            selfRect.offsetMax = Vector2.zero;

            // --- Toggle Button "สัญลักษณ์ (L)" ---
            var toggleGo = new GameObject("ToggleLegendButton", typeof(RectTransform), typeof(Image), typeof(Button));
            toggleGo.transform.SetParent(transform, false);
            var toggleRect = toggleGo.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(1f, 1f);
            toggleRect.anchorMax = new Vector2(1f, 1f);
            toggleRect.pivot = new Vector2(1f, 1f);
            toggleRect.anchoredPosition = new Vector2(-24f, -24f);
            toggleRect.sizeDelta = new Vector2(170f, 44f);

            var toggleImg = toggleGo.GetComponent<Image>();
            toggleImg.color = theme.crimson;

            var toggleBtn = toggleGo.GetComponent<Button>();
            toggleBtn.onClick.AddListener(Toggle);

            var btnInnerGo = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            btnInnerGo.transform.SetParent(toggleGo.transform, false);
            var innerRect = btnInnerGo.GetComponent<RectTransform>();
            UiFactory.Stretch(innerRect, 2f);
            btnInnerGo.GetComponent<Image>().color = theme.panel;

            var toggleText = UiFactory.CreateText(btnInnerGo.transform, "Label", "สัญลักษณ์ (L)",
                theme.labelSize, theme.accent, TextAlignmentOptions.Center, theme.bodyFont);
            UiFactory.Stretch(toggleText.rectTransform, 0f);

            // --- Legend Panel ---
            panelRoot = new GameObject("LegendPanel", typeof(RectTransform), typeof(CanvasGroup));
            panelRoot.transform.SetParent(transform, false);
            panelGroup = panelRoot.GetComponent<CanvasGroup>();

            var panelRect = panelRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-24f, -78f);
            panelRect.sizeDelta = new Vector2(660f, 716f); // wider and taller so rows are not packed together

            // Border & Background
            var border = UiFactory.CreateImage("Border", panelRoot.transform, theme.crimson);
            UiFactory.Stretch(border.rectTransform, 0f);

            var bg = UiFactory.CreateImage("Background", panelRoot.transform, theme.panel);
            UiFactory.Stretch(bg.rectTransform, 3f);

            // Title
            var titleText = UiFactory.CreateText(panelRoot.transform, "Title", "สัญลักษณ์และเส้นทางบนแผนที่",
                theme.bodySize, theme.accent, TextAlignmentOptions.TopLeft, theme.titleFont != null ? theme.titleFont : theme.bodyFont);
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.pivot = new Vector2(0f, 1f);
            titleText.rectTransform.anchoredPosition = new Vector2(26f, -16f);
            titleText.rectTransform.sizeDelta = new Vector2(-52f, 40f);

            // Divider Line
            var divider = UiFactory.CreateImage("Divider", panelRoot.transform, theme.crimson);
            divider.rectTransform.anchorMin = new Vector2(0f, 1f);
            divider.rectTransform.anchorMax = new Vector2(1f, 1f);
            divider.rectTransform.pivot = new Vector2(0f, 1f);
            divider.rectTransform.anchoredPosition = new Vector2(22f, -62f);
            divider.rectTransform.sizeDelta = new Vector2(-44f, 2f);

            // 7 Node Entries
            float yPos = -76f;
            foreach (var nodeType in AllNodeTypes)
            {
                var entryGo = new GameObject($"Entry_{nodeType}", typeof(RectTransform));
                entryGo.transform.SetParent(panelRoot.transform, false);

                var entryRect = entryGo.GetComponent<RectTransform>();
                entryRect.anchorMin = new Vector2(0f, 1f);
                entryRect.anchorMax = new Vector2(1f, 1f);
                entryRect.pivot = new Vector2(0f, 1f);
                entryRect.anchoredPosition = new Vector2(26f, yPos);
                entryRect.sizeDelta = new Vector2(-52f, 84f);

                // Title (Accent peach color, font size >= 20)
                var nameText = UiFactory.CreateText(entryGo.transform, "Name", GetNodeTitle(nodeType),
                    theme.labelSize + 2f, theme.accent, TextAlignmentOptions.TopLeft, theme.bodyFont);
                nameText.fontStyle = FontStyles.Bold;
                nameText.rectTransform.anchorMin = new Vector2(0f, 1f);
                nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
                nameText.rectTransform.pivot = new Vector2(0f, 1f);
                nameText.rectTransform.anchoredPosition = new Vector2(0f, 0f);
                nameText.rectTransform.sizeDelta = new Vector2(0f, 32f);

                // Description (Text gray color, font size >= 20)
                var descText = UiFactory.CreateText(entryGo.transform, "Desc", GetNodeDescription(nodeType),
                    theme.labelSize, theme.text, TextAlignmentOptions.TopLeft, theme.bodyFont);
                descText.rectTransform.anchorMin = new Vector2(0f, 1f);
                descText.rectTransform.anchorMax = new Vector2(1f, 1f);
                descText.rectTransform.pivot = new Vector2(0f, 1f);
                descText.lineSpacing = 8f;
                descText.rectTransform.anchoredPosition = new Vector2(0f, -36f);
                descText.rectTransform.sizeDelta = new Vector2(0f, 52f);

                yPos -= 90f;
            }
        }
    }
}
