using System;
using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.MapEngine
{
    [Serializable]
    public class MapGraphData
    {
        public int seed;
        public int totalFloors;
        public int mapWidth;
        public Vector2Int currentPlayerPosition = new Vector2Int(-1, -1);
        public NodeBlueprint startNode;
        public List<List<NodeBlueprint>> floors = new List<List<NodeBlueprint>>();

        public MapGraphData() { }

        public MapGraphData(int seed, int totalFloors, int mapWidth)
        {
            this.seed = seed;
            this.totalFloors = totalFloors;
            this.mapWidth = mapWidth;
            
            for (int y = 0; y <= totalFloors; y++)
            {
                floors.Add(new List<NodeBlueprint>());
            }
        }

        public NodeBlueprint GetNode(Vector2Int pos)
        {
            if (pos.y == -1)
            {
                return startNode;
            }
            if (pos.y >= 0 && pos.y < floors.Count)
            {
                var floorList = floors[pos.y];
                return floorList.Find(n => n.gridPosition.x == pos.x);
            }
            return null;
        }

        public List<NodeBlueprint> GetNodesOnFloor(int floorIndex)
        {
            if (floorIndex >= 0 && floorIndex < floors.Count)
            {
                return floors[floorIndex];
            }
            return new List<NodeBlueprint>();
        }

        public List<NodeBlueprint> GetAllNodes()
        {
            List<NodeBlueprint> all = new List<NodeBlueprint>();
            foreach (var floor in floors)
            {
                if (floor != null) all.AddRange(floor);
            }
            return all;
        }
    }
}
