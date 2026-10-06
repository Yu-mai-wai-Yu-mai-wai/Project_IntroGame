using TawanOS.Audio;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Plan B1/B2: every sound key the game asks for exists in the library, key normalising and dB maths are
    /// right, and volumes round-trip through PlayerPrefs. Menu: Tools/TawanOS/Tests/Audio Library.
    /// Batch: -executeMethod TawanOS.EditorTools.AudioLibraryTests.Run
    /// </summary>
    public static class AudioLibraryTests
    {
        // Keys from the scene table and the SFX event table in PLAN_to_21Oct.md (B1)
        private static readonly string[] RequiredKeys =
        {
            "bgm_title", "bgm_map", "bgm_event", "bgm_combat", "bgm_boss",
            "amb_map", "amb_event", "amb_combat", "amb_boss",
            "sfx_card_draw", "sfx_card_play", "sfx_card_shuffle", "sfx_card_hover",
            "sfx_merit_gain", "sfx_corruption_break", "sfx_corruption_thunder", "sfx_turn_end",
            "sfx_hit_taken", "sfx_khwan_shake", "sfx_attack", "sfx_fire_on", "sfx_fire_off", "sfx_scream"
        };

        [MenuItem("Tools/TawanOS/Tests/Audio Library")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;

            var library = AudioLibrarySO.Load();
            failures += Expect("AudioLibrary loads through Resources", library != null);
            if (library != null)
            {
                failures += Expect("AudioLibrary references MainMixer", library.mixer != null && library.mixer.name == "MainMixer");
                failures += Expect("MainMixer has a Master group", library.mixer != null && library.mixer.FindMatchingGroups("Master").Length > 0);
                foreach (string key in RequiredKeys)
                    failures += Expect($"key '{key}' has at least one clip", library.Contains(key));
            }

            failures += Expect("NormalizeKey strips _01", AudioManager.NormalizeKey("sfx_card_hover_01") == "sfx_card_hover");
            failures += Expect("NormalizeKey keeps plain names", AudioManager.NormalizeKey("sfx_attack") == "sfx_attack");
            failures += Expect("VolumeToDb(1) = 0 dB", Mathf.Approximately(AudioManager.VolumeToDb(1f), 0f));
            failures += Expect("VolumeToDb(0.5) is about -6 dB", Mathf.Abs(AudioManager.VolumeToDb(0.5f) + 6.0206f) < 0.01f);
            failures += Expect("VolumeToDb(0) = -80 dB", AudioManager.VolumeToDb(0f) == -80f);

            failures += Expect("Default music volume is below effects volume (effects are mastered quieter)",
                AudioManager.DefaultVolume(AudioChannel.Bgm) < AudioManager.DefaultVolume(AudioChannel.Sfx));

            // B2: save, read back; restore whatever the developer had set
            foreach (AudioChannel channel in System.Enum.GetValues(typeof(AudioChannel)))
            {
                string pref = AudioManager.PrefKey(channel);
                bool had = PlayerPrefs.HasKey(pref);
                float old = PlayerPrefs.GetFloat(pref, 1f);
                AudioManager.SaveVolume(channel, 0.37f);
                failures += Expect($"{channel} volume round-trips through PlayerPrefs", Mathf.Approximately(AudioManager.LoadVolume(channel), 0.37f));
                AudioManager.SaveVolume(channel, 5f);
                failures += Expect($"{channel} volume clamps to 1", AudioManager.LoadVolume(channel) == 1f);
                if (had) PlayerPrefs.SetFloat(pref, old); else PlayerPrefs.DeleteKey(pref);
            }

            if (failures == 0) Debug.Log("[AudioLibraryTests] PASS");
            else Debug.LogError($"[AudioLibraryTests] FAIL: {failures} check(s) failed.");
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
