using System;
using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.MapEngine
{
    [Serializable]
    public struct FloorOverrideRule
    {
        public int floorIndex;
        public NodeType nodeType;
    }

    [CreateAssetMenu(fileName = "NewMapConfig", menuName = "TawanOS/MapEngine/Map Config")]
    public class MapConfigSO : ScriptableObject
    {
        [Header("Seed & Grid Settings")]
        public int seed = 12345;
        public bool useRandomSeed = false;
        public int totalFloors = 15;
        public int mapWidth = 7;
        public int startingNodesCount = 3;
        public int preBossNodesCount = 2;
        public int pathCount = 6;
        public int extraPaths = 2;
        public int minEliteGap = 2;
        public int minEliteFloor = 5;

        [Header("Mode & Canvas UI")]
        public bool isUIMode = false;

        [Header("Generation Rules")]
        public bool preventConsecutiveRestSites = true;
        public bool preventConsecutiveShops = true;

        [Header("Visual Spacing & Orientation")]
        public MapOrientation orientation = MapOrientation.BottomToTop;
        public float floorSpacingY = 2.5f;
        public float columnSpacingX = 2.0f;
        public float depthZOffset = 0.2f;
        [Range(0f, 0.8f)]
        public float nodePositionJitter = 0.35f;

        [Header("Profiles")]
        public BiomeProfileSO biomeProfile;
        public List<NodeProfileSO> nodeProfiles = new List<NodeProfileSO>();
        public List<FloorOverrideRule> floorOverrides = new List<FloorOverrideRule>();

        public NodeProfileSO GetProfileForType(NodeType type)
        {
            var profile = nodeProfiles.Find(p => p != null && p.type == type);
            return profile;
        }

        private void OnValidate()
        {
            if (useRandomSeed)
            {
                seed = UnityEngine.Random.Range(10000, 999999);
            }
        }
    }
}
