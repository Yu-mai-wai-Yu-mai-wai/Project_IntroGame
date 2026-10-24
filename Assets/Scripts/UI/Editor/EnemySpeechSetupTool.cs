#if UNITY_EDITOR
using TawanOS.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TawanOS.EditorTools
{
    // Puts the ghost's speech box (EnemySpeech) into a combat scene, built out so its parts can be tuned in the
    // Inspector. Does nothing when the scene already has one.
    public static class EnemySpeechSetupTool
    {
        private const string CombatScenePath = "Assets/Scenes/CombatTestScene.unity";
        // Has the Thai mark positioning the UI theme fonts lack (tone marks over upper vowels, e.g. ที่)
        private const string FontPath = "Assets/Fonts/EkkamaiVibe SDF.asset";

        [MenuItem("Tools/TawanOS/UI/Add Enemy Speech")]
        private static void Add_Menu()
        {
            Add();
        }

        // Batch: -executeMethod TawanOS.EditorTools.EnemySpeechSetupTool.AddToCombatScene
        public static void AddToCombatScene()
        {
            var scene = EditorSceneManager.OpenScene(CombatScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[EnemySpeechSetupTool] {CombatScenePath} could not be opened.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Add();
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("Tools/TawanOS/UI/Rebuild Enemy Speech")]
        private static void Rebuild_Menu()
        {
            Rebuild();
        }

        // Batch: -executeMethod TawanOS.EditorTools.EnemySpeechSetupTool.RebuildInCombatScene
        // Replaces the combat scene's speech box with a fresh one in the current look (24 Oct: red line over a black haze)
        public static void RebuildInCombatScene()
        {
            var scene = EditorSceneManager.OpenScene(CombatScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[EnemySpeechSetupTool] {CombatScenePath} could not be opened.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Rebuild();
            EditorSceneManager.SaveScene(scene);
        }

        // Removes the open scene's speech box, keeping its timing and chance settings, and builds it again
        public static void Rebuild()
        {
            var old = Object.FindFirstObjectByType<EnemySpeech>(FindObjectsInactive.Include);
            string kept = old != null ? JsonUtility.ToJson(old) : null;
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            Add();
            var fresh = Object.FindFirstObjectByType<EnemySpeech>(FindObjectsInactive.Include);
            if (fresh == null || kept == null) return;
            var parts = (fresh.group, fresh.speaker, fresh.line, fresh.font);
            JsonUtility.FromJsonOverwrite(kept, fresh);
            (fresh.group, fresh.speaker, fresh.line, fresh.font) = parts;
            EditorUtility.SetDirty(fresh);
        }

        // Adds it to the open scene
        public static void Add()
        {
            if (Object.FindFirstObjectByType<EnemySpeech>(FindObjectsInactive.Include) != null)
            {
                Debug.Log("[EnemySpeechSetupTool] The scene already has an EnemySpeech - kept.");
                return;
            }

            var go = new GameObject("EnemySpeech");
            Undo.RegisterCreatedObjectUndo(go, "Add Enemy Speech");
            var speech = go.AddComponent<EnemySpeech>();
            speech.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            speech.BuildDefaultUI();
            speech.group.alpha = 0f;
            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;
            Debug.Log($"<color=green>[EnemySpeechSetupTool] EnemySpeech added to {go.scene.name}.</color>");
        }
    }
}
#endif
