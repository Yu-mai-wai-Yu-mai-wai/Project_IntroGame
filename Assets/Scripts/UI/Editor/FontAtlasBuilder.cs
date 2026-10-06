using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Bakes the Thai range (and the ASCII / punctuation the UI uses) into the two game fonts and switches
    /// them from Dynamic to Static, so vowels and tone marks never depend on runtime atlas population.
    /// Works on the existing assets in place, so scene GUID references stay valid.
    /// Menu: Tools/TawanOS/UI/Bake Thai Font Atlases. Batch: -executeMethod TawanOS.EditorTools.FontAtlasBuilder.Run
    /// </summary>
    public static class FontAtlasBuilder
    {
        private const string SarabunSdf = "Assets/Fonts/Sarabun-Regular SDF.asset";
        private const string CharmSdf = "Assets/Fonts/Charm-Bold SDF.asset";
        private const string SarabunTtf = "Assets/Fonts/Sarabun-Regular.ttf";
        private const string CharmTtf = "Assets/Fonts/Charm-Bold.ttf";

        [MenuItem("Tools/TawanOS/UI/Bake Thai Font Atlases")]
        public static void RunFromMenu() { Bake(); }

        public static void Run()
        {
            Bake();
            EditorApplication.Exit(0);
        }

        private static void Bake()
        {
            var sarabun = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SarabunSdf);
            var charm = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CharmSdf);
            if (sarabun == null || charm == null)
            {
                Debug.LogError("[FontAtlasBuilder] SDF assets not found. Run Tools/TawanOS/Card Engine/Setup Thai Fonts & Fallbacks first.");
                return;
            }

            BakeOne(sarabun, AssetDatabase.LoadAssetAtPath<Font>(SarabunTtf));
            BakeOne(charm, AssetDatabase.LoadAssetAtPath<Font>(CharmTtf));

            // Charm is the title face; anything it lacks falls back to Sarabun instead of a blank box.
            if (charm.fallbackFontAssetTable == null) charm.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!charm.fallbackFontAssetTable.Contains(sarabun)) charm.fallbackFontAssetTable.Add(sarabun);
            EditorUtility.SetDirty(charm);

            AssetDatabase.SaveAssets();
            Debug.Log("[FontAtlasBuilder] Thai atlases baked (Static).");
        }

        private static void BakeOne(TMP_FontAsset asset, Font source)
        {
            // Dynamic is needed while adding; Static once everything is in.
            asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            asset.ClearFontAssetData(true);

            var wanted = new List<uint>();
            Add(wanted, source, 0x20, 0x7E);        // ASCII
            Add(wanted, source, 0x0E01, 0x0E3A);    // Thai consonants, vowels, tone marks
            Add(wanted, source, 0x0E3F, 0x0E5B);    // Baht sign .. Thai punctuation and digits
            foreach (uint extra in new uint[] { 0x00A0, 0x00D7, 0x2013, 0x2014, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2026 })
                if (source.HasCharacter((char)extra)) wanted.Add(extra);

            asset.TryAddCharacters(wanted.ToArray(), out uint[] missing, true);
            if (missing != null && missing.Length > 0)
                Debug.LogWarning($"[FontAtlasBuilder] {asset.name}: {missing.Length} code point(s) not added (atlas full or glyph absent).");

            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            asset.isMultiAtlasTexturesEnabled = true;
            EditorUtility.SetDirty(asset);
            if (asset.material != null) EditorUtility.SetDirty(asset.material);
            if (asset.atlasTextures != null)
                foreach (var tex in asset.atlasTextures) if (tex != null) EditorUtility.SetDirty(tex);
        }

        private static void Add(List<uint> list, Font source, int from, int to)
        {
            for (int c = from; c <= to; c++)
                if (source.HasCharacter((char)c)) list.Add((uint)c);
        }
    }
}
