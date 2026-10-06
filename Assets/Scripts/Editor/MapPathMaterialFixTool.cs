using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// MapPathPrefab has no material assigned. In the Editor MapPathRenderer.EnsureValidMaterial fills it from
    /// Assets/ProjectAsset/MapNavigate/String.mat through AssetDatabase, which does not exist in a player build,
    /// so the sacred-thread paths could render without the right material there (plan task A1).
    /// This assigns the material in the prefab itself. Safe to run more than once.
    /// Batch: -executeMethod TawanOS.EditorTools.MapPathMaterialFixTool.Assign
    /// </summary>
    public static class MapPathMaterialFixTool
    {
        private const string PrefabPath = "Assets/MapEngineData/Prefabs/MapPathPrefab.prefab";
        private const string MaterialPath = "Assets/ProjectAsset/MapNavigate/String.mat";

        [MenuItem("Tools/TawanOS/Map Engine/Assign Thread Material To MapPathPrefab")]
        public static void AssignFromMenu()
        {
            Assign();
        }

        public static void Assign()
        {
            bool ok = TryAssign();
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool TryAssign()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Debug.LogError($"[MapPathMaterialFixTool] Material not found at {MaterialPath}");
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var lines = root.GetComponentsInChildren<LineRenderer>(true);
                if (lines.Length == 0)
                {
                    Debug.LogError("[MapPathMaterialFixTool] No LineRenderer in MapPathPrefab");
                    return false;
                }

                bool changed = false;
                foreach (var line in lines)
                {
                    if (line.sharedMaterial == material) continue;
                    line.sharedMaterial = material;
                    changed = true;
                }

                if (changed) PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[MapPathMaterialFixTool] {(changed ? "Assigned" : "Already assigned")} {material.name} on {lines.Length} LineRenderer(s)");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
