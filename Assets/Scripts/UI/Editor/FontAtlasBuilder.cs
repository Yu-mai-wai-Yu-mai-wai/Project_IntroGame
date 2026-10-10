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
        private const string EkkamaiVibeSdf = "Assets/Fonts/EkkamaiVibe SDF.asset";
        private const string RueangLaoSdf = "Assets/Fonts/MN-RueangLao SDF.asset";
        private const string KorKorTorSdf = "Assets/Fonts/KorKorTor SDF.asset";
        private const string CharmSdf = "Assets/Fonts/Charm-Bold SDF.asset";
        private const string SarabunSdf = "Assets/Fonts/Sarabun-Regular SDF.asset";

        private const string EkkamaiVibeTtf = "Assets/Fonts/EkkamaiVibe-Regular.ttf";
        private const string RueangLaoOtf = "Assets/Fonts/MN Rueang Lao.otf";
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
            var ekFont = AssetDatabase.LoadAssetAtPath<Font>(EkkamaiVibeTtf);
            if (ekFont == null)
            {
                Debug.LogError($"[FontAtlasBuilder] EkkamaiVibe-Regular.ttf not found at {EkkamaiVibeTtf}.");
                return;
            }

            var rlFont = AssetDatabase.LoadAssetAtPath<Font>(RueangLaoOtf);
            if (rlFont == null)
            {
                Debug.LogError($"[FontAtlasBuilder] MN Rueang Lao.otf not found at {RueangLaoOtf}.");
                return;
            }

            var charmFont = AssetDatabase.LoadAssetAtPath<Font>(CharmTtf);
            var sarabunFont = AssetDatabase.LoadAssetAtPath<Font>(SarabunTtf);

            // 1. EkkamaiVibe SDF (Body)
            var ekkamai = GetOrCreateFontAsset(ekFont, EkkamaiVibeSdf, "EkkamaiVibe SDF");
            BakeOne(ekkamai, ekFont);

            // 2. MN-RueangLao SDF (Title Horror)
            var rueangLao = GetOrCreateFontAsset(rlFont, RueangLaoSdf, "MN-RueangLao SDF");
            BakeOne(rueangLao, rlFont);

            // 3. Fallbacks: Sarabun & Charm
            TMP_FontAsset sarabun = null;
            if (sarabunFont != null)
            {
                sarabun = GetOrCreateFontAsset(sarabunFont, SarabunSdf, "Sarabun-Regular SDF");
                BakeOne(sarabun, sarabunFont);
            }

            TMP_FontAsset charm = null;
            if (charmFont != null)
            {
                charm = GetOrCreateFontAsset(charmFont, CharmSdf, "Charm-Bold SDF");
                BakeOne(charm, charmFont);
            }

            // 4. Overwrite KorKorTor SDF with EkkamaiVibe data so any lingering reference renders real Thai text
            var korkortor = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KorKorTorSdf);
            if (korkortor != null)
            {
                BakeOne(korkortor, ekFont);
                if (korkortor.fallbackFontAssetTable == null) korkortor.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!korkortor.fallbackFontAssetTable.Contains(ekkamai)) korkortor.fallbackFontAssetTable.Add(ekkamai);
                EditorUtility.SetDirty(korkortor);
            }

            // Fallback hierarchy:
            // EkkamaiVibe -> Sarabun (for ฿ etc.)
            if (ekkamai.fallbackFontAssetTable == null) ekkamai.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (sarabun != null && !ekkamai.fallbackFontAssetTable.Contains(sarabun))
                ekkamai.fallbackFontAssetTable.Add(sarabun);
            EditorUtility.SetDirty(ekkamai);

            // MN-RueangLao (Title) -> EkkamaiVibe -> Sarabun
            if (rueangLao.fallbackFontAssetTable == null) rueangLao.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!rueangLao.fallbackFontAssetTable.Contains(ekkamai))
                rueangLao.fallbackFontAssetTable.Add(ekkamai);
            if (sarabun != null && !rueangLao.fallbackFontAssetTable.Contains(sarabun))
                rueangLao.fallbackFontAssetTable.Add(sarabun);
            EditorUtility.SetDirty(rueangLao);

            // Charm -> EkkamaiVibe -> Sarabun
            if (charm != null)
            {
                if (charm.fallbackFontAssetTable == null) charm.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!charm.fallbackFontAssetTable.Contains(ekkamai)) charm.fallbackFontAssetTable.Add(ekkamai);
                if (sarabun != null && !charm.fallbackFontAssetTable.Contains(sarabun)) charm.fallbackFontAssetTable.Add(sarabun);
                EditorUtility.SetDirty(charm);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[FontAtlasBuilder] Thai atlases baked (Static). Body=EkkamaiVibe, Title=MN-RueangLao, Fallback=Sarabun.");
        }

        private static TMP_FontAsset GetOrCreateFontAsset(Font font, string assetPath, string assetName)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var newAsset = TMP_FontAsset.CreateFontAsset(font);
            newAsset.name = assetName;
            AssetDatabase.CreateAsset(newAsset, assetPath);
            if (newAsset.atlasTextures != null && newAsset.atlasTextures.Length > 0 && newAsset.atlasTextures[0] != null)
            {
                newAsset.atlasTextures[0].name = $"{assetName} Atlas";
                AssetDatabase.AddObjectToAsset(newAsset.atlasTextures[0], newAsset);
            }
            if (newAsset.material != null)
            {
                newAsset.material.name = $"{assetName} Material";
                AssetDatabase.AddObjectToAsset(newAsset.material, newAsset);
            }
            AssetDatabase.SaveAssets();
            return newAsset;
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
