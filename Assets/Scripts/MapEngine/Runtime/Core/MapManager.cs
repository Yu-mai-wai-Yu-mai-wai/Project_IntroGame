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
            }
            else
            {
                GenerateNewMap();
            }

            RenderMap(CurrentGraph, config);
        }

        private Transform nodeParentTransform;
        private Transform pathParentTransform;

        public void GenerateNewMap()
        {
            if (config == null) return;
            CurrentGraph = generator.GenerateMap(config, config.seed);
            if (saveSystem != null) saveSystem.SaveMap(CurrentGraph);
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

        private Vector3 CalculateWorldPosition(NodeBlueprint nodeBlueprint, float startX, MapConfigSO configData)
        {
            Vector2Int gridPos = nodeBlueprint.gridPosition;
            Vector2 offset = nodeBlueprint.positionOffset;

            float baseColsX = startX + gridPos.x * configData.columnSpacingX + offset.x;

            if (configData != null && configData.use3DTableMode)
            {
                if (gridPos.y == -1)
                {
                    return new Vector3(0f, configData.tableHeightY + 0.05f, -2.5f) + configData.startNodeOffset;
                }
                float baseFloorsZ = gridPos.y * configData.floorSpacingY + offset.y;
                return new Vector3(baseColsX, configData.tableHeightY + 0.05f, baseFloorsZ);
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
                    playerMarker.SetPositionImmediate(currentView.transform.position);
                    return;
                }
            }

            if (graphData.startNode != null && nodeViewMap.TryGetValue(graphData.startNode.gridPosition, out var startView))
            {
                playerMarker.SetPositionImmediate(startView.transform.position);
            }
            else
            {
                playerMarker.SetPositionImmediate(new Vector3(0, -2.2f, 0));
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
                    playerMarker.SetPositionImmediate(currentView.transform.position);
                }
            }
            ScrollToFloor(0);
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
                    float targetZ = floorIndex * config.floorSpacingY;
                    Vector3 targetCamPos = new Vector3(0f, config.cameraHeightY, targetZ - config.cameraZDistance);
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
