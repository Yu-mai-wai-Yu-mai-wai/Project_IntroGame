#if UNITY_EDITOR
using System.Collections.Generic;
using TawanOS.EventEngine;
using TawanOS.MapEngine;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Automated EditMode verification of the หมอกดำ event pool (whatever events the catalog holds: ids, result texts, costs, rolling).
    /// Menu: Tools > TawanOS > Tests > Event Catalog
    /// Command line: -executeMethod TawanOS.EditorTools.EventCatalogTests.Run
    /// </summary>
    public static class EventCatalogTests
    {
        private const string CatalogPath = "Assets/EventEngineData/Events/EventCatalog.asset";
        private const string EventProfilePath = "Assets/MapEngineData/Profiles/EventProfile.asset";

        [MenuItem("Tools/TawanOS/Tests/Event Catalog")]
        public static void RunFromMenu() => Run();

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
                    Expect(desc, true);
                }
                else
                {
                    Debug.LogError($"[EventCatalogTests] FAIL: {desc}");
                    Expect(desc, false);
                }
            }

            // 1. Catalog exists and has events
            var catalog = AssetDatabase.LoadAssetAtPath<EventCatalogSO>(CatalogPath);
            Check("EventCatalog.asset exists", catalog != null);
            if (catalog == null)
            {
                Debug.LogError($"[EventCatalogTests] Aborting: catalog not found at {CatalogPath}");
                return;
            }

            int eventCount = catalog.events.Count;
            Check($"Catalog has events (found {eventCount})", eventCount > 0);

            // 2. Every event is valid, has a distinct eventId, and repeatable is false
            var seenIds = new HashSet<string>();
            foreach (var evt in catalog.events)
            {
                Check("Event asset is not null", evt != null);
                if (evt == null) continue;

                Check($"Event '{evt.name}' has non-empty eventId", !string.IsNullOrEmpty(evt.eventId));
                Check($"Event '{evt.name}' has unique eventId '{evt.eventId}'", seenIds.Add(evt.eventId));
                Check($"Event '{evt.name}' is non-repeatable (repeatable == false)", !evt.repeatable);
                Check($"Event '{evt.name}' has minFloor == 0", evt.minFloor == 0);
            }

            // 5. Check Map Node Profile title is 'หมอกดำ'
            var eventProfile = AssetDatabase.LoadAssetAtPath<NodeProfileSO>(EventProfilePath);
            Check("EventProfile.asset exists", eventProfile != null);
            if (eventProfile != null)
            {
                Check("EventProfile title is 'หมอกดำ'", eventProfile.title == "หมอกดำ");
            }

            var allEvents = new List<EventDataSO>(catalog.events);

            // 6. Every choice has non-empty result text
            foreach (var evt in allEvents)
            {
                if (evt == null) continue;
                foreach (var page in evt.pages)
                {
                    if (page == null) continue;
                    foreach (var choice in page.choices)
                    {
                        if (choice == null) continue;
                        Check($"Event '{evt.name}' page '{page.pageId}' choice '{choice.label}' has outcomes",
                            choice.outcomes != null && choice.outcomes.Count > 0);
                        if (choice.outcomes != null)
                        {
                            for (int i = 0; i < choice.outcomes.Count; i++)
                            {
                                Check($"Event '{evt.name}' choice '{choice.label}' outcome {i} has non-empty resultText",
                                    !string.IsNullOrWhiteSpace(choice.outcomes[i].resultText));
                            }
                        }
                    }
                }
            }

            // 7. No incense cost above 15
            foreach (var evt in allEvents)
            {
                if (evt == null) continue;
                foreach (var page in evt.pages)
                {
                    if (page == null) continue;
                    foreach (var choice in page.choices)
                    {
                        if (choice == null) continue;
                        Check($"Event '{evt.name}' choice '{choice.label}' incense cost <= 15 (was {choice.incenseCost})",
                            choice.incenseCost <= 15);
                    }
                }
            }

            // 8. GhostGamble hint matches prize (card, not ของวิเศษ)
            var gamble = catalog.events.Find(e => e != null && e.eventId == "ghost_gamble");
            if (gamble != null && gamble.FirstPage != null)
            {
                var cardChoice = gamble.FirstPage.choices.Find(c => c != null && c.hpCost > 0);
                Check("GhostGamble breath choice hint matches card prize (not ของวิเศษ)",
                    cardChoice != null && cardChoice.hint != null && !cardChoice.hint.Contains("ของวิเศษ") && cardChoice.hint.Contains("การ์ด"));
            }

            // 9. Test PickRandom: rolling once per event with seen tracking yields every event once
            var simulationSeen = new HashSet<string>();
            var rolledEvents = new List<EventDataSO>();
            for (int i = 0; i < eventCount; i++)
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

            Check($"All {eventCount} distinct events were rolled across {eventCount} picks", simulationSeen.Count == eventCount);

            // 10. One more roll with every event seen -> should fall back and still return an event safely
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

        private static int Expect(string label, bool ok)
        {
            Debug.Log($"[EventCatalogTests] {(ok ? "ok  " : "FAIL")} {label}");
            return ok ? 0 : 1;
        }
    }
}
#endif
