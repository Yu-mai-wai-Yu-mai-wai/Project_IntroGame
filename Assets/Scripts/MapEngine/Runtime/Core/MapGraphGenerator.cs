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

            if (config != null && config.use3DTableMode)
            {
                return GenerateTableMap(config, actualSeed, random);
            }

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
            if (config != null && config.floorOverrides != null)
            {
                var rule = config.floorOverrides.Find(r => r.floorIndex == floorIndex);
                if (rule.floorIndex == floorIndex && rule.nodeType != NodeType.MinorEnemy)
                {
                    return rule.nodeType;
                }
            }

            if (config != null)
            {
                if (floorIndex == 0) return NodeType.MinorEnemy;
                if (floorIndex == config.totalFloors / 2) return NodeType.Treasure;
                if (floorIndex == config.totalFloors - 1) return NodeType.RestSite;
                if (floorIndex == config.totalFloors) return NodeType.Boss;
            }
            return null;
        }

        private NodeType RollRandomNodeType(int floorIndex, NodeBlueprint node, MapGraphData graph, MapConfigSO config, System.Random random)
        {
            bool hasIncomingRest = false;
            bool hasIncomingShop = false;

            if (node != null && node.incomingConnections != null && graph != null && config != null && (config.preventConsecutiveRestSites || config.preventConsecutiveShops))
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
            int minElite = (config != null) ? config.minEliteFloor : 5;
            bool checkRest = (config != null) && config.preventConsecutiveRestSites;
            bool checkShop = (config != null) && config.preventConsecutiveShops;

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
                    else if (roll < 0.65) rolledType = (floorIndex >= minElite) ? NodeType.EliteEnemy : NodeType.MinorEnemy;
                    else if (roll < 0.80) rolledType = NodeType.Treasure;
                    else if (roll < 0.90) rolledType = NodeType.Store;
                    else rolledType = NodeType.RestSite;
                }

                isValid = true;
                if (checkRest && hasIncomingRest && rolledType == NodeType.RestSite) isValid = false;
                if (checkShop && hasIncomingShop && rolledType == NodeType.Store) isValid = false;
            }

            if (!isValid) rolledType = NodeType.MinorEnemy; // Safe fallback
            return rolledType;
        }

        private MapGraphData GenerateTableMap(MapConfigSO config, int actualSeed, System.Random random)
        {
            // Total 8 floors (0 to 7) matching the 19 pedestals on the paper board
            int totalFloors = 7;
            int mapWidth = 3;
            MapGraphData graph = new MapGraphData(actualSeed, totalFloors, mapWidth);

            // Floor -1: Start Node (Node.001)
            var baseStartNode = new NodeBlueprint(new Vector2Int(1, -1), NodeType.RestSite);
            baseStartNode.status = NodeStatus.Visited;
            baseStartNode.visibility = NodeVisibility.Visited;
            baseStartNode.positionOffset = Vector2.zero;
            graph.startNode = baseStartNode;
            graph.currentPlayerPosition = baseStartNode.gridPosition;

            // Floor 0: 3 nodes (Node, Node.003, Node.002)
            var node0_0 = new NodeBlueprint(new Vector2Int(0, 0), NodeType.MinorEnemy);
            var node1_0 = new NodeBlueprint(new Vector2Int(1, 0), NodeType.MinorEnemy);
            var node2_0 = new NodeBlueprint(new Vector2Int(2, 0), NodeType.MinorEnemy);
            graph.floors[0].Add(node0_0);
            graph.floors[0].Add(node1_0);
            graph.floors[0].Add(node2_0);

            // Connect Floor -1 to Floor 0
            baseStartNode.AddOutgoingConnection(node0_0.gridPosition);
            node0_0.AddIncomingConnection(baseStartNode.gridPosition);
            baseStartNode.AddOutgoingConnection(node1_0.gridPosition);
            node1_0.AddIncomingConnection(baseStartNode.gridPosition);
            baseStartNode.AddOutgoingConnection(node2_0.gridPosition);
            node2_0.AddIncomingConnection(baseStartNode.gridPosition);

            // Floor 1: 2 nodes (Node.004, Node.005)
            var node0_1 = new NodeBlueprint(new Vector2Int(0, 1), NodeType.MinorEnemy);
            var node1_1 = new NodeBlueprint(new Vector2Int(1, 1), NodeType.MinorEnemy);
            graph.floors[1].Add(node0_1);
            graph.floors[1].Add(node1_1);

            // Connect Floor 0 to Floor 1
            node0_0.AddOutgoingConnection(node0_1.gridPosition);
            node0_1.AddIncomingConnection(node0_0.gridPosition);
            node1_0.AddOutgoingConnection(node0_1.gridPosition);
            node0_1.AddIncomingConnection(node1_0.gridPosition);
            node1_0.AddOutgoingConnection(node1_1.gridPosition);
            node1_1.AddIncomingConnection(node1_0.gridPosition);
            node2_0.AddOutgoingConnection(node1_1.gridPosition);
            node1_1.AddIncomingConnection(node2_0.gridPosition);

            // Floor 2: 3 nodes (Node.006, Node.008, Node.007)
            var node0_2 = new NodeBlueprint(new Vector2Int(0, 2), NodeType.MinorEnemy);
            var node1_2 = new NodeBlueprint(new Vector2Int(1, 2), NodeType.MinorEnemy);
            var node2_2 = new NodeBlueprint(new Vector2Int(2, 2), NodeType.MinorEnemy);
            graph.floors[2].Add(node0_2);
            graph.floors[2].Add(node1_2);
            graph.floors[2].Add(node2_2);

            // Connect Floor 1 to Floor 2
            node0_1.AddOutgoingConnection(node0_2.gridPosition);
            node0_2.AddIncomingConnection(node0_1.gridPosition);
            node0_1.AddOutgoingConnection(node1_2.gridPosition);
            node1_2.AddIncomingConnection(node0_1.gridPosition);
            node1_1.AddOutgoingConnection(node1_2.gridPosition);
            node1_2.AddIncomingConnection(node1_1.gridPosition);
            node1_1.AddOutgoingConnection(node2_2.gridPosition);
            node2_2.AddIncomingConnection(node1_1.gridPosition);

            // Floor 3: 1 node (Node.009) - Midpoint Sanctuary/Treasure
            var node1_3 = new NodeBlueprint(new Vector2Int(1, 3), NodeType.Treasure);
            graph.floors[3].Add(node1_3);

            // Connect Floor 2 to Floor 3
            node0_2.AddOutgoingConnection(node1_3.gridPosition);
            node1_3.AddIncomingConnection(node0_2.gridPosition);
            node1_2.AddOutgoingConnection(node1_3.gridPosition);
            node1_3.AddIncomingConnection(node1_2.gridPosition);
            node2_2.AddOutgoingConnection(node1_3.gridPosition);
            node1_3.AddIncomingConnection(node2_2.gridPosition);

            // Floor 4: 3 nodes (Node.010, Node.012, Node.011)
            var node0_4 = new NodeBlueprint(new Vector2Int(0, 4), NodeType.MinorEnemy);
            var node1_4 = new NodeBlueprint(new Vector2Int(1, 4), NodeType.MinorEnemy);
            var node2_4 = new NodeBlueprint(new Vector2Int(2, 4), NodeType.MinorEnemy);
            graph.floors[4].Add(node0_4);
            graph.floors[4].Add(node1_4);
            graph.floors[4].Add(node2_4);

            // Connect Floor 3 to Floor 4
            node1_3.AddOutgoingConnection(node0_4.gridPosition);
            node0_4.AddIncomingConnection(node1_3.gridPosition);
            node1_3.AddOutgoingConnection(node1_4.gridPosition);
            node1_4.AddIncomingConnection(node1_3.gridPosition);
            node1_3.AddOutgoingConnection(node2_4.gridPosition);
            node2_4.AddIncomingConnection(node1_3.gridPosition);

            // Floor 5: 3 nodes (Node.013, Node.015, Node.014)
            var node0_5 = new NodeBlueprint(new Vector2Int(0, 5), NodeType.MinorEnemy);
            var node1_5 = new NodeBlueprint(new Vector2Int(1, 5), NodeType.MinorEnemy);
            var node2_5 = new NodeBlueprint(new Vector2Int(2, 5), NodeType.MinorEnemy);
            graph.floors[5].Add(node0_5);
            graph.floors[5].Add(node1_5);
            graph.floors[5].Add(node2_5);

            // Connect Floor 4 to Floor 5
            node0_4.AddOutgoingConnection(node0_5.gridPosition);
            node0_5.AddIncomingConnection(node0_4.gridPosition);
            node0_4.AddOutgoingConnection(node1_5.gridPosition);
            node1_5.AddIncomingConnection(node0_4.gridPosition);
            node1_4.AddOutgoingConnection(node1_5.gridPosition);
            node1_5.AddIncomingConnection(node1_4.gridPosition);
            node2_4.AddOutgoingConnection(node1_5.gridPosition);
            node1_5.AddIncomingConnection(node2_4.gridPosition);
            node2_4.AddOutgoingConnection(node2_5.gridPosition);
            node2_5.AddIncomingConnection(node2_4.gridPosition);

            // Floor 6: 2 nodes (Node.016, Node.017) - Pre-Boss Camp
            var node0_6 = new NodeBlueprint(new Vector2Int(0, 6), NodeType.RestSite);
            var node1_6 = new NodeBlueprint(new Vector2Int(1, 6), NodeType.RestSite);
            graph.floors[6].Add(node0_6);
            graph.floors[6].Add(node1_6);

            // Connect Floor 5 to Floor 6
            node0_5.AddOutgoingConnection(node0_6.gridPosition);
            node0_6.AddIncomingConnection(node0_5.gridPosition);
            node1_5.AddOutgoingConnection(node0_6.gridPosition);
            node0_6.AddIncomingConnection(node1_5.gridPosition);
            node1_5.AddOutgoingConnection(node1_6.gridPosition);
            node1_6.AddIncomingConnection(node1_5.gridPosition);
            node2_5.AddOutgoingConnection(node1_6.gridPosition);
            node1_6.AddIncomingConnection(node2_5.gridPosition);

            // Floor 7: 1 node (Node.018) - The Boss
            var bossNode = new NodeBlueprint(new Vector2Int(1, 7), NodeType.Boss);
            graph.floors[7].Add(bossNode);

            // Connect Floor 6 to Boss
            node0_6.AddOutgoingConnection(bossNode.gridPosition);
            bossNode.AddIncomingConnection(node0_6.gridPosition);
            node1_6.AddOutgoingConnection(bossNode.gridPosition);
            bossNode.AddIncomingConnection(node1_6.gridPosition);

            // Assign randomized node types for intermediate floors with incoming validation
            int[] randomizedFloors = new int[] { 1, 2, 4, 5 };
            foreach (int floor in randomizedFloors)
            {
                foreach (var node in graph.floors[floor])
                {
                    node.type = RollRandomNodeType(floor, node, graph, config, random);
                }
            }

            // Zero out position offsets and initialize statuses
            foreach (var node in graph.GetAllNodes())
            {
                node.positionOffset = Vector2.zero;
            }
            if (graph.startNode != null) graph.startNode.positionOffset = Vector2.zero;

            InitializeVisibilityAndStatus(graph);

            return graph;
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
