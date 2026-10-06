using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TawanOS.CardEngine;
using TawanOS.MapEngine;
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
        Meru // appended last: saves store the enum as a number
    }

    /// <summary>
    /// Run-wide player state that survives scene changes (map -> event / shop -> combat -> map).
    /// A run started from the main menu (<see cref="StartNewRun"/> / <see cref="LoadSavedRun"/>) is
    /// saved to disk on every change so Continue can pick it up; a run that simply exists because a
    /// scene was opened directly in the editor is never saved.
    /// </summary>
    public class RunState
    {
        public const int DefaultMaxHp = 50;
        public const int DefaultIncense = 50;
        public const int DefaultTotalFloors = 7;
        private const int SaveVersion = 1;

        private static RunState current;
        public static RunState Current => current ??= new RunState();

        private static string SavePath => Path.Combine(Application.persistentDataPath, "run_save.json");
        public static bool HasSave => File.Exists(SavePath);

        public int CurrentHp { get; private set; }
        public int MaxHp { get; private set; }
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

        /// <summary>True for runs started or loaded from the main menu: these autosave.</summary>
        public bool IsPersistent { get; private set; }

        public event System.Action OnChanged;

        private readonly List<string> relicIds = new List<string>();
        private readonly HashSet<string> seenEventIds = new HashSet<string>();
        private readonly List<CardDataSO> deck = new List<CardDataSO>();
        private bool deckInitialized;

        private RunState()
        {
            Reset();
        }

        // ---------------------------------------------------------------- run lifecycle

        /// <summary>Fresh run from the main menu: default stats, starter deck on first use, autosave on.</summary>
        public static void StartNewRun(int totalFloors = DefaultTotalFloors)
        {
            DeleteSave();
            Current.Reset();
            Current.TotalFloors = totalFloors > 0 ? totalFloors : DefaultTotalFloors;
            Current.IsPersistent = true;
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
                int floors = data.totalFloors > 0 ? data.totalFloors : DefaultTotalFloors;
                string modeStr = floors <= 4 ? "โหมดสั้น 4 ชั้น" : "โหมดเต็ม 7 ชั้น";
                return $"HP {data.currentHp}/{data.maxHp}   •   ธูป {data.incense}   •   สำรับ {cards} ใบ   •   {modeStr}";
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
            Incense = DefaultIncense;
            TotalFloors = DefaultTotalFloors;
            CardRemovalsBought = 0;
            relicIds.Clear();
            seenEventIds.Clear();
            deck.Clear();
            deckInitialized = false;
            Resume = ResumeKind.None;
            OnChanged?.Invoke();
        }

        // ---------------------------------------------------------------- stats

        public void Heal(int amount)
        {
            CurrentHp = Mathf.Clamp(CurrentHp + Mathf.Max(0, amount), 0, MaxHp);
            Changed();
        }

        /// <summary>Events never kill the player outright: HP bottoms out at 1.</summary>
        public void TakeDamage(int amount)
        {
            CurrentHp = Mathf.Clamp(CurrentHp - Mathf.Max(0, amount), 1, MaxHp);
            Changed();
        }

        /// <summary>
        /// Sets HP to an exact value, used to carry the Khwan left at the end of a combat back into the run
        /// (plan task A2). Never below 1: the run ends through defeat, not through this call.
        /// </summary>
        public void SetCurrentHp(int hp)
        {
            CurrentHp = Mathf.Clamp(hp, 1, MaxHp);
            Changed();
        }

        public void ChangeMaxHp(int delta)
        {
            MaxHp = Mathf.Max(1, MaxHp + delta);
            CurrentHp = Mathf.Clamp(delta > 0 ? CurrentHp + delta : CurrentHp, 1, MaxHp);
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
            CurrentHp = Mathf.Clamp(data.currentHp, 1, MaxHp);
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
        }

        /// <summary>On-disk shape of a run. Cards are stored by cardId and resolved through the card catalog.</summary>
        private class RunSaveData
        {
            public int version;
            public int currentHp;
            public int maxHp;
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
        }
    }
}
