#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Shortcut for the manual setup: makes sure the open combat scene has a GraveyardView3D and points its
    // Graveyard Object at the table set's GraveyardZone. Doing it by hand works the same way: add
    // GraveyardView3D to any object and drag the graveyard model into Graveyard Object.
    public static class GraveyardSetupTool
    {
        private const string GraveyardModelName = "GraveyardZone";

        [MenuItem("Tools/TawanOS/Card Engine/Hook Up Graveyard (GraveyardZone)")]
        public static void HookUpGraveyard()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Setup in Play Mode", "Please exit Play Mode first.", "OK");
                return;
            }

            var graveyard = Object.FindFirstObjectByType<GraveyardView3D>(FindObjectsInactive.Include);
            if (graveyard == null)
            {
                var go = new GameObject("Graveyard");
                Undo.RegisterCreatedObjectUndo(go, "Add Graveyard");
                graveyard = go.AddComponent<GraveyardView3D>();
            }

            if (graveyard.graveyardObject == null)
            {
                var model = FindInScene(GraveyardModelName);
                if (model != null)
                {
                    Undo.RecordObject(graveyard, "Assign graveyard object");
                    graveyard.graveyardObject = model;
                }
                else
                {
                    Debug.LogWarning($"[GraveyardSetupTool] No '{GraveyardModelName}' in the open scene - drag your graveyard object into Graveyard Object by hand.");
                }
            }

            EditorSceneManager.MarkSceneDirty(graveyard.gameObject.scene);
            Selection.activeObject = graveyard.gameObject;
            Debug.Log($"<color=green>[GraveyardSetupTool] Graveyard uses '{(graveyard.graveyardObject != null ? graveyard.graveyardObject.name : graveyard.name)}'. Save the scene to keep it.</color>");
        }

        private static GameObject FindInScene(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) return t.gameObject;
            return null;
        }
    }
}
#endif
