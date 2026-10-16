using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Builds Assets/Scenes/StatusFxTestScene.unity: a camera and a StatusTestPanel. Pressing Play there loads the real
    // CombatTestScene (same scene, same name, so its music, vignette and game flow all behave as in a fight) and keeps
    // the panel on screen to give statuses to board cards, add familiars, draw the status cards and refill merit.
    // Re-running rebuilds the scene.
    public static class StatusFxTestSceneTool
    {
        private const string ScenePath = "Assets/Scenes/StatusFxTestScene.unity";
        private const string CardFolder = "Assets/CardEngineData/Cards/Sheet/";

        // The cards that give, take or flip a status (คาถาสาปแช่ง, คาถาสะกด, พิธีฝังรูปฝังรอย, คาถากันภัย,
        // ตุ๊กตาคุณไสย, ผ้ายันต์กลับด้าน, บทชินบัญชร, คาถาถอนคุณไสย)
        private static readonly string[] StatusCardIds = { "s_bi01", "s_bi05", "s_bi06", "s_wi04", "s_ba01", "s_ba04", "s_wi03", "s_wi05" };

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
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Seen for the one frame before the combat scene replaces this one
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            var panel = new GameObject("StatusTestPanel").AddComponent<StatusTestPanel>();
            foreach (var id in StatusCardIds)
            {
                var so = AssetDatabase.LoadAssetAtPath<CardDataSO>(CardFolder + "Card_" + id + ".asset");
                if (so != null) panel.statusCards.Add(so);
                else Debug.LogWarning("[StatusFxTestScene] Card not found: " + id);
            }
            var familiars = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:CardDataSO", new[] { CardFolder.TrimEnd('/') }))
                familiars.Add(AssetDatabase.GUIDToAssetPath(guid));
            familiars.Sort();
            foreach (var path in familiars)
            {
                var so = AssetDatabase.LoadAssetAtPath<CardDataSO>(path);
                if (so != null && so.cardType == CardType.Familiar && !so.cardId.EndsWith("_up")) panel.familiars.Add(so);
            }

            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[StatusFxTestScene] " + (saved ? $"Saved {ScenePath} ({panel.statusCards.Count} status cards, {panel.familiars.Count} familiars)" : "Could not save " + ScenePath));
            return saved;
        }
    }
}
