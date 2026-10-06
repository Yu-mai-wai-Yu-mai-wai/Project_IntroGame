#if UNITY_EDITOR
using System.Collections.Generic;
using TawanOS.CardEngine;
using TawanOS.MapEngine;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EventEngine
{
    /// <summary>
    /// Configures the EventCatalog to include all 5 Dark Fog (หมอกดำ) story events (Plan Task A5)
    /// without recreating or overwriting EventScene.
    /// Menu: Tools > TawanOS > Event Engine > Setup 5 Dark Fog Events
    /// </summary>
    public static class EventCatalogSetupTool
    {
        public const string CatalogPath = "Assets/EventEngineData/Events/EventCatalog.asset";
        public const string EventProfilePath = "Assets/MapEngineData/Profiles/EventProfile.asset";
        public const string PraiEnemyPath = "Assets/CardEngineData/Enemies/PraiGhostProfile.asset";

        public static readonly string[] EventAssetPaths = new string[]
        {
            "Assets/EventEngineData/Events/Event_GhostGamble.asset",
            "Assets/EventEngineData/Events/Event_SpiritHouse.asset",
            "Assets/EventEngineData/Events/Event_WanderingShaman.asset",
            "Assets/EventEngineData/Events/Event_WellVoice.asset",
            "Assets/EventEngineData/Events/Event_Wods.asset",
        };

        public const string OfferingEventPath = "Assets/EventEngineData/Events/Event_OfferingPile.asset";

        [MenuItem("Tools/TawanOS/Event Engine/Setup 5 Dark Fog Events")]
        public static void SetupDarkFogEvents()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EventCatalogSO>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"[EventCatalogSetupTool] EventCatalog not found at {CatalogPath}");
                return;
            }

            Undo.RecordObject(catalog, "Configure 5 Dark Fog Events");
            catalog.events.Clear();

            var loadedEvents = new List<EventDataSO>();
            foreach (var path in EventAssetPaths)
            {
                var evt = AssetDatabase.LoadAssetAtPath<EventDataSO>(path);
                if (evt == null)
                {
                    Debug.LogError($"[EventCatalogSetupTool] Failed to load event asset at {path}");
                    continue;
                }

                // Ensure repeatable is false for clean 5-event non-duplicate cycles
                if (evt.repeatable)
                {
                    Undo.RecordObject(evt, "Set repeatable to false");
                    evt.repeatable = false;
                    EditorUtility.SetDirty(evt);
                }

                // Ensure minFloor is 0 so events can roll at any map floor
                if (evt.minFloor != 0)
                {
                    Undo.RecordObject(evt, "Set minFloor to 0");
                    evt.minFloor = 0;
                    EditorUtility.SetDirty(evt);
                }

                // Fix Thai tone mark typo on 'วอดส์' if needed
                if (evt.name.Contains("Wods") || evt.eventId == "wods" || evt.eventId == "What")
                {
                    if (evt.title == "วอดส์่" || evt.title.Contains("\u0E4C\u0E48"))
                    {
                        Undo.RecordObject(evt, "Fix Thai title tone mark");
                        evt.title = "วอดส์";
                        EditorUtility.SetDirty(evt);
                    }
                }

                catalog.events.Add(evt);
                loadedEvents.Add(evt);
            }

            var offering = AssetDatabase.LoadAssetAtPath<EventDataSO>(OfferingEventPath);
            if (offering != null)
            {
                catalog.offeringEvent = offering;
            }
            else
            {
                Debug.LogWarning($"[EventCatalogSetupTool] Offering event not found at {OfferingEventPath}");
            }

            EditorUtility.SetDirty(catalog);

            // Update Map Node profile title to 'หมอกดำ'
            var eventProfile = AssetDatabase.LoadAssetAtPath<NodeProfileSO>(EventProfilePath);
            if (eventProfile != null)
            {
                Undo.RecordObject(eventProfile, "Update Node Title to หมอกดำ");
                eventProfile.title = "หมอกดำ";
                EditorUtility.SetDirty(eventProfile);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[EventCatalogSetupTool] Successfully configured EventCatalog with {catalog.events.Count} events and verified EventProfile title.");
        }
    }
}
#endif
