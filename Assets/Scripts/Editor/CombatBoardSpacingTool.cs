using TawanOS.CardEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Moves the player's and the enemy's board rows in CombatTestScene further apart, so the center pit's cards fit
    /// between them at the size of a board card (Game Designer, 22 Oct). The row slots (BoardSlotView, deck slots stay)
    /// and the slot frames drawn in the table model (CombatDressing: CardSlot*, CardSlot_Opp* and the cards lying in
    /// them) move to the new rows. Also takes the old draw pit's smoke (DrawPitSmoke) out of the scene: the pit is gone
    /// (Game Designer, 22 Oct). Idempotent. Re-run it after Tools/TawanOS/Combat Scene dressing rebuilds the table.
    /// Menu: Tools/TawanOS/Combat Scene/Space Board Rows. Batch: -executeMethod TawanOS.EditorTools.CombatBoardSpacingTool.Run
    /// </summary>
    public static class CombatBoardSpacingTool
    {
        private const string ScenePath = "Assets/Scenes/CombatTestScene.unity";
        public const float PlayerRowZ = 1.79f; // was 2.43
        public const float EnemyRowZ = 8.21f;  // was 7.57
        private const float OldPlayerRowZ = 2.43f, OldEnemyRowZ = 7.57f;

        [MenuItem("Tools/TawanOS/Combat Scene/Space Board Rows")]
        public static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Apply();
        }

        public static void Run() { EditorApplication.Exit(Apply() ? 0 : 1); }

        private static bool Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int moved = 0;
            foreach (var slot in Object.FindObjectsByType<BoardSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (slot.GetComponent<DeckPileView3D>() != null || slot.name.Contains("Deck")) continue;
                var p = slot.transform.position;
                float z = slot.side == BoardSlotView.SlotSide.Player ? PlayerRowZ : EnemyRowZ;
                if (Mathf.Approximately(p.z, z)) continue;
                Undo.RecordObject(slot.transform, "Space board rows");
                slot.transform.position = new Vector3(p.x, p.y, z);
                moved++;
            }

            // The table model's slot frames (and the cards lying in them) that are still on the old rows
            var dressing = GameObject.Find("CombatDressing");
            if (dressing != null)
            {
                foreach (Transform t in dressing.GetComponentsInChildren<Transform>(true))
                {
                    if (t == dressing.transform) continue;
                    float z = t.position.z;
                    float target = Mathf.Abs(z - OldPlayerRowZ) < 0.06f ? PlayerRowZ + (z - OldPlayerRowZ)
                                 : Mathf.Abs(z - OldEnemyRowZ) < 0.06f ? EnemyRowZ + (z - OldEnemyRowZ) : float.NaN;
                    if (float.IsNaN(target) || !(t.name.StartsWith("CardSlot") || t.name.StartsWith("Card."))) continue;
                    Undo.RecordObject(t, "Space board rows");
                    t.position = new Vector3(t.position.x, t.position.y, target);
                    moved++;
                }
            }

            foreach (var smoke in Object.FindObjectsByType<TawanOS.VFX.DrawPitSmoke>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.DestroyObjectImmediate(smoke.gameObject);
                moved++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[CombatBoardSpacingTool] {moved} board slots and slot frames moved (player row z {PlayerRowZ}, enemy row z {EnemyRowZ}).");
            return true;
        }
    }
}
