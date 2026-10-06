using UnityEngine;
using UnityEngine.SceneManagement;
using TawanOS.CardEngine;
using TawanOS.EventEngine;
using TawanOS.MapEngine;
using TawanOS.ShopEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Bridges the Map, Event, Shop and Combat scenes: sends the player into a combat encounter,
    /// a story event or the spirit-house shop when that node is clicked on the map, and returns
    /// them to the map afterwards. Feeds the run deck into combat; a won fight pays incense and
    /// opens the card reward screen before returning to the map; a lost fight ends the run.
    /// The main menu starts a run through <see cref="StartNewGame"/> or <see cref="ContinueGame"/>.
    /// Bootstraps itself before the first scene loads, so it works regardless of which scene is
    /// opened first (MainMenu, MapTestScene, EventScene, ShopScene or CombatTestScene).
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        public const string MainMenuSceneName = "MainMenu";
        private const string MapSceneName = "MapTestScene";
        private const string CombatSceneName = "CombatTestScene";
        private const string EventSceneName = "EventScene";
        private const string ShopSceneName = "ShopScene";
        private const string RewardSceneName = "RewardScene";
        private const string MeruSceneName = "MeruScene";
        private const float VictoryPanelDelaySeconds = 2f;
        private const float DefeatPanelDelaySeconds = 3f;

        private EnemyProfileSO pendingEnemyProfile;
        private int? pendingEventFloor;
        private bool pendingOffering; // กองของเซ่น: EventScene plays the offering story instead of a random event
        private int pendingVictoryIncense;
        private RewardTier pendingRewardTier = RewardTier.Minor;

        // Filled on victory, consumed when RewardScene loads
        private bool hasPendingReward;
        private int rewardIncense;
        private RewardTier rewardTier;

        // Incense (ธูป) paid on victory, rolled per fight: [min, max] inclusive
        private static readonly Vector2Int MinorEnemyIncense = new Vector2Int(15, 25);
        private static readonly Vector2Int EliteEnemyIncense = new Vector2Int(35, 45);
        private static readonly Vector2Int BossIncense = new Vector2Int(80, 100);
        private static readonly Vector2Int EventFightIncense = new Vector2Int(15, 25);

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

        // ---------------------------------------------------------------- main menu

        /// <summary>Wipes the saved run and map, then starts a fresh run on a newly rolled map.</summary>
        public void StartNewGame()
        {
            RunState.StartNewRun();
            new MapSaveManager().ClearSavedMap();
            ResetPendingState();
            SceneManager.LoadScene(MapSceneName, LoadSceneMode.Single);
        }

        /// <summary>Loads the saved run and returns to where it was left: inside a node, or the map.</summary>
        public void ContinueGame()
        {
            if (!RunState.LoadSavedRun())
            {
                Debug.LogWarning("[GameFlowManager] No readable save - starting a new game instead.");
                StartNewGame();
                return;
            }

            ResetPendingState();
            var run = RunState.Current;
            switch (run.Resume)
            {
                // Quitting mid-node re-enters it, so leaving the game cannot dodge a fight
                case ResumeKind.Combat: HandleCombatNodeEntered(run.ResumeNodeType); break;
                case ResumeKind.Event:
                    if (run.ResumeNodeType == NodeType.Treasure) HandleTreasureNodeEntered(run.ResumeFloor);
                    else HandleEventNodeEntered(run.ResumeFloor);
                    break;
                case ResumeKind.Shop: HandleStoreNodeEntered(); break;
                case ResumeKind.Meru: HandleRestNodeEntered(); break;
                default: SceneManager.LoadScene(MapSceneName, LoadSceneMode.Single); break;
            }
        }

        private void ResetPendingState()
        {
            StopAllCoroutines();
            pendingEnemyProfile = null;
            pendingEventFloor = null;
            pendingOffering = false;
            pendingVictoryIncense = 0;
            pendingRewardTier = RewardTier.Minor;
            hasPendingReward = false;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (MapManager.Instance != null)
            {
                MapManager.Instance.OnCombatNodeEntered -= HandleCombatNodeEntered;
                MapManager.Instance.OnCombatNodeEntered += HandleCombatNodeEntered;
                MapManager.Instance.OnEventNodeEntered -= HandleEventNodeEntered;
                MapManager.Instance.OnEventNodeEntered += HandleEventNodeEntered;
                MapManager.Instance.OnStoreNodeEntered -= HandleStoreNodeEntered;
                MapManager.Instance.OnStoreNodeEntered += HandleStoreNodeEntered;
                MapManager.Instance.OnTreasureNodeEntered -= HandleTreasureNodeEntered;
                MapManager.Instance.OnTreasureNodeEntered += HandleTreasureNodeEntered;
                MapManager.Instance.OnRestNodeEntered -= HandleRestNodeEntered;
                MapManager.Instance.OnRestNodeEntered += HandleRestNodeEntered;
            }

            if (RewardManager.Instance != null)
            {
                RewardManager.Instance.OnRewardFinished -= HandleRewardFinished;
                RewardManager.Instance.OnRewardFinished += HandleRewardFinished;

                if (hasPendingReward)
                {
                    hasPendingReward = false;
                    RewardManager.Instance.BeginReward(rewardIncense, rewardTier);
                }
            }

            if (MeruManager.Instance != null)
            {
                MeruManager.Instance.OnMeruFinished -= HandleMeruFinished;
                MeruManager.Instance.OnMeruFinished += HandleMeruFinished;
            }

            if (ShopManager.Instance != null)
            {
                ShopManager.Instance.OnShopClosed -= HandleShopClosed;
                ShopManager.Instance.OnShopClosed += HandleShopClosed;
            }

            if (EventManager.Instance != null)
            {
                EventManager.Instance.OnEventFinished -= HandleEventFinished;
                EventManager.Instance.OnEventFinished += HandleEventFinished;
                EventManager.Instance.OnCombatRequested -= HandleEventCombatRequested;
                EventManager.Instance.OnCombatRequested += HandleEventCombatRequested;
                EventManager.Instance.OnCardRewardRequested -= HandleEventCardReward;
                EventManager.Instance.OnCardRewardRequested += HandleEventCardReward;

                if (pendingOffering)
                {
                    pendingOffering = false;
                    EventManager.Instance.BeginOfferingEvent();
                }
                else if (pendingEventFloor.HasValue)
                {
                    int floor = pendingEventFloor.Value;
                    pendingEventFloor = null;
                    EventManager.Instance.BeginRandomEvent(floor);
                }
            }

            // Combat draws from the run deck (cards bought in the shop, removed ones gone).
            // Runs in sceneLoaded, before CombatManager.Start builds the draw pile.
            if (CardManager.Instance != null)
            {
                UseRunDeck(CardManager.Instance);
            }

            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnCombatEnded -= HandleCombatEnded;
                CombatManager.Instance.OnCombatEnded += HandleCombatEnded;
                combatEndHandled = false;

                // Enter the fight with the Khwan the run has left (not full), see HandleCombatEnded for the way back
                if (RunState.Current.IsPersistent)
                {
                    CombatManager.Instance.SetStartingKhwan(RunState.Current.CurrentHp, RunState.Current.MaxHp);
                }

                if (pendingEnemyProfile != null)
                {
                    var profile = pendingEnemyProfile;
                    pendingEnemyProfile = null;
                    // CombatManager.Start begins the fight with this profile (sceneLoaded runs before Start).
                    // Calling StartCombat here as well started two overlapping turn loops.
                    CombatManager.Instance.currentEnemyProfile = profile;
                }
            }
        }

        private void HandleCombatNodeEntered(NodeType nodeType)
        {
            RunState.Current.SetResume(ResumeKind.Combat, nodeType);
            pendingEnemyProfile = ResolveEnemyProfile(nodeType);
            pendingVictoryIncense = RollIncense(nodeType switch
            {
                NodeType.Boss => BossIncense,
                NodeType.EliteEnemy => EliteEnemyIncense,
                _ => MinorEnemyIncense,
            });
            pendingRewardTier = nodeType switch
            {
                NodeType.Boss => RewardTier.Boss,
                NodeType.EliteEnemy => RewardTier.Elite,
                _ => RewardTier.Minor,
            };
            SceneManager.LoadScene(CombatSceneName, LoadSceneMode.Single);
        }

        private void HandleEventNodeEntered(int floor)
        {
            RunState.Current.SetResume(ResumeKind.Event, NodeType.Event, floor);
            pendingEventFloor = floor;
            SceneManager.LoadScene(EventSceneName, LoadSceneMode.Single);
        }

        // กองของเซ่น: a short story in EventScene, then the card reward screen
        private void HandleTreasureNodeEntered(int floor)
        {
            RunState.Current.SetResume(ResumeKind.Event, NodeType.Treasure, floor);
            pendingOffering = true;
            SceneManager.LoadScene(EventSceneName, LoadSceneMode.Single);
        }

        private void HandleEventCardReward()
        {
            RunState.Current.ClearResume();
            hasPendingReward = true;
            rewardIncense = 0;
            rewardTier = RewardTier.Offering;
            LoadRewardScene();
        }

        private void HandleEventFinished()
        {
            RunState.Current.ClearResume();
            SceneManager.LoadScene(MapSceneName, LoadSceneMode.Single);
        }

        private void HandleEventCombatRequested(EnemyProfileSO enemy)
        {
            // The event's enemy is not saved by reference; a resumed fight uses the minor-enemy encounter
            RunState.Current.SetResume(ResumeKind.Combat, NodeType.MinorEnemy);
            pendingEnemyProfile = enemy;
            pendingVictoryIncense = RollIncense(EventFightIncense);
            pendingRewardTier = RewardTier.Minor;
            SceneManager.LoadScene(CombatSceneName, LoadSceneMode.Single);
        }

        private void HandleStoreNodeEntered()
        {
            RunState.Current.SetResume(ResumeKind.Shop, NodeType.Store);
            SceneManager.LoadScene(ShopSceneName, LoadSceneMode.Single);
        }

        // เมรุ: burn a card or upgrade one, then back to the map
        private void HandleRestNodeEntered()
        {
            if (!Application.CanStreamedLevelBeLoaded(MeruSceneName))
            {
                Debug.LogWarning("[GameFlowManager] MeruScene is not in Build Settings (run Tools > TawanOS > Meru > Setup Meru Scene) - the node does nothing.");
                if (SceneManager.GetActiveScene().name != MapSceneName) SceneManager.LoadScene(MapSceneName, LoadSceneMode.Single);
                return;
            }
            RunState.Current.SetResume(ResumeKind.Meru, NodeType.RestSite);
            SceneManager.LoadScene(MeruSceneName, LoadSceneMode.Single);
        }

        private void HandleMeruFinished()
        {
            RunState.Current.ClearResume();
            SceneManager.LoadScene(MapSceneName, LoadSceneMode.Single);
        }

        private void HandleShopClosed()
        {
            RunState.Current.ClearResume();
            SceneManager.LoadScene(MapSceneName, LoadSceneMode.Single);
        }

        // One combat pays out or ends the run once, even if the end event were to fire twice (plan task A8)
        private bool combatEndHandled;

        private void HandleCombatEnded(bool isVictory)
        {
            if (combatEndHandled) return;
            combatEndHandled = true;

            if (!isVictory)
            {
                // Defeat ends the run: the save goes, the Defeat panel shows, then back to the menu
                RunState.EndRun();
                new MapSaveManager().ClearSavedMap();
                StartCoroutine(GoToMainMenuAfterDelay());
                return;
            }

            // Khwan left after the fight is what the run keeps (plan task A2). Only a run that exists in a save
            // is written back, so a combat scene opened on its own does not touch it.
            if (RunState.Current.IsPersistent && CombatManager.Instance != null)
            {
                RunState.Current.SetCurrentHp(CombatManager.Instance.State.playerKhwan);
            }

            // The fight is won: a quit from here on no longer replays it
            RunState.Current.ClearResume();

            // Combat scene opened on its own (no map node) still pays a minor reward
            int reward = pendingVictoryIncense > 0 ? pendingVictoryIncense : RollIncense(MinorEnemyIncense);
            pendingVictoryIncense = 0;
            RunState.Current.AddIncense(reward);
            Debug.Log($"[GameFlowManager] Victory: +{reward} incense (total {RunState.Current.Incense})");

            hasPendingReward = true;
            rewardIncense = reward;
            rewardTier = pendingRewardTier;
            pendingRewardTier = RewardTier.Minor;

            StartCoroutine(GoToRewardAfterDelay());
        }

        private void HandleRewardFinished()
        {
            SceneManager.LoadScene(MapSceneName, LoadSceneMode.Single);
        }

        private static int RollIncense(Vector2Int range)
        {
            return Random.Range(range.x, range.y + 1);
        }

        private static void UseRunDeck(CardManager cardManager)
        {
            var source = cardManager.defaultDeckConfig;
            if (source == null) return;

            var run = RunState.Current;
            run.EnsureDeck(source);

            // Runtime copy so the deck asset on disk is never modified
            var runDeck = ScriptableObject.CreateInstance<DeckConfigSO>();
            runDeck.name = source.name + " (Run)";
            runDeck.deckId = source.deckId;
            runDeck.deckName = source.deckName;
            runDeck.defaultDrawCount = source.defaultDrawCount;
            runDeck.maxHandSize = source.maxHandSize;
            runDeck.startingCards = new System.Collections.Generic.List<CardDataSO>(run.Deck);
            cardManager.defaultDeckConfig = runDeck;
        }

        private System.Collections.IEnumerator GoToMainMenuAfterDelay()
        {
            yield return new WaitForSeconds(DefeatPanelDelaySeconds);
            if (Application.CanStreamedLevelBeLoaded(MainMenuSceneName))
            {
                SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
            }
        }

        private System.Collections.IEnumerator GoToRewardAfterDelay()
        {
            yield return new WaitForSeconds(VictoryPanelDelaySeconds);
            LoadRewardScene();
        }

        private void LoadRewardScene()
        {
            // RewardScene is built by a setup tool; until it exists, go straight back to the map
            if (Application.CanStreamedLevelBeLoaded(RewardSceneName))
            {
                SceneManager.LoadScene(RewardSceneName, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogWarning("[GameFlowManager] RewardScene is not in Build Settings (run Tools > TawanOS > Rewards > Setup Reward Scene) - skipping the card reward.");
                hasPendingReward = false;
                SceneManager.LoadScene(MapSceneName, LoadSceneMode.Single);
            }
        }

        private EnemyProfileSO ResolveEnemyProfile(NodeType nodeType)
        {
            var table = EncounterTableSO.Load();
            if (table == null)
            {
                Debug.LogError($"[GameFlowManager] Resources/{EncounterTableSO.ResourceName}.asset is missing (run Tools > TawanOS > Game Flow > Create Encounter Table)");
                return null;
            }

            var profile = table.Pick(nodeType);
            if (profile == null)
            {
                Debug.LogError($"[GameFlowManager] The encounter table has no enemy for node type {nodeType}");
            }
            return profile;
        }
    }
}
