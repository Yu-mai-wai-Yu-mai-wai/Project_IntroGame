using System.IO;
using UnityEngine;

namespace TawanOS.MapEngine
{
    public class MapSaveManager : IMapSaveSystem
    {
        private readonly string saveFilePath;

        public MapSaveManager()
        {
            saveFilePath = Path.Combine(Application.persistentDataPath, "map_save.json");
        }

        public void SaveMap(MapGraphData data)
        {
            if (data == null) return;
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(saveFilePath, json);
            PlayerPrefs.SetString("MapSaveData", json);
            PlayerPrefs.Save();
        }

        public MapGraphData LoadMap()
        {
            if (HasSavedMap())
            {
                string json = PlayerPrefs.GetString("MapSaveData", string.Empty);
                if (string.IsNullOrEmpty(json) && File.Exists(saveFilePath))
                {
                    json = File.ReadAllText(saveFilePath);
                }

                if (!string.IsNullOrEmpty(json))
                {
                    return JsonUtility.FromJson<MapGraphData>(json);
                }
            }
            return null;
        }

        public bool HasSavedMap()
        {
            return PlayerPrefs.HasKey("MapSaveData") || File.Exists(saveFilePath);
        }

        public void ClearSavedMap()
        {
            PlayerPrefs.DeleteKey("MapSaveData");
            if (File.Exists(saveFilePath))
            {
                File.Delete(saveFilePath);
            }
        }
    }
}
