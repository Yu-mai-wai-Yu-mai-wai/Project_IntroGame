using UnityEngine;

namespace TawanOS.MapEngine
{
    [CreateAssetMenu(fileName = "NewBiomeProfile", menuName = "TawanOS/MapEngine/Biome Profile")]
    public class BiomeProfileSO : ScriptableObject
    {
        public string biomeName = "Default Act";
        public Color pathBaseColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        public Color pathVisitedColor = new Color(1f, 0.84f, 0f, 1f);
        public Color fogColor = new Color(0.1f, 0.1f, 0.15f, 0.8f);
        public Material pathMaterial;
    }
}
