#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TawanOS.MapEngine
{
    [InitializeOnLoad]
    public static class MapEngineAutoRunner
    {
        static MapEngineAutoRunner()
        {
            EditorApplication.delayCall += RunOnce;
        }

        private static void RunOnce()
        {
            string flagPath = "Temp/MapEngineSetupRunFlag_v4.txt";
            if (!System.IO.File.Exists(flagPath))
            {
                System.IO.File.WriteAllText(flagPath, "done");
                Debug.Log("[MapEngineAutoRunner] Triggering SetupTestSceneAndProfiles (v4)...");
                new MapSaveManager().ClearSavedMap();
                MapEngineSetupTool.SetupTestSceneAndProfiles();
            }
        }
    }
}
#endif
