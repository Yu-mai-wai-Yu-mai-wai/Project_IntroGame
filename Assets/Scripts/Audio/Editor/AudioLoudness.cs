using System.Collections.Generic;
using TawanOS.Audio;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Evens out how loud the sounds are (Game Designer, 25 Oct: some were much louder than others). Measures every
    /// clip in the audio library (the RMS of its non-silent part) and stores, per clip, the gain that brings it to its
    /// kind's target: sound effects <see cref="SfxTargetDb"/>, music <see cref="MusicTargetDb"/>, ambience
    /// <see cref="AmbienceTargetDb"/>, never so much that its peak would clip. Quiet clips need a boost a source cannot
    /// give (its volume stops at 1), so the effects and music mixer groups are lifted by the largest boost of their kind
    /// and each clip is turned down from there (AudioLibrarySO.Pick). The sound files themselves are not touched.
    /// Run again after adding or replacing sounds; Build Audio Library runs it too.
    /// Menu: Tools/TawanOS/Audio/Even Out Loudness. Batch: -executeMethod TawanOS.EditorTools.AudioLoudness.Run
    /// </summary>
    public static class AudioLoudness
    {
        public const float SfxTargetDb = -20f;
        public const float MusicTargetDb = -24f;   // under the effects, so they read over it
        public const float AmbienceTargetDb = -30f; // a bed under everything
        private const float PeakCeilingDb = -1f;
        private const float MaxBoostDb = 15f;
        private const string LibraryPath = "Assets/Resources/AudioLibrary.asset";

        [MenuItem("Tools/TawanOS/Audio/Even Out Loudness")]
        public static void RunFromMenu() => Apply(AssetDatabase.LoadAssetAtPath<AudioLibrarySO>(LibraryPath));

        public static void Run()
        {
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrarySO>(LibraryPath);
            if (library == null) { Debug.LogError($"[AudioLoudness] {LibraryPath} not found."); EditorApplication.Exit(1); return; }
            Apply(library);
            EditorApplication.Exit(0);
        }

        public static void Apply(AudioLibrarySO library)
        {
            if (library == null) return;
            float sfxBoost = 0f, musicBoost = 0f;
            foreach (var entry in library.entries)
            {
                if (entry == null || entry.variants == null) continue;
                float target = entry.key.StartsWith("bgm_") ? MusicTargetDb : entry.key.StartsWith("amb_") ? AmbienceTargetDb : SfxTargetDb;
                entry.gainDb = new float[entry.variants.Length];
                for (int i = 0; i < entry.variants.Length; i++)
                {
                    if (!Measure(entry.variants[i], out float loudDb, out float peakDb)) continue;
                    float gain = Mathf.Min(target - loudDb, PeakCeilingDb - peakDb);
                    entry.gainDb[i] = gain;
                    if (entry.key.StartsWith("sfx_")) sfxBoost = Mathf.Max(sfxBoost, gain);
                    else if (entry.key.StartsWith("bgm_")) musicBoost = Mathf.Max(musicBoost, gain);
                    Debug.Log($"[AudioLoudness] {entry.variants[i].name}: {loudDb:F1} dB (peak {peakDb:F1}) -> {gain:+0.0;-0.0} dB");
                }
            }
            library.sfxBoostDb = Mathf.Clamp(sfxBoost, 0f, MaxBoostDb);
            library.musicBoostDb = Mathf.Clamp(musicBoost, 0f, MaxBoostDb);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[AudioLoudness] Done. Effects group +{library.sfxBoostDb:F1} dB, music group +{library.musicBoostDb:F1} dB.</color>");
        }

        // RMS over 50 ms windows, leaving out the near-silent ones; streamed clips are decompressed for a moment to read them
        private static bool Measure(AudioClip clip, out float loudDb, out float peakDb)
        {
            loudDb = peakDb = -99f;
            if (clip == null) return false;
            string path = AssetDatabase.GetAssetPath(clip);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            AudioImporterSampleSettings? restore = null;
            bool restoreBackground = false;
            if (importer != null && (importer.defaultSampleSettings.loadType != AudioClipLoadType.DecompressOnLoad || importer.loadInBackground))
            {
                restore = importer.defaultSampleSettings;
                restoreBackground = importer.loadInBackground;
                var s = importer.defaultSampleSettings;
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                importer.defaultSampleSettings = s;
                importer.loadInBackground = false; // read it now, not later on another thread
                importer.SaveAndReimport();
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            try
            {
                if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
                var data = new float[clip.samples * clip.channels];
                if (!clip.GetData(data, 0)) { Debug.LogWarning($"[AudioLoudness] Could not read {clip.name} ({clip.loadState}); left as it is."); return false; }
                int window = Mathf.Max(1, clip.frequency * clip.channels / 20);
                double sum = 0;
                int counted = 0;
                float peak = 0f;
                for (int i = 0; i < data.Length; i += window)
                {
                    double s = 0;
                    int n = Mathf.Min(window, data.Length - i);
                    for (int k = 0; k < n; k++)
                    {
                        float x = data[i + k];
                        s += x * x;
                        if (Mathf.Abs(x) > peak) peak = Mathf.Abs(x);
                    }
                    if (System.Math.Sqrt(s / n) > 0.003) { sum += s / n; counted++; }
                }
                if (counted == 0) return false;
                loudDb = 10f * Mathf.Log10((float)(sum / counted));
                peakDb = 20f * Mathf.Log10(Mathf.Max(peak, 1e-6f));
                return true;
            }
            finally
            {
                if (restore.HasValue)
                {
                    importer.defaultSampleSettings = restore.Value;
                    importer.loadInBackground = restoreBackground;
                    importer.SaveAndReimport();
                }
            }
        }
    }
}
