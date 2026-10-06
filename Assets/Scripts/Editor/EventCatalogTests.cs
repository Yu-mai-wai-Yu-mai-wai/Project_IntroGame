#if UNITY_EDITOR
using System.Collections.Generic;
using TawanOS.EventEngine;
using TawanOS.MapEngine;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Automated EditMode verification test for Plan Task A5 (อีเวนต์หมอกดำครบ 5 เรื่อง).
    /// Menu: Tools > TawanOS > Tests > Event Catalog
    /// Command line: -executeMethod TawanOS.EditorTools.EventCatalogTests.Run
    /// </summary>
    public static class EventCatalogTests
    {
        private const string CatalogPath = "Assets/EventEngineData/Events/EventCatalog.asset";
        private const string EventProfilePath = "Assets/MapEngineData/Profiles/EventProfile.asset";

        [MenuItem("Tools/TawanOS/Tests/Event Catalog")]
        public static void Run()
        {
            int passCount = 0;
            int totalCount = 0;

            void Check(string desc, bool condition)
            {
                totalCount++;
                if (condition)
                {
                    passCount++;
                }
                else
                {
                    Debug.LogError($"[EventCatalogTests] FAIL: {desc}");
                }
            }

            // 1. Catalog exists and has 5 events
            var catalog = AssetDatabase.LoadAssetAtPath<EventCatalogSO>(CatalogPath);
            Check("EventCatalog.asset exists", catalog != null);
            if (catalog == null)
            {
                Debug.LogError($"[EventCatalogTests] Aborting: catalog not found at {CatalogPath}");
                return;
            }

            Check($"Catalog has exactly 5 events (found {catalog.events.Count})", catalog.events.Count == 5);

            // 2. Offering event is valid
            Check("Catalog has offeringEvent assigned", catalog.offeringEvent != null);
            if (catalog.offeringEvent != null)
            {
                Check("Offering event id is 'offering_pile'", catalog.offeringEvent.eventId == "offering_pile");
            }

            // 3. All 5 events are valid, have distinct eventIds, and repeatable is false
            var seenIds = new HashSet<string>();
            foreach (var evt in catalog.events)
            {
                Check($"Event asset is not null", evt != null);
                if (evt == null) continue;

                Check($"Event '{evt.name}' has non-empty eventId", !string.IsNullOrEmpty(evt.eventId));
                Check($"Event '{evt.name}' has unique eventId '{evt.eventId}'", seenIds.Add(evt.eventId));
                Check($"Event '{evt.name}' is non-repeatable (repeatable == false)", !evt.repeatable);
                Check($"Event '{evt.name}' has minFloor == 0", evt.minFloor == 0);
            }

            // 4. Check Thai title spelling for 'วอดส์' (no duplicate tone marks)
            var wodsEvent = catalog.events.Find(e => e != null && (e.eventId == "wods" || e.name.Contains("Wods")));
            Check("Event_Wods exists in catalog", wodsEvent != null);
            if (wodsEvent != null)
            {
                Check("Event_Wods title is exactly 'วอดส์'", wodsEvent.title == "วอดส์");
                Check("Event_Wods title does not contain invalid tone marks", !wodsEvent.title.Contains("\u0E4C\u0E48"));
            }

            // 5. Check Map Node Profile title is 'หมอกดำ'
            var eventProfile = AssetDatabase.LoadAssetAtPath<NodeProfileSO>(EventProfilePath);
            Check("EventProfile.asset exists", eventProfile != null);
            if (eventProfile != null)
            {
                Check("EventProfile title is 'หมอกดำ'", eventProfile.title == "หมอกดำ");
            }

            // 6. Test PickRandom: Rolling 5 times sequentially with seen tracking yields 5 distinct events
            var simulationSeen = new HashSet<string>();
            var rolledEvents = new List<EventDataSO>();
            for (int i = 0; i < 5; i++)
            {
                var rolled = catalog.PickRandom(0, simulationSeen);
                Check($"Roll {i + 1} returned a valid event", rolled != null);
                if (rolled != null)
                {
                    Check($"Roll {i + 1} returned an unseen event '{rolled.eventId}'", !simulationSeen.Contains(rolled.eventId));
                    simulationSeen.Add(rolled.eventId);
                    rolledEvents.Add(rolled);
                }
            }

            Check("All 5 distinct events were rolled across 5 picks", simulationSeen.Count == 5);

            // 7. Roll 6th time with all 5 seen -> should fall back and still return an event safely
            var fallbackRolled = catalog.PickRandom(0, simulationSeen);
            Check("PickRandom falls back safely when all events are seen", fallbackRolled != null);

            if (passCount == totalCount)
            {
                Debug.Log($"[EventCatalogTests] PASS: {passCount}/{totalCount} checks passed.");
            }
            else
            {
                Debug.LogError($"[EventCatalogTests] FAILED: {passCount}/{totalCount} checks passed.");
            }
        }
    }
}
#endif
