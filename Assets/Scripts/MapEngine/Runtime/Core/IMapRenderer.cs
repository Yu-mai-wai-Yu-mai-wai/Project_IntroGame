using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.MapEngine
{
    public interface IMapRenderer
    {
        void RenderMap(MapGraphData graphData, MapConfigSO config);
        void UpdateNodeState(Vector2Int nodePos, NodeStatus status, NodeVisibility visibility);
        void ClearMap();
    }
}
