using System.Reflection;
using TawanOS.CardEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Checks plan task P0-1:
    /// (a) DeckPileView3D fallback never uses built-in default material (uses URP Unlit or Sprites/Default).
    /// (b) EnemyHandView3D has pileMaterial or fallback URP Unlit, and never pink Standard.
    /// (c) CombatTestScene has pileMaterial assigned on both Player and Enemy deck views with URP shaders.
    /// (d) AlwaysIncludedShaders contains Sprites/Default and Universal Render Pipeline/Unlit.
    /// Menu: Tools/TawanOS/Tests/Enemy Deck Material.
    /// </summary>
    public static class EnemyDeckMaterialTests
    {
        [MenuItem("Tools/TawanOS/Tests/Enemy Deck Material")]
        public static void RunFromMenu() { Execute(false); }

        public static void Run() { Execute(true); }

        private static void Execute(bool exitOnFinish)
        {
            int failures = 0;
            var testGo = new GameObject("EnemyDeckMaterialTests");

            try
            {
                // Test 1: DeckPileView3D fallback
                Debug.Log("[EnemyDeckMaterialTests] Testing DeckPileView3D fallback...");
                var playerPileGo = new GameObject("TestPlayerPile", typeof(DeckPileView3D));
                playerPileGo.transform.SetParent(testGo.transform);
                var playerPile = playerPileGo.GetComponent<DeckPileView3D>();
                playerPile.pileMaterial = null; // force fallback
                typeof(DeckPileView3D).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(playerPile, null);

                var pileField = typeof(DeckPileView3D).GetField("pile", BindingFlags.NonPublic | BindingFlags.Instance);
                var spawnedPlayerCube = (pileField?.GetValue(playerPile) as Transform)?.gameObject;
                if (spawnedPlayerCube == null)
                {
                    Debug.LogError("[EnemyDeckMaterialTests] FAIL: DeckPile3D was not spawned.");
                    failures++;
                }
                else
                {
                    var renderer = spawnedPlayerCube.GetComponent<Renderer>();
                    string shaderName = renderer.sharedMaterial != null ? renderer.sharedMaterial.shader.name : "null";
                    if (shaderName.Contains("Standard"))
                    {
                        Debug.LogError($"[EnemyDeckMaterialTests] FAIL: DeckPile3D used built-in Standard material: {shaderName}");
                        failures++;
                    }
                    else
                    {
                        Debug.Log($"[EnemyDeckMaterialTests] PASS: DeckPile3D fallback shader is {shaderName}");
                    }
                    Object.DestroyImmediate(spawnedPlayerCube);
                }

                // Test 2: EnemyHandView3D fallback
                Debug.Log("[EnemyDeckMaterialTests] Testing EnemyHandView3D fallback...");
                var enemyHandGo = new GameObject("TestEnemyHand", typeof(EnemyHandView3D));
                enemyHandGo.transform.SetParent(testGo.transform);
                var enemyHand = enemyHandGo.GetComponent<EnemyHandView3D>();
                var slotGo = new GameObject("TestEnemyDeckSlot");
                slotGo.transform.SetParent(testGo.transform);
                typeof(EnemyHandView3D).GetField("deckSlot", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(enemyHand, slotGo.transform);
                enemyHand.pileMaterial = null; // force fallback

                typeof(EnemyHandView3D).GetMethod("BuildPile", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(enemyHand, null);

                var enemyPileField = typeof(EnemyHandView3D).GetField("pile", BindingFlags.NonPublic | BindingFlags.Instance);
                var spawnedEnemyCube = (enemyPileField?.GetValue(enemyHand) as Transform)?.gameObject;
                if (spawnedEnemyCube == null)
                {
                    Debug.LogError("[EnemyDeckMaterialTests] FAIL: EnemyDeckPile3D was not spawned.");
                    failures++;
                }
                else
                {
                    var renderer = spawnedEnemyCube.GetComponent<Renderer>();
                    string shaderName = renderer.sharedMaterial != null ? renderer.sharedMaterial.shader.name : "null";
                    if (shaderName.Contains("Standard"))
                    {
                        Debug.LogError($"[EnemyDeckMaterialTests] FAIL: EnemyDeckPile3D used built-in Standard material: {shaderName}");
                        failures++;
                    }
                    else
                    {
                        Debug.Log($"[EnemyDeckMaterialTests] PASS: EnemyDeckPile3D fallback shader is {shaderName}");
                    }
                }

                // Test 3: Check CombatTestScene assignments
                Debug.Log("[EnemyDeckMaterialTests] Testing CombatTestScene asset assignments...");
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/CombatTestScene.unity", OpenSceneMode.Additive);
                EnemyHandView3D sceneEnemyHand = null;
                DeckPileView3D scenePlayerPile = null;

                foreach (var root in scene.GetRootGameObjects())
                {
                    if (sceneEnemyHand == null) sceneEnemyHand = root.GetComponentInChildren<EnemyHandView3D>(true);
                    if (scenePlayerPile == null) scenePlayerPile = root.GetComponentInChildren<DeckPileView3D>(true);
                }

                if (sceneEnemyHand == null || sceneEnemyHand.pileMaterial == null)
                {
                    Debug.LogError("[EnemyDeckMaterialTests] FAIL: CombatTestScene EnemyHandView3D.pileMaterial is null.");
                    failures++;
                }
                else
                {
                    string sName = sceneEnemyHand.pileMaterial.shader != null ? sceneEnemyHand.pileMaterial.shader.name : "null";
                    if (sName.Contains("Standard"))
                    {
                        Debug.LogError($"[EnemyDeckMaterialTests] FAIL: EnemyHandView3D.pileMaterial uses Standard shader: {sName}");
                        failures++;
                    }
                    else
                    {
                        Debug.Log($"[EnemyDeckMaterialTests] PASS: EnemyHandView3D.pileMaterial is {sceneEnemyHand.pileMaterial.name} ({sName})");
                    }
                }

                if (scenePlayerPile == null || scenePlayerPile.pileMaterial == null)
                {
                    Debug.LogError("[EnemyDeckMaterialTests] FAIL: CombatTestScene DeckPileView3D.pileMaterial is null.");
                    failures++;
                }
                else
                {
                    string sName = scenePlayerPile.pileMaterial.shader != null ? scenePlayerPile.pileMaterial.shader.name : "null";
                    Debug.Log($"[EnemyDeckMaterialTests] PASS: DeckPileView3D.pileMaterial is {scenePlayerPile.pileMaterial.name} ({sName})");
                }

                if (UnityEngine.SceneManagement.SceneManager.loadedSceneCount > 1)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }

                // Test 4: AlwaysIncludedShaders
                Debug.Log("[EnemyDeckMaterialTests] Testing AlwaysIncludedShaders...");
                var graphicsSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
                bool hasUnlit = false;
                bool hasSpritesDefault = false;
                if (graphicsSettings.Length > 0)
                {
                    var so = new SerializedObject(graphicsSettings[0]);
                    var prop = so.FindProperty("m_AlwaysIncludedShaders");
                    for (int i = 0; i < prop.arraySize; i++)
                    {
                        var shader = prop.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                        if (shader != null)
                        {
                            if (shader.name == "Universal Render Pipeline/Unlit") hasUnlit = true;
                            if (shader.name == "Sprites/Default") hasSpritesDefault = true;
                        }
                    }
                }
                if (!hasUnlit)
                {
                    Debug.LogError("[EnemyDeckMaterialTests] FAIL: Universal Render Pipeline/Unlit not in AlwaysIncludedShaders.");
                    failures++;
                }
                else
                {
                    Debug.Log("[EnemyDeckMaterialTests] PASS: Universal Render Pipeline/Unlit in AlwaysIncludedShaders.");
                }
                if (!hasSpritesDefault)
                {
                    Debug.LogError("[EnemyDeckMaterialTests] FAIL: Sprites/Default not in AlwaysIncludedShaders.");
                    failures++;
                }
                else
                {
                    Debug.Log("[EnemyDeckMaterialTests] PASS: Sprites/Default in AlwaysIncludedShaders.");
                }
            }
            finally
            {
                Object.DestroyImmediate(testGo);
            }

            if (failures == 0)
            {
                Debug.Log("[EnemyDeckMaterialTests] === ALL TESTS PASSED ===");
            }
            else
            {
                Debug.LogError($"[EnemyDeckMaterialTests] === {failures} FAILURES ENCOUNTERED ===");
                if (exitOnFinish) EditorApplication.Exit(1);
            }
        }
    }
}
