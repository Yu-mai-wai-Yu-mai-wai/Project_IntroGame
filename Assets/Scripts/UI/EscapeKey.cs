using UnityEngine;

namespace TawanOS.UI
{
    /// <summary>
    /// One Esc press, several listeners (plan task F7). Anything that uses Esc to cancel or close (a held card,
    /// a target choice, a detail panel, the settings window) reads it through <see cref="Use"/>, which marks the
    /// press as handled. The Pause menu runs in LateUpdate and opens only on a press nobody handled, so Esc
    /// cancels the innermost thing first and opens Pause last, whatever order the Updates ran in.
    /// </summary>
    public static class EscapeKey
    {
        private static int handledFrame = -1;

        /// <summary>True when Esc went down this frame; marks it handled. Call it only when the caller will act on it.</summary>
        public static bool Use()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return false;
            handledFrame = Time.frameCount;
            return true;
        }

        /// <summary>Esc went down this frame and no <see cref="Use"/> caller took it.</summary>
        public static bool PressedAndUnhandled => Input.GetKeyDown(KeyCode.Escape) && handledFrame != Time.frameCount;
    }
}
