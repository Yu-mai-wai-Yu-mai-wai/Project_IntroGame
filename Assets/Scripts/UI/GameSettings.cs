using UnityEngine;

namespace TawanOS.UI
{
    /// <summary>Display settings kept in PlayerPrefs. Volumes live in TawanOS.Audio.AudioManager.</summary>
    public static class GameSettings
    {
        public const string PrefFullscreen = "fullscreen";

        public static bool Fullscreen => PlayerPrefs.GetInt(PrefFullscreen, Screen.fullScreen ? 1 : 0) == 1;

        public static void SetFullscreen(bool on)
        {
            PlayerPrefs.SetInt(PrefFullscreen, on ? 1 : 0);
            PlayerPrefs.Save();
            Screen.fullScreen = on;
        }

        // Only a saved choice overrides the build's default window mode.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySaved()
        {
            if (PlayerPrefs.HasKey(PrefFullscreen)) Screen.fullScreen = PlayerPrefs.GetInt(PrefFullscreen) == 1;
        }
    }
}
