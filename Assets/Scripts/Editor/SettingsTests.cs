using System.Collections.Generic;
using TawanOS.Settings;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Checks plan task B2 without Play mode: every setting written through GameSettings reads back from
    /// PlayerPrefs under its key, values are clamped, resets restore defaults, the dB conversion and the text
    /// speed scale. The player's own PlayerPrefs values are restored afterwards.
    /// Menu: Tools/TawanOS/Tests/Settings. Batch: -executeMethod TawanOS.EditorTools.SettingsTests.Run (exit code 1 on failure).
    /// </summary>
    public static class SettingsTests
    {
        private static readonly string[] FloatKeys =
        {
            GameSettings.MasterVolumeKey, GameSettings.BgmVolumeKey, GameSettings.AmbienceVolumeKey, GameSettings.SfxVolumeKey,
        };

        private static readonly string[] IntKeys =
        {
            GameSettings.MuteInBackgroundKey, GameSettings.DisplayModeKey, GameSettings.LegacyFullscreenKey, GameSettings.ResolutionKey,
            GameSettings.VSyncKey, GameSettings.FrameLimitKey, GameSettings.QualityKey, GameSettings.ShowFpsKey,
            GameSettings.TextSpeedKey, GameSettings.ReduceMotionKey,
        };

        [MenuItem("Tools/TawanOS/Tests/Settings")]
        public static void RunFromMenu()
        {
            Execute(false);
        }

        public static void Run()
        {
            Execute(true);
        }

        private static void Execute(bool exitOnFinish)
        {
            var savedFloats = new Dictionary<string, float>();
            var savedInts = new Dictionary<string, int>();
            foreach (var key in FloatKeys) if (PlayerPrefs.HasKey(key)) savedFloats[key] = PlayerPrefs.GetFloat(key);
            foreach (var key in IntKeys) if (PlayerPrefs.HasKey(key)) savedInts[key] = PlayerPrefs.GetInt(key);
            float savedListener = AudioListener.volume;
            int savedTargetFrameRate = Application.targetFrameRate;

            int failures = 0;
            try
            {
                // --- audio: plan keys read back, clamped, reset
                GameSettings.SetVolume(VolumeChannel.Master, 0.25f);
                GameSettings.SetVolume(VolumeChannel.Bgm, 0.5f);
                GameSettings.SetVolume(VolumeChannel.Ambience, 0.6f);
                GameSettings.SetVolume(VolumeChannel.Sfx, 0.75f);
                failures += Expect("vol_master reads back 0.25", Mathf.Approximately(PlayerPrefs.GetFloat("vol_master"), 0.25f));
                failures += Expect("vol_bgm reads back 0.5", Mathf.Approximately(PlayerPrefs.GetFloat("vol_bgm"), 0.5f));
                failures += Expect("vol_amb reads back 0.6", Mathf.Approximately(PlayerPrefs.GetFloat("vol_amb"), 0.6f));
                failures += Expect("vol_sfx reads back 0.75", Mathf.Approximately(PlayerPrefs.GetFloat("vol_sfx"), 0.75f));
                failures += Expect("master volume drives AudioListener", Mathf.Approximately(AudioListener.volume, 0.25f));

                GameSettings.SetVolume(VolumeChannel.Sfx, 1.7f);
                failures += Expect("volume above 1 is clamped", Mathf.Approximately(GameSettings.GetVolume(VolumeChannel.Sfx), 1f));
                GameSettings.SetVolume(VolumeChannel.Sfx, -0.3f);
                failures += Expect("volume below 0 is clamped", Mathf.Approximately(GameSettings.GetVolume(VolumeChannel.Sfx), 0f));

                GameSettings.MuteInBackground = false;
                GameSettings.SetApplicationFocus(false);
                failures += Expect("unfocused window keeps sound when mute-in-background is off", Mathf.Approximately(AudioListener.volume, 0.25f));
                GameSettings.MuteInBackground = true;
                failures += Expect("unfocused window is silent when mute-in-background is on", Mathf.Approximately(AudioListener.volume, 0f));
                GameSettings.SetApplicationFocus(true);
                failures += Expect("focus brings the master volume back", Mathf.Approximately(AudioListener.volume, 0.25f));

                GameSettings.ResetAudio();
                failures += Expect("audio reset restores the default volume", Mathf.Approximately(GameSettings.GetVolume(VolumeChannel.Master), GameSettings.DefaultVolume));
                failures += Expect("audio reset turns mute-in-background back on", GameSettings.MuteInBackground);

                failures += Expect("0 is silent (-80 dB)", Mathf.Approximately(GameSettings.ToDecibels(0f), -80f));
                failures += Expect("1 is 0 dB", Mathf.Approximately(GameSettings.ToDecibels(1f), 0f));
                failures += Expect("0.5 is about -6 dB", Mathf.Abs(GameSettings.ToDecibels(0.5f) + 6.02f) < 0.01f);

                // --- display
                foreach (DisplayMode mode in System.Enum.GetValues(typeof(DisplayMode)))
                {
                    GameSettings.Mode = mode;
                    failures += Expect($"display mode {mode} persists", GameSettings.Mode == mode && PlayerPrefs.GetInt("display_mode") == (int)mode);
                }

                PlayerPrefs.DeleteKey(GameSettings.DisplayModeKey);
                PlayerPrefs.SetInt(GameSettings.LegacyFullscreenKey, 0);
                GameSettings.MigrateLegacyKeys();
                failures += Expect("old 'fullscreen' key migrates to display_mode", GameSettings.Mode == DisplayMode.Windowed && !PlayerPrefs.HasKey(GameSettings.LegacyFullscreenKey));

                int count = GameSettings.Resolutions.Count;
                failures += Expect("at least one resolution", count > 0);
                GameSettings.ResolutionIndex = count - 1;
                failures += Expect("resolution index persists", PlayerPrefs.GetInt("resolution") == count - 1 && GameSettings.ResolutionIndex == count - 1);
                GameSettings.ResolutionIndex = count + 10;
                failures += Expect("resolution index is clamped", GameSettings.ResolutionIndex == count - 1);

                GameSettings.VSync = false;
                GameSettings.FrameLimitIndex = 0;
                failures += Expect("frame limit caps the frame rate when V-Sync is off", Application.targetFrameRate == GameSettings.FrameLimits[0]);
                GameSettings.FrameLimitIndex = System.Array.IndexOf(GameSettings.FrameLimits, 0);
                failures += Expect("unlimited frame rate is -1", Application.targetFrameRate == -1);
                GameSettings.FrameLimitIndex = 0;
                GameSettings.VSync = true;
                failures += Expect("V-Sync on ignores the frame limit", Application.targetFrameRate == -1 && PlayerPrefs.GetInt("vsync") == 1);

                GameSettings.QualityIndex = QualitySettings.names.Length - 1;
                failures += Expect("quality index persists", PlayerPrefs.GetInt("quality") == QualitySettings.names.Length - 1);

                GameSettings.ShowFps = true;
                failures += Expect("show FPS persists", PlayerPrefs.GetInt("show_fps") == 1);
                GameSettings.ResetDisplay();
                failures += Expect("display reset turns FPS off and V-Sync on", !GameSettings.ShowFps && GameSettings.VSync);

                // --- gameplay
                GameSettings.TextSpeed = TextSpeed.Fast;
                failures += Expect("text speed persists", PlayerPrefs.GetInt("text_speed") == (int)TextSpeed.Fast);
                failures += Expect("fast text doubles the typing speed", Mathf.Approximately(GameSettings.CharactersPerSecond(40f), 80f));
                GameSettings.TextSpeed = TextSpeed.Slow;
                failures += Expect("slow text halves the typing speed", Mathf.Approximately(GameSettings.CharactersPerSecond(40f), 20f));
                GameSettings.TextSpeed = TextSpeed.Instant;
                failures += Expect("instant text shows a long page in one frame at 30 FPS", GameSettings.CharactersPerSecond(40f) / 30f > 1000f);

                GameSettings.ReduceMotion = true;
                failures += Expect("reduce motion persists", PlayerPrefs.GetInt("reduce_motion") == 1);
                GameSettings.ResetGameplay();
                failures += Expect("gameplay reset restores normal text speed and motion", GameSettings.TextSpeed == TextSpeed.Normal && !GameSettings.ReduceMotion);
            }
            finally
            {
                foreach (var key in FloatKeys)
                {
                    if (savedFloats.TryGetValue(key, out float v)) PlayerPrefs.SetFloat(key, v);
                    else PlayerPrefs.DeleteKey(key);
                }
                foreach (var key in IntKeys)
                {
                    if (savedInts.TryGetValue(key, out int v)) PlayerPrefs.SetInt(key, v);
                    else PlayerPrefs.DeleteKey(key);
                }
                PlayerPrefs.Save();
                GameSettings.SetApplicationFocus(true);
                AudioListener.volume = savedListener;
                Application.targetFrameRate = savedTargetFrameRate;
            }

            Debug.Log(failures == 0 ? "[SettingsTests] PASS" : $"[SettingsTests] FAIL: {failures} check(s) failed");
            if (exitOnFinish) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Expect(string name, bool condition)
        {
            Debug.Log($"[SettingsTests] {(condition ? "ok  " : "FAIL")} {name}");
            return condition ? 0 : 1;
        }
    }
}
