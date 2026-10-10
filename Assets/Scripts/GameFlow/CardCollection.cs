using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TawanOS.CardEngine;
using UnityEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// สมุดการ์ด: every card the player has come across, over all runs. Kept in its own file next to the run save
    /// (card_collection.json), so it survives a run ending, a new game and quitting. Cards are keyed by cardId
    /// (s_xxxx), so renaming or moving a card asset keeps it collected. What counts as "seen" is recorded by
    /// <see cref="CardCollectionTracker"/>.
    /// </summary>
    public class CardCollection
    {
        private const int SaveVersion = 1;
        private static string SavePath => Path.Combine(Application.persistentDataPath, "card_collection.json");

        private static CardCollection current;
        public static CardCollection Current => current ??= Load();

        private readonly HashSet<string> seen = new HashSet<string>();

        /// <summary>A card was seen for the first time (its cardId).</summary>
        public event Action<string> OnDiscovered;

        public int Count => seen.Count;

        public bool Has(CardDataSO card) => card != null && !string.IsNullOrEmpty(card.cardId) && seen.Contains(card.cardId);

        /// <summary>Records a card; true when it is new to the collection.</summary>
        public bool Add(CardDataSO card)
        {
            if (!AddQuiet(card)) return false;
            Save();
            OnDiscovered?.Invoke(card.cardId);
            return true;
        }

        /// <summary>Records several cards with one save.</summary>
        public void AddAll(IEnumerable<CardDataSO> cards)
        {
            if (cards == null) return;
            List<string> added = null;
            foreach (var card in cards)
            {
                if (!AddQuiet(card)) continue;
                (added ??= new List<string>()).Add(card.cardId);
            }
            if (added == null) return;
            Save();
            foreach (var id in added) OnDiscovered?.Invoke(id);
        }

        private bool AddQuiet(CardDataSO card)
        {
            return card != null && !string.IsNullOrEmpty(card.cardId) && seen.Add(card.cardId);
        }

        // ---------------------------------------------------------------- file

        [Serializable]
        private class SaveData
        {
            public int version;
            public List<string> seenCardIds = new List<string>();
        }

        private static CardCollection Load()
        {
            var collection = new CardCollection();
            if (!File.Exists(SavePath)) return collection;
            try
            {
                var data = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(SavePath));
                if (data?.seenCardIds != null)
                    foreach (var id in data.seenCardIds)
                        if (!string.IsNullOrEmpty(id)) collection.seen.Add(id);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CardCollection] Could not read '{SavePath}' ({e.Message}) - starting an empty collection.");
            }
            return collection;
        }

        private void Save()
        {
            try
            {
                var ids = new List<string>(seen);
                ids.Sort(StringComparer.Ordinal);
                var data = new SaveData { version = SaveVersion, seenCardIds = ids };
                File.WriteAllText(SavePath, JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch (Exception e)
            {
                Debug.LogError($"[CardCollection] Could not write '{SavePath}': {e.Message}");
            }
        }
    }
}
