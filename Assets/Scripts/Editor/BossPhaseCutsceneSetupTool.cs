#if UNITY_EDITOR
using System.Collections.Generic;
using TawanOS.CardEngine;
using TawanOS.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TawanOS.EditorTools
{
    // Puts a BossPhaseCutscene (the boss's comeback when it changes phase) in CombatTestScene and fills its boss (the
    // dressing's Scarecrow) and its props: the dressing's things standing on the table top (candles, beads, decks,
    // slot outlines), every board slot, so the cards in play jump too, the draw pile and the graveyard. Running it
    // again refills the boss and the props and keeps the other values. The scene is
    // opened additively when it is not open, saved and closed again, so the scene open in the editor is untouched.
    public static class BossPhaseCutsceneSetupTool
    {
        private const string ScenePath = "Assets/Scenes/CombatTestScene.unity";
        private const string DressingName = "CombatDressing";
        private const string TableName = "Table";
        private const string BossName = "Scarecrow";
        private const string ObjectName = "BossPhaseCutscene";

        // Dressing parts that are not on the table top: the table, the backdrop and the strings overhead
        private static readonly string[] NotProps = { "Table", "Camera", "Bg", "Ground", "TreeBG", "Scarecrow", "RedString" };

        [MenuItem("Tools/TawanOS/Combat Scene/Add Boss Phase Cutscene")]
        private static void Setup_Menu()
        {
            Setup();
        }

        // Batch: -executeMethod TawanOS.EditorTools.BossPhaseCutsceneSetupTool.Setup
        public static void Setup()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            bool ok = AddTo(scene);
            if (ok) EditorSceneManager.SaveScene(scene);
            if (opened && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
            if (!ok && Application.isBatchMode) EditorApplication.Exit(1);
        }

        private static bool AddTo(Scene scene)
        {
            GameObject dressing = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == DressingName) dressing = root;
            }
            var table = dressing != null ? dressing.transform.Find(TableName) : null;
            var tableRenderer = table != null ? table.GetComponentInChildren<Renderer>() : null;
            if (tableRenderer == null)
            {
                Debug.LogError($"[BossPhaseCutsceneSetupTool] {DressingName}/{TableName} not found in {scene.path} (run Apply Blender Dressing first).");
                return false;
            }

            BossPhaseCutscene fx = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (fx == null) fx = root.GetComponentInChildren<BossPhaseCutscene>(true);
            }
            if (fx == null)
            {
                var go = new GameObject(ObjectName);
                SceneManager.MoveGameObjectToScene(go, scene);
                Undo.RegisterCreatedObjectUndo(go, "Add Boss Phase Cutscene");
                fx = go.AddComponent<BossPhaseCutscene>();
            }

            Undo.RecordObject(fx, "Fill Boss Phase Cutscene");
            Undo.RecordObject(fx.gameObject, "Fill Boss Phase Cutscene");
            fx.gameObject.name = ObjectName;
            fx.boss = dressing.transform.Find(BossName);
            if (fx.boss == null) Debug.LogWarning($"[BossPhaseCutsceneSetupTool] {DressingName}/{BossName} not found: set the boss in the Inspector.");
            var top = tableRenderer.bounds;
            fx.props = new List<Transform>();
            foreach (Transform child in dressing.transform)
            {
                if (IsNotProp(child.name)) continue;
                var p = child.position;
                // On the table: over its top, not under it or far above it
                if (p.x < top.min.x || p.x > top.max.x || p.z < top.min.z || p.z > top.max.z) continue;
                if (p.y < top.max.y - 0.5f || p.y > top.max.y + 4f) continue;
                fx.props.Add(child);
            }
            int dressingProps = fx.props.Count;
            foreach (var root in scene.GetRootGameObjects())
            {
                // The deck pile and the grave slab are drawn where their objects stand, so they jump with them
                foreach (var slot in root.GetComponentsInChildren<BoardSlotView>(true)) AddOnce(fx.props, slot.transform);
                foreach (var deck in root.GetComponentsInChildren<DeckPileView3D>(true)) AddOnce(fx.props, deck.transform);
                foreach (var grave in root.GetComponentsInChildren<GraveyardView3D>(true)) AddOnce(fx.props, grave.transform);
            }
            EditorUtility.SetDirty(fx);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"<color=green>[BossPhaseCutsceneSetupTool] BossPhaseCutscene in {scene.name}: boss {(fx.boss != null ? fx.boss.name : "none")}, {dressingProps} table props and {fx.props.Count - dressingProps} board objects.</color>");
            return true;
        }

        private static void AddOnce(List<Transform> list, Transform t)
        {
            if (!list.Contains(t)) list.Add(t);
        }

        private static bool IsNotProp(string name)
        {
            foreach (var n in NotProps)
            {
                if (name == n || name.StartsWith(n + ".")) return true;
            }
            return false;
        }
    }
}
#endif
