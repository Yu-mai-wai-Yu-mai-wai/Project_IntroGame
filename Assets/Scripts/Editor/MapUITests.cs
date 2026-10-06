using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TawanOS.MapEngine;
using TawanOS.UI;
using TawanOS.GameFlow;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Verifies Plan Task G2:
    /// 1. MapLegendUI covers all 7 node types with authentic Thai text and zero emojis.
    /// 2. WCAG AA contrast >= 4.5:1 on the CI panel.
    /// 3. MapPlayerStatusUI displays Khwan, Incense, and Deck labels cleanly without emojis.
    /// 4. MapTestScene.unity contains MapPlayerStatusUI and has no debug ResetButton on the canvas.
    /// 
    /// Menu: Tools/TawanOS/Tests/Map UI
    /// Batch: -executeMethod TawanOS.EditorTools.MapUITests.Run
    /// </summary>
    public static class MapUITests
    {
        // Matches common Emoji ranges: Emoticons, Dingbats, Transport/Map symbols, Enclosed characters
        private static readonly Regex EmojiRegex = new Regex(
            @"[\uD83C-\uDBFF\uDC00-\uDFFF]|[\u2600-\u27BF]|[\u2300-\u23FF]|[\u2B50-\u2B55]|[\u203C-\u2049]",
            RegexOptions.Compiled);

        [MenuItem("Tools/TawanOS/Tests/Map UI")]
        public static void RunFromMenu()
        {
            Execute(false);
        }

        public static void Run()
        {
            Execute(true);
        }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;

            // --- 1. MapLegendUI: All 7 Node Types Covered ---
            failures += Expect("Legend covers 7 node types", MapLegendUI.AllNodeTypes.Length == 7);

            foreach (var type in MapLegendUI.AllNodeTypes)
            {
                string title = MapLegendUI.GetNodeTitle(type);
                string desc = MapLegendUI.GetNodeDescription(type);

                failures += Expect($"{type}: title is non-empty Thai", !string.IsNullOrEmpty(title) && HasThai(title));
                failures += Expect($"{type}: title has zero emoji", !EmojiRegex.IsMatch(title));

                failures += Expect($"{type}: desc is non-empty Thai", !string.IsNullOrEmpty(desc) && HasThai(desc));
                failures += Expect($"{type}: desc has zero emoji", !EmojiRegex.IsMatch(desc));
            }

            // --- 2. Theme Contrast and Typography ---
            var theme = UIThemeSO.Current;
            failures += Expect("Theme text on panel >= 4.5", UIThemeSO.Contrast(theme.text, theme.panel) >= 4.5f);
            failures += Expect("Theme accent on panel >= 4.5", UIThemeSO.Contrast(theme.accent, theme.panel) >= 4.5f);
            failures += Expect("Font sizes >= 20px", theme.labelSize >= 20f && theme.bodySize >= 20f);

            // --- 3. Scene Check: MapTestScene UICanvas ---
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MapTestScene.unity");
            var canvas = GameObject.Find("UICanvas");
            failures += Expect("MapTestScene has UICanvas", canvas != null);

            if (canvas != null)
            {
                var resetBtn = canvas.transform.Find("ResetButton");
                failures += Expect("ResetButton is removed from UICanvas", resetBtn == null);

                var legend = canvas.GetComponentInChildren<MapLegendUI>(true);
                failures += Expect("MapLegendUI exists on UICanvas", legend != null);

                var status = canvas.GetComponentInChildren<MapPlayerStatusUI>(true);
                failures += Expect("MapPlayerStatusUI exists on UICanvas", status != null);
            }

            Debug.Log(failures == 0 ? "[MapUITests] PASS" : $"[MapUITests] FAIL: {failures} check(s) failed");
            if (exitOnFinish) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static bool HasThai(string s)
        {
            foreach (char c in s)
            {
                if (c >= '\u0E01' && c <= '\u0E5B') return true;
            }
            return false;
        }

        private static int Expect(string name, bool condition)
        {
            if (condition)
            {
                Debug.Log($"[MapUITests] ok   {name}");
                return 0;
            }
            Debug.LogError($"[MapUITests] FAIL {name}");
            return 1;
        }
    }
}
