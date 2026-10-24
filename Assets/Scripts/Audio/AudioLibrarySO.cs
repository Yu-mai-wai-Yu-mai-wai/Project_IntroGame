using System;
using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.Audio
{
    /// <summary>
    /// Lookup table from a sound key (file name without the _01/_02 variant suffix) to its clips.
    /// Built by Tools/TawanOS/Audio/Build Audio Library; loaded through Resources so it works in a player build.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "TawanOS/Audio/Audio Library")]
    public class AudioLibrarySO : ScriptableObject
    {
        public const string ResourceName = "AudioLibrary";

        [Serializable]
        public class Entry
        {
            public string key;
            public AudioClip[] variants;
            [Tooltip("Per variant: the gain (dB) that brings it to its kind's loudness. Written by Tools/TawanOS/Audio/Even Out Loudness.")]
            public float[] gainDb;
        }

        [Header("Loudness (Tools/TawanOS/Audio/Even Out Loudness)")]
        [Tooltip("The effects mixer group is lifted by this much, so the quietest effects reach the target; every effect is turned down from there.")]
        public float sfxBoostDb;
        [Tooltip("The same for the music group.")]
        public float musicBoostDb;

        public List<Entry> entries = new List<Entry>();

        [Tooltip("Optional. When set, every AudioSource outputs to this mixer's Master group. Filled by Build Audio Library from Assets/MainMixer.mixer.")]
        public UnityEngine.Audio.AudioMixer mixer;

        private static AudioLibrarySO cached;
        private Dictionary<string, Entry> lookup;

        public static AudioLibrarySO Load()
        {
            if (cached == null) cached = Resources.Load<AudioLibrarySO>(ResourceName);
            return cached;
        }

        public bool Contains(string key) => TryGet(key, out var e) && e.variants != null && e.variants.Length > 0;

        public bool TryGet(string key, out Entry entry)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<string, Entry>();
                foreach (var e in entries)
                    if (e != null && !string.IsNullOrEmpty(e.key)) lookup[e.key] = e;
            }
            return lookup.TryGetValue(key ?? string.Empty, out entry);
        }

        /// <summary>A random variant for the key, or null when the key is unknown or empty.</summary>
        public AudioClip Pick(string key) => Pick(key, out _);

        /// <summary>A random variant and the volume (0..1) that evens it out with the others of its kind.</summary>
        public AudioClip Pick(string key, out float gain)
        {
            gain = 1f;
            if (!TryGet(key, out var e) || e.variants == null || e.variants.Length == 0) return null;
            int i = UnityEngine.Random.Range(0, e.variants.Length);
            if (e.gainDb != null && i < e.gainDb.Length)
            {
                float lift = key.StartsWith("sfx_") ? sfxBoostDb : key.StartsWith("bgm_") ? musicBoostDb : 0f; // ambience has no group of its own
                gain = Mathf.Clamp01(Mathf.Pow(10f, (e.gainDb[i] - lift) / 20f));
            }
            return e.variants[i];
        }

        private void OnValidate()
        {
            lookup = null;
        }
    }
}
