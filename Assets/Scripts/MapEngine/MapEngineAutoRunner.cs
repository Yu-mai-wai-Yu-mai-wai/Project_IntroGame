#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TawanOS.MapEngine
{
    // This used to run automatically on every Editor load that had no flag file in Temp/ (a fresh clone,
    // or after Temp/ was cleared). That rebuilt MapTestScene and the node profiles over the team's work:
    // it replaced the Thai node names with English defaults and rewrote the scene with new fileIDs, which
    // is also why two people's MapTestScene versions could not be merged. It now only runs on request.
    public static class MapEngineAutoRunner
    {
        [MenuItem("Tools/TawanOS/Map Engine/Reset Map Save + Rebuild Test Scene (overwrites MapTestScene and profiles)")]
        private static void RebuildOnRequest()
        {
            if (!EditorUtility.DisplayDialog(
                    "Rebuild MapTestScene and node profiles?",
                    "This overwrites MapTestScene, the node profiles and the saved map with the generated defaults. " +
                    "Commit or back up your work first.",
                    "Rebuild", "Cancel"))
            {
                return;
            }

            Debug.Log("[MapEngineAutoRunner] Rebuilding on request...");
            new MapSaveManager().ClearSavedMap();
            MapEngineSetupTool.SetupTestSceneAndProfiles();
        }
    }
}
#endif
