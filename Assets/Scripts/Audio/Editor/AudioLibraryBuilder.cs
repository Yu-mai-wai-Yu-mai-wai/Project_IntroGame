using System.Collections.Generic;
using System.IO;
using TawanOS.Audio;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Scans Assets/Audio/** and writes Assets/Resources/AudioLibrary.asset (key = file name without _01/_02).
    /// BGM and ambience stream from disk; short SFX decompress on load.
    /// Menu: Tools/TawanOS/Audio/Build Audio Library. Batch: -executeMethod TawanOS.EditorTools.AudioLibraryBuilder.Run
    /// </summary>
    public static class AudioLibraryBuilder
    {
        private const string AudioRoot = "Assets/Audio";
        private const string LibraryPath = "Assets/Resources/AudioLibrary.asset";

        [MenuItem("Tools/TawanOS/Audio/Build Audio Library")]
        public static void RunFromMenu() { Build(); }

        public static void Run()
        {
            Build();
            EditorApplication.Exit(0);
        }

        private static void Build()
        {
            var byKey = new SortedDictionary<string, List<AudioClip>>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                ConfigureImport(path);

                string key = AudioManager.NormalizeKey(Path.GetFileNameWithoutExtension(path));
                if (!byKey.TryGetValue(key, out var list)) byKey[key] = list = new List<AudioClip>();
                list.Add(clip);
            }

            Directory.CreateDirectory("Assets/Resources");
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrarySO>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<AudioLibrarySO>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.entries = new List<AudioLibrarySO.Entry>();
            foreach (var pair in byKey)
            {
                pair.Value.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                library.entries.Add(new AudioLibrarySO.Entry { key = pair.Key, variants = pair.Value.ToArray() });
            }
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AudioLibraryBuilder] {library.entries.Count} keys written to {LibraryPath}.");
        }

        private static void ConfigureImport(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;

            bool longTrack = path.StartsWith(AudioRoot + "/BGM/") || path.StartsWith(AudioRoot + "/Ambience/");
            var settings = importer.defaultSampleSettings;
            var wanted = longTrack ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            if (settings.loadType == wanted && importer.loadInBackground == longTrack) return;

            settings.loadType = wanted;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = longTrack;
            importer.SaveAndReimport();
        }
    }
}
