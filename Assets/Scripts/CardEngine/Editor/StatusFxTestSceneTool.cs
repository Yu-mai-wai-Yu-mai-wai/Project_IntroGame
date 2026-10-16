using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TawanOS.CardEngine
{
    // Builds Assets/Scenes/StatusFxTestScene.unity: the combat table (camera, candle light, dressing, volume, board
    // slots copied from CombatTestScene, no combat systems) with a card on each player slot, placed the way a card
    // played onto the board sits, carrying เกราะคุ้มภัย, ยั่วยุ, ผีบังตา, ผวา and all of them (StatusFxShowcase).
    // Press Play to see the icons and point at them. Re-running rebuilds the scene.
    public static class StatusFxTestSceneTool
    {
        private const string ScenePath = "Assets/Scenes/StatusFxTestScene.unity";
        private const string CombatScenePath = "Assets/Scenes/CombatTestScene.unity";
        private const string CardPrefab = "Assets/CardEngineData/Prefabs/CardCube3DPrefab.prefab";
        private const string CardFolder = "Assets/CardEngineData/Cards/Sheet/";

        // The combat scene's set: what you see around the cards. Its managers, hands and UI stay behind.
        private static readonly string[] TableObjects =
            { "Main Camera", "Candle Light", "CombatTable", "CombatDressing", "CombatDressingVolume", "DrawPitSmoke", "BoardSlots" };

        [MenuItem("Tools/TawanOS/Test/Status FX Test Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        // -executeMethod TawanOS.CardEngine.StatusFxTestSceneTool.BuildBatch
        public static void BuildBatch()
        {
            if (!Build()) EditorApplication.Exit(1);
        }

        public static bool Build()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefab);
            if (prefab == null)
            {
                Debug.LogError("[StatusFxTestScene] Card prefab not found: " + CardPrefab);
                return false;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var combat = EditorSceneManager.OpenScene(CombatScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            foreach (var root in combat.GetRootGameObjects())
            {
                if (System.Array.IndexOf(TableObjects, root.name) < 0) continue;
                var copy = Object.Instantiate(root);
                copy.name = root.name;
                SceneManager.MoveGameObjectToScene(copy, scene);
            }
            EditorSceneManager.CloseScene(combat, true);

            // The player row's slots, left to right. Their BoardSlotView / DeckPileView3D would talk to the combat
            // systems (and clear the slot at Start), so only their place and size are used.
            var playerSlots = new System.Collections.Generic.List<BoardSlotView>();
            foreach (var slot in Object.FindObjectsByType<BoardSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (slot.gameObject.scene != scene) continue;
                if (slot.side == BoardSlotView.SlotSide.Player && slot.transform.parent != null && slot.transform.parent.name.Contains("Player")) playerSlots.Add(slot);
            }
            playerSlots.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));
            var fitted = new System.Collections.Generic.List<(Transform slot, Vector3 scale)>();
            foreach (var slot in playerSlots) fitted.Add((slot.transform, slot.FitCardScale(prefab.transform.localScale)));
            foreach (var deck in Object.FindObjectsByType<DeckPileView3D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (deck.gameObject.scene == scene) Object.DestroyImmediate(deck);
            foreach (var slot in playerSlots) Object.DestroyImmediate(slot);
            foreach (var slot in Object.FindObjectsByType<BoardSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (slot.gameObject.scene == scene) Object.DestroyImmediate(slot);

            (string id, string title, CardStatusType[] statuses)[] rows =
            {
                ("s_bf01", "เกราะคุ้มภัย", new[] { CardStatusType.Protect }),
                ("s_wf04", "ยั่วยุ", new[] { CardStatusType.Taunt }),
                ("s_wf03", "ผีบังตา", new[] { CardStatusType.Blinded }),
                ("s_bf03", "ผวา", new[] { CardStatusType.Phawa }),
                ("s_wf02", "ทุกอย่าง", new[] { CardStatusType.Protect, CardStatusType.Taunt, CardStatusType.Blinded, CardStatusType.Phawa }),
            };
            if (fitted.Count < rows.Length)
            {
                Debug.LogError($"[StatusFxTestScene] Found {fitted.Count} player slots in {CombatScenePath}, need {rows.Length}");
                return false;
            }

            var showcase = new GameObject("StatusFxShowcase").AddComponent<StatusFxShowcase>();
            showcase.slots = new StatusFxShowcase.Slot[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                // As EffectResolver places a played card: on the slot, flat with it, scaled to fit it
                var card = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                card.name = "Card_" + rows[i].id;
                card.transform.SetParent(fitted[i].slot, false);
                card.transform.localPosition = Vector3.zero;
                card.transform.localRotation = Quaternion.identity;
                card.transform.localScale = fitted[i].scale;
                var view = card.GetComponent<CardView3D>();
                view.cardDataAsset = AssetDatabase.LoadAssetAtPath<CardDataSO>(CardFolder + "Card_" + rows[i].id + ".asset");
                if (view.cardDataAsset == null) Debug.LogWarning("[StatusFxTestScene] Card not found: " + rows[i].id);
                showcase.slots[i] = new StatusFxShowcase.Slot { card = view, title = rows[i].title, statuses = rows[i].statuses };
            }

            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[StatusFxTestScene] " + (saved ? "Saved " + ScenePath : "Could not save " + ScenePath));
            return saved;
        }
    }
}
