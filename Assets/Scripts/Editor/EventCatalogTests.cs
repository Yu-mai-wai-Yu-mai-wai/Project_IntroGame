#if UNITY_EDITOR
using System.Collections.Generic;
using TawanOS.EventEngine;
using TawanOS.MapEngine;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Automated EditMode verification test for Plan Task P1-7 (อีเวนต์หมอกดำ 4 เรื่อง, ปรับ balance ธูปและขวัญ).
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

            // 1. Catalog exists and has 4 events (Wods removed)
            var catalog = AssetDatabase.LoadAssetAtPath<EventCatalogSO>(CatalogPath);
            Check("EventCatalog.asset exists", catalog != null);
            if (catalog == null)
            {
                Debug.LogError($"[EventCatalogTests] Aborting: catalog not found at {CatalogPath}");
                return;
            }

            Check($"Catalog has exactly 4 events (found {catalog.events.Count})", catalog.events.Count == 4);

            // 2. Offering event is valid
            Check("Catalog has offeringEvent assigned", catalog.offeringEvent != null);
            if (catalog.offeringEvent != null)
            {
                Check("Offering event id is 'offering_pile'", catalog.offeringEvent.eventId == "offering_pile");
            }

            // 3. All 4 events are valid, have distinct eventIds, and repeatable is false
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

            // 4. No Wods event in catalog
            var wodsEvent = catalog.events.Find(e => e != null && (e.eventId == "wods" || e.name.Contains("Wods")));
            Check("Event_Wods does NOT exist in catalog", wodsEvent == null);

            // 5. Check Map Node Profile title is 'หมอกดำ'
            var eventProfile = AssetDatabase.LoadAssetAtPath<NodeProfileSO>(EventProfilePath);
            Check("EventProfile.asset exists", eventProfile != null);
            if (eventProfile != null)
            {
                Check("EventProfile title is 'หมอกดำ'", eventProfile.title == "หมอกดำ");
            }

            // Collect all events including offeringEvent
            var allEvents = new List<EventDataSO>(catalog.events);
            if (catalog.offeringEvent != null && !allEvents.Contains(catalog.offeringEvent))
            {
                allEvents.Add(catalog.offeringEvent);
            }

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

            // 9. Test PickRandom: Rolling 4 times sequentially with seen tracking yields 4 distinct events
            var simulationSeen = new HashSet<string>();
            var rolledEvents = new List<EventDataSO>();
            for (int i = 0; i < 4; i++)
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

            Check("All 4 distinct events were rolled across 4 picks", simulationSeen.Count == 4);

            // 10. Roll 5th time with all 4 seen -> should fall back and still return an event safely
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
