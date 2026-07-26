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

            // Step 1: Pick starting nodes on Floor 0
            List<int> startingCols = GetRandomStartingColumns(config.mapWidth, config.startingNodesCount, random);
            foreach (int col in startingCols)
            {
                var startNode = new NodeBlueprint(new Vector2Int(col, 0), NodeType.MinorEnemy);
                graph.floors[0].Add(startNode);
            }

            // Step 2: Generate paths from Floor 0 up to totalFloors - 1
            for (int pathIdx = 0; pathIdx < config.pathCount; pathIdx++)
            {
                // Select a random starting node from Floor 0
                var startNodes = graph.floors[0];
                NodeBlueprint currentNode = startNodes[random.Next(startNodes.Count)];

                for (int y = 0; y < config.totalFloors - 1; y++)
                {
                    int currentX = currentNode.gridPosition.x;
                    int nextY = y + 1;

                    // Determine valid next columns [-1, 0, +1]
                    List<int> validNextCols = GetValidNextColumns(currentX, config.mapWidth, graph, y);
                    int nextX = validNextCols[random.Next(validNextCols.Count)];

                    // Get or create node at (nextX, nextY)
                    NodeBlueprint nextNode = graph.GetNode(new Vector2Int(nextX, nextY));
                    if (nextNode == null)
                    {
                        nextNode = new NodeBlueprint(new Vector2Int(nextX, nextY), NodeType.MinorEnemy);
                        graph.floors[nextY].Add(nextNode);
                    }

                    // Add directed connection
                    currentNode.AddOutgoingConnection(nextNode.gridPosition);
                    nextNode.AddIncomingConnection(currentNode.gridPosition);

                    currentNode = nextNode;
                }
            }

            // Step 3: Create Boss Node on the final floor and connect all top-floor nodes to it
            int bossY = config.totalFloors;
            int bossX = config.mapWidth / 2;
            NodeBlueprint bossNode = new NodeBlueprint(new Vector2Int(bossX, bossY), NodeType.Boss);
            graph.floors[bossY].Add(bossNode);

            foreach (var topNode in graph.floors[config.totalFloors - 1])
            {
                topNode.AddOutgoingConnection(bossNode.gridPosition);
                bossNode.AddIncomingConnection(topNode.gridPosition);
            }

            // Step 4: Cleanup orphan nodes (nodes with 0 incoming connections except Floor 0)
            CleanupOrphans(graph);

            // Step 5: Assign Node Types based on Floor Rules and Weighted Probabilities
            AssignNodeTypes(graph, config, random);

            // Step 6: Initialize Visibility & Status (Floor 0 = Attainable & Visible, others = Locked)
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
                    // Check for line crossings
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
            // Check if there is a connection from (fromX + 1) to (fromX) or similar cross
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

        private void CleanupOrphans(MapGraphData graph)
        {
            for (int y = 1; y < graph.floors.Count; y++)
            {
                var floorList = graph.floors[y];
                for (int i = floorList.Count - 1; i >= 0; i--)
                {
                    if (floorList[i].incomingConnections.Count == 0)
                    {
                        // Remove outgoing connections from this orphan to upper floors
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
                        node.type = RollRandomNodeType(y, config, random);
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
            if (floorIndex == config.totalFloors) return NodeType.Boss;
            return null;
        }

        private NodeType RollRandomNodeType(int floorIndex, MapConfigSO config, System.Random random)
        {
            double roll = random.NextDouble();
            if (floorIndex < 4)
            {
                // Early floors: mostly enemies & events
                if (roll < 0.60) return NodeType.MinorEnemy;
                if (roll < 0.80) return NodeType.Store;
                return NodeType.RestSite;
            }
            else
            {
                // Higher floors: add Elites & Treasure
                if (roll < 0.45) return NodeType.MinorEnemy;
                if (roll < 0.65) return NodeType.EliteEnemy;
                if (roll < 0.80) return NodeType.Treasure;
                if (roll < 0.90) return NodeType.Store;
                return NodeType.RestSite;
            }
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
                        node.visibility = NodeVisibility.Visible; // Fog of War: Visible but Locked
                    }
                }
            }
        }
    }
}
