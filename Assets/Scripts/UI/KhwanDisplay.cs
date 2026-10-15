using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.UI
{
    /// <summary>
    /// How Khwan is shown wherever the player's Khwan can be above max (ขวัญชั่วคราว from the วัด, Game Designer
    /// 15 Oct): the number is plain "25/20", and the bar stays full with a gold strip over it for the extra, so
    /// taking 5 visibly removes the gold while the bar never reads as a max of 25.
    /// </summary>
    public static class KhwanDisplay
    {
        public static readonly Color TempColour = new Color(1f, 0.8f, 0.35f);
        /// <summary>"25/20" or "17/20" (Game Designer: no separate "+5"). <paramref name="separator"/> goes between the numbers.</summary>
        public static string Text(int khwan, int max, string separator = "/") => $"{khwan}{separator}{max}";

        /// <summary>The bar's fill for the ordinary Khwan (0..1); the extra is the gold strip.</summary>
        public static float Fill(int khwan, int max) => max > 0 ? Mathf.Clamp01((float)khwan / max) : 0f;

        /// <summary>Adds the gold strip to a bar track, over its fill (create it after the fill). Starts hidden.</summary>
        public static RectTransform AddTempStrip(RectTransform track, float inset = 0f)
        {
            var go = new GameObject("TempKhwan", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(track, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            var image = go.GetComponent<Image>();
            image.color = TempColour;
            image.raycastTarget = false;
            go.SetActive(false);
            return rect;
        }

        /// <summary>Sizes the gold strip to the ขวัญชั่วคราว as a share of max, from the left; hidden when there is none.</summary>
        public static void SetTempStrip(RectTransform strip, int khwan, int max)
        {
            if (strip == null) return;
            int temp = Mathf.Max(0, khwan - max);
            strip.gameObject.SetActive(temp > 0);
            if (temp > 0) strip.anchorMax = new Vector2(Mathf.Clamp01((float)temp / max), 1f);
        }
    }
}
