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
        }

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
        public AudioClip Pick(string key)
        {
            if (!TryGet(key, out var e) || e.variants == null || e.variants.Length == 0) return null;
            return e.variants[UnityEngine.Random.Range(0, e.variants.Length)];
        }

        private void OnValidate()
        {
            lookup = null;
        }
    }
}
