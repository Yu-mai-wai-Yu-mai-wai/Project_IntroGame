using System;
using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.Settings
{
    public enum VolumeChannel
    {
        Master,
        Bgm,
        Ambience,
        Sfx,
    }

    public enum DisplayMode
    {
        Fullscreen,
        Borderless,
        Windowed,
    }

    public enum TextSpeed
    {
        Slow,
        Normal,
        Fast,
        Instant,
    }

    /// <summary>
    /// Player settings kept in PlayerPrefs (plan tasks B2 / F4): audio, display and gameplay. Applied before the
    /// first scene loads; every setter saves, applies at once and raises <see cref="OnChanged"/>.
    /// The AudioManager (task B1) reads the BGM / ambience / SFX volumes from here; master volume already works
    /// through <see cref="AudioListener.volume"/>.
    /// Display mode, resolution, V-Sync and quality only apply in a build: in the Editor they would change the
    /// Game view or the project's QualitySettings asset, so there they are saved but not applied.
    /// </summary>
    public static class GameSettings
    {
        // Audio
        public const string MasterVolumeKey = "vol_master";
        public const string BgmVolumeKey = "vol_bgm";
        public const string AmbienceVolumeKey = "vol_amb";
        public const string SfxVolumeKey = "vol_sfx";
        public const string MuteInBackgroundKey = "mute_in_background";
        // Display
        public const string DisplayModeKey = "display_mode";
        public const string LegacyFullscreenKey = "fullscreen";
        public const string ResolutionKey = "resolution";
        public const string VSyncKey = "vsync";
        public const string FrameLimitKey = "frame_limit";
        public const string QualityKey = "quality";
        public const string ShowFpsKey = "show_fps";
        // Gameplay
        public const string TextSpeedKey = "text_speed";
        public const string ReduceMotionKey = "reduce_motion";

        public const float DefaultVolume = 0.8f;
        public const int DefaultFrameLimitIndex = 1; // 60

        /// <summary>Frame-rate caps offered in the menu; 0 = unlimited.</summary>
        public static readonly int[] FrameLimits = { 30, 60, 120, 144, 0 };

        private static readonly float[] TextSpeedMultipliers = { 0.5f, 1f, 2f };

        public static event Action OnChanged;

        private static bool hasFocus = true;
        private static int defaultQualityLevel = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnStartup()
        {
            defaultQualityLevel = QualitySettings.GetQualityLevel();
            MigrateLegacyKeys();
            ApplyAudio();
            ApplyFrameRate();
            if (!Application.isEditor)
            {
                if (PlayerPrefs.HasKey(QualityKey)) QualitySettings.SetQualityLevel(QualityIndex, true);
                ApplyFrameRate(); // the quality level carries its own V-Sync value
                if (PlayerPrefs.HasKey(DisplayModeKey) || PlayerPrefs.HasKey(ResolutionKey)) ApplyDisplay();
            }
            SettingsRuntime.EnsureExists();
        }

        /// <summary>The first version stored only fullscreen on/off; turns that into a display mode once.</summary>
        public static void MigrateLegacyKeys()
        {
            if (PlayerPrefs.HasKey(DisplayModeKey) || !PlayerPrefs.HasKey(LegacyFullscreenKey)) return;
            bool fullscreen = PlayerPrefs.GetInt(LegacyFullscreenKey) == 1;
            PlayerPrefs.SetInt(DisplayModeKey, (int)(fullscreen ? DisplayMode.Borderless : DisplayMode.Windowed));
            PlayerPrefs.DeleteKey(LegacyFullscreenKey);
        }

        private static void Changed()
        {
            OnChanged?.Invoke();
        }

        // ---------------------------------------------------------------- audio

        public static float GetVolume(VolumeChannel channel)
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey(channel), DefaultVolume));
        }

        public static void SetVolume(VolumeChannel channel, float value)
        {
            PlayerPrefs.SetFloat(VolumeKey(channel), Mathf.Clamp01(value));
            ApplyAudio();
            Changed();
        }

        /// <summary>0..1 slider value to AudioMixer decibels (0 = silent at -80 dB).</summary>
        public static float ToDecibels(float value)
        {
            return value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;
        }

        public static string VolumeKey(VolumeChannel channel)
        {
            return channel switch
            {
                VolumeChannel.Bgm => BgmVolumeKey,
                VolumeChannel.Ambience => AmbienceVolumeKey,
                VolumeChannel.Sfx => SfxVolumeKey,
                _ => MasterVolumeKey,
            };
        }

        public static bool MuteInBackground
        {
            get => GetBool(MuteInBackgroundKey, true);
            set { SetBool(MuteInBackgroundKey, value); ApplyAudio(); Changed(); }
        }

        /// <summary>Called by <see cref="SettingsRuntime"/> when the game window gains or loses focus.</summary>
        public static void SetApplicationFocus(bool focused)
        {
            hasFocus = focused;
            ApplyAudio();
        }

        private static void ApplyAudio()
        {
            bool muted = !hasFocus && MuteInBackground;
            AudioListener.volume = muted ? 0f : GetVolume(VolumeChannel.Master);
        }

        // ---------------------------------------------------------------- display

        public static DisplayMode Mode
        {
            get
            {
                int saved = PlayerPrefs.GetInt(DisplayModeKey, -1);
                if (saved >= 0 && saved <= (int)DisplayMode.Windowed) return (DisplayMode)saved;
                return Screen.fullScreenMode switch
                {
                    FullScreenMode.ExclusiveFullScreen => DisplayMode.Fullscreen,
                    FullScreenMode.Windowed => DisplayMode.Windowed,
                    _ => DisplayMode.Borderless,
                };
            }
            set { PlayerPrefs.SetInt(DisplayModeKey, (int)value); ApplyDisplay(); Changed(); }
        }

        /// <summary>Screen sizes the monitor supports, smallest first, one entry per size (refresh rates merged).</summary>
        public static List<Vector2Int> Resolutions
        {
            get
            {
                var sizes = new List<Vector2Int>();
                foreach (var r in Screen.resolutions)
                {
                    var size = new Vector2Int(r.width, r.height);
                    if (!sizes.Contains(size)) sizes.Add(size);
                }
                if (sizes.Count == 0) sizes.Add(new Vector2Int(Screen.width, Screen.height));
                sizes.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
                return sizes;
            }
        }

        /// <summary>Index into <see cref="Resolutions"/>. Unsaved = the current window size, else the largest.</summary>
        public static int ResolutionIndex
        {
            get
            {
                var sizes = Resolutions;
                int saved = PlayerPrefs.GetInt(ResolutionKey, -1);
                if (saved >= 0 && saved < sizes.Count) return saved;
                int current = sizes.IndexOf(new Vector2Int(Screen.width, Screen.height));
                return current >= 0 ? current : sizes.Count - 1;
            }
            set { PlayerPrefs.SetInt(ResolutionKey, Mathf.Clamp(value, 0, Resolutions.Count - 1)); ApplyDisplay(); Changed(); }
        }

        public static bool VSync
        {
            get => GetBool(VSyncKey, true);
            set { SetBool(VSyncKey, value); ApplyFrameRate(); Changed(); }
        }

        /// <summary>Index into <see cref="FrameLimits"/>. Ignored while V-Sync is on.</summary>
        public static int FrameLimitIndex
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(FrameLimitKey, DefaultFrameLimitIndex), 0, FrameLimits.Length - 1);
            set { PlayerPrefs.SetInt(FrameLimitKey, Mathf.Clamp(value, 0, FrameLimits.Length - 1)); ApplyFrameRate(); Changed(); }
        }

        /// <summary>Index into QualitySettings.names. Unsaved = the project's current level.</summary>
        public static int QualityIndex
        {
            get
            {
                int saved = PlayerPrefs.GetInt(QualityKey, -1);
                return saved >= 0 && saved < QualitySettings.names.Length ? saved : QualitySettings.GetQualityLevel();
            }
            set
            {
                PlayerPrefs.SetInt(QualityKey, Mathf.Clamp(value, 0, QualitySettings.names.Length - 1));
                if (!Application.isEditor)
                {
                    QualitySettings.SetQualityLevel(QualityIndex, true);
                    ApplyFrameRate();
                }
                Changed();
            }
        }

        public static bool ShowFps
        {
            get => GetBool(ShowFpsKey, false);
            set { SetBool(ShowFpsKey, value); Changed(); }
        }

        private static void ApplyDisplay()
        {
            if (Application.isEditor) return;
            var size = Resolutions[ResolutionIndex];
            var mode = Mode switch
            {
                DisplayMode.Fullscreen => FullScreenMode.ExclusiveFullScreen,
                DisplayMode.Windowed => FullScreenMode.Windowed,
                _ => FullScreenMode.FullScreenWindow,
            };
            Screen.SetResolution(size.x, size.y, mode);
        }

        private static void ApplyFrameRate()
        {
            // Changing vSyncCount in the Editor would dirty ProjectSettings/QualitySettings.asset
            if (!Application.isEditor) QualitySettings.vSyncCount = VSync ? 1 : 0;
            int limit = FrameLimits[FrameLimitIndex];
            Application.targetFrameRate = VSync || limit == 0 ? -1 : limit;
        }

        // ---------------------------------------------------------------- gameplay

        public static TextSpeed TextSpeed
        {
            get => (TextSpeed)Mathf.Clamp(PlayerPrefs.GetInt(TextSpeedKey, (int)TextSpeed.Normal), 0, (int)TextSpeed.Instant);
            set { PlayerPrefs.SetInt(TextSpeedKey, (int)value); Changed(); }
        }

        /// <summary>A typewriter's base speed scaled by the player's text speed; Instant shows a whole page in one frame.</summary>
        public static float CharactersPerSecond(float baseRate)
        {
            var speed = TextSpeed;
            return speed == TextSpeed.Instant ? 100000f : baseRate * TextSpeedMultipliers[(int)speed];
        }

        /// <summary>Screen shake and other large movements are skipped when on.</summary>
        public static bool ReduceMotion
        {
            get => GetBool(ReduceMotionKey, false);
            set { SetBool(ReduceMotionKey, value); Changed(); }
        }

        // ---------------------------------------------------------------- defaults and persistence

        /// <summary>Writes PlayerPrefs to disk (Unity also does this on quit).</summary>
        public static void Save()
        {
            PlayerPrefs.Save();
        }

        public static void ResetAudio()
        {
            foreach (VolumeChannel channel in Enum.GetValues(typeof(VolumeChannel)))
            {
                PlayerPrefs.DeleteKey(VolumeKey(channel));
            }
            PlayerPrefs.DeleteKey(MuteInBackgroundKey);
            ApplyAudio();
            Changed();
        }

        /// <summary>Keeps the display mode and resolution: resetting those could leave the player on a broken screen.</summary>
        public static void ResetDisplay()
        {
            PlayerPrefs.DeleteKey(VSyncKey);
            PlayerPrefs.DeleteKey(FrameLimitKey);
            PlayerPrefs.DeleteKey(QualityKey);
            PlayerPrefs.DeleteKey(ShowFpsKey);
            if (!Application.isEditor && defaultQualityLevel >= 0) QualitySettings.SetQualityLevel(defaultQualityLevel, true);
            ApplyFrameRate();
            Changed();
        }

        public static void ResetGameplay()
        {
            PlayerPrefs.DeleteKey(TextSpeedKey);
            PlayerPrefs.DeleteKey(ReduceMotionKey);
            Changed();
        }

        private static bool GetBool(string key, bool fallback)
        {
            return PlayerPrefs.GetInt(key, fallback ? 1 : 0) == 1;
        }

        private static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
        }
    }
}
