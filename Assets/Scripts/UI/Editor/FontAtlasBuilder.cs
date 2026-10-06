using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Bakes the Thai range (and the ASCII / punctuation the UI uses) into the game fonts and switches
    /// them from Dynamic to Static, so vowels and tone marks never depend on runtime atlas population.
    /// KorKorTor is the body face; Charm is the title face; Sarabun is fallback for ฿ (U+0E3F) which
    /// KorKorTor lacks.
    /// Works on the existing assets in place, so scene GUID references stay valid.
    /// Menu: Tools/TawanOS/UI/Bake Thai Font Atlases. Batch: -executeMethod TawanOS.EditorTools.FontAtlasBuilder.Run
    /// </summary>
    public static class FontAtlasBuilder
    {
        private const string KorKorTorSdf = "Assets/Fonts/KorKorTor SDF.asset";
        private const string CharmSdf = "Assets/Fonts/Charm-Bold SDF.asset";
        private const string SarabunSdf = "Assets/Fonts/Sarabun-Regular SDF.asset";
        private const string KorKorTorTtf = "Assets/Fonts/KorKorTor.ttf";
        private const string CharmTtf = "Assets/Fonts/Charm-Bold.ttf";
        private const string SarabunTtf = "Assets/Fonts/Sarabun-Regular.ttf";

        [MenuItem("Tools/TawanOS/UI/Bake Thai Font Atlases")]
        public static void RunFromMenu() { Bake(); }

        public static void Run()
        {
            Bake();
            EditorApplication.Exit(0);
        }

        private static void Bake()
        {
            var kkFont = AssetDatabase.LoadAssetAtPath<Font>(KorKorTorTtf);
            if (kkFont == null)
            {
                Debug.LogError($"[FontAtlasBuilder] KorKorTor.ttf not found at {KorKorTorTtf}.");
                return;
            }

            var charm = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CharmSdf);
            if (charm == null)
            {
                Debug.LogError("[FontAtlasBuilder] Charm-Bold SDF asset not found.");
                return;
            }

            var sarabun = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SarabunSdf);

            // Recreate KorKorTor SDF freshly from TTF so its atlas texture and material are always clean sub-assets.
            var korkortor = TMP_FontAsset.CreateFontAsset(kkFont);
            korkortor.name = "KorKorTor SDF";
            AssetDatabase.CreateAsset(korkortor, KorKorTorSdf);
            if (korkortor.atlasTextures != null && korkortor.atlasTextures.Length > 0 && korkortor.atlasTextures[0] != null)
            {
                korkortor.atlasTextures[0].name = "KorKorTor SDF Atlas";
                AssetDatabase.AddObjectToAsset(korkortor.atlasTextures[0], korkortor);
            }
            if (korkortor.material != null)
            {
                korkortor.material.name = "KorKorTor SDF Material";
                AssetDatabase.AddObjectToAsset(korkortor.material, korkortor);
            }

            BakeOne(korkortor, kkFont);
            if (sarabun != null) BakeOne(sarabun, AssetDatabase.LoadAssetAtPath<Font>(SarabunTtf));
            BakeOne(charm, AssetDatabase.LoadAssetAtPath<Font>(CharmTtf));

            // KorKorTor lacks ฿ (U+0E3F); Sarabun covers it as fallback.
            if (korkortor.fallbackFontAssetTable == null) korkortor.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (sarabun != null && !korkortor.fallbackFontAssetTable.Contains(sarabun))
                korkortor.fallbackFontAssetTable.Add(sarabun);
            EditorUtility.SetDirty(korkortor);

            // Charm title face falls back to KorKorTor (body), then Sarabun.
            if (charm.fallbackFontAssetTable == null) charm.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!charm.fallbackFontAssetTable.Contains(korkortor)) charm.fallbackFontAssetTable.Add(korkortor);
            if (sarabun != null && !charm.fallbackFontAssetTable.Contains(sarabun)) charm.fallbackFontAssetTable.Add(sarabun);
            EditorUtility.SetDirty(charm);

            AssetDatabase.SaveAssets();
            Debug.Log("[FontAtlasBuilder] Thai atlases baked (Static). Body=KorKorTor, Title=Charm, Fallback=Sarabun.");
        }

        private static void BakeOne(TMP_FontAsset asset, Font source)
        {
            // Dynamic is needed while adding; Static once everything is in.
            asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            asset.ClearFontAssetData(false);

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

            // Ensure any new atlas textures are sub-assets
            if (asset.atlasTextures != null)
            {
                for (int i = 0; i < asset.atlasTextures.Length; i++)
                {
                    var tex = asset.atlasTextures[i];
                    if (tex != null && !AssetDatabase.Contains(tex))
                    {
                        tex.name = $"{asset.name} Atlas" + (i > 0 ? $" {i}" : "");
                        AssetDatabase.AddObjectToAsset(tex, asset);
                    }
                }
            }

            if (asset.material != null && !AssetDatabase.Contains(asset.material))
            {
                asset.material.name = $"{asset.name} Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

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
