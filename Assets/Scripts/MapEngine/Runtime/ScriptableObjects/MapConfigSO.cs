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
        public int pathCount = 6;
        public int minEliteGap = 2;

        [Header("Visual Spacing (2.5D World Space)")]
        public float floorSpacingY = 2.5f;
        public float columnSpacingX = 2.0f;
        public float depthZOffset = 0.2f;

        [Header("Profiles")]
        public BiomeProfileSO biomeProfile;
        public List<NodeProfileSO> nodeProfiles = new List<NodeProfileSO>();
        public List<FloorOverrideRule> floorOverrides = new List<FloorOverrideRule>();

        public NodeProfileSO GetProfileForType(NodeType type)
        {
            var profile = nodeProfiles.Find(p => p != null && p.type == type);
            return profile;
        }
    }
}
