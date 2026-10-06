using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EventEngine
{
    // Event catalog inspector: every story the map can roll, with quick create / collect buttons.
    [CustomEditor(typeof(EventCatalogSO))]
    public class EventCatalogSOEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var catalog = (EventCatalogSO)target;

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("+ New Event", EditorStyles.toolbarButton, GUILayout.Width(90))) EventDataSOEditor.CreateNewEvent();
            if (GUILayout.Button("Add All Events in Project", EditorStyles.toolbarButton, GUILayout.Width(160))) CollectAll(catalog);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Events rolled on \"?\" map nodes ({catalog.events.Count(e => e != null)})", EditorStyles.boldLabel);

            for (int i = 0; i < catalog.events.Count; i++)
            {
                var evt = catalog.events[i];
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                if (evt == null)
                {
                    EditorGUILayout.LabelField("(empty)", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField(evt.title, EditorStyles.boldLabel, GUILayout.MinWidth(120));
                    string info = $"Floor {evt.minFloor}+  •  {evt.pages.Count} pages" + (evt.repeatable ? "  •  repeatable" : "");
                    EditorGUILayout.LabelField(info, EditorStyles.miniLabel, GUILayout.MinWidth(120));
                    if (GUILayout.Button("Edit", EditorStyles.miniButtonLeft, GUILayout.Width(46))) Selection.activeObject = evt;
                }
                if (GUILayout.Button("✕", evt == null ? EditorStyles.miniButton : EditorStyles.miniButtonRight, GUILayout.Width(22)))
                {
                    Undo.RecordObject(catalog, "Remove event from catalog");
                    catalog.events.RemoveAt(i);
                    EditorUtility.SetDirty(catalog);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }

            // Drop zone: drag Event assets here to add them
            var drop = GUILayoutUtility.GetRect(0, 36, GUILayout.ExpandWidth(true));
            GUI.Box(drop, "Drag Event assets here to add them", EditorStyles.helpBox);
            HandleDrop(drop, catalog);
        }

        private static void CollectAll(EventCatalogSO catalog)
        {
            Undo.RecordObject(catalog, "Collect events");
            catalog.events.RemoveAll(e => e == null);
            foreach (string guid in AssetDatabase.FindAssets("t:EventDataSO"))
            {
                var evt = AssetDatabase.LoadAssetAtPath<EventDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (evt != null && !catalog.events.Contains(evt)) catalog.events.Add(evt);
            }
            EditorUtility.SetDirty(catalog);
        }

        private static void HandleDrop(Rect area, EventCatalogSO catalog)
        {
            var e = Event.current;
            if (!area.Contains(e.mousePosition) || (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)) return;

            var events = DragAndDrop.objectReferences.OfType<EventDataSO>().ToList();
            DragAndDrop.visualMode = events.Count > 0 ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            if (e.type != EventType.DragPerform || events.Count == 0) return;

            DragAndDrop.AcceptDrag();
            Undo.RecordObject(catalog, "Add events to catalog");
            foreach (var evt in events)
                if (!catalog.events.Contains(evt)) catalog.events.Add(evt);
            EditorUtility.SetDirty(catalog);
            e.Use();
        }
    }
}
