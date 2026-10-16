using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.EventEngine
{
    [CreateAssetMenu(fileName = "EventCatalog", menuName = "TawanOS/EventEngine/Event Catalog")]
    public class EventCatalogSO : ScriptableObject
    {
        public List<EventDataSO> events = new List<EventDataSO>();

        /// <summary>Random event not yet seen this run (repeatables always qualify). Falls back to any event.</summary>
        public EventDataSO PickRandom(int floor, IReadOnlyCollection<string> seenEventIds)
        {
            var pool = events.FindAll(e => e != null && e.minFloor <= floor
                && (e.repeatable || seenEventIds == null || !Contains(seenEventIds, e.eventId)));
            if (pool.Count == 0) pool = events.FindAll(e => e != null);
            return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
        }

        private static bool Contains(IReadOnlyCollection<string> set, string id)
        {
            foreach (var s in set) if (s == id) return true;
            return false;
        }
    }
}
