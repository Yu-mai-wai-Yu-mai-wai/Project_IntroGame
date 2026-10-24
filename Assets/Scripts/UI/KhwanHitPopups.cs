using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TawanOS.UI
{
    /// <summary>
    /// Khwan the player loses in a fight shows as a small line just above the player's khwan bar, one per hit
    /// ("−3 ขวัญ กุมารทอง"), rising a little and fading (Game Designer, 25 Oct: not a big warning at the bottom of the
    /// screen). PlayerNotice sends khwan-loss notices here. Newer lines push the older ones up.
    /// </summary>
    public class KhwanHitPopups : MonoBehaviour
    {
        private const float LifeSeconds = 1.8f, FadeSeconds = 0.5f, LineHeight = 30f, Rise = 18f;
        private static readonly Color HitColor = new Color(1f, 0.38f, 0.32f);

        private class Line { public TextMeshProUGUI text; public float age; }

        private static KhwanHitPopups instance;
        private readonly List<Line> lines = new List<Line>();
        private RectTransform layer;

        public static void Show(string message)
        {
            if (instance == null)
            {
                if (!Application.isPlaying) return;
                instance = new GameObject("KhwanHitPopups").AddComponent<KhwanHitPopups>();
            }
            instance.Add(message);
        }

        private void Awake()
        {
            var canvas = UiFactory.CreateOverlayCanvas("KhwanHitCanvas", 70, transform);
            canvas.GetComponent<CanvasGroup>().blocksRaycasts = false;
            layer = UiFactory.CreateRect("Lines", canvas.transform);
            UiFactory.Stretch(layer, 0f);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Add(string message)
        {
            var theme = UIThemeSO.Current;
            var text = UiFactory.CreateText(layer, "Hit", message, 26f, HitColor, TextAlignmentOptions.BottomLeft,
                theme != null ? theme.titleFont : null);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.outlineWidth = 0.25f;
            text.outlineColor = new Color32(0, 0, 0, 255);
            text.raycastTarget = false;
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.sizeDelta = new Vector2(420f, LineHeight);
            lines.Add(new Line { text = text });
            Place();
        }

        private void LateUpdate()
        {
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                var line = lines[i];
                line.age += Time.unscaledDeltaTime;
                if (line.text == null || line.age >= LifeSeconds)
                {
                    if (line.text != null) Destroy(line.text.gameObject);
                    lines.RemoveAt(i);
                    continue;
                }
                line.text.alpha = Mathf.Min(1f, line.age / 0.1f) * Mathf.Clamp01((LifeSeconds - line.age) / FadeSeconds);
            }
            if (lines.Count > 0) Place();
        }

        // Just above the top-left corner of the player's khwan panel (or the screen's lower left without one)
        private void Place()
        {
            Vector2 at = new Vector2(30f, 250f);
            var panel = GameObject.Find("PlayerPanel");
            if (panel != null && panel.activeInHierarchy && panel.transform is RectTransform prt)
            {
                var corners = new Vector3[4];
                prt.GetWorldCorners(corners); // overlay canvas: screen pixels
                RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, corners[1], null, out Vector2 local);
                at = local - layer.rect.min + new Vector2(0f, 6f);
            }
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.text == null) continue;
                int fromNewest = lines.Count - 1 - i;
                float rise = Rise * Mathf.Clamp01(line.age / LifeSeconds);
                line.text.rectTransform.anchoredPosition = at + new Vector2(0f, fromNewest * LineHeight + rise);
            }
        }
    }
}
