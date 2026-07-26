using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.MapEngine
{
    public class MapManager : MonoBehaviour, IMapRenderer
    {
        [Header("Configuration")]
        [SerializeField] private MapConfigSO config;
        [SerializeField] private bool autoGenerateOnStart = true;
        [SerializeField] private bool loadSavedMapIfAvailable = true;

        [Header("Prefabs")]
        [SerializeField] private MapNodeView nodePrefab;
        [SerializeField] private MapPathRenderer pathPrefab;
        [SerializeField] private PlayerMarker playerMarkerPrefab;
        [SerializeField] private MapTooltipView tooltipView;

        [Header("Parents / Hierarchy")]
        [SerializeField] private Transform nodesParent;
        [SerializeField] private Transform pathsParent;

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
            generator = new MapGraphGenerator();
            saveSystem = new MapSaveManager();
        }

        private void Start()
        {
            if (autoGenerateOnStart && config != null)
            {
                InitializeEngine();
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

            nodeViewMap.Clear();
            pathRenderers.Clear();

            // Render Nodes in 2.5D World Space
            for (int y = 0; y < graphData.floors.Count; y++)
            {
                var floorList = graphData.floors[y];
                float totalWidth = (configData.mapWidth - 1) * configData.columnSpacingX;
                float startX = -totalWidth * 0.5f;

                foreach (var nodeBlueprint in floorList)
                {
                    Vector3 worldPos = CalculateWorldPosition(nodeBlueprint.gridPosition, startX, configData);

                    MapNodeView nodeView = nodePool != null ? nodePool.Get() : Instantiate(nodePrefab, nodeParentTransform);
                    nodeView.Setup(nodeBlueprint, configData.GetProfileForType(nodeBlueprint.type), worldPos);
                    
                    nodeView.OnNodeClicked += HandleNodeClicked;
                    nodeView.OnNodeHoverEnter += HandleNodeHoverEnter;
                    nodeView.OnNodeHoverExit += HandleNodeHoverExit;

                    nodeViewMap[nodeBlueprint.gridPosition] = nodeView;
                }
            }

            // Render Paths (Quadratic Bezier Dotted Lines)
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

            // Setup Player Marker Position
            SetupPlayerMarker(graphData);
        }

        private Vector3 CalculateWorldPosition(Vector2Int gridPos, float startX, MapConfigSO configData)
        {
            float x = startX + gridPos.x * configData.columnSpacingX;
            float y = gridPos.y * configData.floorSpacingY;
            float z = gridPos.y * configData.depthZOffset; // 2.5D Depth Layering
            return new Vector3(x, y, z);
        }

        private void SetupPlayerMarker(MapGraphData graphData)
        {
            if (playerMarkerPrefab == null) return;

            if (playerMarker == null)
            {
                playerMarker = Instantiate(playerMarkerPrefab, transform);
            }

            if (graphData.currentPlayerPosition.x >= 0 && nodeViewMap.TryGetValue(graphData.currentPlayerPosition, out var currentView))
            {
                playerMarker.SetPositionImmediate(currentView.transform.position);
            }
            else
            {
                // Position at bottom center as starting default
                playerMarker.SetPositionImmediate(new Vector3(0, -1f, 0));
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

            // Move Player Marker smoothly
            if (playerMarker != null)
            {
                playerMarker.MoveToPosition(clickedView.transform.position);
            }

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
            if (nodePool != null) nodePool.ReturnAll();
            if (pathPool != null) pathPool.ReturnAll();
            nodeViewMap.Clear();
            pathRenderers.Clear();
        }

        public void ResetAndRegenerate()
        {
            if (saveSystem != null) saveSystem.ClearSavedMap();
            GenerateNewMap();
            RenderMap(CurrentGraph, config);
        }
    }
}
