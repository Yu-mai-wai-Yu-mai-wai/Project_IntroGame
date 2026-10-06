using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TawanOS.MapEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Thai text must render without missing vowels / tone marks, and the map legend button must sit
    /// at the top-right. Menu: Tools/TawanOS/Tests/Thai Glyphs. Batch:
    /// -executeMethod TawanOS.EditorTools.ThaiGlyphTests.Run (exit code 1 on failure).
    /// </summary>
    public static class ThaiGlyphTests
    {
        private const string SarabunSdf = "Assets/Fonts/Sarabun-Regular SDF.asset";
        private const string CharmSdf = "Assets/Fonts/Charm-Bold SDF.asset";
        private const string SarabunTtf = "Assets/Fonts/Sarabun-Regular.ttf";
        private const string CharmTtf = "Assets/Fonts/Charm-Bold.ttf";

        // Text that appears in the UI today (menu, map HUD/legend, shop, events share the Thai range test below).
        private static readonly string[] Samples =
        {
            "เริ่มเกมใหม่", "เล่นต่อ", "ออกจากเกม", "เส้นทางของหมอธรรม", "ขวัญ", "ธูป", "สำรับ",
            "สัญลักษณ์ (L)", "สัญลักษณ์และเส้นทางบนแผนที่", "โหมดสั้น 4 ชั้น", "เล่นเต็ม (7 ชั้น)",
            "ผู้ ปู ซื้อ ที่ ปี่ ก๊ ก๋ ก้ ก็ กั กิ กี กึ กื กุ กู เ แ โ ใ ไ ำ ์ ํ ๆ"
        };

        [MenuItem("Tools/TawanOS/Tests/Thai Glyphs")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;
            failures += CheckFont("Sarabun", SarabunSdf, SarabunTtf);
            failures += CheckFont("Charm", CharmSdf, CharmTtf);
            failures += CheckLegendCorner();

            if (failures == 0) Debug.Log("[ThaiGlyphTests] PASS");
            else Debug.LogError($"[ThaiGlyphTests] FAIL: {failures} check(s) failed.");
            if (exitOnFinish) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int CheckFont(string label, string sdfPath, string ttfPath)
        {
            int failures = 0;
            var sdf = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(sdfPath);
            var ttf = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            failures += Expect($"{label}: SDF asset loads", sdf != null);
            failures += Expect($"{label}: source TTF loads", ttf != null);
            if (sdf == null || ttf == null) return failures;

            failures += Expect($"{label}: atlas is static (pre-baked, not Dynamic)",
                sdf.atlasPopulationMode == AtlasPopulationMode.Static);

            // Every Thai code point the source font has must be baked in (search fallbacks, never add on the fly).
            var missingInAtlas = new StringBuilder();
            for (int c = 0x0E01; c <= 0x0E5B; c++)
            {
                if (c > 0x0E3A && c < 0x0E3F) continue; // unassigned
                if (!ttf.HasCharacter((char)c)) continue;
                if (!sdf.HasCharacter((char)c, searchFallbacks: true, tryAddCharacter: false))
                    missingInAtlas.Append($"U+{c:X4} ");
            }
            failures += Expect($"{label}: all Thai glyphs baked [missing: {missingInAtlas}]", missingInAtlas.Length == 0);

            foreach (var sample in Samples)
            {
                bool ok = sdf.HasCharacters(sample, out var missing, searchFallbacks: true, tryAddCharacter: false);
                failures += Expect($"{label}: renders \"{sample}\" [missing: {missing?.Length ?? 0}]", ok);
            }
            return failures;
        }

        private static int CheckLegendCorner()
        {
            int failures = 0;
            var canvasGo = new GameObject("TestCanvas", typeof(Canvas));
            try
            {
                // Same shape MapEngineSetupTool builds: an empty child under the canvas.
                var legendGo = new GameObject("MapLegendUI");
                legendGo.transform.SetParent(canvasGo.transform, false);
                var legend = legendGo.AddComponent<MapLegendUI>();
                typeof(MapLegendUI).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(legend, null);

                var root = legendGo.GetComponent<RectTransform>();
                failures += Expect("Legend root is a RectTransform stretched over the canvas",
                    root != null && root.anchorMin == Vector2.zero && root.anchorMax == Vector2.one
                    && root.offsetMin == Vector2.zero && root.offsetMax == Vector2.zero);

                var toggle = legendGo.transform.Find("ToggleLegendButton") as RectTransform;
                failures += Expect("Legend toggle button exists", toggle != null);
                if (toggle != null)
                {
                    failures += Expect("Toggle anchored to top-right (1,1)", toggle.anchorMin == Vector2.one && toggle.anchorMax == Vector2.one);
                    failures += Expect("Toggle pivot top-right and offset left of edge", toggle.pivot == Vector2.one && toggle.anchoredPosition.x < 0f);
                }
                var panel = legendGo.transform.Find("LegendPanel") as RectTransform;
                failures += Expect("Legend panel anchored to top-right", panel != null && panel.anchorMin == Vector2.one && panel.pivot == Vector2.one);
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
            }
            return failures;
        }

        private static int Expect(string name, bool ok)
        {
            if (ok) { Debug.Log($"  PASS {name}"); return 0; }
            Debug.LogError($"  FAIL {name}");
            return 1;
        }
    }
}
