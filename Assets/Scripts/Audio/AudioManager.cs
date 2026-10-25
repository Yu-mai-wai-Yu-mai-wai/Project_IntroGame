using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TawanOS.Audio
{
    public enum AudioChannel { Master, Bgm, Sfx }

    /// <summary>
    /// Plays music, ambience and one-shot sound effects by key. Created automatically before the first scene
    /// (same pattern as GameFlowManager) and kept across scene loads. Volumes persist in PlayerPrefs.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public const string PrefMaster = "vol_master";
        public const string PrefBgm = "vol_bgm";
        public const string PrefSfx = "vol_sfx";
        public const float CrossfadeSeconds = 1f;

        private static readonly Regex VariantSuffix = new Regex(@"_\d{2}$", RegexOptions.Compiled);

        public static AudioManager Instance { get; private set; }

        // ponytail: upgraded from per-AudioSource volume to mixer exposed parameters (MasterVol/BgmVol/SfxVol).
        // Falls back to AudioSource.volume when mixer is absent (test environments).
        private AudioSource bgmA, bgmB, ambience, sfx;
        private AudioSource bgmActive;
        private string currentBgmKey, currentAmbienceKey;
        private Coroutine crossfade, ambienceFade;
        private AudioSource ambienceOld; // the ambience fading out under the new one
        private AudioLibrarySO library;
        private UnityEngine.Audio.AudioMixer mixer;
        private UnityEngine.Audio.AudioMixerGroup masterGroup, bgmGroup, sfxGroup;
        private readonly HashSet<string> warned = new HashSet<string>();

        private const string MixerParamMaster = "MasterVol";
        private const string MixerParamBgm = "BgmVol";
        private const string MixerParamSfx = "SfxVol";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("AudioManager");
            go.AddComponent<AudioManager>();
            go.AddComponent<AudioDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            library = AudioLibrarySO.Load();
            if (library == null) Debug.LogWarning("[AudioManager] Resources/AudioLibrary not found. Run Tools/TawanOS/Audio/Build Audio Library.");
            if (library != null && library.mixer != null)
            {
                mixer = library.mixer;
                var masterGroups = mixer.FindMatchingGroups("Master");
                if (masterGroups.Length > 0) masterGroup = masterGroups[0];
                var bgmGroups = mixer.FindMatchingGroups("BgmVol");
                if (bgmGroups.Length > 0) bgmGroup = bgmGroups[0];
                var sfxGroups = mixer.FindMatchingGroups("SfxVol");
                if (sfxGroups.Length > 0) sfxGroup = sfxGroups[0];
            }

            bgmA = NewSource("BgmA", loop: true, bgmGroup ?? masterGroup);
            bgmB = NewSource("BgmB", loop: true, bgmGroup ?? masterGroup);
            // Ambience has its own slider in Settings, so it skips the music group (the music slider would scale it too)
            ambience = NewSource("Ambience", loop: true, masterGroup);
            ambienceOld = NewSource("AmbienceOld", loop: true, masterGroup);
            sfx = NewSource("Sfx", loop: false, sfxGroup ?? masterGroup);
            bgmActive = bgmA;
            ApplyVolumes();
            // The Settings sliders write through GameSettings; follow them at once, not only on the next launch
            TawanOS.Settings.GameSettings.OnChanged += ApplyVolumes;

            // Most game scenes ship without an AudioListener, and Unity plays nothing without one.
            // This object lives for the whole session, so it owns the one listener; others get switched off.
            gameObject.AddComponent<AudioListener>();
            SceneManager.sceneLoaded += OnSceneLoadedMuteOtherListeners;
            MuteOtherListeners();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoadedMuteOtherListeners;
            if (Instance == this) TawanOS.Settings.GameSettings.OnChanged -= ApplyVolumes;
            if (Instance == this) Instance = null;
        }

        // An AudioMixer ignores SetFloat in Awake, so the saved volumes only took hold once a slider moved: the game
        // started at full volume (Game Designer, 25 Oct: "set in the menu, very loud in the game"). Set them again here
        // and on every scene load.
        private void Start() => ApplyVolumes();

        private void OnSceneLoadedMuteOtherListeners(Scene scene, LoadSceneMode mode)
        {
            MuteOtherListeners();
            ApplyVolumes();
        }

        private void MuteOtherListeners()
        {
            var own = GetComponent<AudioListener>();
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (listener != own && listener.enabled) listener.enabled = false;
        }

        private AudioSource NewSource(string name, bool loop, UnityEngine.Audio.AudioMixerGroup group)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var src = child.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f;
            if (group != null) src.outputAudioMixerGroup = group;
            return src;
        }

        // ---------------------------------------------------------------- play

        public void PlayBgm(string key)
        {
            if (key == currentBgmKey) return;
            currentBgmKey = key;
            var clip = Resolve(key, out float gain);
            var from = bgmActive;
            var to = bgmActive == bgmA ? bgmB : bgmA;
            bgmActive = to;
            if (to == bgmA) bgmGainA = gain; else bgmGainB = gain;
            to.clip = clip;
            to.volume = 0f;
            if (clip != null) to.Play();
            if (crossfade != null) StopCoroutine(crossfade);
            crossfade = StartCoroutine(Crossfade(from, to));
        }

        public void PlayAmbience(string key)
        {
            if (key == currentAmbienceKey) return;
            currentAmbienceKey = key;
            var clip = Resolve(key, out ambienceGain);
            // The old ambience fades out while the new one fades in, like the music (Game Designer, 25 Oct: no hard cut)
            (ambience, ambienceOld) = (ambienceOld, ambience);
            ambience.Stop();
            ambience.clip = clip;
            ambience.volume = 0f;
            if (clip != null) ambience.Play();
            if (ambienceFade != null) StopCoroutine(ambienceFade);
            ambienceFade = StartCoroutine(FadeAmbience());
        }

        public void PlaySfx(string key) => PlaySfx(key, 1f);

        /// <summary>A sound effect played quieter (volumeScale below 1) or louder than usual.</summary>
        public void PlaySfx(string key, float volumeScale)
        {
            var clip = Resolve(key, out float gain);
            if (clip != null)
            {
                float vol = mixer != null ? 1f : LoadVolume(AudioChannel.Sfx); // master = AudioListener.volume (GameSettings)
                sfx.PlayOneShot(clip, vol * volumeScale * gain); // gain evens it out with the other effects
            }
        }

        // Per-clip volume from the library's loudness pass (Tools/TawanOS/Audio/Even Out Loudness)
        private float bgmGainA = 1f, bgmGainB = 1f, ambienceGain = 1f;
        private float GainOf(AudioSource bgm) => bgm == bgmA ? bgmGainA : bgmGainB;

        private AudioClip Resolve(string key, out float gain)
        {
            gain = 1f;
            if (string.IsNullOrEmpty(key)) return null;
            if (library == null) library = AudioLibrarySO.Load();
            var clip = library != null ? library.Pick(key, out gain) : null;
            if (clip == null && warned.Add(key)) Debug.LogWarning($"[AudioManager] No clip for key '{key}'.");
            return clip;
        }

        private IEnumerator FadeAmbience()
        {
            float start = ambienceOld.volume;
            for (float t = 0f; t < CrossfadeSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / CrossfadeSeconds;
                ambienceOld.volume = start * (1f - k);
                ambience.volume = AmbienceVolume() * ambienceGain * k;
                yield return null;
            }
            ambienceOld.Stop();
            ambienceOld.volume = 0f;
            ambience.volume = AmbienceVolume() * ambienceGain;
            ambienceFade = null;
        }

        private IEnumerator Crossfade(AudioSource from, AudioSource to)
        {
            float start = from != null ? from.volume : 0f;
            for (float t = 0f; t < CrossfadeSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / CrossfadeSeconds;
                if (from != null) from.volume = start * (1f - k);
                to.volume = SourceVolumeBgm() * GainOf(to) * k;
                yield return null;
            }
            if (from != null && from != to) { from.Stop(); from.volume = 0f; }
            to.volume = SourceVolumeBgm() * GainOf(to);
            crossfade = null;
        }

        // ---------------------------------------------------------------- volume

        public float GetVolume(AudioChannel channel) => LoadVolume(channel);

        public void SetVolume(AudioChannel channel, float value)
        {
            SaveVolume(channel, value);
            ApplyVolumes();
        }

        /// <summary>For crossfade math only — the per-source volume for BGM sources. Mixer handles master attenuation.</summary>
        private float SourceVolumeBgm()
        {
            // When mixer is present, master × bgm is handled by mixer groups; source stays at 1.
            // When mixer is absent (tests), multiply manually.
            if (mixer != null) return 1f;
            return LoadVolume(AudioChannel.Bgm); // master = AudioListener.volume (GameSettings)
        }

        // The ambience slider; it is not on a mixer group of its own, so it is set on the source
        private static float AmbienceVolume() => TawanOS.Settings.GameSettings.GetVolume(TawanOS.Settings.VolumeChannel.Ambience);

        private void ApplyVolumes()
        {
            if (mixer != null)
            {
                mixer.SetFloat(MixerParamMaster, 0f); // master is applied once, through AudioListener.volume
                // The groups are lifted so the quietest clips reach their loudness (AudioLibrarySO); silent stays silent
                float bgmDb = VolumeToDb(LoadVolume(AudioChannel.Bgm)), sfxDb = VolumeToDb(LoadVolume(AudioChannel.Sfx));
                mixer.SetFloat(MixerParamBgm, bgmDb <= -80f ? -80f : bgmDb + (library != null ? library.musicBoostDb : 0f));
                mixer.SetFloat(MixerParamSfx, sfxDb <= -80f ? -80f : sfxDb + (library != null ? library.sfxBoostDb : 0f));
            }
            // Source volume for crossfade and non-mixer fallback
            float bgmVol = SourceVolumeBgm();
            if (crossfade == null && bgmActive != null) bgmActive.volume = bgmVol * GainOf(bgmActive);
            if (ambience != null && ambienceFade == null) ambience.volume = AmbienceVolume() * ambienceGain;
            // SFX uses PlayOneShot volume; when mixer is absent, scale is applied there.
        }

        public static string PrefKey(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Bgm: return PrefBgm;
                case AudioChannel.Sfx: return PrefSfx;
                default: return PrefMaster;
            }
        }

        // The card and UI sound effects were mastered 10-20 dB quieter than the music (measured with ffmpeg
        // volumedetect, see PLAN B1), so music starts lower to keep effects audible. Players can change it in Settings.
        public static float DefaultVolume(AudioChannel channel) => channel == AudioChannel.Bgm ? TawanOS.Settings.GameSettings.DefaultMusicVolume : TawanOS.Settings.GameSettings.DefaultVolume;

        public static float LoadVolume(AudioChannel channel) => Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKey(channel), DefaultVolume(channel)));

        public static void SaveVolume(AudioChannel channel, float value)
        {
            PlayerPrefs.SetFloat(PrefKey(channel), Mathf.Clamp01(value));
            PlayerPrefs.Save();
        }

        /// <summary>Linear 0..1 to decibels; silence at or below 0.0001 (-80 dB).</summary>
        public static float VolumeToDb(float v) => v <= 0.0001f ? -80f : Mathf.Log10(v) * 20f;

        /// <summary>"sfx_card_hover_01" becomes "sfx_card_hover"; names without a _NN suffix stay as they are.</summary>
        public static string NormalizeKey(string clipName) => VariantSuffix.Replace(clipName ?? string.Empty, string.Empty);
    }
}
