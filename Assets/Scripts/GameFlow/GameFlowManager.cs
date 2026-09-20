using UnityEngine;
using UnityEngine.SceneManagement;
using TawanOS.CardEngine;
using TawanOS.MapEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Bridges the Map and Combat scenes: sends the player into a combat encounter
    /// when a combat node is clicked on the map, and returns them to the map on victory.
    /// Bootstraps itself before the first scene loads, so it works regardless of which
    /// scene is opened first (MapTestScene or CombatTestScene).
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        private const string MapSceneName = "MapTestScene";
        private const string CombatSceneName = "CombatTestScene";
        private const float ReturnToMapDelaySeconds = 2f;

        // TODO: replace with a proper per-biome encounter table once more enemies exist.
        private const string MinorEnemyAssetPath = "Assets/CardEngineData/Enemies/PraiGhostProfile.asset";
        private const string EliteEnemyAssetPath = "Assets/CardEngineData/Enemies/PraiGhostProfile.asset";
        private const string BossEnemyAssetPath = "Assets/CardEngineData/Enemies/PhiTaiHongBossProfile.asset";

        private EnemyProfileSO pendingEnemyProfile;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;

            var go = new GameObject("GameFlowManager");
            go.AddComponent<GameFlowManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (MapManager.Instance != null)
            {
                MapManager.Instance.OnCombatNodeEntered -= HandleCombatNodeEntered;
                MapManager.Instance.OnCombatNodeEntered += HandleCombatNodeEntered;
            }

            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnCombatEnded -= HandleCombatEnded;
                CombatManager.Instance.OnCombatEnded += HandleCombatEnded;

                if (pendingEnemyProfile != null)
                {
                    var profile = pendingEnemyProfile;
                    pendingEnemyProfile = null;
                    CombatManager.Instance.StartCombat(profile);
                }
            }
        }

        private void HandleCombatNodeEntered(NodeType nodeType)
        {
            pendingEnemyProfile = ResolveEnemyProfile(nodeType);
            SceneManager.LoadScene(CombatSceneName, LoadSceneMode.Single);
        }

        private void HandleCombatEnded(bool isVictory)
        {
            if (!isVictory) return; // Defeat: stay on the Defeat panel (no restart flow specced yet)
            StartCoroutine(ReturnToMapAfterDelay());
        }

        private System.Collections.IEnumerator ReturnToMapAfterDelay()
        {
            yield return new WaitForSeconds(ReturnToMapDelaySeconds);
            SceneManager.LoadScene(MapSceneName, LoadSceneMode.Single);
        }

        private EnemyProfileSO ResolveEnemyProfile(NodeType nodeType)
        {
            string path = nodeType switch
            {
                NodeType.Boss => BossEnemyAssetPath,
                NodeType.EliteEnemy => EliteEnemyAssetPath,
                _ => MinorEnemyAssetPath,
            };

#if UNITY_EDITOR
            var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(path);
            if (profile == null)
            {
                Debug.LogError($"[GameFlowManager] Could not load EnemyProfileSO at '{path}' for node type {nodeType}");
            }
            return profile;
#else
            Debug.LogError("[GameFlowManager] Runtime (non-editor) enemy profile resolution is not implemented yet - needs a Resources-based encounter table.");
            return null;
#endif
        }
    }
}
