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

        // ponytail: volume is applied per AudioSource (master x channel) instead of an AudioMixer, because Unity
        // has no public API to create a mixer with exposed parameters. Upgrade: route sources into MainMixer groups.
        private AudioSource bgmA, bgmB, ambience, sfx;
        private AudioSource bgmActive;
        private string currentBgmKey, currentAmbienceKey;
        private Coroutine crossfade;
        private AudioLibrarySO library;
        private UnityEngine.Audio.AudioMixerGroup outputGroup;
        private readonly HashSet<string> warned = new HashSet<string>();

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
                var groups = library.mixer.FindMatchingGroups("Master");
                if (groups.Length > 0) outputGroup = groups[0];
            }

            bgmA = NewSource("BgmA", loop: true);
            bgmB = NewSource("BgmB", loop: true);
            ambience = NewSource("Ambience", loop: true);
            sfx = NewSource("Sfx", loop: false);
            bgmActive = bgmA;
            ApplyVolumes();

            // Most game scenes ship without an AudioListener, and Unity plays nothing without one.
            // This object lives for the whole session, so it owns the one listener; others get switched off.
            gameObject.AddComponent<AudioListener>();
            SceneManager.sceneLoaded += OnSceneLoadedMuteOtherListeners;
            MuteOtherListeners();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoadedMuteOtherListeners;
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoadedMuteOtherListeners(Scene scene, LoadSceneMode mode) => MuteOtherListeners();

        private void MuteOtherListeners()
        {
            var own = GetComponent<AudioListener>();
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (listener != own && listener.enabled) listener.enabled = false;
        }

        private AudioSource NewSource(string name, bool loop)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var src = child.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0f;
            if (outputGroup != null) src.outputAudioMixerGroup = outputGroup;
            return src;
        }

        // ---------------------------------------------------------------- play

        public void PlayBgm(string key)
        {
            if (key == currentBgmKey) return;
            currentBgmKey = key;
            var clip = Resolve(key);
            var from = bgmActive;
            var to = bgmActive == bgmA ? bgmB : bgmA;
            bgmActive = to;
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
            var clip = Resolve(key);
            ambience.Stop();
            ambience.clip = clip;
            ambience.volume = EffectiveVolume(AudioChannel.Bgm);
            if (clip != null) ambience.Play();
        }

        public void PlaySfx(string key)
        {
            var clip = Resolve(key);
            if (clip != null) sfx.PlayOneShot(clip, EffectiveVolume(AudioChannel.Sfx));
        }

        private AudioClip Resolve(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (library == null) library = AudioLibrarySO.Load();
            var clip = library != null ? library.Pick(key) : null;
            if (clip == null && warned.Add(key)) Debug.LogWarning($"[AudioManager] No clip for key '{key}'.");
            return clip;
        }

        private IEnumerator Crossfade(AudioSource from, AudioSource to)
        {
            float start = from != null ? from.volume : 0f;
            for (float t = 0f; t < CrossfadeSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / CrossfadeSeconds;
                if (from != null) from.volume = start * (1f - k);
                to.volume = EffectiveVolume(AudioChannel.Bgm) * k;
                yield return null;
            }
            if (from != null && from != to) { from.Stop(); from.volume = 0f; }
            to.volume = EffectiveVolume(AudioChannel.Bgm);
            crossfade = null;
        }

        // ---------------------------------------------------------------- volume

        public float GetVolume(AudioChannel channel) => LoadVolume(channel);

        public void SetVolume(AudioChannel channel, float value)
        {
            SaveVolume(channel, value);
            ApplyVolumes();
        }

        private float EffectiveVolume(AudioChannel channel)
        {
            float master = LoadVolume(AudioChannel.Master);
            return channel == AudioChannel.Master ? master : master * LoadVolume(channel);
        }

        private void ApplyVolumes()
        {
            if (crossfade == null && bgmActive != null) bgmActive.volume = EffectiveVolume(AudioChannel.Bgm);
            if (ambience != null) ambience.volume = EffectiveVolume(AudioChannel.Bgm);
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
        public static float DefaultVolume(AudioChannel channel) => channel == AudioChannel.Bgm ? 0.5f : 1f;

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
