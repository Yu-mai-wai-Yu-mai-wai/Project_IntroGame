using System.IO;
using System.Reflection;
using TawanOS.CardEngine;
using TawanOS.GameFlow;
using TawanOS.UI;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Checks plan task P0-2:
    /// (a) After EndCombat(false) (defeat), all combat input gates are closed (CardPlayController3D, TurnPhaseController, CombatCameraRig3D, CardView3D).
    /// (b) Defeat sequence UI created: character art (IMG_7920), [ชั่วคราว] dialogue lines, procedural red blood overlay, and stats panel.
    /// Menu: Tools/TawanOS/Tests/Defeat Sequence.
    /// Batch: -executeMethod TawanOS.EditorTools.DefeatSequenceTests.Run
    /// </summary>
    public static class DefeatSequenceTests
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "run_save.json");

        [MenuItem("Tools/TawanOS/Tests/Defeat Sequence")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;
            string backupSave = File.Exists(SavePath) ? File.ReadAllText(SavePath) : null;
            var testGo = new GameObject("DefeatSequenceTests");
            var prevCombat = CombatManager.Instance;
            var prevCameraRig = CombatCameraRig3D.Instance;

            try
            {
                // =========================================================================
                // Part (a): Input gate after EndCombat(false)
                // =========================================================================
                Debug.Log("[DefeatSequenceTests] --- Testing Input Gate Closure on Defeat ---");

                var combatGo = new GameObject("TestCombatManager", typeof(CombatManager));
                combatGo.transform.SetParent(testGo.transform);
                var combat = combatGo.GetComponent<CombatManager>();
                typeof(CombatManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                    ?.GetSetMethod(true)?.Invoke(null, new object[] { combat });

                var cameraRigGo = new GameObject("TestCombatCameraRig", typeof(CombatCameraRig3D));
                cameraRigGo.transform.SetParent(testGo.transform);
                var cameraRig = cameraRigGo.GetComponent<CombatCameraRig3D>();
                typeof(CombatCameraRig3D).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                    ?.GetSetMethod(true)?.Invoke(null, new object[] { cameraRig });

                var playCtrlGo = new GameObject("TestCardPlayController", typeof(CardPlayController3D));
                playCtrlGo.transform.SetParent(testGo.transform);
                var playCtrl = playCtrlGo.GetComponent<CardPlayController3D>();

                var turnsGo = new GameObject("TestTurnPhaseController", typeof(TurnPhaseController));
                turnsGo.transform.SetParent(testGo.transform);
                var turns = turnsGo.GetComponent<TurnPhaseController>();

                var cardGo = new GameObject("TestCardView", typeof(BoxCollider), typeof(CardView3D));
                cardGo.transform.SetParent(testGo.transform);
                var cardView = cardGo.GetComponent<CardView3D>();
                var dummyCard = new CardInstance { cardNameThai = "DummyFamiliar", cardType = CardType.Familiar };
                cardView.CardData = dummyCard;

                // End combat with defeat
                combat.EndCombat(false);

                failures += Expect("CombatManager marked IsCombatOver after EndCombat(false)", combat.IsCombatOver);
                failures += Expect("CombatCameraRig3D.BlocksInput is closed (true) after defeat", CombatCameraRig3D.BlocksInput);
                failures += Expect("CardView3D.IgnoresMouse is true after defeat", cardView.IgnoresMouse);
                failures += Expect("TurnPhaseController.CanPlayCard returns false after defeat", !turns.CanPlayCard(dummyCard));

                // Attempt to select a card into CardPlayController3D
                playCtrl.Select(cardView);
                failures += Expect("CardPlayController3D.Select is blocked (card not held)", !playCtrl.IsHolding(cardView));

                // Attempt to end turn via TurnPhaseController
                turns.RequestEndTurn();
                var endTurnField = typeof(TurnPhaseController).GetField("endTurnRequested", BindingFlags.NonPublic | BindingFlags.Instance);
                bool endTurnRequested = (bool)endTurnField.GetValue(turns);
                failures += Expect("TurnPhaseController.RequestEndTurn is blocked after defeat", !endTurnRequested);

                // Attempt to toggle camera top view
                cameraRig.ToggleTopView();
                var userTopField = typeof(CombatCameraRig3D).GetField("userTop", BindingFlags.NonPublic | BindingFlags.Instance);
                bool userTop = (bool)userTopField.GetValue(cameraRig);
                failures += Expect("CombatCameraRig3D.ToggleTopView is blocked after defeat", !userTop);

                // =========================================================================
                // Part (b): Defeat sequence UI & Assets
                // =========================================================================
                Debug.Log("[DefeatSequenceTests] --- Testing Defeat Sequence UI ---");

                var summary = new GameOverSummary
                {
                    enemyName = "วิญญาณร้าย",
                    incense = 12,
                    deckSize = 10,
                    totalFloors = 8,
                    maxHp = 20
                };

                var screen = GameOverScreen.Show(summary);
                failures += Expect("GameOverScreen instance created", screen != null && GameOverScreen.Instance == screen);
                failures += Expect("No character art on the defeat screen (dialogue only)", screen.characterImage == null);
                failures += Expect("Blood overlay image exists and has procedural sprite assigned", screen.bloodOverlay != null && screen.bloodOverlay.sprite != null);

                // Check dialogue lines start with [ชั่วคราว]
                failures += Expect("Defeat dialogue lines count >= 2", GameOverScreen.DefeatDialogueLines.Length >= 2);
                bool allStartWithTemp = true;
                foreach (var line in GameOverScreen.DefeatDialogueLines)
                {
                    if (!line.StartsWith("[ชั่วคราว]"))
                    {
                        allStartWithTemp = false;
                        break;
                    }
                }
                failures += Expect("All defeat dialogue lines start with [ชั่วคราว]", allStartWithTemp);
                failures += Expect("Dialogue text component exists", screen.dialogueText != null);

                // Check stats panel & buttons
                failures += Expect("Stats title is 'ขวัญหลุด'", screen.titleText != null && screen.titleText.text == "ขวัญหลุด");
                failures += Expect("Summary text contains enemy name and stats", screen.summaryText != null && screen.summaryText.text.Contains("วิญญาณร้าย") && screen.summaryText.text.Contains("12"));
                failures += Expect("Restart button exists", screen.restartButton != null);
                failures += Expect("MainMenu button exists", screen.menuButton != null);

                // Check fonts
                var theme = UIThemeSO.Current;
                if (theme.titleFont != null)
                {
                    failures += Expect("Title uses RueangLao (theme.titleFont)", screen.titleText.font == theme.titleFont);
                }
                if (theme.bodyFont != null)
                {
                    failures += Expect("Summary text uses EkkamaiVibe (theme.bodyFont)", screen.summaryText.font == theme.bodyFont);
                    failures += Expect("Dialogue text uses EkkamaiVibe (theme.bodyFont)", screen.dialogueText.font == theme.bodyFont);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DefeatSequenceTests] Unexpected exception: {ex}");
                failures++;
            }
            finally
            {
                typeof(CombatManager).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                    ?.GetSetMethod(true)?.Invoke(null, new object[] { prevCombat });
                typeof(CombatCameraRig3D).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                    ?.GetSetMethod(true)?.Invoke(null, new object[] { prevCameraRig });

                if (GameOverScreen.Instance != null)
                {
                    Object.DestroyImmediate(GameOverScreen.Instance.gameObject);
                }
                Object.DestroyImmediate(testGo);
                RunState.EndRun();
                if (backupSave != null)
                {
                    File.WriteAllText(SavePath, backupSave);
                }
                else
                {
                    RunState.DeleteSave();
                }
            }

            if (failures == 0)
                Debug.Log("[DefeatSequenceTests] PASS (all checks passed)");
            else
                Debug.LogError($"[DefeatSequenceTests] FAIL ({failures} checks failed)");

            if (exitOnFinish && Application.isBatchMode)
                EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Expect(string label, bool condition)
        {
            if (condition)
            {
                Debug.Log($"[DefeatSequenceTests] ok   {label}");
                return 0;
            }
            Debug.LogError($"[DefeatSequenceTests] FAIL {label}");
            return 1;
        }
    }
}
