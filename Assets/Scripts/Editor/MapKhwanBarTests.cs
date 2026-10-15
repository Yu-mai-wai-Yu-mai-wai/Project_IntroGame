using System.IO;
using TawanOS.GameFlow;
using TawanOS.MapEngine;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Checks plan task P0-10: Map top bar (MapPlayerStatusUI) Khwan bar fill is proportional to current/max HP
    /// and refreshes automatically on RunState.Current.OnChanged.
    /// Menu: Tools/TawanOS/Tests/Map Khwan Bar.
    /// Batch: -executeMethod TawanOS.EditorTools.MapKhwanBarTests.Run
    /// </summary>
    public static class MapKhwanBarTests
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "run_save.json");

        [MenuItem("Tools/TawanOS/Tests/Map Khwan Bar")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;
            string backupSave = File.Exists(SavePath) ? File.ReadAllText(SavePath) : null;
            var testGo = new GameObject("MapKhwanBarTests");

            try
            {
                // Start a fresh run (default MaxHp = 20, CurrentHp = 20)
                RunState.StartNewRun(RunState.DefaultTotalFloors);

                var statusGo = new GameObject("TestMapPlayerStatusUI", typeof(RectTransform), typeof(MapPlayerStatusUI));
                statusGo.transform.SetParent(testGo.transform);
                var statusUI = statusGo.GetComponent<MapPlayerStatusUI>();

                statusUI.EnsureUI();
                statusUI.Subscribe();
                statusUI.UpdateDisplay();

                failures += Expect("MapPlayerStatusUI builds HP bar fill image", statusUI.HpBarFill != null);
                failures += Expect("HP bar fill image has sprite assigned for filled rendering", statusUI.HpBarFill.sprite != null);
                failures += Expect("HP bar fill image is type Filled", statusUI.HpBarFill.type == Image.Type.Filled);
                failures += Expect("HP bar fill image method is Horizontal", statusUI.HpBarFill.fillMethod == Image.FillMethod.Horizontal);
                failures += Expect("Initial fillAmount is 1.0 (20/20)", Mathf.Approximately(statusUI.FillAmount, 1f));
                failures += Expect("Initial HP text displays 20/20", statusUI.HpText != null && statusUI.HpText.text == "20/20");

                // --- Test ticket P0-10 core requirement: set HP 10/20 -> fill 0.5 ---
                RunState.Current.SetCurrentHp(10);

                failures += Expect("Current HP is 10", statusUI.CurrentHp == 10);
                failures += Expect("Max HP is 20", statusUI.MaxHp == 20);
                failures += Expect("Setting HP 10/20 updates fillAmount to 0.5 via OnChanged", Mathf.Approximately(statusUI.FillAmount, 0.5f));
                failures += Expect("HP text displays 10/20", statusUI.HpText != null && statusUI.HpText.text == "10/20");

                // --- Test additional ratios ---
                RunState.Current.SetCurrentHp(5);
                failures += Expect("Setting HP 5/20 updates fillAmount to 0.25", Mathf.Approximately(statusUI.FillAmount, 0.25f));
                failures += Expect("HP text displays 5/20", statusUI.HpText != null && statusUI.HpText.text == "5/20");

                RunState.Current.Heal(5);
                failures += Expect("Healing 5 restores HP to 10/20 (fill 0.5)", Mathf.Approximately(statusUI.FillAmount, 0.5f));

                // --- Test MaxHp changes ---
                RunState.Current.ChangeMaxHp(5); // MaxHp becomes 25, CurrentHp becomes 15 -> 15/25 = 0.6
                failures += Expect("Changing MaxHp +5 (15/25) updates fillAmount to 0.6", Mathf.Approximately(statusUI.FillAmount, 0.6f));
                failures += Expect("HP text displays 15/25", statusUI.HpText != null && statusUI.HpText.text == "15/25");

                statusUI.Unsubscribe();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MapKhwanBarTests] Unexpected exception: {ex}");
                failures++;
            }
            finally
            {
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
                Debug.Log("[MapKhwanBarTests] PASS (all checks passed)");
            else
                Debug.LogError($"[MapKhwanBarTests] FAIL ({failures} checks failed)");

            if (exitOnFinish && Application.isBatchMode)
                EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Expect(string label, bool condition)
        {
            if (condition)
            {
                Debug.Log($"[MapKhwanBarTests] ok   {label}");
                return 0;
            }
            Debug.LogError($"[MapKhwanBarTests] FAIL {label}");
            return 1;
        }
    }
}
