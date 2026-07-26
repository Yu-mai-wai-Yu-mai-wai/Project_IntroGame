using UnityEngine;

namespace TawanOS.MapEngine
{
    [CreateAssetMenu(fileName = "NewNodeProfile", menuName = "TawanOS/MapEngine/Node Profile")]
    public class NodeProfileSO : ScriptableObject
    {
        public NodeType type;
        public Sprite icon;
        public Color baseColor = Color.white;
        public Color hoverColor = Color.yellow;
        public Color visitedColor = new Color(0.8f, 0.65f, 0.2f);
        public string title = "Node Title";
        [TextArea(2, 5)]
        public string description = "Node Description";
    }
}
