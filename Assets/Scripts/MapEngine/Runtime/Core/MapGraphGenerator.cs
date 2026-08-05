using System;
using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.MapEngine
{
    public class MapGraphGenerator : IMapGenerator
    {
        public MapGraphData GenerateMap(MapConfigSO config, int seed)
        {
            int actualSeed = config.useRandomSeed ? UnityEngine.Random.Range(1, 999999) : seed;
            System.Random random = new System.Random(actualSeed);

            MapGraphData graph = new MapGraphData(actualSeed, config.totalFloors, config.mapWidth);

            // Step 1: Pick starting nodes on Floor 0 (exact count: config.startingNodesCount)
            List<int> startingCols = GetRandomStartingColumns(config.mapWidth, config.startingNodesCount, random);
            foreach (int col in startingCols)
            {
                var startNodeObj = new NodeBlueprint(new Vector2Int(col, 0), NodeType.MinorEnemy);
                graph.floors[0].Add(startNodeObj);
            }

            // Step 1b: Create Start Base Anchor Node at Floor -1 (Center Start)
            int centerStartX = config.mapWidth / 2;
            var baseStartNode = new NodeBlueprint(new Vector2Int(centerStartX, -1), NodeType.RestSite);
            baseStartNode.status = NodeStatus.Visited;
            baseStartNode.visibility = NodeVisibility.Visited;

            foreach (var nodeOnFloor0 in graph.floors[0])
            {
                baseStartNode.AddOutgoingConnection(nodeOnFloor0.gridPosition);
                nodeOnFloor0.AddIncomingConnection(baseStartNode.gridPosition);
            }
            graph.startNode = baseStartNode;
            graph.currentPlayerPosition = baseStartNode.gridPosition;

            // Step 2: Pick pre-boss nodes on Floor (totalFloors - 1) (exact count: config.preBossNodesCount)
            int preBossY = config.totalFloors - 1;
            List<int> preBossCols = GetRandomStartingColumns(config.mapWidth, Math.Min(config.preBossNodesCount, config.mapWidth), random);
            foreach (int col in preBossCols)
            {
                if (graph.GetNode(new Vector2Int(col, preBossY)) == null)
                {
                    graph.floors[preBossY].Add(new NodeBlueprint(new Vector2Int(col, preBossY), NodeType.RestSite));
                }
            }

            // Step 3: Generate paths (pathCount + extraPaths) using deterministic for-loop
            int totalPathCount = config.pathCount + config.extraPaths;
            for (int pathIdx = 0; pathIdx < totalPathCount; pathIdx++)
            {
                var startNodes = graph.floors[0];
                NodeBlueprint currentNode = startNodes[random.Next(startNodes.Count)];

                for (int y = 0; y < config.totalFloors - 1; y++)
                {
                    int currentX = currentNode.gridPosition.x;
                    int nextY = y + 1;

                    List<int> validNextCols = GetValidNextColumns(currentX, config.mapWidth, graph, y);
                    
                    // If moving into pre-boss floor, prefer preBossCols candidates
                    if (nextY == preBossY)
                    {
                        var matchingPreBossCols = validNextCols.FindAll(c => preBossCols.Contains(c));
                        if (matchingPreBossCols.Count > 0)
                        {
                            validNextCols = matchingPreBossCols;
                        }
                    }

                    int nextX = validNextCols[random.Next(validNextCols.Count)];

                    NodeBlueprint nextNode = graph.GetNode(new Vector2Int(nextX, nextY));
                    if (nextNode == null)
                    {
                        nextNode = new NodeBlueprint(new Vector2Int(nextX, nextY), NodeType.MinorEnemy);
                        graph.floors[nextY].Add(nextNode);
                    }

                    currentNode.AddOutgoingConnection(nextNode.gridPosition);
                    nextNode.AddIncomingConnection(currentNode.gridPosition);

                    currentNode = nextNode;
                }
            }

            // Step 4: Create Boss Node on the final floor and connect top-floor pre-boss nodes to it
            int bossY = config.totalFloors;
            int bossX = config.mapWidth / 2;
            NodeBlueprint bossNode = new NodeBlueprint(new Vector2Int(bossX, bossY), NodeType.Boss);
            graph.floors[bossY].Add(bossNode);

            foreach (var topNode in graph.floors[preBossY])
            {
                topNode.AddOutgoingConnection(bossNode.gridPosition);
                bossNode.AddIncomingConnection(topNode.gridPosition);
            }

            // Step 4: Remove cross connections (X-crossings between adjacent columns)
            RemoveCrossConnections(graph);

            // Step 5: Cleanup orphan nodes (nodes with 0 incoming connections except Floor 0)
            CleanupOrphans(graph);

            // Step 6: Generate Organic Position Jitter per node
            GenerateNodeJitter(graph, config, random);

            // Step 7: Assign Node Types based on Floor Rules and Weighted Probabilities
            AssignNodeTypes(graph, config, random);

            // Step 8: Initialize Visibility & Status (Floor 0 = Attainable & Visible, others = Locked)
            InitializeVisibilityAndStatus(graph);

            return graph;
        }

        private List<int> GetRandomStartingColumns(int mapWidth, int count, System.Random random)
        {
            List<int> available = new List<int>();
            for (int i = 0; i < mapWidth; i++) available.Add(i);
            
            List<int> result = new List<int>();
            for (int i = 0; i < count && available.Count > 0; i++)
            {
                int index = random.Next(available.Count);
                result.Add(available[index]);
                available.RemoveAt(index);
            }
            result.Sort();
            return result;
        }

        private List<int> GetValidNextColumns(int currentX, int mapWidth, MapGraphData graph, int currentY)
        {
            List<int> candidates = new List<int>();
            int[] offsets = { -1, 0, 1 };

            foreach (int offset in offsets)
            {
                int targetX = currentX + offset;
                if (targetX >= 0 && targetX < mapWidth)
                {
                    if (!WouldCrossExistingEdge(currentX, currentY, targetX, graph))
                    {
                        candidates.Add(targetX);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                candidates.Add(currentX); // Fallback straight up
            }

            return candidates;
        }

        private bool WouldCrossExistingEdge(int fromX, int fromY, int toX, MapGraphData graph)
        {
            int nextY = fromY + 1;
            if (toX > fromX)
            {
                var neighbor = graph.GetNode(new Vector2Int(fromX + 1, fromY));
                if (neighbor != null && neighbor.outgoingConnections.Contains(new Vector2Int(fromX, nextY)))
                {
                    return true;
                }
            }
            else if (toX < fromX)
            {
                var neighbor = graph.GetNode(new Vector2Int(fromX - 1, fromY));
                if (neighbor != null && neighbor.outgoingConnections.Contains(new Vector2Int(fromX, nextY)))
                {
                    return true;
                }
            }
            return false;
        }

        private void RemoveCrossConnections(MapGraphData graph)
        {
            for (int y = 0; y < graph.floors.Count - 1; y++)
            {
                foreach (var node in graph.floors[y])
                {
                    for (int i = node.outgoingConnections.Count - 1; i >= 0; i--)
                    {
                        Vector2Int targetPos = node.outgoingConnections[i];
                        int fromX = node.gridPosition.x;
                        int toX = targetPos.x;

                        // Check for crossing edge from adjacent column
                        if (toX != fromX)
                        {
                            var neighborNode = graph.GetNode(new Vector2Int(toX, y));
                            if (neighborNode != null && neighborNode.outgoingConnections.Contains(new Vector2Int(fromX, y + 1)))
                            {
                                // Cross detected: Remove neighbor's connection to keep current node's path
                                neighborNode.outgoingConnections.Remove(new Vector2Int(fromX, y + 1));
                                var targetNode = graph.GetNode(new Vector2Int(fromX, y + 1));
                                if (targetNode != null)
                                {
                                    targetNode.incomingConnections.Remove(neighborNode.gridPosition);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void CleanupOrphans(MapGraphData graph)
        {
            for (int y = 1; y < graph.floors.Count; y++)
            {
                var floorList = graph.floors[y];
                for (int i = floorList.Count - 1; i >= 0; i--)
                {
                    if (floorList[i].incomingConnections.Count == 0)
                    {
                        foreach (var targetPos in floorList[i].outgoingConnections)
                        {
                            var targetNode = graph.GetNode(targetPos);
                            if (targetNode != null)
                            {
                                targetNode.incomingConnections.Remove(floorList[i].gridPosition);
                            }
                        }
                        floorList.RemoveAt(i);
                    }
                }
            }
        }

        private void GenerateNodeJitter(MapGraphData graph, MapConfigSO config, System.Random random)
        {
            if (config.nodePositionJitter <= 0f) return;

            for (int y = 0; y < graph.floors.Count; y++)
            {
                foreach (var node in graph.floors[y])
                {
                    if (node.type == NodeType.Boss || y == config.totalFloors)
                    {
                        node.positionOffset = Vector2.zero;
                        continue;
                    }

                    float jitterX = (float)(random.NextDouble() * 2.0 - 1.0) * config.nodePositionJitter;
                    float jitterY = (float)(random.NextDouble() * 2.0 - 1.0) * config.nodePositionJitter;
                    node.positionOffset = new Vector2(jitterX, jitterY);
                }
            }
        }

        private void AssignNodeTypes(MapGraphData graph, MapConfigSO config, System.Random random)
        {
            for (int y = 0; y < graph.floors.Count; y++)
            {
                NodeType? overrideType = GetFloorOverride(y, config);

                foreach (var node in graph.floors[y])
                {
                    if (overrideType.HasValue)
                    {
                        node.type = overrideType.Value;
                    }
                    else
                    {
                        node.type = RollRandomNodeType(y, node, graph, config, random);
                    }
                }
            }
        }

        private NodeType? GetFloorOverride(int floorIndex, MapConfigSO config)
        {
            var rule = config.floorOverrides.Find(r => r.floorIndex == floorIndex);
            if (rule.floorIndex == floorIndex && rule.nodeType != NodeType.MinorEnemy)
            {
                return rule.nodeType;
            }
            if (floorIndex == 0) return NodeType.MinorEnemy;
            if (floorIndex == config.totalFloors / 2) return NodeType.Treasure;
            if (floorIndex == config.totalFloors - 1) return NodeType.RestSite;
            if (floorIndex == config.totalFloors) return NodeType.Boss;
            return null;
        }

        private NodeType RollRandomNodeType(int floorIndex, NodeBlueprint node, MapGraphData graph, MapConfigSO config, System.Random random)
        {
            bool hasIncomingRest = false;
            bool hasIncomingShop = false;

            if (config.preventConsecutiveRestSites || config.preventConsecutiveShops)
            {
                foreach (var incomingPos in node.incomingConnections)
                {
                    var parentNode = graph.GetNode(incomingPos);
                    if (parentNode != null)
                    {
                        if (parentNode.type == NodeType.RestSite) hasIncomingRest = true;
                        if (parentNode.type == NodeType.Store) hasIncomingShop = true;
                    }
                }
            }

            NodeType rolledType = NodeType.MinorEnemy;
            bool isValid = false;
            int maxAttempts = 10;

            for (int attempt = 0; attempt < maxAttempts && !isValid; attempt++)
            {
                double roll = random.NextDouble();
                if (floorIndex < 4)
                {
                    if (roll < 0.65) rolledType = NodeType.MinorEnemy;
                    else if (roll < 0.85) rolledType = NodeType.Store;
                    else rolledType = NodeType.RestSite;
                }
                else
                {
                    if (roll < 0.45) rolledType = NodeType.MinorEnemy;
                    else if (roll < 0.65) rolledType = (floorIndex >= config.minEliteFloor) ? NodeType.EliteEnemy : NodeType.MinorEnemy;
                    else if (roll < 0.80) rolledType = NodeType.Treasure;
                    else if (roll < 0.90) rolledType = NodeType.Store;
                    else rolledType = NodeType.RestSite;
                }

                isValid = true;
                if (config.preventConsecutiveRestSites && hasIncomingRest && rolledType == NodeType.RestSite) isValid = false;
                if (config.preventConsecutiveShops && hasIncomingShop && rolledType == NodeType.Store) isValid = false;
            }

            if (!isValid) rolledType = NodeType.MinorEnemy; // Safe fallback
            return rolledType;
        }

        private void InitializeVisibilityAndStatus(MapGraphData graph)
        {
            for (int y = 0; y < graph.floors.Count; y++)
            {
                foreach (var node in graph.floors[y])
                {
                    if (y == 0)
                    {
                        node.status = NodeStatus.Attainable;
                        node.visibility = NodeVisibility.Reachable;
                    }
                    else
                    {
                        node.status = NodeStatus.Locked;
                        node.visibility = NodeVisibility.Visible;
                    }
                }
            }
        }
    }
}
