#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace TawanOS.MapEngine
{
    [CustomEditor(typeof(MapConfigSO))]
    public class MapConfigSOEditor : Editor
    {
        private MapGraphData previewGraph;
        private bool showPreview = true;
        private Vector2 scrollPos;

        private void OnEnable()
        {
            GeneratePreviewGraph();
        }

        public override void OnInspectorGUI()
        {
            MapConfigSO config = (MapConfigSO)target;

            EditorGUI.BeginChangeCheck();

            // Draw Default Fields
            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("⚡ Map Engine Quick Controls", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🎲 Randomize Seed & Regenerate", GUILayout.Height(32)))
            {
                Undo.RecordObject(config, "Randomize Map Seed");
                config.seed = UnityEngine.Random.Range(10000, 999999);
                EditorUtility.SetDirty(config);
                GeneratePreviewGraph();
                RefreshActiveMap(config);
            }

            if (GUILayout.Button("🔄 Refresh Preview", GUILayout.Height(32)))
            {
                GeneratePreviewGraph();
                RefreshActiveMap(config);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            showPreview = EditorGUILayout.Foldout(showPreview, "📊 Visual Node Map Preview (Inspector)", true, EditorStyles.foldoutHeader);

            if (showPreview)
            {
                DrawMapPreviewBox(config);
            }

            if (EditorGUI.EndChangeCheck())
            {
                GeneratePreviewGraph();
                RefreshActiveMap(config);
            }
        }

        private void GeneratePreviewGraph()
        {
            MapConfigSO config = (MapConfigSO)target;
            if (config == null) return;
            MapGraphGenerator generator = new MapGraphGenerator();
            previewGraph = generator.GenerateMap(config, config.seed);
        }

        private void RefreshActiveMap(MapConfigSO config)
        {
            if (Application.isPlaying)
            {
                MapManager manager = MapManager.Instance != null ? MapManager.Instance : FindFirstObjectByType<MapManager>();
                if (manager != null)
                {
                    manager.ResetAndRegenerate();
                }
            }
        }

        private void DrawMapPreviewBox(MapConfigSO config)
        {
            if (previewGraph == null) GeneratePreviewGraph();
            if (previewGraph == null) return;

            // Legend & Info Header
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Seed: {previewGraph.seed} | Floors: {config.totalFloors} | Width: {config.mapWidth} | Orientation: {config.orientation}", EditorStyles.miniBoldLabel);
            
            // Color Legend Bar
            EditorGUILayout.BeginHorizontal();
            DrawLegendBadge("🔴 Enemy", GetDefaultTypeColor(NodeType.MinorEnemy));
            DrawLegendBadge("💎 Elite", GetDefaultTypeColor(NodeType.EliteEnemy));
            DrawLegendBadge("🔥 Rest", GetDefaultTypeColor(NodeType.RestSite));
            DrawLegendBadge("🎁 Treasure", GetDefaultTypeColor(NodeType.Treasure));
            DrawLegendBadge("🛒 Shop", GetDefaultTypeColor(NodeType.Store));
            DrawLegendBadge("👑 Boss", GetDefaultTypeColor(NodeType.Boss));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            float totalFloors = Mathf.Max(1, config.totalFloors);
            float mapWidth = Mathf.Max(1, config.mapWidth);

            float calculatedCanvasHeight = Mathf.Max(380f, totalFloors * 28f);
            float containerBoxHeight = Mathf.Min(420f, calculatedCanvasHeight + 20f);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(containerBoxHeight));

            Rect outerRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(calculatedCanvasHeight), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(outerRect, new Color(0.10f, 0.10f, 0.14f, 1f));

            Rect previewRect = new Rect(outerRect.x + 20, outerRect.y + 20, outerRect.width - 40, outerRect.height - 40);
            EditorGUI.DrawRect(previewRect, new Color(0.06f, 0.06f, 0.09f, 1f));

            // Floor Guide Lines
            Handles.BeginGUI();
            Handles.color = new Color(0.2f, 0.2f, 0.25f, 0.4f);
            for (int y = 0; y <= totalFloors; y++)
            {
                Vector2 p1 = GridToGUIPos(new Vector2Int(0, y), Vector2.zero, previewRect, totalFloors, mapWidth, config.orientation);
                Vector2 p2 = GridToGUIPos(new Vector2Int((int)mapWidth - 1, y), Vector2.zero, previewRect, totalFloors, mapWidth, config.orientation);
                Handles.DrawLine(p1, p2);
            }

            // Draw Connection Lines
            foreach (var floor in previewGraph.floors)
            {
                if (floor == null) continue;
                foreach (var sourceNode in floor)
                {
                    Vector2 sourceGUIPos = GridToGUIPos(sourceNode.gridPosition, sourceNode.positionOffset, previewRect, totalFloors, mapWidth, config.orientation);

                    foreach (Vector2Int targetGridPos in sourceNode.outgoingConnections)
                    {
                        NodeBlueprint targetNode = previewGraph.GetNode(targetGridPos);
                        if (targetNode != null)
                        {
                            Vector2 targetGUIPos = GridToGUIPos(targetNode.gridPosition, targetNode.positionOffset, previewRect, totalFloors, mapWidth, config.orientation);
                            Handles.color = new Color(0.7f, 0.7f, 0.8f, 0.65f);
                            Handles.DrawAAPolyLine(2.5f, sourceGUIPos, targetGUIPos);
                        }
                    }
                }
            }

            // Node Text Style
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                normal = { textColor = Color.white }
            };

            // Draw Node Discs & Badges
            foreach (var floor in previewGraph.floors)
            {
                if (floor == null) continue;
                foreach (var node in floor)
                {
                    Vector2 guiPos = GridToGUIPos(node.gridPosition, node.positionOffset, previewRect, totalFloors, mapWidth, config.orientation);

                    NodeProfileSO profile = config.GetProfileForType(node.type);
                    Color nodeColor = profile != null ? profile.baseColor : GetDefaultTypeColor(node.type);

                    float circleRadius = (node.type == NodeType.Boss) ? 12f : 9f;
                    
                    // Outer Shadow Disc
                    Handles.color = new Color(0f, 0f, 0f, 0.5f);
                    Handles.DrawSolidDisc(guiPos + new Vector2(1f, 1f), Vector3.forward, circleRadius + 1f);

                    // Base Color Disc
                    Handles.color = nodeColor;
                    Handles.DrawSolidDisc(guiPos, Vector3.forward, circleRadius);

                    // White Border Wire Ring
                    Handles.color = Color.white;
                    Handles.DrawWireDisc(guiPos, Vector3.forward, circleRadius + 0.5f);

                    // Node Type Label Code
                    string code = GetNodeTypeCode(node.type);
                    Rect labelRect = new Rect(guiPos.x - circleRadius, guiPos.y - circleRadius, circleRadius * 2, circleRadius * 2);
                    GUI.Label(labelRect, code, labelStyle);
                }
            }

            Handles.EndGUI();
            EditorGUILayout.EndScrollView();
        }

        private Vector2 GridToGUIPos(Vector2Int gridPos, Vector2 offset, Rect bounds, float totalFloors, float mapWidth, MapOrientation orientation)
        {
            float clampedOffsetX = Mathf.Clamp(offset.x, -0.25f, 0.25f);
            float clampedOffsetY = Mathf.Clamp(offset.y, -0.25f, 0.25f);

            float normX = (gridPos.x + clampedOffsetX + 0.5f) / mapWidth;
            float normY = (gridPos.y + clampedOffsetY + 0.5f) / totalFloors;

            float px, py;

            switch (orientation)
            {
                case MapOrientation.TopToBottom:
                    px = bounds.x + normX * bounds.width;
                    py = bounds.y + normY * bounds.height;
                    break;
                case MapOrientation.LeftToRight:
                    px = bounds.x + normY * bounds.width;
                    py = bounds.y + (1.0f - normX) * bounds.height;
                    break;
                case MapOrientation.RightToLeft:
                    px = bounds.x + (1.0f - normY) * bounds.width;
                    py = bounds.y + normX * bounds.height;
                    break;
                case MapOrientation.BottomToTop:
                default:
                    px = bounds.x + normX * bounds.width;
                    py = bounds.y + (1.0f - normY) * bounds.height;
                    break;
            }

            return new Vector2(px, py);
        }

        private void DrawLegendBadge(string text, Color color)
        {
            GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = color },
                fontStyle = FontStyle.Bold
            };
            GUILayout.Label(text, badgeStyle, GUILayout.ExpandWidth(true));
        }

        private string GetNodeTypeCode(NodeType type)
        {
            switch (type)
            {
                case NodeType.MinorEnemy: return "E";
                case NodeType.EliteEnemy: return "EL";
                case NodeType.RestSite: return "R";
                case NodeType.Treasure: return "T";
                case NodeType.Store: return "S";
                case NodeType.Boss: return "B";
                default: return "?";
            }
        }

        private Color GetDefaultTypeColor(NodeType type)
        {
            switch (type)
            {
                case NodeType.MinorEnemy: return new Color(0.95f, 0.3f, 0.3f);
                case NodeType.EliteEnemy: return new Color(1.0f, 0.2f, 0.5f);
                case NodeType.RestSite: return new Color(0.3f, 0.85f, 0.4f);
                case NodeType.Treasure: return new Color(1.0f, 0.85f, 0.2f);
                case NodeType.Store: return new Color(0.2f, 0.85f, 1.0f);
                case NodeType.Boss: return new Color(0.7f, 0.3f, 0.95f);
                default: return Color.gray;
            }
        }
    }
}
#endif
