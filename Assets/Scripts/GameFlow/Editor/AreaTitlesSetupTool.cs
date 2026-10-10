using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Makes Resources/AreaTitles.asset (<see cref="AreaTitlesSO"/>, the place name and task shown when entering a
    /// map node) from <see cref="AreaTitlesSO.Defaults"/>. Keeps every entry already written on the asset and only
    /// adds the node types it lacks. Safe to run again.
    /// </summary>
    public static class AreaTitlesSetupTool
    {
        private const string AssetPath = "Assets/Resources/AreaTitles.asset";

        [MenuItem("Tools/TawanOS/Game Flow/Create Area Titles")]
        private static void CreateMenu()
        {
            Create();
        }

        /// <summary>-executeMethod TawanOS.GameFlow.AreaTitlesSetupTool.CreateBatch</summary>
        public static void CreateBatch()
        {
            EditorApplication.Exit(Create() ? 0 : 1);
        }

        public static bool Create()
        {
            var asset = AssetDatabase.LoadAssetAtPath<AreaTitlesSO>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<AreaTitlesSO>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            if (asset.entries == null) asset.entries = new List<AreaTitlesSO.Entry>();

            int added = 0;
            foreach (var d in AreaTitlesSO.Defaults)
            {
                if (asset.entries.Exists(e => e.nodeType == d.nodeType)) continue;
                asset.entries.Add(d);
                added++;
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AreaTitlesSetupTool] {AssetPath} ready ({asset.entries.Count} node types, {added} added).");
            return asset.entries.Count >= AreaTitlesSO.Defaults.Length;
        }
    }
}
