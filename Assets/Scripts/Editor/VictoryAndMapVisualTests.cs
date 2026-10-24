using System.Reflection;
using NUnit.Framework;
using TawanOS.CardEngine;
using TawanOS.GameFlow;
using TawanOS.MapEngine;
using TawanOS.UI;
using TawanOS.VFX;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.Tests
{
    /// <summary>
    /// Verification tests for Tickets P1-8 and P1-9:
    /// - P1-8: Victory cutscene sequence, prominent incense reward, and hidden stats on incantation cards.
    /// - P1-9: Enlarged MapPlayerStatusUI dimensions, SoftEdgeImage edge polish, and UiSmokeButton on Deck button.
    /// </summary>
    public static class VictoryAndMapVisualTests
    {
        [MenuItem("Tools/TawanOS/Tests/Victory And Map Visual")]
        public static void RunFromMenu()
        {
            int passed = 0;
            int failed = 0;

            void Check(string name, bool condition)
            {
                if (condition)
                {
                    passed++;
                    Debug.Log($"<color=green>[PASS]</color> {name}");
                }
                else
                {
                    failed++;
                    Debug.LogError($"<color=red>[FAIL]</color> {name}");
                }
            }

            var testRoot = new GameObject("VictoryAndMapVisualTests");

            try
            {
                // ==========================================
                // 1. P1-8: Incantation CardInstance maxKhwan
                // ==========================================
                var spellData = ScriptableObject.CreateInstance<CardDataSO>();
                spellData.cardId = "test_spell";
                spellData.cardNameThai = "คาถาทดสอบ";
                spellData.cardType = CardType.Incantation;
                spellData.familiarHealth = 1; // Default in asset
                spellData.familiarDamage = 0;

                var spellInst = new CardInstance(spellData);
                Check("Incantation CardInstance maxKhwan is 0 (not inheriting familiarHealth)", spellInst.maxKhwan == 0);

                var famData = ScriptableObject.CreateInstance<CardDataSO>();
                famData.cardId = "test_fam";
                famData.cardNameThai = "บริวารทดสอบ";
                famData.cardType = CardType.Familiar;
                famData.familiarHealth = 4;
                famData.familiarDamage = 2;

                var famInst = new CardInstance(famData);
                Check("Familiar CardInstance maxKhwan retains familiarHealth (4)", famInst.maxKhwan == 4);

                // ==========================================
                // 2. P1-8: VictoryViewUI Cutscene Fields
                // ==========================================
                var victoryGo = new GameObject("TestVictoryViewUI");
                victoryGo.transform.SetParent(testRoot.transform);
                var victoryUI = victoryGo.AddComponent<VictoryViewUI>();

                Check("VictoryViewUI cutsceneGroup field exists", typeof(VictoryViewUI).GetField("cutsceneGroup") != null);
                Check("VictoryViewUI cutsceneVideoImage field exists", typeof(VictoryViewUI).GetField("cutsceneVideoImage") != null);
                Check("VictoryViewUI cutsceneVideoPlayer field exists", typeof(VictoryViewUI).GetField("cutsceneVideoPlayer") != null);
                Check("VictoryViewUI cutsceneCaptionText field exists", typeof(VictoryViewUI).GetField("cutsceneCaptionText") != null);

                // ==========================================
                // 3. P1-9: MapPlayerStatusUI Dimensions & Polish
                // ==========================================
                var statusGo = new GameObject("TestMapPlayerStatusUI", typeof(RectTransform), typeof(MapPlayerStatusUI));
                statusGo.transform.SetParent(testRoot.transform);
                var statusUI = statusGo.GetComponent<MapPlayerStatusUI>();
                statusUI.EnsureUI();

                var statusRect = statusGo.GetComponent<RectTransform>();
                Check("MapPlayerStatusUI root size is (450, 56): Khwan and incense, no deck button", statusRect.sizeDelta == new Vector2(450f, 56f));
                Check("MapPlayerStatusUI root anchoredPosition is (28, -18)", statusRect.anchoredPosition == new Vector2(28f, -18f));

                var border = statusGo.transform.Find("Border");
                Check("MapPlayerStatusUI Border has SoftEdgeImage", border != null && border.GetComponent<SoftEdgeImage>() != null);

                var panel = statusGo.transform.Find("Panel");
                Check("MapPlayerStatusUI Panel has SoftEdgeImage", panel != null && panel.GetComponent<SoftEdgeImage>() != null);

                var hpBarBg = statusGo.transform.Find("HpBarBg") as RectTransform;
                Check("MapPlayerStatusUI HpBarBg size is (130, 22)", hpBarBg != null && hpBarBg.sizeDelta == new Vector2(130f, 22f));

                // The deck is the pile at the bottom of the screen now (RunDeckButton), not a button in the status bar
                Check("MapPlayerStatusUI has no DeckButton", statusGo.transform.Find("DeckButton") == null);

                // ==========================================
                // 4. P1-9: MapLegendUI SoftEdge Polish
                // ==========================================
                var legendGo = new GameObject("TestMapLegendUI", typeof(RectTransform), typeof(MapLegendUI));
                legendGo.transform.SetParent(testRoot.transform);
                var legendUI = legendGo.GetComponent<MapLegendUI>();
                legendUI.EnsureUI();

                var popupBg = legendGo.transform.Find("NodePopup/Background");
                Check("MapLegendUI NodePopup Background has SoftEdgeImage", popupBg != null && popupBg.GetComponent<SoftEdgeImage>() != null);

                var popupBorder = legendGo.transform.Find("NodePopup/Border");
                Check("MapLegendUI NodePopup Border has SoftEdgeImage", popupBorder != null && popupBorder.GetComponent<SoftEdgeImage>() != null);

                // ==========================================
                // Summary
                // ==========================================
                Debug.Log($"[VictoryAndMapVisualTests] Finished: {passed} passed, {failed} failed");
            }
            finally
            {
                Object.DestroyImmediate(testRoot);
            }
        }
    }
}
