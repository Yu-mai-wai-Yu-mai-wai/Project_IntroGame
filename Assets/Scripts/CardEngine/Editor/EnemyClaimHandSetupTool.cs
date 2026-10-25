#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // The enemy's hand on a card it claims in the centre pit is the map's 3D pointing hand (MapNavigate.fbx, the
    // player marker). Makes the hand prefab once (an existing one is kept, with whatever was tuned on it), puts a
    // CenterPit3D object with CenterPitView3D in the combat scene and drags the prefab into its Claim Hand Prefab.
    // By hand: add CenterPitView3D to any object in the combat scene and drag the prefab into Claim Hand Prefab.
    public static class EnemyClaimHandSetupTool
    {
        public const string PrefabPath = "Assets/CardEngineData/Prefabs/EnemyClaimHand3D.prefab";
        private const string ScenePath = "Assets/Scenes/CombatTestScene.unity";
        private const string ModelPath = "Assets/ProjectAsset/NavigateFinal/MapNavigate.fbx";
        private const string HandMatPath = "Assets/ProjectAsset/NavigateFinal/Material/HandMat.mat";
        private const string NailMatPath = "Assets/ProjectAsset/NavigateFinal/Material/FingernailMat.mat";

        [MenuItem("Tools/TawanOS/Card Engine/Hook Up Enemy Claim Hand")]
        private static void HookUp_Menu()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Setup in Play Mode", "Please exit Play Mode first.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            HookUp();
        }

        // -executeMethod TawanOS.CardEngine.EnemyClaimHandSetupTool.HookUpBatch
        public static void HookUpBatch()
        {
            if (!HookUp()) EditorApplication.Exit(1);
        }

        public static bool HookUp()
        {
            var prefab = EnsurePrefab();
            if (prefab == null) return false;

            var previous = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var view = Object.FindFirstObjectByType<CenterPitView3D>(FindObjectsInactive.Include);
            if (view == null)
            {
                var go = new GameObject("CenterPit3D");
                Undo.RegisterCreatedObjectUndo(go, "Add Centre Pit");
                view = go.AddComponent<CenterPitView3D>();
            }
            Undo.RecordObject(view, "Assign enemy claim hand");
            view.claimHandPrefab = prefab;
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                Debug.LogError($"[EnemyClaimHandSetupTool] Could not save {ScenePath}");
                return false;
            }
            if (!string.IsNullOrEmpty(previous) && previous != ScenePath && !Application.isBatchMode)
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);

            Debug.Log($"<color=green>[EnemyClaimHandSetupTool] {ScenePath}: CenterPit3D uses {PrefabPath} as the enemy's claim hand.</color>");
            return true;
        }

        private static GameObject EnsurePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;

            var mesh = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Mesh>().FirstOrDefault(m => m.name == "Hand");
            var handMat = AssetDatabase.LoadAssetAtPath<Material>(HandMatPath);
            var nailMat = AssetDatabase.LoadAssetAtPath<Material>(NailMatPath);
            if (mesh == null || handMat == null || nailMat == null)
            {
                Debug.LogError($"[EnemyClaimHandSetupTool] Missing the hand mesh in {ModelPath} or its materials ({HandMatPath}, {NailMatPath})");
                return null;
            }

            // The mesh points its finger along +Y with the palm toward +Z; the child scales it to 1 unit long and
            // centres it, so CenterPitView3D only sets the root's rotation and length
            var root = new GameObject("EnemyClaimHand3D");
            var hand = new GameObject("Hand");
            hand.transform.SetParent(root.transform, false);
            float scale = 1f / mesh.bounds.size.y;
            hand.transform.localScale = Vector3.one * scale;
            hand.transform.localPosition = -mesh.bounds.center * scale;
            hand.AddComponent<MeshFilter>().sharedMesh = mesh;
            hand.AddComponent<MeshRenderer>().sharedMaterials = new[] { handMat, nailMat };

            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            if (saved == null) Debug.LogError($"[EnemyClaimHandSetupTool] Could not save {PrefabPath}");
            return saved;
        }
    }
}
#endif
