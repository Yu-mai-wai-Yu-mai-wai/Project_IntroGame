namespace TawanOS.MapEngine
{
    public interface IMapGenerator
    {
        MapGraphData GenerateMap(MapConfigSO config, int seed);
    }
}
