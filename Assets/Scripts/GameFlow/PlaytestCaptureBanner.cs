using TawanOS.UI;
using TMPro;
using UnityEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Playtest only: a big "แคปหน้านี้ด้วย" across the top of the Game Over and Victory screens, so testers send a
    /// screenshot of the run's stats (time played and the rest). Turn <see cref="Enabled"/> off to take it out.
    /// </summary>
    public static class PlaytestCaptureBanner
    {
        public const bool Enabled = true;
        public const string Message = "แคปหน้านี้ด้วย";

        /// <summary>Puts the banner at the top of `screen` (a full-screen RectTransform), once.</summary>
        public static void Add(Transform screen)
        {
            if (!Enabled || screen == null || screen.Find("PlaytestCaptureBanner") != null) return;
            var theme = UIThemeSO.Current;
            var text = UiFactory.CreateText(screen, "PlaytestCaptureBanner", Message, 64f, new Color(1f, 0.28f, 0.22f),
                TextAlignmentOptions.Center, theme != null ? theme.titleFont : null);
            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.895f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, -10f); // room for the vowels above the line
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 255);
            text.raycastTarget = false;
            text.transform.SetAsLastSibling();
        }
    }
}
