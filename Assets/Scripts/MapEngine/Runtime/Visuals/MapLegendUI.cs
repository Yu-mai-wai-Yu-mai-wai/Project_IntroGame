using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TawanOS.UI;

namespace TawanOS.MapEngine
{
    /// <summary>
    /// The map's node names and descriptions in Thai for the 7 node types (Plan Task G2; Elite is no longer rolled and
    /// วัด took its row, Game Designer 11 Oct), shown as a pop-up beside whichever node the pointer is on, whether the
    /// player can go there, has not reached it yet or has passed it (Game Designer, 15 Oct: replaces the legend panel
    /// and its "สัญลักษณ์ (L)" button at the top right). The pop-up sits above the node, or below it near the top of the
    /// screen, kept inside the screen, and follows the node while the camera moves. Styled with UIThemeSO.
    /// </summary>
    public class MapLegendUI : MonoBehaviour
    {
        public static readonly NodeType[] AllNodeTypes = new[]
        {
            NodeType.MinorEnemy,
            NodeType.RestSite,
            NodeType.Temple,
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
                case NodeType.Temple:     return "วัด";
                case NodeType.Treasure:   return "กองของเซ่น";
                case NodeType.Store:      return "ศาลตายาย";
                case NodeType.Boss:       return "ใจกลางป่าช้า";
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
                case NodeType.RestSite:   return "ไฟเมรุไม่เคยดับ ชำระการ์ด 1 ใบในไฟให้แกร่งขึ้น (อัปเกรดการ์ด)";
                case NodeType.Temple:     return "ลานวัดสงบกลางป่า กราบพระเพื่อฟื้นขวัญเต็มหลอด ไม่เสียธูป";
                case NodeType.Treasure:   return "เครื่องเซ่นไหว้โบราณ บันทึกเรื่องเล่าและเลือกรับการ์ดใหม่";
                case NodeType.Store:      return "ศาลบูชาเร้นลับ แลกเปลี่ยนธูปเพื่อซื้อการ์ดและเครื่องราง";
                case NodeType.Boss:       return "โหนดสุดท้ายของแผนที่ ที่สถิตของผีตายโหง ไม่พบระหว่างทาง ปราบให้สิ้นเพื่อเรียกขวัญกลับคืน";
                case NodeType.Event:      return "เหตุการณ์ลึกลับในสายหมอก การตัดสินใจจะเปลี่ยนชะตากรรม";
                default:                  return "จุดหมายที่ยังไม่มีข้อมูล";
            }
        }

        [Tooltip("Width of the pop-up, in reference pixels (1920 wide screen).")]
        [SerializeField] private float popupWidth = 440f;
        [Tooltip("Gap between the node and the pop-up, in reference pixels.")]
        [SerializeField] private float popupGap = 46f;
        [SerializeField] private float fadeSeconds = 0.12f;

        private RectTransform selfRect;
        private RectTransform popup;
        private CanvasGroup popupGroup;
        private TextMeshProUGUI nameText;
        private TextMeshProUGUI descText;
        private MapNodeView shown;

        /// <summary>The node whose pop-up is up, or null.</summary>
        public MapNodeView Shown => shown;

        public void EnsureUI()
        {
            if (popup == null)
            {
                BuildUI();
            }
        }

        private void Awake()
        {
            EnsureUI();
        }

        private void OnEnable()
        {
            MapNodeView.PointerEntered += Show;
            MapNodeView.PointerLeft += Hide;
        }

        private void OnDisable()
        {
            MapNodeView.PointerEntered -= Show;
            MapNodeView.PointerLeft -= Hide;
        }

        public void Show(MapNodeView node)
        {
            if (node == null || node.NodeData == null || popup == null) return;
            shown = node;
            nameText.text = GetNodeTitle(node.NodeData.type);
            descText.text = GetNodeDescription(node.NodeData.type);
            // Height from the description's wrapped lines
            float textWidth = popupWidth - 40f;
            float descHeight = descText.GetPreferredValues(descText.text, textWidth, 0f).y;
            popup.sizeDelta = new Vector2(popupWidth, 20f + 34f + 6f + descHeight + 20f);
            Place();
            popupGroup.DOKill();
            popupGroup.DOFade(1f, fadeSeconds).SetLink(popup.gameObject);
        }

        public void Hide(MapNodeView node)
        {
            if (node != shown) return;
            shown = null;
            if (popupGroup == null) return;
            popupGroup.DOKill();
            popupGroup.DOFade(0f, fadeSeconds).SetLink(popup.gameObject);
        }

        private void LateUpdate()
        {
            if (shown != null) Place();
        }

        // Above the node; below it when there is no room above; slid sideways to stay inside the screen
        private void Place()
        {
            var cam = Camera.main;
            if (cam == null || shown == null) return;
            Vector3 screen = cam.WorldToScreenPoint(shown.transform.position);
            if (screen.z < 0f) return;
            var canvas = GetComponentInParent<Canvas>();
            var eventCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(selfRect, screen, eventCam, out var local)) return;

            var area = selfRect.rect;
            var size = popup.sizeDelta;
            bool above = local.y + popupGap + size.y <= area.yMax - 12f;
            float y = above ? local.y + popupGap + size.y * 0.5f : local.y - popupGap - size.y * 0.5f;
            float x = Mathf.Clamp(local.x, area.xMin + size.x * 0.5f + 12f, area.xMax - size.x * 0.5f - 12f);
            popup.anchoredPosition = new Vector2(x, y);
        }

        private void BuildUI()
        {
            var theme = UIThemeSO.Current;

            // The setup tool creates this object empty under the Canvas, so it has a zero-size rect at the
            // screen centre. Stretch it over the whole canvas so positions are measured from the screen's middle.
            selfRect = GetComponent<RectTransform>();
            if (selfRect == null) selfRect = gameObject.AddComponent<RectTransform>();
            selfRect.anchorMin = Vector2.zero;
            selfRect.anchorMax = Vector2.one;
            selfRect.offsetMin = Vector2.zero;
            selfRect.offsetMax = Vector2.zero;

            var go = new GameObject("NodePopup", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(transform, false);
            popup = (RectTransform)go.transform;
            popup.anchorMin = popup.anchorMax = new Vector2(0.5f, 0.5f);
            popup.pivot = new Vector2(0.5f, 0.5f);
            popup.sizeDelta = new Vector2(popupWidth, 120f);
            popupGroup = go.GetComponent<CanvasGroup>();
            popupGroup.alpha = 0f;
            popupGroup.blocksRaycasts = false; // never in the way of the pointer on the map
            popupGroup.interactable = false;

            var border = UiFactory.CreateImage("Border", popup, theme.crimson);
            UiFactory.Stretch(border.rectTransform, 0f);
            border.raycastTarget = false;
            var softBorder = border.gameObject.AddComponent<SoftEdgeImage>();
            softBorder.SetSides(0.06f, 0.06f, 0.06f, 0.06f);

            var bg = UiFactory.CreateImage("Background", popup, theme.panel);
            UiFactory.Stretch(bg.rectTransform, 3f);
            bg.raycastTarget = false;
            var softBg = bg.gameObject.AddComponent<SoftEdgeImage>();
            softBg.SetSides(0.06f, 0.06f, 0.06f, 0.06f);

            nameText = UiFactory.CreateText(popup, "Name", "", theme.labelSize + 4f, theme.accent, TextAlignmentOptions.TopLeft, theme.bodyFont);
            nameText.fontStyle = FontStyles.Bold;
            nameText.raycastTarget = false;
            var nameRect = nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.anchoredPosition = new Vector2(20f, -20f);
            nameRect.sizeDelta = new Vector2(-40f, 34f);

            descText = UiFactory.CreateText(popup, "Desc", "", theme.labelSize, theme.text, TextAlignmentOptions.TopLeft, theme.bodyFont);
            descText.textWrappingMode = TextWrappingModes.Normal;
            descText.lineSpacing = 8f;
            descText.raycastTarget = false;
            var descRect = descText.rectTransform;
            descRect.anchorMin = new Vector2(0f, 0f);
            descRect.anchorMax = new Vector2(1f, 1f);
            descRect.offsetMin = new Vector2(20f, 20f);
            descRect.offsetMax = new Vector2(-20f, -60f);
        }
    }
}
