using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using TawanOS.GameFlow;
using TawanOS.ShopEngine;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Checks plan task P0-8: Shop economy and blessing price escalation.
    /// MinorEnemyIncense and EventFightIncense are (6,10).
    /// Blessings scale: MaxKhwan base 8 step +4; MaxMerit base 15 step +8.
    /// Menu: Tools/TawanOS/Tests/Shop Economy.
    /// Batch: -executeMethod TawanOS.EditorTools.ShopEconomyTests.Run
    /// </summary>
    public static class ShopEconomyTests
    {
        private const string ShopConfigPath = "Assets/ShopEngineData/ShopConfig.asset";
        private static string SavePath => Path.Combine(Application.persistentDataPath, "run_save.json");

        [MenuItem("Tools/TawanOS/Tests/Shop Economy")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;

            // 1. GameFlowManager incense drop ranges
            var minorIncenseField = typeof(GameFlowManager).GetField("MinorEnemyIncense", BindingFlags.NonPublic | BindingFlags.Static);
            var eventIncenseField = typeof(GameFlowManager).GetField("EventFightIncense", BindingFlags.NonPublic | BindingFlags.Static);
            failures += Expect("GameFlowManager has MinorEnemyIncense field", minorIncenseField != null);
            failures += Expect("GameFlowManager has EventFightIncense field", eventIncenseField != null);
            if (minorIncenseField != null)
            {
                var val = (Vector2Int)minorIncenseField.GetValue(null);
                failures += Expect("MinorEnemyIncense is (6, 10)", val == new Vector2Int(6, 10));
            }
            if (eventIncenseField != null)
            {
                var val = (Vector2Int)eventIncenseField.GetValue(null);
                failures += Expect("EventFightIncense is (6, 10)", val == new Vector2Int(6, 10));
            }

            // 2. ShopConfig asset configuration
            var config = AssetDatabase.LoadAssetAtPath<ShopConfigSO>(ShopConfigPath);
            failures += Expect("ShopConfig.asset loads", config != null);
            if (config != null)
            {
                var khwanBlessing = config.blessings.FirstOrDefault(b => b.kind == ShopBlessingKind.MaxKhwan);
                failures += Expect("ShopConfig has MaxKhwan blessing", khwanBlessing != null);
                if (khwanBlessing != null)
                {
                    failures += Expect("MaxKhwan blessing amount is 5", khwanBlessing.amount == 5);
                    failures += Expect("MaxKhwan blessing base price is 8", khwanBlessing.price == 8);
                    failures += Expect("MaxKhwan blessing price step is 4", khwanBlessing.priceStep == 4);
                }

                var meritBlessing = config.blessings.FirstOrDefault(b => b.kind == ShopBlessingKind.MaxMerit);
                failures += Expect("ShopConfig has MaxMerit blessing", meritBlessing != null);
                if (meritBlessing != null)
                {
                    failures += Expect("MaxMerit blessing amount is 1", meritBlessing.amount == 1);
                    failures += Expect("MaxMerit blessing base price is 15", meritBlessing.price == 15);
                    failures += Expect("MaxMerit blessing price step is 8", meritBlessing.priceStep == 8);
                }
            }

            // 3. Purchase escalation and persistence: buy max-khwan blessing 3 times -> prices 8, 12, 16
            string backupSave = File.Exists(SavePath) ? File.ReadAllText(SavePath) : null;
            var go = new GameObject("ShopEconomyTests");
            try
            {
                RunState.StartNewRun(RunState.DefaultTotalFloors);
                var run = RunState.Current;
                run.AddIncense(500); // give enough incense for all test buys

                var shop = go.AddComponent<ShopManager>();
                shop.config = config;

                // First visit / buy 1
                var rollStockMethod = typeof(ShopManager).GetMethod("RollStock", BindingFlags.NonPublic | BindingFlags.Instance);
                rollStockMethod.Invoke(shop, null);

                var offer1 = shop.BlessingOffers.FirstOrDefault(o => o.blessing.kind == ShopBlessingKind.MaxKhwan);
                failures += Expect("Visit 1: MaxKhwan blessing offered", offer1 != null);
                failures += Expect("Visit 1: MaxKhwan price is 8", offer1 != null && offer1.price == 8);
                if (offer1 != null) shop.BuyBlessing(offer1);
                failures += Expect("After buy 1: times bought is 1", run.GetBlessingPurchases(ShopBlessingKind.MaxKhwan) == 1);

                // Second visit / buy 2
                rollStockMethod.Invoke(shop, null);
                var offer2 = shop.BlessingOffers.FirstOrDefault(o => o.blessing.kind == ShopBlessingKind.MaxKhwan);
                failures += Expect("Visit 2: MaxKhwan price is 12 (8 + 4)", offer2 != null && offer2.price == 12);
                if (offer2 != null) shop.BuyBlessing(offer2);
                failures += Expect("After buy 2: times bought is 2", run.GetBlessingPurchases(ShopBlessingKind.MaxKhwan) == 2);

                // Third visit / buy 3
                rollStockMethod.Invoke(shop, null);
                var offer3 = shop.BlessingOffers.FirstOrDefault(o => o.blessing.kind == ShopBlessingKind.MaxKhwan);
                failures += Expect("Visit 3: MaxKhwan price is 16 (8 + 8)", offer3 != null && offer3.price == 16);
                if (offer3 != null) shop.BuyBlessing(offer3);
                failures += Expect("After buy 3: times bought is 3", run.GetBlessingPurchases(ShopBlessingKind.MaxKhwan) == 3);

                // 4. Persistence across Save / Load
                failures += Expect("Save file exists after purchases", RunState.HasSave);
                string json = File.ReadAllText(SavePath);
                failures += Expect("Save JSON records blessing purchases", json.Contains("blessingPurchases") || json.Contains("MaxKhwan"));

                // Reset in-memory run and reload from disk
                RunState.LoadSavedRun();
                failures += Expect("Reloaded run retains 3 MaxKhwan blessing purchases",
                    RunState.Current.GetBlessingPurchases(ShopBlessingKind.MaxKhwan) == 3);

                // Next visit should offer at 20 (8 + 4*3)
                rollStockMethod.Invoke(shop, null);
                var offer4 = shop.BlessingOffers.FirstOrDefault(o => o.blessing.kind == ShopBlessingKind.MaxKhwan);
                failures += Expect("Visit 4 after reload: MaxKhwan price is 20", offer4 != null && offer4.price == 20);

                // MaxMerit blessing should still be at base price 15
                var meritOffer = shop.BlessingOffers.FirstOrDefault(o => o.blessing.kind == ShopBlessingKind.MaxMerit);
                failures += Expect("MaxMerit untouched blessing price is 15", meritOffer != null && meritOffer.price == 15);
            }
            finally
            {
                Object.DestroyImmediate(go);
                RunState.EndRun();
                if (backupSave != null)
                {
                    File.WriteAllText(SavePath, backupSave);
                    RunState.LoadSavedRun();
                }
            }

            Debug.Log(failures == 0 ? "[ShopEconomyTests] PASS" : $"[ShopEconomyTests] FAIL: {failures} check(s) failed");
            if (exitOnFinish) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static int Expect(string label, bool ok)
        {
            Debug.Log($"[ShopEconomyTests] {(ok ? "ok  " : "FAIL")} {label}");
            return ok ? 0 : 1;
        }
    }
}
