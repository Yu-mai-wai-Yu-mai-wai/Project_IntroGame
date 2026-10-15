using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TawanOS.CardEngine;
using TawanOS.MapEngine;
using TawanOS.ShopEngine;
using UnityEngine;

namespace TawanOS.GameFlow
{
    /// <summary>Which node the player was inside when the game was last saved (Continue re-enters it).</summary>
    public enum ResumeKind
    {
        None,
        Combat,
        Event,
        Shop,
        Meru, // appended last: saves store the enum as a number
        Temple,
        Reward
    }

    [System.Serializable]
    public class SavedShopOffer
    {
        public string id;
        public int price;
        public bool sold;
    }

    [System.Serializable]
    public class SavedShopStock
    {
        public List<SavedShopOffer> cards = new List<SavedShopOffer>();
        public List<SavedShopOffer> amulets = new List<SavedShopOffer>();
        public List<SavedShopOffer> blessings = new List<SavedShopOffer>();
    }

    [System.Serializable]
    public class SavedRewardData
    {
        public List<string> cardIds = new List<string>();
        public int incenseEarned;
        public RewardTier tier;
    }

    /// <summary>
    /// Run-wide player state that survives scene changes (map -> event / shop -> combat -> map).
    /// A run started from the main menu (<see cref="StartNewRun"/> / <see cref="LoadSavedRun"/>) is
    /// saved to disk on every change so Continue can pick it up; a run that simply exists because a
    /// scene was opened directly in the editor is never saved.
    /// </summary>
    public class RunState
    {
        public const int DefaultMaxHp = 20; // Game Designer, 15 Oct; was 50
        public const int DefaultMaxMerit = 6;
        public const int DefaultIncense = 5; // enough for a cheap card or two (Game Designer, 15 Oct; was 50)
        // 8 floors then the boss (Game Designer, 11 Oct; was 7). Saves keep the floor count they were made with.
        public const int DefaultTotalFloors = 8;
        private const int SaveVersion = 1;

        private static RunState current;
        public static RunState Current => current ??= new RunState();

        private static string SavePath => Path.Combine(Application.persistentDataPath, "run_save.json");
        public static bool HasSave => File.Exists(SavePath);

        public int CurrentHp { get; private set; }
        public int MaxHp { get; private set; }
        public int MaxMerit { get; private set; } = DefaultMaxMerit; // Player's กุศล cap in combat, raised at ศาลตายาย
        public int Incense { get; private set; } // Run currency (ธูป)
        public int TotalFloors { get; private set; } = DefaultTotalFloors;
        public IReadOnlyList<string> RelicIds => relicIds;
        public IReadOnlyCollection<string> SeenEventIds => seenEventIds;

        /// <summary>The player's deck for this run. Empty until <see cref="EnsureDeck"/> seeds it from a starter deck.</summary>
        public IReadOnlyList<CardDataSO> Deck => deck;
        public bool HasDeck => deckInitialized;
        public int CardRemovalsBought { get; private set; }

        public ResumeKind Resume { get; private set; }
        public NodeType ResumeNodeType { get; private set; }
        public int ResumeFloor { get; private set; }

        public bool MeruUsed { get; private set; }
        public void SetMeruUsed(bool used)
        {
            MeruUsed = used;
            Changed();
        }

        public void ClearMeruUsed()
        {
            if (!MeruUsed) return;
            MeruUsed = false;
            Changed();
        }

        public SavedShopStock ShopStock { get; private set; }
        public bool HasSavedShopStock => ShopStock != null && (
            (ShopStock.cards != null && ShopStock.cards.Count > 0) ||
            (ShopStock.amulets != null && ShopStock.amulets.Count > 0) ||
            (ShopStock.blessings != null && ShopStock.blessings.Count > 0));

        public void SetShopStock(SavedShopStock stock)
        {
            ShopStock = stock;
            Changed();
        }

        public void ClearShopStock()
        {
            if (ShopStock == null) return;
            ShopStock = null;
            Changed();
        }

        public SavedRewardData PendingReward { get; private set; }
        public bool HasPendingReward => PendingReward != null;
        public bool HasPendingRewardCards => PendingReward != null && PendingReward.cardIds != null && PendingReward.cardIds.Count > 0;

        public void SetPendingReward(List<string> cardIds, int incenseEarned, RewardTier tier)
        {
            PendingReward = new SavedRewardData
            {
                cardIds = cardIds != null ? new List<string>(cardIds) : new List<string>(),
                incenseEarned = incenseEarned,
                tier = tier
            };
            Changed();
        }

        public void ClearPendingReward()
        {
            if (PendingReward == null) return;
            PendingReward = null;
            Changed();
        }

        /// <summary>True for runs started or loaded from the main menu: these autosave.</summary>
        public bool IsPersistent { get; private set; }

        public event System.Action OnChanged;

        private readonly List<string> relicIds = new List<string>();
        private readonly HashSet<string> seenEventIds = new HashSet<string>();
        private readonly List<CardDataSO> deck = new List<CardDataSO>();
        private readonly Dictionary<string, int> blessingPurchases = new Dictionary<string, int>();
        private bool deckInitialized;

        private RunState()
        {
            Reset();
        }

        // ---------------------------------------------------------------- run lifecycle

        /// <summary>Fresh run from the main menu: default stats, the starter deck, autosave on.</summary>
        public static void StartNewRun(int totalFloors = DefaultTotalFloors)
        {
            DeleteSave();
            Current.Reset();
            Current.TotalFloors = totalFloors > 0 ? totalFloors : DefaultTotalFloors;
            Current.IsPersistent = true;
            Current.EnsureStarterDeck();
            Current.Save();
        }

        /// <summary>Restores the saved run. Returns false if there is no readable save.</summary>
        public static bool LoadSavedRun()
        {
            if (!HasSave) return false;
            try
            {
                var data = JsonConvert.DeserializeObject<RunSaveData>(File.ReadAllText(SavePath));
                if (data == null) return false;
                Current.Apply(data);
                Current.IsPersistent = true;
                Current.EnsureStarterDeck(); // a save from before the deck was seeded at New Game
                Current.Changed();
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RunState] Could not read save '{SavePath}': {e.Message}");
                return false;
            }
        }

        /// <summary>One-line summary of the saved run for the main menu, or null if there is none.</summary>
        public static string DescribeSave()
        {
            if (!HasSave) return null;
            try
            {
                var data = JsonConvert.DeserializeObject<RunSaveData>(File.ReadAllText(SavePath));
                if (data == null) return null;
                int cards = data.deckCardIds != null ? data.deckCardIds.Count : 0;
                return $"ขวัญ {data.currentHp}/{data.maxHp}   •   ธูป {data.incense}   •   สำรับ {cards} ใบ";
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        /// <summary>What the Continue popup shows: where the saved run stands and its status.</summary>
        public struct SaveSummary
        {
            public int currentHp, maxHp, incense, deckCards, totalFloors;
            public bool shortMode;
            /// <summary>-1 = still at the start of the map; 0-based floor index otherwise.</summary>
            public int floor;
            public bool atBoss;
            public bool mapKnown;
            public ResumeKind resume;
            public NodeType resumeNodeType;
            /// <summary>Node the player stands on (map save), valid when <see cref="mapKnown"/>.</summary>
            public NodeType nodeType;
        }

        /// <summary>Reads the run save and the map save for the Continue popup; null when there is no readable save.</summary>
        public static SaveSummary? ReadSaveSummary()
        {
            if (!HasSave) return null;
            try
            {
                var data = JsonConvert.DeserializeObject<RunSaveData>(File.ReadAllText(SavePath));
                if (data == null) return null;
                int floors = data.totalFloors > 0 ? data.totalFloors : DefaultTotalFloors;
                var summary = new SaveSummary
                {
                    currentHp = data.currentHp, maxHp = data.maxHp, incense = data.incense,
                    deckCards = data.deckCardIds != null ? data.deckCardIds.Count : 0,
                    totalFloors = floors, shortMode = floors <= 4, floor = -1,
                    resume = data.resume, resumeNodeType = data.resumeNodeType,
                };
                var map = new MapSaveManager().LoadMap();
                if (map != null)
                {
                    var node = map.GetNode(map.currentPlayerPosition);
                    if (node != null)
                    {
                        summary.mapKnown = true;
                        summary.floor = map.currentPlayerPosition.y;
                        summary.nodeType = node.type;
                        summary.atBoss = node.type == NodeType.Boss;
                    }
                }
                return summary;
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        /// <summary>Run over (defeat): the save is removed so Continue disappears.</summary>
        public static void EndRun()
        {
            DeleteSave();
            Current.IsPersistent = false;
        }

        public static void DeleteSave()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
        }

        private void Reset()
        {
            MaxHp = DefaultMaxHp;
            CurrentHp = DefaultMaxHp;
            MaxMerit = DefaultMaxMerit;
            Incense = DefaultIncense;
            TotalFloors = DefaultTotalFloors;
            CardRemovalsBought = 0;
            relicIds.Clear();
            seenEventIds.Clear();
            deck.Clear();
            blessingPurchases.Clear();
            deckInitialized = false;
            Resume = ResumeKind.None;
            MeruUsed = false;
            ShopStock = null;
            PendingReward = null;
            OnChanged?.Invoke();
        }

        // ---------------------------------------------------------------- stats

        /// <summary>
        /// ขวัญชั่วคราว: Khwan above MaxHp, given only by bowing at the วัด with full Khwan (Game Designer, 15 Oct).
        /// It is ordinary Khwan that damage takes first, so it is gone once Khwan drops back to MaxHp; heals never
        /// raise Khwan past MaxHp nor cut the extra off.
        /// </summary>
        public int TempKhwan => Mathf.Max(0, CurrentHp - MaxHp);

        public void Heal(int amount)
        {
            CurrentHp = Mathf.Max(CurrentHp, Mathf.Min(MaxHp, CurrentHp + Mathf.Max(0, amount)));
            Changed();
        }

        /// <summary>Events never kill the player outright: HP bottoms out at 1.</summary>
        public void TakeDamage(int amount)
        {
            CurrentHp = Mathf.Max(1, CurrentHp - Mathf.Max(0, amount));
            Changed();
        }

        /// <summary>
        /// Sets HP to an exact value, used to carry the Khwan left at the end of a combat back into the run
        /// (plan task A2). Never below 1: the run ends through defeat, not through this call.
        /// </summary>
        public void SetCurrentHp(int hp)
        {
            // Above MaxHp only up to the ขวัญชั่วคราว already held: a fight can lose it but never adds to it
            CurrentHp = Mathf.Clamp(hp, 1, Mathf.Max(MaxHp, CurrentHp));
            Changed();
        }

        public void ChangeMaxHp(int delta)
        {
            int temp = TempKhwan; // ขวัญชั่วคราว rides on top of whatever the max becomes
            int baseHp = CurrentHp - temp;
            MaxHp = Mathf.Max(1, MaxHp + delta);
            CurrentHp = Mathf.Clamp(delta > 0 ? baseHp + delta : baseHp, 1, MaxHp) + temp;
            Changed();
        }

        /// <summary>Adds ขวัญชั่วคราว: Khwan above MaxHp (from full, or on top of what is already there).</summary>
        public void AddTempKhwan(int amount)
        {
            CurrentHp = Mathf.Max(CurrentHp, MaxHp) + Mathf.Max(0, amount);
            Changed();
        }

        public void ChangeMaxMerit(int delta)
        {
            MaxMerit = Mathf.Max(1, MaxMerit + delta);
            Changed();
        }

        public void AddIncense(int amount)
        {
            Incense = Mathf.Max(0, Incense + amount);
            Changed();
        }

        public bool CanAfford(int incense) => Incense >= incense;

        /// <summary>Pays the price if affordable. Returns false (and changes nothing) otherwise.</summary>
        public bool TrySpendIncense(int price)
        {
            if (!CanAfford(price)) return false;
            AddIncense(-price);
            return true;
        }

        public void AddRelic(string relicId)
        {
            if (string.IsNullOrEmpty(relicId)) return;
            relicIds.Add(relicId);
            Changed();
        }

        public void MarkEventSeen(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;
            seenEventIds.Add(eventId);
            Changed();
        }

        // ---------------------------------------------------------------- blessings (ศาลตายาย)

        public int GetBlessingPurchases(ShopBlessingKind kind)
        {
            return blessingPurchases.TryGetValue(kind.ToString(), out int count) ? count : 0;
        }

        public int GetBlessingPurchases(string kindName)
        {
            if (string.IsNullOrEmpty(kindName)) return 0;
            return blessingPurchases.TryGetValue(kindName, out int count) ? count : 0;
        }

        public void RecordBlessingPurchase(ShopBlessingKind kind)
        {
            string key = kind.ToString();
            blessingPurchases[key] = GetBlessingPurchases(kind) + 1;
            Changed();
        }

        public void SetBlessingPurchases(ShopBlessingKind kind, int count)
        {
            blessingPurchases[kind.ToString()] = Mathf.Max(0, count);
            Changed();
        }

        // ---------------------------------------------------------------- deck

        /// <summary>Seeds the run deck from the starter deck the first time any scene needs it.</summary>
        public void EnsureDeck(DeckConfigSO starterDeck)
        {
            if (deckInitialized || starterDeck == null) return;
            deck.Clear();
            deck.AddRange(starterDeck.startingCards.FindAll(c => c != null));
            deckInitialized = true;
            Changed();
        }

        /// <summary>Seeds the run deck from the starter deck on Resources/CardCatalog, so it is there from the first map screen.</summary>
        public void EnsureStarterDeck()
        {
            var catalog = CardCatalogSO.Load();
            if (catalog == null || catalog.starterDeck == null)
            {
                Debug.LogWarning("[RunState] Resources/CardCatalog has no Starter Deck - the deck is seeded on the first fight instead.");
                return;
            }
            EnsureDeck(catalog.starterDeck);
        }

        public void AddCard(CardDataSO card)
        {
            if (card == null) return;
            deck.Add(card);
            Changed();
        }

        public bool RemoveCard(CardDataSO card, bool countsAsPurchase)
        {
            if (!deck.Remove(card)) return false;
            if (countsAsPurchase) CardRemovalsBought++;
            Changed();
            return true;
        }

        // เมรุ: swaps one copy of the card for its upgraded version
        public bool UpgradeCard(CardDataSO card)
        {
            int index = card != null && card.upgradedCard != null ? deck.IndexOf(card) : -1;
            if (index < 0) return false;
            deck[index] = card.upgradedCard;
            Changed();
            return true;
        }

        // ---------------------------------------------------------------- resume point

        public void SetResume(ResumeKind kind, NodeType nodeType = NodeType.MinorEnemy, int floor = 0)
        {
            Resume = kind;
            ResumeNodeType = nodeType;
            ResumeFloor = floor;
            Changed();
        }

        public void ClearResume()
        {
            if (Resume == ResumeKind.None) return;
            Resume = ResumeKind.None;
            Changed();
        }

        // ---------------------------------------------------------------- save

        private void Changed()
        {
            OnChanged?.Invoke();
            if (IsPersistent) Save();
        }

        private void Save()
        {
            var data = new RunSaveData
            {
                version = SaveVersion,
                currentHp = CurrentHp,
                maxHp = MaxHp,
                maxMerit = MaxMerit,
                incense = Incense,
                totalFloors = TotalFloors,
                cardRemovalsBought = CardRemovalsBought,
                deckInitialized = deckInitialized,
                deckCardIds = deck.ConvertAll(c => c.cardId),
                relicIds = new List<string>(relicIds),
                seenEventIds = new List<string>(seenEventIds),
                resume = Resume,
                resumeNodeType = ResumeNodeType,
                resumeFloor = ResumeFloor,
                blessingPurchases = new Dictionary<string, int>(blessingPurchases),
                meruUsed = MeruUsed,
                shopStock = ShopStock,
                pendingReward = PendingReward,
            };
            try
            {
                File.WriteAllText(SavePath, JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RunState] Could not write save '{SavePath}': {e.Message}");
            }
        }

        private void Apply(RunSaveData data)
        {
            MaxHp = Mathf.Max(1, data.maxHp);
            CurrentHp = Mathf.Max(1, data.currentHp); // above MaxHp = ขวัญชั่วคราว
            MaxMerit = data.maxMerit > 0 ? data.maxMerit : DefaultMaxMerit; // saves from before the field have 0
            Incense = Mathf.Max(0, data.incense);
            TotalFloors = data.totalFloors > 0 ? data.totalFloors : DefaultTotalFloors;
            CardRemovalsBought = data.cardRemovalsBought;

            relicIds.Clear();
            if (data.relicIds != null) relicIds.AddRange(data.relicIds);
            seenEventIds.Clear();
            if (data.seenEventIds != null) seenEventIds.UnionWith(data.seenEventIds);

            deck.Clear();
            deckInitialized = data.deckInitialized;
            var catalog = CardCatalogSO.Load();
            if (data.deckCardIds != null)
            {
                foreach (string id in data.deckCardIds)
                {
                    var card = catalog != null ? catalog.cards.Find(c => c != null && c.cardId == id) : null;
                    if (card != null) deck.Add(card);
                    else Debug.LogWarning($"[RunState] Saved card '{id}' is not in Resources/CardCatalog - dropped from the deck.");
                }
            }

            Resume = data.resume;
            ResumeNodeType = data.resumeNodeType;
            ResumeFloor = data.resumeFloor;

            blessingPurchases.Clear();
            if (data.blessingPurchases != null)
            {
                foreach (var kvp in data.blessingPurchases)
                    blessingPurchases[kvp.Key] = kvp.Value;
            }

            MeruUsed = data.meruUsed;
            ShopStock = data.shopStock;
            PendingReward = data.pendingReward;
        }

        /// <summary>On-disk shape of a run. Cards are stored by cardId and resolved through the card catalog.</summary>
        private class RunSaveData
        {
            public int version;
            public int currentHp;
            public int maxHp;
            public int maxMerit;
            public int incense;
            public int totalFloors;
            public int cardRemovalsBought;
            public bool deckInitialized;
            public List<string> deckCardIds;
            public List<string> relicIds;
            public List<string> seenEventIds;
            public ResumeKind resume;
            public NodeType resumeNodeType;
            public int resumeFloor;
            public Dictionary<string, int> blessingPurchases;
            public bool meruUsed;
            public SavedShopStock shopStock;
            public SavedRewardData pendingReward;
        }
    }
}
