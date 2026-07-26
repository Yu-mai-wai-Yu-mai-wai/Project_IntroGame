using System;

namespace TawanOS.MapEngine
{
    public enum NodeType
    {
        MinorEnemy,
        EliteEnemy,
        RestSite,
        Treasure,
        Store,
        Boss
    }

    public enum NodeStatus
    {
        Locked,
        Attainable,
        Visited,
        Disabled
    }

    public enum NodeVisibility
    {
        Hidden,
        Visible,
        Reachable,
        Visited
    }
}
