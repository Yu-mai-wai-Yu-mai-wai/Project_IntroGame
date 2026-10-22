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
            var startNodes = graph.floors[0];
            int totalPathCount = Mathf.Max(config.pathCount + config.extraPaths, startNodes.Count);
            for (int pathIdx = 0; pathIdx < totalPathCount; pathIdx++)
            {
                NodeBlueprint currentNode = (pathIdx < startNodes.Count)
                    ? startNodes[pathIdx]
                    : startNodes[random.Next(startNodes.Count)];

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
                                // Cross detected: Prefer removing connection from the node that has multiple alternatives
                                if (neighborNode.outgoingConnections.Count > 1)
                                {
                                    neighborNode.outgoingConnections.Remove(new Vector2Int(fromX, y + 1));
                                    var targetNode = graph.GetNode(new Vector2Int(fromX, y + 1));
                                    if (targetNode != null)
                                    {
                                        targetNode.incomingConnections.Remove(neighborNode.gridPosition);
                                    }
                                }
                                else if (node.outgoingConnections.Count > 1)
                                {
                                    node.outgoingConnections.Remove(targetPos);
                                    var targetNode = graph.GetNode(targetPos);
                                    if (targetNode != null)
                                    {
                                        targetNode.incomingConnections.Remove(node.gridPosition);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void CleanupOrphans(MapGraphData graph)
        {
            // Forward pass: clean up nodes with 0 incoming connections (Floor 1 upwards)
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

            // Backward pass: clean up dead-end nodes with 0 outgoing connections (pre-boss down to Floor 0)
            for (int y = graph.floors.Count - 2; y >= 0; y--)
            {
                var floorList = graph.floors[y];
                for (int i = floorList.Count - 1; i >= 0; i--)
                {
                    if (floorList[i].outgoingConnections.Count == 0)
                    {
                        foreach (var parentPos in floorList[i].incomingConnections)
                        {
                            var parentNode = graph.GetNode(parentPos);
                            if (parentNode != null)
                            {
                                parentNode.outgoingConnections.Remove(floorList[i].gridPosition);
                            }
                        }
                        if (y == 0 && graph.startNode != null)
                        {
                            graph.startNode.outgoingConnections.Remove(floorList[i].gridPosition);
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
            bool hasIncomingTemple = false;

            if (node != null && node.incomingConnections != null && graph != null && config != null && (config.preventConsecutiveRestSites || config.preventConsecutiveShops))
            {
                foreach (var incomingPos in node.incomingConnections)
                {
                    var parentNode = graph.GetNode(incomingPos);
                    if (parentNode != null)
                    {
                        if (parentNode.type == NodeType.RestSite) hasIncomingRest = true;
                        if (parentNode.type == NodeType.Store) hasIncomingShop = true;
                        if (parentNode.type == NodeType.Temple) hasIncomingTemple = true;
                    }
                }
            }

            NodeType rolledType = NodeType.MinorEnemy;
            bool isValid = false;
            int maxAttempts = 10;
            bool checkRest = (config != null) && config.preventConsecutiveRestSites;
            bool checkShop = (config != null) && config.preventConsecutiveShops;

            // No Elite nodes on the map (Game Designer, 11 Oct): their share went to minor enemies.
            // วัด (Temple) rolls as often as เมรุ, and like it never follows itself on the next floor.
            for (int attempt = 0; attempt < maxAttempts && !isValid; attempt++)
            {
                double roll = random.NextDouble();
                if (floorIndex < 4)
                {
                    if (roll < 0.35) rolledType = NodeType.MinorEnemy;
                    else if (roll < 0.55) rolledType = NodeType.Event;
                    else if (roll < 0.70) rolledType = NodeType.Store;
                    else if (roll < 0.85) rolledType = NodeType.RestSite;
                    else rolledType = NodeType.Temple;
                }
                else
                {
                    if (roll < 0.43) rolledType = NodeType.MinorEnemy;
                    else if (roll < 0.61) rolledType = NodeType.Event;
                    else if (roll < 0.73) rolledType = NodeType.Treasure;
                    else if (roll < 0.82) rolledType = NodeType.Store;
                    else if (roll < 0.91) rolledType = NodeType.RestSite;
                    else rolledType = NodeType.Temple;
                }

                isValid = true;
                if (checkRest && hasIncomingRest && rolledType == NodeType.RestSite) isValid = false;
                if (checkRest && hasIncomingTemple && rolledType == NodeType.Temple) isValid = false;
                if (checkShop && hasIncomingShop && rolledType == NodeType.Store) isValid = false;
            }

            if (!isValid) rolledType = NodeType.MinorEnemy; // Safe fallback
            return rolledType;
        }

        private MapGraphData GenerateTableMap(MapConfigSO config, int actualSeed, System.Random random)
        {
            if (config != null && config.totalFloors <= 4)
            {
                return GenerateShortTableMap(config, actualSeed, random);
            }
            if (config != null && config.totalFloors >= 8)
            {
                return config.randomTableLinks
                    ? GenerateRandomLinkTableMap(config, actualSeed, random)
                    : GenerateEightFloorTableMap(config, actualSeed, random);
            }

            // The 7-floor map (saves made before 11 Oct): 8 rows (0 to 7) matching the 19 pedestals on the paper board
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

        // Node count of each floor on the 8-floor map, floor 0 first (the boss comes after, on floor 8)
        public static readonly int[] EightFloorWidths = { 3, 2, 3, 1, 3, 3, 2, 2 };

        // Fight floors: floors 1, 4 and 7 counted from 1 (Game Designer, 11 Oct). Every node there is a fight, so no
        // path gets past one without a fight (like Inscryption's battle rows, 23 Oct)
        public static readonly int[] EightFloorCombatFloors = { 0, 3, 6 };

        // Which nodes of the floor above each node leads to, by column (same paths as the pedestal map, plus floor 7 -> 8)
        private static readonly int[][][] EightFloorLinks =
        {
            new[] { new[] { 0 }, new[] { 0, 1 }, new[] { 1 } },          // floor 0 (3) -> floor 1 (2)
            new[] { new[] { 0, 1 }, new[] { 1, 2 } },                    // floor 1 (2) -> floor 2 (3)
            new[] { new[] { 0 }, new[] { 0 }, new[] { 0 } },             // floor 2 (3) -> floor 3 (1)
            new[] { new[] { 0, 1, 2 } },                                 // floor 3 (1) -> floor 4 (3)
            new[] { new[] { 0, 1 }, new[] { 1 }, new[] { 1, 2 } },       // floor 4 (3) -> floor 5 (3)
            new[] { new[] { 0 }, new[] { 0, 1 }, new[] { 1 } },          // floor 5 (3) -> floor 6 (2)
            new[] { new[] { 0, 1 }, new[] { 1 } },                       // floor 6 (2) -> floor 7 (2)
        };

        /// <summary>
        /// The 8-floor map (Game Designer, 11 Oct): start, 8 floors of 3,2,3,1,3,3,2,2 nodes, then the boss.
        /// Floors 1, 4 and 7 (counted from 1) have one fight each; floor 8 (just before the boss) deals วัด, ศาลตายาย, เมรุ; every other node rolls หมอกดำ, กองของเซ่น, ศาลตายาย,
        /// เมรุ and วัด with the same chance, with no fixed nodes. The board has pedestals for 7 floors only, so
        /// MapManager places these nodes by floor (TableFloorX) instead of on pedestals.
        /// </summary>
        private MapGraphData GenerateEightFloorTableMap(MapConfigSO config, int actualSeed, System.Random random)
        {
            int floorsBeforeBoss = EightFloorWidths.Length;
            MapGraphData graph = new MapGraphData(actualSeed, floorsBeforeBoss, 3);

            var start = new NodeBlueprint(new Vector2Int(1, -1), NodeType.RestSite);
            start.status = NodeStatus.Visited;
            start.visibility = NodeVisibility.Visited;
            graph.startNode = start;
            graph.currentPlayerPosition = start.gridPosition;

            for (int y = 0; y < floorsBeforeBoss; y++)
                for (int x = 0; x < EightFloorWidths[y]; x++)
                    graph.floors[y].Add(new NodeBlueprint(new Vector2Int(x, y), NodeType.MinorEnemy));
            var boss = new NodeBlueprint(new Vector2Int(1, floorsBeforeBoss), NodeType.Boss);
            graph.floors[floorsBeforeBoss].Add(boss);

            foreach (var node in graph.floors[0]) Connect(start, node);
            for (int y = 0; y < EightFloorLinks.Length; y++)
                for (int x = 0; x < EightFloorLinks[y].Length; x++)
                    foreach (int next in EightFloorLinks[y][x])
                        Connect(graph.floors[y][x], graph.floors[y + 1][next]);
            foreach (var node in graph.floors[floorsBeforeBoss - 1]) Connect(node, boss);

            // Floor by floor, so a node can look at the types of the floor before it
            for (int y = 0; y < floorsBeforeBoss; y++)
                AssignFloorTypes(graph, y, Array.IndexOf(EightFloorCombatFloors, y) >= 0, y == floorsBeforeBoss - 1, config, random);

            foreach (var node in graph.GetAllNodes()) node.positionOffset = Vector2.zero;
            start.positionOffset = Vector2.zero;
            InitializeVisibilityAndStatus(graph);
            return graph;
        }

        // Rows of the board a random-link node can stand on (x of its grid position). The start and the boss use row 1.
        public const int TableLanes = 3;

        /// <summary>
        /// The random-link map (Game Designer, 22 Oct): which nodes exist and how they link is rolled every run.
        /// A few walks go from the start to the boss; each step moves to the next floor (same row or one row over) or,
        /// now and then, skips a floor and stays on its row, so some paths pass fewer nodes than others. No two links
        /// cross and no link runs through a node. Floors 1, 4, 7 (counted from 1) are all fights and no link skips over
        /// one, so every path has three fights (Game Designer, 23 Oct); the last floor before the boss deals วัด,
        /// ศาลตายาย, เมรุ; the others roll a non-fight type. Each node is nudged a little off its spot so the map does
        /// not read as a grid.
        /// </summary>
        private MapGraphData GenerateRandomLinkTableMap(MapConfigSO config, int actualSeed, System.Random random)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                var graph = TryBuildRandomLinks(config, actualSeed, random);
                if (graph == null) continue;

                int preBoss = config.totalFloors - 1;
                for (int y = 0; y <= preBoss; y++)
                    AssignFloorTypes(graph, y, IsFightFloor(y, preBoss), y == preBoss, config, random);

                // x: across the rows, y: along the floors (MapManager places it on the table)
                foreach (var node in graph.GetAllNodes())
                    if (node.type != NodeType.Boss)
                        node.positionOffset = new Vector2(RandomRange(random, NodeJitter.x), RandomRange(random, NodeJitter.y));

                InitializeVisibilityAndStatus(graph);
                return graph;
            }

            Debug.LogWarning($"[MapGraphGenerator] Seed {actualSeed}: no random-link map came out right, using the fixed map");
            return GenerateEightFloorTableMap(config, actualSeed, random);
        }

        // How far a random-link node may sit off its spot, in table units: across the rows, along the floors
        public static readonly Vector2 NodeJitter = new Vector2(0.35f, 0.6f);

        private static float RandomRange(System.Random random, float range) => (float)(random.NextDouble() * 2 - 1) * range;

        private static bool IsFightFloor(int y, int preBoss) => y < preBoss && Array.IndexOf(EightFloorCombatFloors, y) >= 0;

        // The rest stops of the floor before the boss: วัด, ศาลตายาย, เมรุ (Game Designer, 23 Oct: not all วัด)
        private static readonly NodeType[] PreBossTypes = { NodeType.Temple, NodeType.Store, NodeType.RestSite };

        /// <summary>
        /// Types one floor (its parents are typed already). A fight floor is all fights; the floor before the boss deals วัด, ศาลตายาย and เมรุ out without repeats; any other floor rolls a
        /// non-fight type per node. A node leading into the floor before the boss rolls none of those three, so the stop
        /// before the boss never follows one of its own type.
        /// </summary>
        private void AssignFloorTypes(MapGraphData graph, int y, bool fightFloor, bool preBossFloor, MapConfigSO config, System.Random random)
        {
            var nodes = graph.floors[y];
            if (preBossFloor)
            {
                var deal = new List<NodeType>(PreBossTypes);
                Shuffle(deal, random);
                for (int i = 0; i < nodes.Count; i++) nodes[i].type = deal[i % deal.Count];
                return;
            }

            int preBoss = graph.totalFloors - 1;
            foreach (var node in nodes)
            {
                bool intoPreBoss = node.outgoingConnections.Exists(pos => pos.y == preBoss);
                node.type = fightFloor ? NodeType.MinorEnemy : RollNonCombatType(node, graph, config, random, intoPreBoss);
            }
        }

        // One try at the shape; null when a walk gets stuck or the map misses a rule, and the caller rolls again
        private MapGraphData TryBuildRandomLinks(MapConfigSO config, int actualSeed, System.Random random)
        {
            int floors = config.totalFloors;
            int preBoss = floors - 1;
            var graph = new MapGraphData(actualSeed, floors, TableLanes) { randomLinks = true };

            var start = new NodeBlueprint(new Vector2Int(1, -1), NodeType.RestSite);
            start.status = NodeStatus.Visited;
            start.visibility = NodeVisibility.Visited;
            graph.startNode = start;
            graph.currentPlayerPosition = start.gridPosition;
            var boss = new NodeBlueprint(new Vector2Int(1, floors), NodeType.Boss);
            graph.floors[floors].Add(boss);

            // Links between floor 0 and the last floor before the boss; the start and boss fans can never cross them
            var links = new List<(Vector2Int a, Vector2Int b)>();
            int walks = Mathf.Max(2, config.tablePathCount);
            int firstLane = random.Next(TableLanes);
            for (int w = 0; w < walks; w++)
            {
                // The first two walks start on different rows, so the run opens with a choice
                int lane = w == 0 ? firstLane
                    : w == 1 ? (firstLane + 1 + random.Next(TableLanes - 1)) % TableLanes
                    : random.Next(TableLanes);
                var node = GetOrAddNode(graph, new Vector2Int(lane, 0));
                Connect(start, node);

                while (node.gridPosition.y < preBoss)
                {
                    Vector2Int here = node.gridPosition;
                    var steps = new List<Vector2Int>();
                    // A skip never jumps over a fight floor, so no path gets past a fight
                    if (here.y + 2 <= preBoss && !IsFightFloor(here.y + 1, preBoss) && random.NextDouble() < config.skipLinkChance)
                        steps.Add(new Vector2Int(here.x, here.y + 2));
                    var sideways = new List<int> { -1, 0, 1 };
                    Shuffle(sideways, random);
                    foreach (int d in sideways)
                        if (here.x + d >= 0 && here.x + d < TableLanes) steps.Add(new Vector2Int(here.x + d, here.y + 1));

                    int pick = steps.FindIndex(to => !LinkBlocked(graph, links, here, to));
                    if (pick < 0) return null;
                    Vector2Int target = steps[pick];
                    var next = GetOrAddNode(graph, target);
                    if (!node.outgoingConnections.Contains(target)) links.Add((here, target));
                    Connect(node, next);
                    node = next;
                }
                Connect(node, boss);
            }

            // Every floor has a node, the run opens with a choice, the floor before the boss offers one too, at least one
            // link skips a floor, and at least one path still passes every floor
            for (int y = 0; y <= preBoss; y++)
                if (graph.floors[y].Count == 0) return null;
            if (graph.floors[0].Count < 2 || graph.floors[preBoss].Count < 2) return null;
            if (!links.Exists(l => l.b.y - l.a.y == 2)) return null;
            var longest = new Dictionary<Vector2Int, int>();
            for (int y = preBoss; y >= 0; y--)
            {
                foreach (var n in graph.floors[y])
                {
                    int best = 0;
                    foreach (var pos in n.outgoingConnections)
                        if (pos.y <= preBoss) best = Math.Max(best, longest[pos]);
                    longest[n.gridPosition] = best + 1;
                }
            }
            bool fullPath = false;
            foreach (var n in graph.floors[0]) if (longest[n.gridPosition] == floors) fullPath = true;
            return fullPath ? graph : null;
        }

        private static NodeBlueprint GetOrAddNode(MapGraphData graph, Vector2Int pos)
        {
            var node = graph.GetNode(pos);
            if (node != null) return node;
            node = new NodeBlueprint(pos, NodeType.MinorEnemy);
            graph.floors[pos.y].Add(node);
            return node;
        }

        private static void Shuffle<T>(List<T> list, System.Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // A new link from -> to is blocked when it crosses or overlaps a link, runs through a node, or ends on a new
        // node that an existing link already runs through. Points are (row, floor), so a skip link's middle is a spot.
        private static bool LinkBlocked(MapGraphData graph, List<(Vector2Int a, Vector2Int b)> links, Vector2Int from, Vector2Int to)
        {
            foreach (var link in links)
            {
                if (link.a == from && link.b == to) return false; // the walk reuses a link that is already there
            }
            foreach (var link in links)
            {
                if (LinksClash(from, to, link.a, link.b)) return true;
            }
            foreach (var node in graph.GetAllNodes())
            {
                if (node.type != NodeType.Boss && OnLinkInside(node.gridPosition, from, to)) return true;
            }
            if (graph.GetNode(to) == null)
            {
                foreach (var link in links)
                    if (OnLinkInside(to, link.a, link.b)) return true;
            }
            return false;
        }

        private static bool LinksClash(Vector2Int a, Vector2Int b, Vector2Int c, Vector2Int d)
        {
            if (a == c || a == d || b == c || b == d)
            {
                // Links from the same node only clash when one lies along the other
                return OnLinkInside(c, a, b) || OnLinkInside(d, a, b) || OnLinkInside(a, c, d) || OnLinkInside(b, c, d);
            }
            int o1 = Orientation(a, b, c), o2 = Orientation(a, b, d), o3 = Orientation(c, d, a), o4 = Orientation(c, d, b);
            if (o1 * o2 < 0 && o3 * o4 < 0) return true;
            return OnLink(c, a, b) || OnLink(d, a, b) || OnLink(a, c, d) || OnLink(b, c, d);
        }

        private static int Orientation(Vector2Int a, Vector2Int b, Vector2Int c)
        {
            long cross = (long)(b.x - a.x) * (c.y - a.y) - (long)(b.y - a.y) * (c.x - a.x);
            return Math.Sign(cross);
        }

        // p lies on the link a-b, ends included
        private static bool OnLink(Vector2Int p, Vector2Int a, Vector2Int b)
        {
            return Orientation(a, b, p) == 0
                && p.x >= Math.Min(a.x, b.x) && p.x <= Math.Max(a.x, b.x)
                && p.y >= Math.Min(a.y, b.y) && p.y <= Math.Max(a.y, b.y);
        }

        // p lies on the link a-b, ends left out
        private static bool OnLinkInside(Vector2Int p, Vector2Int a, Vector2Int b)
        {
            return p != a && p != b && OnLink(p, a, b);
        }

        private static void Connect(NodeBlueprint from, NodeBlueprint to)
        {
            from.AddOutgoingConnection(to.gridPosition);
            to.AddIncomingConnection(from.gridPosition);
        }

        private static readonly NodeType[] NonCombatTypes =
            { NodeType.Event, NodeType.Treasure, NodeType.Store, NodeType.RestSite, NodeType.Temple };

        // Every non-fight node type with the same chance; เมรุ, ศาลตายาย and วัด never follow a node of their own type
        // noRestStops: no วัด, ศาลตายาย or เมรุ (the node leads into the floor before the boss, which has them)
        private NodeType RollNonCombatType(NodeBlueprint node, MapGraphData graph, MapConfigSO config, System.Random random, bool noRestStops = false)
        {
            bool checkRest = config == null || config.preventConsecutiveRestSites;
            bool checkShop = config == null || config.preventConsecutiveShops;
            var blocked = new HashSet<NodeType>();
            if (noRestStops) blocked.UnionWith(PreBossTypes);
            foreach (var pos in node.incomingConnections)
            {
                var parent = graph.GetNode(pos);
                if (parent == null || pos.y < 0) continue; // the start node is typed เมรุ but is no stop
                if (checkRest && (parent.type == NodeType.RestSite || parent.type == NodeType.Temple)) blocked.Add(parent.type);
                if (checkShop && parent.type == NodeType.Store) blocked.Add(parent.type);
            }

            var choices = new List<NodeType>();
            foreach (var type in NonCombatTypes) if (!blocked.Contains(type)) choices.Add(type);
            return choices[random.Next(choices.Count)];
        }

        private MapGraphData GenerateShortTableMap(MapConfigSO config, int actualSeed, System.Random random)
        {
            int totalFloors = 4;
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

            // Floor 3: 2 nodes (Node.016, Node.017) - Pre-Boss Camp
            var node0_3 = new NodeBlueprint(new Vector2Int(0, 3), NodeType.RestSite);
            var node1_3 = new NodeBlueprint(new Vector2Int(1, 3), NodeType.RestSite);
            graph.floors[3].Add(node0_3);
            graph.floors[3].Add(node1_3);

            // Connect Floor 2 to Floor 3
            node0_2.AddOutgoingConnection(node0_3.gridPosition);
            node0_3.AddIncomingConnection(node0_2.gridPosition);
            node1_2.AddOutgoingConnection(node0_3.gridPosition);
            node0_3.AddIncomingConnection(node1_2.gridPosition);
            node1_2.AddOutgoingConnection(node1_3.gridPosition);
            node1_3.AddIncomingConnection(node1_2.gridPosition);
            node2_2.AddOutgoingConnection(node1_3.gridPosition);
            node1_3.AddIncomingConnection(node2_2.gridPosition);

            // Floor 4: 1 node (Node.018) - The Boss
            var bossNode = new NodeBlueprint(new Vector2Int(1, 4), NodeType.Boss);
            graph.floors[4].Add(bossNode);

            // Connect Floor 3 to Boss
            node0_3.AddOutgoingConnection(bossNode.gridPosition);
            bossNode.AddIncomingConnection(node0_3.gridPosition);
            node1_3.AddOutgoingConnection(bossNode.gridPosition);
            bossNode.AddIncomingConnection(node1_3.gridPosition);

            // Assign randomized node types for intermediate floors (Floor 1, 2)
            int[] randomizedFloors = new int[] { 1, 2 };
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
