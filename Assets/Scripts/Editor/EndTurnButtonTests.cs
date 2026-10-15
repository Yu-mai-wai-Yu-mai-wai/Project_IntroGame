using System.Reflection;
using NUnit.Framework;
using TawanOS.CardEngine;
using TawanOS.UI;
using TawanOS.VFX;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.Tests
{
    public static class EndTurnButtonTests
    {
        [MenuItem("Tools/TawanOS/Tests/End Turn Button Tests")]
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

            var hudGo = new GameObject("TestCombatStatusHUD");
            var hud = hudGo.AddComponent<CombatStatusHUD>();

            // BuildUI via reflection
            var buildMethod = typeof(CombatStatusHUD).GetMethod("BuildUI", BindingFlags.NonPublic | BindingFlags.Instance);
            buildMethod.Invoke(hud, null);

            var playerField = typeof(CombatStatusHUD).GetField("player", BindingFlags.NonPublic | BindingFlags.Instance);
            object playerObj = playerField.GetValue(hud);

            var playerType = playerObj.GetType();
            var root = playerType.GetField("root").GetValue(playerObj) as RectTransform;
            var title = playerType.GetField("title").GetValue(playerObj) as TMP_Text;
            var detail = playerType.GetField("detail").GetValue(playerObj) as TMP_Text;
            var fill = playerType.GetField("fill").GetValue(playerObj) as RectTransform;
            var button = playerType.GetField("button").GetValue(playerObj) as Button;
            var buttonLabel = playerType.GetField("buttonLabel").GetValue(playerObj) as TMP_Text;

            Check("Player panel root exists", root != null);
            Check("Player panel has SoftEdgeImage attached", root != null && root.GetComponent<SoftEdgeImage>() != null);
            Check("Player panel has title 'ผู้เล่น'", title != null && title.text == "ผู้เล่น");
            Check("Player panel has button", button != null);
            Check("Player panel button has UiSmokeButton attached", button != null && button.GetComponent<UiSmokeButton>() != null);
            Check("Player panel button label exists", buttonLabel != null);

            var theme = UIThemeSO.Current;
            if (theme != null && theme.titleFont != null)
            {
                Check("Title uses MN-RueangLao SDF", title.font == theme.titleFont);
                Check("Button label uses MN-RueangLao SDF", buttonLabel.font == theme.titleFont);
            }

            // Test phase labels via Update simulation
            var combatGo = new GameObject("TestCombatManager");
            var combat = combatGo.AddComponent<CombatManager>();
            typeof(CombatStatusHUD).GetField("combat", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hud, combat);

            var turnsGo = new GameObject("TestTurnPhaseController");
            var turns = turnsGo.AddComponent<TurnPhaseController>();

            var updateMethod = typeof(CombatStatusHUD).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
            var setPhaseMethod = typeof(TurnPhaseController).GetMethod("SetPhase", BindingFlags.NonPublic | BindingFlags.Instance);

            // Test Summon phase
            if (setPhaseMethod != null) setPhaseMethod.Invoke(turns, new object[] { TurnPhase.PlayerBoard });
            updateMethod.Invoke(hud, null);
            Check("In PlayerBoard phase, button label is 'จบช่วงลงบริวาร'", buttonLabel.text == "จบช่วงลงบริวาร");
            Check("In PlayerBoard phase, button is interactable", button.interactable == true);

            // Test Spell phase
            if (setPhaseMethod != null) setPhaseMethod.Invoke(turns, new object[] { TurnPhase.PlayerSpell });
            updateMethod.Invoke(hud, null);
            Check("In PlayerSpell phase, button label is 'จบเทิร์น'", buttonLabel.text == "จบเทิร์น");
            Check("In PlayerSpell phase, button is interactable", button.interactable == true);

            // Test Enemy phase
            if (setPhaseMethod != null) setPhaseMethod.Invoke(turns, new object[] { TurnPhase.EnemyBoard });
            updateMethod.Invoke(hud, null);
            Check("In EnemyBoard phase, button is disabled", button.interactable == false);

            // Test Combat over
            combat.EndCombat(true);
            updateMethod.Invoke(hud, null);
            Check("When combat is over, button is disabled", button.interactable == false);

            Object.DestroyImmediate(turnsGo);
            Object.DestroyImmediate(combatGo);
            Object.DestroyImmediate(hudGo);

            Debug.Log($"<b>[EndTurnButtonTests Summary]</b> Passed: {passed}, Failed: {failed}");
        }
    }
}
