using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace TawanOS.MapEngine
{
    [Serializable]
    [JsonObject(MemberSerialization.OptOut)]
    public class NodeBlueprint
    {
        public Vector2Int gridPosition; // x = Column, y = Floor
        public Vector2 positionOffset; // Random organic visual jitter offset
        public NodeType type;
        public NodeStatus status = NodeStatus.Locked;
        public NodeVisibility visibility = NodeVisibility.Hidden;
        public List<Vector2Int> outgoingConnections = new List<Vector2Int>();
        public List<Vector2Int> incomingConnections = new List<Vector2Int>();

        public NodeBlueprint() { }

        public NodeBlueprint(Vector2Int gridPosition, NodeType type)
        {
            this.gridPosition = gridPosition;
            this.type = type;
        }

        public void AddOutgoingConnection(Vector2Int targetPos)
        {
            if (!outgoingConnections.Contains(targetPos))
            {
                outgoingConnections.Add(targetPos);
            }
        }

        public void AddIncomingConnection(Vector2Int sourcePos)
        {
            if (!incomingConnections.Contains(sourcePos))
            {
                incomingConnections.Add(sourcePos);
            }
        }
    }
}
