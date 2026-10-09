using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TawanOS.EventEngine
{
    // Story event inspector: write and tune an event without touching code.
    //  - Pages -> choices -> outcomes -> effects, each with add / move / delete buttons
    //  - "Go to page" is a dropdown of the event's pages (or create a new page on the spot)
    //  - Live checks for broken links, missing enemies, zero weights and unreachable pages
    //  - Story outline, catalog membership and a one-click "Test play" in EventScene
    [CustomEditor(typeof(EventDataSO))]
    public class EventDataSOEditor : Editor
    {
        public const string EventFolder = "Assets/EventEngineData/Events";
        public const string CatalogPath = "Assets/EventEngineData/Events/EventCatalog.asset";
        private const string ScenePath = "Assets/Scenes/EventScene.unity";

        private const string EndLabel = "— End event (\"Leave\" button) —";
        private const string NewPageLabel = "+ New page...";

        private static bool showOutline = true;
        private static bool showChecks = true;

        private GUIStyle bodyStyle;
        private GUIStyle headerStyle;

        public override void OnInspectorGUI()
        {
            var evt = (EventDataSO)target;
            EnsureStyles();
            serializedObject.Update();

            DrawToolbar();
            EditorGUILayout.Space(4);

            DrawHeaderFields(evt);
            EditorGUILayout.Space(6);
            DrawChecks(evt);
            EditorGUILayout.Space(6);
            DrawPages();

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            DrawOutline(evt);
            EditorGUILayout.Space(8);
            DrawUseInGame(evt);
        }

        private void EnsureStyles()
        {
            if (bodyStyle != null) return;
            bodyStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            headerStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
        }

        // ---------------------------------------------------------------- toolbar

        private static void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("+ New Event", EditorStyles.toolbarButton, GUILayout.Width(90))) CreateNewEvent();
            if (GUILayout.Button("Show Events Folder", EditorStyles.toolbarButton, GUILayout.Width(130)))
            {
                var folder = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.IsValidFolder(EventFolder) ? EventFolder : "Assets");
                if (folder != null) EditorGUIUtility.PingObject(folder);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        [MenuItem("Tools/TawanOS/Event Engine/Create New Event")]
        public static void CreateNewEvent()
        {
            EnsureFolders();
            string path = AssetDatabase.GenerateUniqueAssetPath(EventFolder + "/Event_New.asset");
            var evt = CreateInstance<EventDataSO>();
            evt.InitializeDefaults();
            evt.eventId = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

            AssetDatabase.CreateAsset(evt, path);
            var catalog = LoadOrCreateCatalog();
            Undo.RecordObject(catalog, "Add event to catalog");
            catalog.events.Add(evt);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Selection.activeObject = evt;
            EditorGUIUtility.PingObject(evt);
        }

        // ---------------------------------------------------------------- header

        private void DrawHeaderFields(EventDataSO evt)
        {
            Section("Event Info");
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            Prop("title", "Title");
            Prop("eventId", "Event ID", "Must be unique across all events");
            Prop("speakerName", "Speaker", "Leave empty for narration");
            Prop("illustration", "Illustration");
            Prop("minFloor", "Min Floor", "Only rolled on this map floor or higher");
            Prop("repeatable", "Repeatable", "Can show up again in the same run");
            EditorGUILayout.EndVertical();

            if (evt.illustration != null)
            {
                var rect = GUILayoutUtility.GetRect(96, 96, GUILayout.Width(96), GUILayout.Height(96));
                DrawSprite(rect, evt.illustration);
            }
            EditorGUILayout.EndHorizontal();
        }

        // ---------------------------------------------------------------- pages

        private void DrawPages()
        {
            var pages = serializedObject.FindProperty("pages");
            Section($"Pages ({pages.arraySize})");
            EditorGUILayout.LabelField("The first page is where the event starts • Outcomes can jump to other pages via \"Then\"", EditorStyles.miniLabel);

            string[] pageIds = CollectPageIds(pages);

            for (int i = 0; i < pages.arraySize; i++)
            {
                var page = pages.GetArrayElementAtIndex(i);
                var pageId = page.FindPropertyRelative("pageId");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                string title = $"Page {i + 1}: {pageId.stringValue}" + (i == 0 ? "   (start)" : "");
                page.isExpanded = EditorGUILayout.Foldout(page.isExpanded, title, true, headerStyle);
                RowButtons(pages, i, "page \"" + pageId.stringValue + "\"");
                EditorGUILayout.EndHorizontal();

                if (page.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    Field(pageId, "Page ID", "Referenced by an outcome's \"Then\" dropdown");
                    Field(page.FindPropertyRelative("speakerOverride"), "Speaker Override", "Empty = use the event's speaker");
                    TextArea(page.FindPropertyRelative("body"), "Story Text", 90);
                    EditorGUI.indentLevel--;

                    EditorGUILayout.Space(2);
                    DrawChoices(pages, page.FindPropertyRelative("choices"), pageIds);
                }
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Page", GUILayout.Height(24)))
            {
                AddPage(pages, UniquePageId(pageIds));
                Commit();
            }
        }

        private void DrawChoices(SerializedProperty pages, SerializedProperty choices, string[] pageIds)
        {
            for (int j = 0; j < choices.arraySize; j++)
            {
                var choice = choices.GetArrayElementAtIndex(j);
                var label = choice.FindPropertyRelative("label");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);
                choice.isExpanded = EditorGUILayout.Foldout(choice.isExpanded, $"Choice {j + 1}: {label.stringValue}", true);
                RowButtons(choices, j, "choice \"" + label.stringValue + "\"");
                EditorGUILayout.EndHorizontal();

                if (choice.isExpanded)
                {
                    EditorGUI.indentLevel += 2;
                    Field(label, "Button Text");
                    Field(choice.FindPropertyRelative("hint"), "Hint", "Small grey line under the button that hints at the outcome");
                    EditorGUILayout.BeginHorizontal();
                    Field(choice.FindPropertyRelative("incenseCost"), "Incense Cost");
                    Field(choice.FindPropertyRelative("hpCost"), "HP Cost");
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.indentLevel -= 2;

                    DrawOutcomes(pages, choice.FindPropertyRelative("outcomes"), pageIds);
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);
            if (GUILayout.Button("+ Add Choice"))
            {
                var c = Append(choices);
                c.FindPropertyRelative("label").stringValue = "New choice";
                c.FindPropertyRelative("hint").stringValue = string.Empty;
                c.FindPropertyRelative("incenseCost").intValue = 0;
                c.FindPropertyRelative("hpCost").intValue = 0;
                var outcomes = c.FindPropertyRelative("outcomes");
                outcomes.arraySize = 0;
                ResetOutcome(Append(outcomes));
                c.isExpanded = true;
                Commit();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawOutcomes(SerializedProperty pages, SerializedProperty outcomes, string[] pageIds)
        {
            int totalWeight = 0;
            for (int k = 0; k < outcomes.arraySize; k++)
                totalWeight += Mathf.Max(0, outcomes.GetArrayElementAtIndex(k).FindPropertyRelative("weight").intValue);

            bool random = outcomes.arraySize > 1;
            for (int k = 0; k < outcomes.arraySize; k++)
            {
                var outcome = outcomes.GetArrayElementAtIndex(k);
                var weight = outcome.FindPropertyRelative("weight");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(28);
                EditorGUILayout.LabelField(random ? $"Outcome {k + 1} (random)" : "Outcome", EditorStyles.boldLabel, GUILayout.Width(110));
                if (random)
                {
                    EditorGUILayout.LabelField("Weight", GUILayout.Width(44));
                    weight.intValue = Mathf.Max(0, EditorGUILayout.IntField(weight.intValue, GUILayout.Width(40)));
                    float pct = totalWeight > 0 ? 100f * Mathf.Max(0, weight.intValue) / totalWeight : 0f;
                    EditorGUILayout.LabelField($"≈ {pct:0}%", EditorStyles.miniBoldLabel, GUILayout.Width(50));
                }
                GUILayout.FlexibleSpace();
                if (random && GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22)))
                {
                    outcomes.DeleteArrayElementAtIndex(k);
                    Commit();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel += 3;
                TextArea(outcome.FindPropertyRelative("resultText"), "Result Text", 50);
                DrawEffects(outcome.FindPropertyRelative("effects"));
                DrawNextPage(pages, outcome.FindPropertyRelative("nextPageId"), pageIds);
                EditorGUI.indentLevel -= 3;
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(32);
            if (GUILayout.Button(random ? "+ Add Random Outcome" : "+ Add Random Outcome (makes this choice a gamble)", EditorStyles.miniButton))
            {
                ResetOutcome(Append(outcomes));
                Commit();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawEffects(SerializedProperty effects)
        {
            EditorGUILayout.LabelField("Effects", EditorStyles.miniBoldLabel);
            for (int e = 0; e < effects.arraySize; e++)
            {
                EditorGUILayout.BeginHorizontal();
                var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
                int indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                EditorGUI.PropertyField(rect, effects.GetArrayElementAtIndex(e), GUIContent.none);
                EditorGUI.indentLevel = indent;
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22)))
                {
                    effects.DeleteArrayElementAtIndex(e);
                    Commit();
                }
                EditorGUILayout.EndHorizontal();
            }

            var buttonRect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
            buttonRect.width = Mathf.Min(buttonRect.width, 140);
            if (GUI.Button(buttonRect, "+ Add Effect", EditorStyles.miniButton))
            {
                var fx = Append(effects);
                fx.FindPropertyRelative("type").enumValueIndex = (int)EventEffectType.Heal;
                fx.FindPropertyRelative("amount").intValue = 5;
                fx.FindPropertyRelative("relicId").stringValue = string.Empty;
                fx.FindPropertyRelative("enemy").objectReferenceValue = null;
                Commit();
            }
        }

        private void DrawNextPage(SerializedProperty pages, SerializedProperty nextPageId, string[] pageIds)
        {
            var options = new List<string> { EndLabel };
            options.AddRange(pageIds.Select(id => "Go to page: " + id));
            string current = nextPageId.stringValue;
            int index = 0;
            if (!string.IsNullOrEmpty(current))
            {
                int found = System.Array.IndexOf(pageIds, current);
                if (found >= 0) index = found + 1;
                else
                {
                    options.Add($"⚠ Missing page \"{current}\"");
                    index = options.Count - 1;
                }
            }
            options.Add(NewPageLabel);

            int picked = EditorGUILayout.Popup("Then", index, options.ToArray());
            if (picked == index) return;

            if (picked == 0) nextPageId.stringValue = string.Empty;
            else if (picked <= pageIds.Length) nextPageId.stringValue = pageIds[picked - 1];
            else if (picked == options.Count - 1)
            {
                string id = UniquePageId(pageIds);
                AddPage(pages, id);
                nextPageId.stringValue = id;
                Commit();
            }
        }

        // ---------------------------------------------------------------- checks

        private void DrawChecks(EventDataSO evt)
        {
            var problems = Validate(evt);
            int errors = problems.Count(p => p.type == MessageType.Error);
            int warnings = problems.Count(p => p.type == MessageType.Warning);
            string title = problems.Count == 0 ? "Checks: ✓ Ready to play" : $"Checks: {errors} errors • {warnings} warnings";

            showChecks = EditorGUILayout.Foldout(showChecks, title, true, EditorStyles.foldoutHeader);
            if (!showChecks) return;
            foreach (var p in problems) EditorGUILayout.HelpBox(p.message, p.type);
        }

        private struct Problem
        {
            public string message;
            public MessageType type;
        }

        private static List<Problem> Validate(EventDataSO evt)
        {
            var list = new List<Problem>();
            void Add(string msg, MessageType type) => list.Add(new Problem { message = msg, type = type });

            if (string.IsNullOrWhiteSpace(evt.eventId)) Add("Event ID is empty", MessageType.Warning);
            if (evt.pages.Count == 0)
            {
                Add("Event has no pages", MessageType.Error);
                return list;
            }

            var ids = evt.pages.Select(p => p.pageId).ToList();
            foreach (var dup in ids.GroupBy(id => id).Where(g => g.Count() > 1))
                Add($"Page ID \"{dup.Key}\" is used by {dup.Count()} pages", MessageType.Error);

            var reachable = new HashSet<string> { evt.pages[0].pageId };
            foreach (var page in evt.pages)
            {
                string where = $"Page \"{page.pageId}\"";
                if (string.IsNullOrWhiteSpace(page.body)) Add($"{where}: story text is empty", MessageType.Warning);
                if (page.choices.Count == 0) Add($"{where}: no choices, only the \"Leave\" button will show", MessageType.Info);

                foreach (var choice in page.choices)
                {
                    string cw = $"{where} › \"{choice.label}\"";
                    if (choice.outcomes.Count == 0) Add($"{cw}: has no outcomes", MessageType.Warning);
                    else if (choice.outcomes.Sum(o => Mathf.Max(0, o.weight)) == 0) Add($"{cw}: outcome weights add up to 0", MessageType.Warning);

                    foreach (var outcome in choice.outcomes)
                    {
                        if (!string.IsNullOrEmpty(outcome.nextPageId))
                        {
                            if (!ids.Contains(outcome.nextPageId)) Add($"{cw}: goes to page \"{outcome.nextPageId}\", which does not exist", MessageType.Error);
                            reachable.Add(outcome.nextPageId);
                        }
                        foreach (var fx in outcome.effects)
                        {
                            if (fx.type == EventEffectType.StartCombat && fx.enemy == null) Add($"{cw}: \"Start Combat\" has no enemy", MessageType.Error);
                            if (fx.type == EventEffectType.GainRelic && string.IsNullOrWhiteSpace(fx.relicId)) Add($"{cw}: \"Gain Relic\" has no relic name", MessageType.Warning);
                            if (fx.type == EventEffectType.StartCombat && !string.IsNullOrEmpty(outcome.nextPageId))
                                Add($"{cw}: a combat outcome skips its next page (combat ends the event)", MessageType.Info);
                        }
                    }
                }
            }

            foreach (var page in evt.pages)
                if (!reachable.Contains(page.pageId)) Add($"Page \"{page.pageId}\" is unreachable (no outcome leads to it)", MessageType.Info);

            return list;
        }

        // ---------------------------------------------------------------- outline

        private static void DrawOutline(EventDataSO evt)
        {
            showOutline = EditorGUILayout.Foldout(showOutline, "Story Outline (every choice at a glance)", true, EditorStyles.foldoutHeader);
            if (!showOutline) return;

            var style = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true, fontSize = 11 };
            var sb = new StringBuilder();
            foreach (var page in evt.pages)
            {
                sb.AppendLine($"<b>▣ {page.pageId}</b>");
                for (int i = 0; i < page.choices.Count; i++)
                {
                    var choice = page.choices[i];
                    string cost = CostText(choice);
                    sb.AppendLine($"    {i + 1}. {choice.label}{(cost.Length > 0 ? $"  <color=#d9b25f>[{cost}]</color>" : "")}");

                    int total = choice.outcomes.Sum(o => Mathf.Max(0, o.weight));
                    foreach (var o in choice.outcomes)
                    {
                        string chance = choice.outcomes.Count > 1 && total > 0 ? $"{100f * Mathf.Max(0, o.weight) / total:0}%  " : "";
                        var fx = o.effects.Select(EventEffectDrawer.Describe).Where(s => s != null).ToList();
                        string fxText = fx.Count > 0 ? string.Join(", ", fx) : "no effect";
                        bool fight = o.effects.Any(e => e.type == EventEffectType.StartCombat);
                        string next = fight ? "combat" : string.IsNullOrEmpty(o.nextPageId) ? "end" : "page " + o.nextPageId;
                        sb.AppendLine($"         {chance}{fxText}  → <i>{next}</i>");
                    }
                }
            }
            EditorGUILayout.LabelField(sb.ToString(), style);
        }

        private static string CostText(EventChoice choice)
        {
            var parts = new List<string>();
            if (choice.incenseCost > 0) parts.Add($"Incense {choice.incenseCost}");
            if (choice.hpCost > 0) parts.Add($"HP {choice.hpCost}");
            return string.Join(", ", parts);
        }

        // ---------------------------------------------------------------- use in game

        private static void DrawUseInGame(EventDataSO evt)
        {
            Section("In Game");

            var catalog = LoadCatalog();
            if (catalog == null)
            {
                EditorGUILayout.HelpBox("No Event Catalog found", MessageType.Warning);
                if (GUILayout.Button("Create Event Catalog and add this event"))
                {
                    catalog = LoadOrCreateCatalog();
                    catalog.events.Add(evt);
                    EditorUtility.SetDirty(catalog);
                }
            }
            else if (catalog.events.Contains(evt))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("In the Event Catalog (rolled when entering a \"?\" node on the map)", EditorStyles.miniLabel);
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    Undo.RecordObject(catalog, "Remove event from catalog");
                    catalog.events.Remove(evt);
                    EditorUtility.SetDirty(catalog);
                }
                EditorGUILayout.EndHorizontal();
            }
            else if (GUILayout.Button("Add to Event Catalog"))
            {
                Undo.RecordObject(catalog, "Add event to catalog");
                catalog.events.Add(evt);
                EditorUtility.SetDirty(catalog);
            }

            EditorGUILayout.Space(4);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("▶  Test Play This Event", GUILayout.Height(30))) TestPlay(evt);
            }
        }

        private static void TestPlay(EventDataSO evt)
        {
            AssetDatabase.SaveAssets();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                if (!EditorUtility.DisplayDialog("EventScene not found", "EventScene has to be built first. Build it now?", "Build", "Cancel")) return;
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EventEngineSetupTool.SetupEventScene();
            }
            else
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(ScenePath);
            }

            SessionState.SetString(EventManager.EditorTestEventKey, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(evt)));
            EditorApplication.isPlaying = true;
        }

        // ---------------------------------------------------------------- catalog helpers

        public static EventCatalogSO LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EventCatalogSO>(CatalogPath);
            if (catalog != null) return catalog;
            string guid = AssetDatabase.FindAssets("t:EventCatalogSO").FirstOrDefault();
            return guid != null ? AssetDatabase.LoadAssetAtPath<EventCatalogSO>(AssetDatabase.GUIDToAssetPath(guid)) : null;
        }

        public static EventCatalogSO LoadOrCreateCatalog()
        {
            var catalog = LoadCatalog();
            if (catalog != null) return catalog;
            EnsureFolders();
            catalog = CreateInstance<EventCatalogSO>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/EventEngineData")) AssetDatabase.CreateFolder("Assets", "EventEngineData");
            if (!AssetDatabase.IsValidFolder(EventFolder)) AssetDatabase.CreateFolder("Assets/EventEngineData", "Events");
        }

        // ---------------------------------------------------------------- array helpers

        /// <summary>▲ ▼ ✕ buttons for an array row. A change exits the GUI pass via Commit.</summary>
        private void RowButtons(SerializedProperty array, int index, string what)
        {
            using (new EditorGUI.DisabledScope(index == 0))
                if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(22)))
                {
                    array.MoveArrayElement(index, index - 1);
                    Commit();
                }
            using (new EditorGUI.DisabledScope(index == array.arraySize - 1))
                if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(22)))
                {
                    array.MoveArrayElement(index, index + 1);
                    Commit();
                }
            if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(22))
                && EditorUtility.DisplayDialog("Delete", $"Delete {what}?", "Delete", "Cancel"))
            {
                array.DeleteArrayElementAtIndex(index);
                Commit();
            }
        }

        /// <summary>Apply a structural change and restart the GUI pass so layout stays consistent.</summary>
        private void Commit()
        {
            serializedObject.ApplyModifiedProperties();
            GUIUtility.ExitGUI();
        }

        private static SerializedProperty Append(SerializedProperty array)
        {
            array.arraySize++;
            return array.GetArrayElementAtIndex(array.arraySize - 1);
        }

        private static void AddPage(SerializedProperty pages, string id)
        {
            var page = Append(pages);
            page.FindPropertyRelative("pageId").stringValue = id;
            page.FindPropertyRelative("speakerOverride").stringValue = string.Empty;
            page.FindPropertyRelative("body").stringValue = string.Empty;
            page.FindPropertyRelative("choices").arraySize = 0;
            page.isExpanded = true;
        }

        private static void ResetOutcome(SerializedProperty outcome)
        {
            outcome.FindPropertyRelative("weight").intValue = 1;
            outcome.FindPropertyRelative("resultText").stringValue = string.Empty;
            outcome.FindPropertyRelative("effects").arraySize = 0;
            outcome.FindPropertyRelative("nextPageId").stringValue = string.Empty;
        }

        private static string[] CollectPageIds(SerializedProperty pages)
        {
            var ids = new string[pages.arraySize];
            for (int i = 0; i < ids.Length; i++) ids[i] = pages.GetArrayElementAtIndex(i).FindPropertyRelative("pageId").stringValue;
            return ids;
        }

        private static string UniquePageId(string[] existing)
        {
            for (int n = existing.Length + 1; ; n++)
            {
                string id = "page_" + n;
                if (System.Array.IndexOf(existing, id) < 0) return id;
            }
        }

        // ---------------------------------------------------------------- field helpers

        private static void Section(string title)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private void Prop(string name, string label, string tooltip = null)
        {
            Field(serializedObject.FindProperty(name), label, tooltip);
        }

        private static void Field(SerializedProperty prop, string label, string tooltip = null)
        {
            EditorGUILayout.PropertyField(prop, new GUIContent(label, tooltip));
        }

        private void TextArea(SerializedProperty prop, string label, float minHeight)
        {
            EditorGUILayout.LabelField(label);
            // Grow with the text so long narration never gets clipped
            float width = EditorGUIUtility.currentViewWidth - 50f - EditorGUI.indentLevel * 15f;
            float height = Mathf.Max(minHeight, bodyStyle.CalcHeight(new GUIContent(prop.stringValue), width) + 6f);
            var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false, height));
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            EditorGUI.BeginChangeCheck();
            string value = EditorGUI.TextArea(rect, prop.stringValue, bodyStyle);
            if (EditorGUI.EndChangeCheck()) prop.stringValue = value;
            EditorGUI.indentLevel = indent;
        }

        private static void DrawSprite(Rect rect, Sprite sprite)
        {
            var tex = sprite.texture;
            var r = sprite.textureRect;
            var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
            float aspect = r.width / r.height;
            var fit = rect;
            if (aspect > 1f) { fit.height = rect.width / aspect; fit.y += (rect.height - fit.height) * 0.5f; }
            else { fit.width = rect.height * aspect; fit.x += (rect.width - fit.width) * 0.5f; }
            GUI.DrawTextureWithTexCoords(fit, tex, uv);
        }
    }
}
