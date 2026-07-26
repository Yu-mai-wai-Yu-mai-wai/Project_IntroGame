namespace TawanOS.MapEngine
{
    public interface IMapSaveSystem
    {
        void SaveMap(MapGraphData data);
        MapGraphData LoadMap();
        bool HasSavedMap();
        void ClearSavedMap();
    }
}
