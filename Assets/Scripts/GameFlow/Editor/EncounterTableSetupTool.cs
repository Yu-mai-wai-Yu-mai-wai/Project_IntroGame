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
        private const string KrasueGhostPath = "Assets/CardEngineData/Enemies/NewEnemyProfile.asset";
        private const string GraveGhostPath = "Assets/CardEngineData/Enemies/GraveGhostProfile.asset";
        private const string PraiElitePath = "Assets/CardEngineData/Enemies/PraiGhostEliteProfile.asset";
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
            var krasue = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(KrasueGhostPath);
            var grave = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(GraveGhostPath);
            var praiElite = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(PraiElitePath);
            var boss = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(BossPath);

            if (prai == null || krasue == null || grave == null || praiElite == null || boss == null)
            {
                Debug.LogError($"[EncounterTableSetupTool] Missing enemy asset (Prai={(prai != null)}, Krasue={(krasue != null)}, Grave={(grave != null)}, Elite={(praiElite != null)}, Boss={(boss != null)})");
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

            Undo.RecordObject(table, "Setup Full Encounter Table");
            table.minor.Clear();
            table.elite.Clear();
            table.boss.Clear();

            AddIfMissing(table.minor, prai);
            AddIfMissing(table.minor, krasue);
            AddIfMissing(table.minor, grave);

            AddIfMissing(table.elite, praiElite);
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
