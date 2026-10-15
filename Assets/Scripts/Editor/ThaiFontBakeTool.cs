using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Gives MN-RueangLao SDF its own ำ (U+0E33). The font draws ำ through its 'ccmp' feature (ำ → ํ + า), which TMP
    /// does not run, and its plain ำ glyph has no advance and sits one า-width to the left. So the atlas was baked
    /// without ำ and TMP drew it from the EkkamaiVibe fallback, in a different style from the other vowels.
    /// This maps ำ to that glyph and moves it one า-advance right with า's advance: a normal spacing ำ whose
    /// circle overhangs the consonant before it, like the ccmp result. The glyph is already in the atlas.
    /// Menu: Tools/TawanOS/Fonts/Fix RueangLao Sara Am. Batch:
    /// -executeMethod TawanOS.EditorTools.ThaiFontBakeTool.Run (exit code 1 on failure).
    /// </summary>
    public static class ThaiFontBakeTool
    {
        private const string RueangLaoSdf = "Assets/Fonts/MN-RueangLao SDF.asset";
        private const uint SaraAm = 0x0E33;
        private const uint SaraAa = 0x0E32;
        private const uint SaraAmGlyphIndex = 54; // uni0E33 in MN Rueang Lao.otf

        [MenuItem("Tools/TawanOS/Fonts/Fix RueangLao Sara Am")]
        public static void RunFromMenu() { Fix(); }

        public static void Run() { EditorApplication.Exit(Fix() ? 0 : 1); }

        private static bool Fix()
        {
            var sdf = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RueangLaoSdf);
            if (sdf == null) { Debug.LogError($"[ThaiFontBakeTool] Cannot load {RueangLaoSdf}."); return false; }
            // characterTable, not characterLookupTable: TMP caches a character found in a fallback in the lookup
            if (sdf.characterTable.Exists(c => c.unicode == SaraAm))
            {
                Debug.Log("[ThaiFontBakeTool] MN-RueangLao SDF already has ำ.");
                return true;
            }

            sdf.glyphLookupTable.TryGetValue(SaraAmGlyphIndex, out Glyph am);
            var aa = sdf.characterTable.Find(c => c.unicode == SaraAa);
            if (am == null || aa == null || am.metrics.horizontalAdvance != 0)
            {
                Debug.LogError("[ThaiFontBakeTool] MN-RueangLao SDF: the ำ glyph is not the zero-advance one this fix expects.");
                return false;
            }

            float advance = aa.glyph.metrics.horizontalAdvance;
            var m = am.metrics;
            am.metrics = new GlyphMetrics(m.width, m.height, m.horizontalBearingX + advance, m.horizontalBearingY, advance);
            sdf.characterTable.Add(new TMP_Character(SaraAm, sdf, am));
            sdf.ReadFontAssetDefinition();
            EditorUtility.SetDirty(sdf);
            AssetDatabase.SaveAssets();
            Debug.Log("[ThaiFontBakeTool] MN-RueangLao SDF: ำ mapped to its own glyph.");
            return true;
        }
    }
}
