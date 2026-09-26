using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace TawanOS.MapEngine
{
    public class MapManager : MonoBehaviour, IMapRenderer
    {
        [Header("Configuration")]
        public MapConfigSO config;
        public bool autoGenerateOnStart = true;
        public bool loadSavedMapIfAvailable = true;

        [Header("Prefabs")]
        public MapNodeView nodePrefab;
        public MapPathRenderer pathPrefab;
        public PlayerMarker playerMarkerPrefab;
        public MapTooltipView tooltipView;

        [Header("Parents / Hierarchy / UI ScrollRect")]
        public Transform nodesParent;
        public Transform pathsParent;
        public UnityEngine.UI.ScrollRect mapScrollRect;
        public MapScrollController scrollController;

        public static MapManager Instance { get; private set; }

        public MapGraphData CurrentGraph { get; private set; }
        public event System.Action<NodeType> OnCombatNodeEntered;

        private IMapGenerator generator;
        private IMapSaveSystem saveSystem;
        private MapObjectPool<MapNodeView> nodePool;
        private MapObjectPool<MapPathRenderer> pathPool;
        private PlayerMarker playerMarker;
        private Dictionary<Vector2Int, MapNodeView> nodeViewMap = new Dictionary<Vector2Int, MapNodeView>();
        private List<MapPathRenderer> pathRenderers = new List<MapPathRenderer>();

        private void Awake()
        {
            Instance = this;
            generator = new MapGraphGenerator();
            saveSystem = new MapSaveManager();
        }

        private void Start()
        {
            if (config == null)
            {
#if UNITY_EDITOR
                string configPath = "Assets/MapEngineData/Profiles/DefaultMapConfig.asset";
                config = UnityEditor.AssetDatabase.LoadAssetAtPath<MapConfigSO>(configPath);
                if (config != null)
                {
                    Debug.Log("[MapManager] Auto-loaded DefaultMapConfig.asset");
                }
#endif
            }

            if (autoGenerateOnStart && config != null)
            {
                InitializeEngine();
            }
            else if (config == null)
            {
                Debug.LogError("[MapManager] MapConfigSO is unassigned! Please assign MapConfigSO in Inspector or run Setup Tool.");
            }
        }

        public void InitializeEngine()
        {
            ClearMap();

            if (nodeParentTransform == null) nodeParentTransform = nodesParent != null ? nodesParent : transform;
            if (pathParentTransform == null) pathParentTransform = pathsParent != null ? pathsParent : transform;

            if (nodePrefab != null) nodePool = new MapObjectPool<MapNodeView>(nodePrefab, 30, nodeParentTransform);
            if (pathPrefab != null) pathPool = new MapObjectPool<MapPathRenderer>(pathPrefab, 50, pathParentTransform);

            if (loadSavedMapIfAvailable && saveSystem.HasSavedMap())
            {
                CurrentGraph = saveSystem.LoadMap();
                if (!IsValidGraphForConfig(CurrentGraph, config))
                {
                    Debug.LogWarning("[MapManager] Saved map is incompatible with current configuration. Clearing saved map and generating a fresh map.");
                    saveSystem.ClearSavedMap();
                    GenerateNewMap();
                }
            }
            else
            {
                GenerateNewMap();
            }

            RenderMap(CurrentGraph, config);
            if (CurrentGraph != null) ScrollToFloor(CurrentGraph.currentPlayerPosition.y);
        }

        private Transform nodeParentTransform;
        private Transform pathParentTransform;

        public void GenerateNewMap()
        {
            if (config == null) return;
            CurrentGraph = generator.GenerateMap(config, config.seed);
            if (saveSystem != null) saveSystem.SaveMap(CurrentGraph);
        }

        public bool IsValidGraphForConfig(MapGraphData graph, MapConfigSO cfg)
        {
            if (graph == null || cfg == null) return false;
            if (graph.floors == null || graph.floors.Count == 0) return false;

            if (cfg.use3DTableMode)
            {
                if (graph.totalFloors != cfg.totalFloors || graph.mapWidth != cfg.mapWidth)
                {
                    return false;
                }

                for (int y = 0; y < graph.floors.Count; y++)
                {
                    if (y > cfg.totalFloors) return false;
                    var floorList = graph.floors[y];
                    if (floorList == null) continue;
                    foreach (var node in floorList)
                    {
                        if (node == null) continue;
                        if (node.gridPosition.x >= cfg.mapWidth || node.gridPosition.y > cfg.totalFloors) return false;
                        if (!TableNodeFbxNames.ContainsKey(node.gridPosition)) return false;
                    }
                }
            }
            else
            {
                if (graph.totalFloors != cfg.totalFloors || graph.mapWidth != cfg.mapWidth)
                {
                    return false;
                }
            }

            return true;
        }

        public void RenderMap(MapGraphData graphData, MapConfigSO configData)
        {
            if (graphData == null || configData == null) return;

            ClearMap();

            float totalWidth = (configData.mapWidth - 1) * configData.columnSpacingX;
            float startX = -totalWidth * 0.5f;

            // Render Start Base Node (Floor -1) if available
            if (graphData.startNode != null)
            {
                Vector3 startWorldPos = CalculateWorldPosition(graphData.startNode, startX, configData);
                MapNodeView startNodeView = nodePool != null ? nodePool.Get() : Instantiate(nodePrefab, nodeParentTransform);
                startNodeView.Setup(graphData.startNode, configData.GetProfileForType(graphData.startNode.type), startWorldPos);
                
                startNodeView.OnNodeClicked += HandleNodeClicked;
                startNodeView.OnNodeHoverEnter += HandleNodeHoverEnter;
                startNodeView.OnNodeHoverExit += HandleNodeHoverExit;

                nodeViewMap[graphData.startNode.gridPosition] = startNodeView;
            }

            // Render Nodes in 2.5D/3D World Space
            for (int y = 0; y < graphData.floors.Count; y++)
            {
                var floorList = graphData.floors[y];

                foreach (var nodeBlueprint in floorList)
                {
                    Vector3 worldPos = CalculateWorldPosition(nodeBlueprint, startX, configData);

                    MapNodeView nodeView = nodePool != null ? nodePool.Get() : Instantiate(nodePrefab, nodeParentTransform);
                    nodeView.Setup(nodeBlueprint, configData.GetProfileForType(nodeBlueprint.type), worldPos);
                    
                    nodeView.OnNodeClicked += HandleNodeClicked;
                    nodeView.OnNodeHoverEnter += HandleNodeHoverEnter;
                    nodeView.OnNodeHoverExit += HandleNodeHoverExit;

                    nodeViewMap[nodeBlueprint.gridPosition] = nodeView;
                }
            }

            // Render Paths (Quadratic Bezier Dotted Lines including Start Base Connections)
            foreach (var kvp in nodeViewMap)
            {
                var sourceView = kvp.Value;
                var sourceBlueprint = sourceView.NodeData;

                foreach (var targetGridPos in sourceBlueprint.outgoingConnections)
                {
                    if (nodeViewMap.TryGetValue(targetGridPos, out var targetView))
                    {
                        MapPathRenderer path = pathPool != null ? pathPool.Get() : Instantiate(pathPrefab, pathParentTransform);
                        path.SetupPath(sourceView.transform.position, targetView.transform.position, sourceBlueprint.gridPosition, targetGridPos, configData.biomeProfile);

                        if (sourceBlueprint.status == NodeStatus.Visited && targetView.NodeData.status == NodeStatus.Visited)
                        {
                            path.SetVisited(configData.biomeProfile);
                        }

                        pathRenderers.Add(path);
                    }
                }
            }

            // Record & Log Post-Randomization Exact Node World Positions
            NodeWorldPositions.Clear();
            foreach (var kvp in nodeViewMap)
            {
                NodeWorldPositions[kvp.Key] = kvp.Value.transform.position;
            }

            // Setup Player Marker Position
            SetupPlayerMarker(graphData);
        }

        public Dictionary<Vector2Int, Vector3> NodeWorldPositions { get; private set; } = new Dictionary<Vector2Int, Vector3>();

        public Vector3 GetExactNodeWorldPosition(Vector2Int gridPos)
        {
            if (NodeWorldPositions.TryGetValue(gridPos, out var pos))
            {
                return pos;
            }
            return Vector3.zero;
        }

        public static readonly Dictionary<Vector2Int, string> TableNodeFbxNames = new Dictionary<Vector2Int, string>
        {
            // FBX import mirrors X: Node.001 sits at Unity +X (screen left for the -Z facing camera), Node.018 at -X.
            { new Vector2Int(0, -1), "Node.001" },
            { new Vector2Int(1, -1), "Node.001" },
            { new Vector2Int(0, 0),  "Node" },
            { new Vector2Int(1, 0),  "Node.003" },
            { new Vector2Int(2, 0),  "Node.002" },
            { new Vector2Int(0, 1),  "Node.004" },
            { new Vector2Int(1, 1),  "Node.005" },
            { new Vector2Int(0, 2),  "Node.006" },
            { new Vector2Int(1, 2),  "Node.008" },
            { new Vector2Int(2, 2),  "Node.007" },
            { new Vector2Int(0, 3),  "Node.009" },
            { new Vector2Int(1, 3),  "Node.009" },
            { new Vector2Int(0, 4),  "Node.010" },
            { new Vector2Int(1, 4),  "Node.012" },
            { new Vector2Int(2, 4),  "Node.011" },
            { new Vector2Int(0, 5),  "Node.013" },
            { new Vector2Int(1, 5),  "Node.015" },
            { new Vector2Int(2, 5),  "Node.014" },
            { new Vector2Int(0, 6),  "Node.016" },
            { new Vector2Int(1, 6),  "Node.017" },
            { new Vector2Int(0, 7),  "Node.018" },
            { new Vector2Int(1, 7),  "Node.018" },
        };

        public static readonly Dictionary<string, Vector3> TablePedestalWorldPositions = new Dictionary<string, Vector3>
        {
            // Fallback only (live pedestal transforms win). Unity pos = (-fbxX, fbxY, fbxZ) / 100.
            { "Node.001", new Vector3(18.608f,  0.025f, 0.0f) },
            { "Node",     new Vector3(13.546f,  0.025f, -2.121f) },
            { "Node.003", new Vector3(13.546f,  0.025f, 0.029f) },
            { "Node.002", new Vector3(13.546f,  0.025f, 2.179f) },
            { "Node.004", new Vector3(9.653f,   0.025f, -1.021f) },
            { "Node.005", new Vector3(9.653f,   0.025f, 1.129f) },
            { "Node.006", new Vector3(5.800f,   0.025f, -2.121f) },
            { "Node.008", new Vector3(5.800f,   0.025f, 0.029f) },
            { "Node.007", new Vector3(5.800f,   0.025f, 2.179f) },
            { "Node.009", new Vector3(2.146f,   0.025f, 0.0f) },
            { "Node.010", new Vector3(-2.929f,  0.025f, -2.121f) },
            { "Node.012", new Vector3(-2.878f,  0.025f, 0.0f) },
            { "Node.011", new Vector3(-2.929f,  0.025f, 2.179f) },
            { "Node.013", new Vector3(-7.622f,  0.025f, -2.121f) },
            { "Node.015", new Vector3(-7.571f,  0.025f, 0.0f) },
            { "Node.014", new Vector3(-7.622f,  0.025f, 2.179f) },
            { "Node.016", new Vector3(-11.540f, 0.025f, -1.021f) },
            { "Node.017", new Vector3(-11.540f, 0.025f, 1.129f) },
            { "Node.018", new Vector3(-15.466f, 0.025f, 0.029f) },
        };

        private Transform environmentTransform;
        private Dictionary<string, Transform> pedestalCache;

        private Vector3 CalculateWorldPosition(NodeBlueprint nodeBlueprint, float startX, MapConfigSO configData)
        {
            Vector2Int gridPos = nodeBlueprint.gridPosition;
            Vector2 offset = nodeBlueprint.positionOffset;
            float baseColsX = startX + gridPos.x * configData.columnSpacingX + offset.x;

            if (configData != null && configData.use3DTableMode)
            {
                if (TableNodeFbxNames.TryGetValue(gridPos, out string nodeName))
                {
                    if (environmentTransform == null)
                    {
                        var env = GameObject.Find("MapNavigateEnvironment");
                        if (env != null) environmentTransform = env.transform;
                    }

                    if (environmentTransform != null)
                    {
                        if (pedestalCache == null) pedestalCache = new Dictionary<string, Transform>();
                        if (!pedestalCache.TryGetValue(nodeName, out Transform pedestal) || pedestal == null)
                        {
                            foreach (Transform t in environmentTransform.GetComponentsInChildren<Transform>(true))
                            {
                                if (t.name == nodeName)
                                {
                                    pedestal = t;
                                    pedestalCache[nodeName] = t;
                                    break;
                                }
                            }
                        }

                        if (pedestal != null)
                        {
                            // Hide the baked FBX icon disc; the procedural MapNodeView sprite is the only node visual.
                            if (pedestal.TryGetComponent<Renderer>(out var pedestalRenderer)) pedestalRenderer.enabled = false;
                            return new Vector3(pedestal.position.x, pedestal.position.y + 0.025f, pedestal.position.z);
                        }
                    }

                    if (TablePedestalWorldPositions.TryGetValue(nodeName, out Vector3 fallbackPos))
                    {
                        return fallbackPos;
                    }
                }

                // Safety guard for 3D Table mode: lock Y strictly to table surface
                float safeX = 18.608f - (gridPos.y + 1) * 4.26f;
                float safeZ = (gridPos.x - 1) * 2.1f;
                return new Vector3(safeX, configData.tableHeightY + 0.025f, safeZ);
            }

            if (gridPos.y == -1)
            {
                return new Vector3(0f, -2.2f, -0.2f) + configData.startNodeOffset;
            }

            float baseFloorsY = gridPos.y * configData.floorSpacingY + offset.y;
            float baseDepthZ = gridPos.y * configData.depthZOffset;

            switch (configData.orientation)
            {
                case MapOrientation.TopToBottom:
                    return new Vector3(baseColsX, -baseFloorsY, baseDepthZ);
                case MapOrientation.LeftToRight:
                    return new Vector3(baseFloorsY, baseColsX, baseDepthZ);
                case MapOrientation.RightToLeft:
                    return new Vector3(-baseFloorsY, baseColsX, baseDepthZ);
                case MapOrientation.BottomToTop:
                default:
                    return new Vector3(baseColsX, baseFloorsY, baseDepthZ);
            }
        }

        private void SetupPlayerMarker(MapGraphData graphData)
        {
            if (playerMarkerPrefab == null) return;

            if (playerMarker == null)
            {
                playerMarker = Instantiate(playerMarkerPrefab, transform);
            }

            Vector2Int targetGridPos = graphData.currentPlayerPosition;
            if (targetGridPos.x >= 0 || targetGridPos.y == -1)
            {
                if (nodeViewMap.TryGetValue(targetGridPos, out var currentView))
                {
                    playerMarker.SetPositionImmediate(currentView.transform.position, new Vector3(-1f, 0f, 0f));
                    return;
                }
            }

            if (graphData.startNode != null && nodeViewMap.TryGetValue(graphData.startNode.gridPosition, out var startView))
            {
                playerMarker.SetPositionImmediate(startView.transform.position, new Vector3(-1f, 0f, 0f));
            }
            else
            {
                playerMarker.SetPositionImmediate(new Vector3(18.608f, 0.025f, 0f), new Vector3(-1f, 0f, 0f));
            }
        }

        private void HandleNodeClicked(MapNodeView clickedView)
        {
            var nodeData = clickedView.NodeData;
            if (nodeData.status != NodeStatus.Attainable) return;

            // Step 1: Mark clicked node as Visited
            nodeData.status = NodeStatus.Visited;
            nodeData.visibility = NodeVisibility.Visited;
            CurrentGraph.currentPlayerPosition = nodeData.gridPosition;

            // Move Player Marker smoothly to recorded exact node world position
            if (playerMarker != null)
            {
                Vector3 targetWorldPos = GetExactNodeWorldPosition(nodeData.gridPosition);
                if (targetWorldPos == Vector3.zero) targetWorldPos = clickedView.transform.position;
                playerMarker.MoveToPosition(targetWorldPos);
                Debug.Log($"[MapManager] Moving 3D Spirit Glass to Node Grid{nodeData.gridPosition} at Recorded WorldPos: {targetWorldPos}");
            }

            // Auto-scroll to player floor smoothly
            ScrollToFloor(nodeData.gridPosition.y);

            // Step 2: Update all other nodes on same floor to Disabled if not visited
            var sameFloorNodes = CurrentGraph.GetNodesOnFloor(nodeData.gridPosition.y);
            foreach (var sameFloorNode in sameFloorNodes)
            {
                if (sameFloorNode.gridPosition != nodeData.gridPosition && sameFloorNode.status != NodeStatus.Visited)
                {
                    sameFloorNode.status = NodeStatus.Disabled;
                }
            }

            // Step 3: Set outgoing target nodes to Attainable
            foreach (var targetPos in nodeData.outgoingConnections)
            {
                var targetNode = CurrentGraph.GetNode(targetPos);
                if (targetNode != null)
                {
                    targetNode.status = NodeStatus.Attainable;
                    targetNode.visibility = NodeVisibility.Reachable;
                }
            }

            // Step 4: Refresh all node visuals & save progress
            foreach (var kvp in nodeViewMap)
            {
                kvp.Value.UpdateVisualState();
            }

            // Refresh path visited colors
            foreach (var path in pathRenderers)
            {
                var sourceNode = CurrentGraph.GetNode(path.SourcePos);
                var targetNode = CurrentGraph.GetNode(path.TargetPos);
                if (sourceNode != null && targetNode != null && sourceNode.status == NodeStatus.Visited && targetNode.status == NodeStatus.Visited)
                {
                    path.SetVisited(config.biomeProfile);
                }
            }

            saveSystem.SaveMap(CurrentGraph);
            Debug.Log($"[MapManager] Selected Node: {nodeData.type} at Floor {nodeData.gridPosition.y}, Column {nodeData.gridPosition.x}");

            if (nodeData.type == NodeType.MinorEnemy || nodeData.type == NodeType.EliteEnemy || nodeData.type == NodeType.Boss)
            {
                OnCombatNodeEntered?.Invoke(nodeData.type);
            }
        }

        private void HandleNodeHoverEnter(MapNodeView view)
        {
            if (tooltipView != null && view.Profile != null)
            {
                tooltipView.ShowTooltip(view.Profile.title, view.Profile.description, view.transform.position);
            }
        }

        private void HandleNodeHoverExit(MapNodeView view)
        {
            if (tooltipView != null)
            {
                tooltipView.HideTooltip();
            }
        }

        public void UpdateNodeState(Vector2Int nodePos, NodeStatus status, NodeVisibility visibility)
        {
            var node = CurrentGraph?.GetNode(nodePos);
            if (node != null)
            {
                node.status = status;
                node.visibility = visibility;
                if (nodeViewMap.TryGetValue(nodePos, out var view))
                {
                    view.UpdateVisualState();
                }
            }
        }

        public void ClearMap()
        {
            foreach (var kvp in nodeViewMap)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.OnNodeClicked -= HandleNodeClicked;
                    kvp.Value.OnNodeHoverEnter -= HandleNodeHoverEnter;
                    kvp.Value.OnNodeHoverExit -= HandleNodeHoverExit;
                }
            }

            if (nodePool != null) nodePool.ReturnAll();
            if (pathPool != null) pathPool.ReturnAll();

            if (nodeParentTransform != null)
            {
                for (int i = nodeParentTransform.childCount - 1; i >= 0; i--)
                {
                    Transform child = nodeParentTransform.GetChild(i);
                    if (child != null && child.gameObject.activeSelf)
                    {
                        var view = child.GetComponent<MapNodeView>();
                        if (view != null && nodePool != null) nodePool.Return(view);
                        else Destroy(child.gameObject);
                    }
                }
            }

            if (pathParentTransform != null)
            {
                for (int i = pathParentTransform.childCount - 1; i >= 0; i--)
                {
                    Transform child = pathParentTransform.GetChild(i);
                    if (child != null && child.gameObject.activeSelf)
                    {
                        var renderer = child.GetComponent<MapPathRenderer>();
                        if (renderer != null && pathPool != null) pathPool.Return(renderer);
                        else Destroy(child.gameObject);
                    }
                }
            }

            nodeViewMap.Clear();
            pathRenderers.Clear();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetAndRegenerate();
            }
        }

        public void ResetAndRegenerate()
        {
            if (saveSystem != null) saveSystem.ClearSavedMap();
            GenerateNewMap();
            RenderMap(CurrentGraph, config);
            if (playerMarker != null)
            {
                playerMarker.ResetMarker();
                if (CurrentGraph != null && CurrentGraph.startNode != null && nodeViewMap.TryGetValue(CurrentGraph.startNode.gridPosition, out var startNodeView))
                {
                    playerMarker.SetPositionImmediate(startNodeView.transform.position);
                }
                else if (CurrentGraph != null && nodeViewMap.TryGetValue(CurrentGraph.currentPlayerPosition, out var currentView))
                {
                    playerMarker.SetPositionImmediate(currentView.transform.position, new Vector3(-1f, 0f, 0f));
                }
            }
            ScrollToFloor(-1);
        }

        public void ScrollToFloor(int floorIndex)
        {
            if (config == null || config.totalFloors <= 0) return;

            if (mapScrollRect != null)
            {
                float targetPos = Mathf.Clamp01((float)floorIndex / (float)config.totalFloors);
                if (config.orientation == MapOrientation.LeftToRight || config.orientation == MapOrientation.RightToLeft)
                {
                    mapScrollRect.DOHorizontalNormalizedPos(targetPos, 0.5f).SetEase(Ease.OutCubic);
                }
                else
                {
                    mapScrollRect.DOVerticalNormalizedPos(targetPos, 0.5f).SetEase(Ease.OutCubic);
                }
            }
            else if (scrollController != null)
            {
                scrollController.ScrollToFloor(floorIndex);
            }
            else if (Camera.main != null)
            {
                if (config != null && config.use3DTableMode)
                {
                    Vector3 targetCamPos;
                    if (config.orientation == MapOrientation.LeftToRight)
                    {
                        float targetX;
                        if (floorIndex < 0) targetX = 18.6f;
                        else if (floorIndex >= 7) targetX = -15.5f;
                        else
                        {
                            float[] floorXs = { 13.55f, 9.65f, 5.80f, 2.15f, -2.93f, -7.62f, -11.54f, -15.47f };
                            targetX = floorXs[Mathf.Clamp(floorIndex, 0, floorXs.Length - 1)];
                        }
                        targetCamPos = new Vector3(targetX, config.cameraHeightY, config.cameraZDistance);
                    }
                    else
                    {
                        float targetZ = floorIndex * config.floorSpacingY;
                        targetCamPos = new Vector3(0f, config.cameraHeightY, targetZ - config.cameraZDistance);
                    }
                    Camera.main.transform.DOMove(targetCamPos, 0.6f).SetEase(Ease.OutCubic);
                    return;
                }

                float targetCoord = floorIndex * config.floorSpacingY;
                Vector3 targetPos = Camera.main.transform.position;

                switch (config.orientation)
                {
                    case MapOrientation.TopToBottom:
                        targetPos.y = -targetCoord;
                        break;
                    case MapOrientation.LeftToRight:
                        targetPos.x = targetCoord;
                        break;
                    case MapOrientation.RightToLeft:
                        targetPos.x = -targetCoord;
                        break;
                    case MapOrientation.BottomToTop:
                    default:
                        targetPos.y = targetCoord;
                        break;
                }

                Camera.main.transform.DOMove(targetPos, 0.6f).SetEase(Ease.OutCubic);
            }
        }
    }
}
