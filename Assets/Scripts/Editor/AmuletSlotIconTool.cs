#if UNITY_EDITOR
using TawanOS.CardEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TawanOS.EditorTools
{
    // Gives the amulet slot (column 0) of both board rows in CombatTestScene the picture that stands beside it
    // (BoardSlotView.amuletSlotIcon): the ตะกรุด illustration. Only sets that one field, so the hand-dressed scene is
    // kept. Tune the offset and height on the slot in the Inspector afterwards; running again keeps them.
    // Batch: -executeMethod TawanOS.EditorTools.AmuletSlotIconTool.Apply
    public static class AmuletSlotIconTool
    {
        private const string ScenePath = "Assets/Scenes/CombatTestScene.unity";
        private const string IconPath = "Assets/Art/Cards/Illustrations/s_wa01_ตะกรุด.png";

        [MenuItem("Tools/TawanOS/Combat Scene/Amulet Slot Icon")]
        private static void Apply_Menu()
        {
            Apply();
        }

        public static void Apply()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            if (icon == null)
            {
                Debug.LogError($"[AmuletSlotIconTool] No sprite at {IconPath}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int set = 0;
            foreach (var slot in Object.FindObjectsByType<BoardSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (slot.slotIndex != EffectResolver.AmuletSlot) continue;
                if (slot.GetComponent<DeckPileView3D>() != null || slot.name.Contains("Deck")) continue;
                Undo.RecordObject(slot, "Amulet slot icon");
                slot.amuletSlotIcon = icon;
                EditorUtility.SetDirty(slot);
                set++;
            }

            if (set == 0)
            {
                Debug.LogError("[AmuletSlotIconTool] No board slot with slotIndex 0 found in CombatTestScene");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"<color=green>[AmuletSlotIconTool] Amulet slot icon set on {set} slot(s).</color>");
        }
    }
}
#endif
