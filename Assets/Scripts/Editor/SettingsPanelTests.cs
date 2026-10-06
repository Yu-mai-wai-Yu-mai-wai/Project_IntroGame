using System.IO;
using TawanOS.Audio;
using TawanOS.UI;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Plan B2: moving a volume slider saves to PlayerPrefs, reopening the panel shows the saved values
    /// (what "restart the game and the sliders are still there" relies on), the fullscreen toggle saves, and the
    /// main menu scene has the Settings button. Menu: Tools/TawanOS/Tests/Settings Panel.
    /// Batch: -executeMethod TawanOS.EditorTools.SettingsPanelTests.Run
    /// </summary>
    public static class SettingsPanelTests
    {
        [MenuItem("Tools/TawanOS/Tests/Settings Panel")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;

            // Remember what the developer had so the test leaves no trace
            var saved = new System.Collections.Generic.Dictionary<string, float>();
            foreach (AudioChannel c in System.Enum.GetValues(typeof(AudioChannel)))
                if (PlayerPrefs.HasKey(AudioManager.PrefKey(c))) saved[AudioManager.PrefKey(c)] = PlayerPrefs.GetFloat(AudioManager.PrefKey(c));
            bool hadFullscreen = PlayerPrefs.HasKey(GameSettings.PrefFullscreen);
            int oldFullscreen = PlayerPrefs.GetInt(GameSettings.PrefFullscreen, 0);

            var canvasGo = new GameObject("TestCanvas", typeof(Canvas));
            try
            {
                var panel = SettingsPanelUI.Create(canvasGo.transform);
                failures += Expect("Panel is created closed", panel != null && !panel.IsOpen);
                panel.Open();
                failures += Expect("Panel opens", panel.IsOpen);

                panel.masterSlider.value = 0.42f;
                panel.musicSlider.value = 0.13f;
                panel.sfxSlider.value = 0.77f;
                failures += Expect("Master slider saves to PlayerPrefs", Mathf.Approximately(PlayerPrefs.GetFloat(AudioManager.PrefMaster), 0.42f));
                failures += Expect("Music slider saves to PlayerPrefs", Mathf.Approximately(PlayerPrefs.GetFloat(AudioManager.PrefBgm), 0.13f));
                failures += Expect("Effects slider saves to PlayerPrefs", Mathf.Approximately(PlayerPrefs.GetFloat(AudioManager.PrefSfx), 0.77f));

                // Simulate a restart: a brand new panel must show the saved values
                panel.Close();
                Object.DestroyImmediate(panel.gameObject);
                var again = SettingsPanelUI.Create(canvasGo.transform);
                again.Open();
                failures += Expect("Reopened panel shows saved master volume", Mathf.Approximately(again.masterSlider.value, 0.42f));
                failures += Expect("Reopened panel shows saved music volume", Mathf.Approximately(again.musicSlider.value, 0.13f));
                failures += Expect("Reopened panel shows saved effects volume", Mathf.Approximately(again.sfxSlider.value, 0.77f));

                again.fullscreenToggle.isOn = !again.fullscreenToggle.isOn;
                failures += Expect("Fullscreen toggle saves to PlayerPrefs",
                    PlayerPrefs.GetInt(GameSettings.PrefFullscreen, -1) == (again.fullscreenToggle.isOn ? 1 : 0));

                // 44 px minimum touch/click target at the 1920x1080 reference
                var rect = (RectTransform)again.closeButton.transform;
                failures += Expect("Close button is at least 44 px tall at reference size", rect.anchorMax.y - rect.anchorMin.y > 0 && (rect.anchorMax.y - rect.anchorMin.y) * 0.6f * 1080f >= 44f);

                again.Close();
                failures += Expect("Panel closes", !again.IsOpen);
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
                foreach (AudioChannel c in System.Enum.GetValues(typeof(AudioChannel)))
                {
                    string key = AudioManager.PrefKey(c);
                    if (saved.TryGetValue(key, out float v)) PlayerPrefs.SetFloat(key, v); else PlayerPrefs.DeleteKey(key);
                }
                if (hadFullscreen) PlayerPrefs.SetInt(GameSettings.PrefFullscreen, oldFullscreen); else PlayerPrefs.DeleteKey(GameSettings.PrefFullscreen);
                PlayerPrefs.Save();
            }

            string menuScene = File.Exists("Assets/Scenes/MainMenu.unity") ? File.ReadAllText("Assets/Scenes/MainMenu.unity") : "";
            failures += Expect("MainMenu scene has the Settings button", menuScene.Contains("SettingsButton"));

            if (failures == 0) Debug.Log("[SettingsPanelTests] PASS");
            else Debug.LogError($"[SettingsPanelTests] FAIL: {failures} check(s) failed.");
            if (exitOnFinish) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Expect(string name, bool ok)
        {
            if (ok) { Debug.Log($"  PASS {name}"); return 0; }
            Debug.LogError($"  FAIL {name}");
            return 1;
        }
    }
}
