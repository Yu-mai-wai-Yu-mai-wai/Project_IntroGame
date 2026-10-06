using System;
using UnityEditor;
using UnityEngine;
using TawanOS.CardEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Creates or refills Assets/Resources/EncounterTable.asset (plan task A1). Refilling only adds enemies that
    /// are missing, so hand edits to the lists are kept. Batch:
    /// Unity.exe -batchmode -nographics -quit -projectPath ... -executeMethod TawanOS.GameFlow.EncounterTableSetupTool.CreateEncounterTable
    /// </summary>
    public static class EncounterTableSetupTool
    {
        private const string AssetFolder = "Assets/Resources";
        private const string AssetPath = AssetFolder + "/" + EncounterTableSO.ResourceName + ".asset";

        private const string PraiGhostPath = "Assets/CardEngineData/Enemies/PraiGhostProfile.asset";
        private const string BossPath = "Assets/CardEngineData/Enemies/PhiTaiHongBossProfile.asset";

        [MenuItem("Tools/TawanOS/Game Flow/Create Encounter Table")]
        public static void CreateEncounterTableFromMenu()
        {
            CreateEncounterTable();
        }

        // Entry point for -executeMethod. Exits with code 1 when an enemy asset cannot be found.
        public static void CreateEncounterTable()
        {
            bool ok = TryCreate();
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool TryCreate()
        {
            var prai = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(PraiGhostPath);
            var boss = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(BossPath);
            if (prai == null || boss == null)
            {
                Debug.LogError($"[EncounterTableSetupTool] Missing enemy asset (PraiGhost={(prai != null)}, Boss={(boss != null)})");
                return false;
            }

            if (!AssetDatabase.IsValidFolder(AssetFolder)) AssetDatabase.CreateFolder("Assets", "Resources");

            var table = AssetDatabase.LoadAssetAtPath<EncounterTableSO>(AssetPath);
            bool created = table == null;
            if (created)
            {
                table = ScriptableObject.CreateInstance<EncounterTableSO>();
                AssetDatabase.CreateAsset(table, AssetPath);
            }

            // Starting roster (plan A1): the elite slot reuses PraiGhost until A7 adds a real elite profile
            AddIfMissing(table.minor, prai);
            AddIfMissing(table.elite, prai);
            AddIfMissing(table.boss, boss);

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            Debug.Log($"[EncounterTableSetupTool] {(created ? "Created" : "Updated")} {AssetPath}: minor={table.minor.Count} elite={table.elite.Count} boss={table.boss.Count}");
            return true;
        }

        private static void AddIfMissing(System.Collections.Generic.List<EnemyProfileSO> list, EnemyProfileSO enemy)
        {
            if (!list.Contains(enemy)) list.Add(enemy);
        }
    }
}
