#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TawanOS.MapEngine.Editor
{
    public class MapEngineSetupTool
    {
        [MenuItem("TawanOS/Map Engine/Setup Test Scene & Profiles")]
        public static void SetupTestSceneAndProfiles()
        {
            string baseFolder = "Assets/MapEngineData";
            if (!AssetDatabase.IsValidFolder(baseFolder))
            {
                AssetDatabase.CreateFolder("Assets", "MapEngineData");
            }
            if (!AssetDatabase.IsValidFolder(baseFolder + "/Profiles"))
            {
                AssetDatabase.CreateFolder(baseFolder, "Profiles");
            }
            if (!AssetDatabase.IsValidFolder(baseFolder + "/Prefabs"))
            {
                AssetDatabase.CreateFolder(baseFolder, "Prefabs");
            }

            // 1. Create Node Profiles
            var enemyProfile = CreateOrGetProfile(baseFolder + "/Profiles/EnemyProfile.asset", NodeType.MinorEnemy, "Minor Enemy", "A basic monster blocking your path.", Color.red);
            var eliteProfile = CreateOrGetProfile(baseFolder + "/Profiles/EliteProfile.asset", NodeType.EliteEnemy, "Elite Enemy", "A powerful elite monster guarding rare rewards.", new Color(1f, 0.2f, 0.4f));
            var restProfile = CreateOrGetProfile(baseFolder + "/Profiles/RestProfile.asset", NodeType.RestSite, "Rest Site", "Rest by the campfire to heal or upgrade cards.", Color.green);
            var treasureProfile = CreateOrGetProfile(baseFolder + "/Profiles/TreasureProfile.asset", NodeType.Treasure, "Treasure Chest", "A mystery chest filled with relics and gold.", Color.yellow);
            var storeProfile = CreateOrGetProfile(baseFolder + "/Profiles/StoreProfile.asset", NodeType.Store, "Shop Merchant", "Buy cards, relics, or remove unwanted cards.", Color.cyan);
            var bossProfile = CreateOrGetProfile(baseFolder + "/Profiles/BossProfile.asset", NodeType.Boss, "Boss", "The final boss of this act!", new Color(0.6f, 0.1f, 0.8f));

            // 2. Create Biome Profile
            string biomePath = baseFolder + "/Profiles/DefaultBiome.asset";
            BiomeProfileSO biome = AssetDatabase.LoadAssetAtPath<BiomeProfileSO>(biomePath);
            if (biome == null)
            {
                biome = ScriptableObject.CreateInstance<BiomeProfileSO>();
                AssetDatabase.CreateAsset(biome, biomePath);
            }

            // 3. Create Map Config
            string configPath = baseFolder + "/Profiles/DefaultMapConfig.asset";
            MapConfigSO config = AssetDatabase.LoadAssetAtPath<MapConfigSO>(configPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MapConfigSO>();
                config.seed = 12345;
                config.totalFloors = 15;
                config.mapWidth = 7;
                config.startingNodesCount = 3;
                config.pathCount = 6;
                config.floorSpacingY = 2.5f;
                config.columnSpacingX = 2.0f;
                config.biomeProfile = biome;

                config.nodeProfiles.Add(enemyProfile);
                config.nodeProfiles.Add(eliteProfile);
                config.nodeProfiles.Add(restProfile);
                config.nodeProfiles.Add(treasureProfile);
                config.nodeProfiles.Add(storeProfile);
                config.nodeProfiles.Add(bossProfile);

                AssetDatabase.CreateAsset(config, configPath);
            }

            // 4. Create Node Prefab
            GameObject nodeGo = new GameObject("NodePrefab");
            var nodeView = nodeGo.AddComponent<MapNodeView>();
            var sr = nodeGo.AddComponent<SpriteRenderer>();
            var col = nodeGo.AddComponent<SphereCollider>();
            col.radius = 0.5f;
            
            // Set private serializable field via SerializedObject
            SerializedObject soNodeView = new SerializedObject(nodeView);
            soNodeView.FindProperty("iconRenderer").objectReferenceValue = sr;
            soNodeView.ApplyModifiedProperties();

            string nodePrefabPath = baseFolder + "/Prefabs/MapNodePrefab.prefab";
            GameObject nodePrefab = PrefabUtility.SaveAsPrefabAsset(nodeGo, nodePrefabPath);
            Object.DestroyImmediate(nodeGo);

            // 5. Create Path Prefab
            GameObject pathGo = new GameObject("PathPrefab");
            var pathView = pathGo.AddComponent<MapPathRenderer>();
            var lr = pathGo.GetComponent<LineRenderer>();
            lr.startWidth = 0.12f;
            lr.endWidth = 0.12f;
            lr.material = new Material(Shader.Find("Sprites/Default"));

            string pathPrefabPath = baseFolder + "/Prefabs/MapPathPrefab.prefab";
            GameObject pathPrefab = PrefabUtility.SaveAsPrefabAsset(pathGo, pathPrefabPath);
            Object.DestroyImmediate(pathGo);

            // 6. Create Player Marker Prefab
            GameObject markerGo = new GameObject("PlayerMarkerPrefab");
            markerGo.AddComponent<PlayerMarker>();
            var markerSr = markerGo.AddComponent<SpriteRenderer>();
            markerSr.color = Color.magenta;

            string markerPrefabPath = baseFolder + "/Prefabs/PlayerMarkerPrefab.prefab";
            GameObject markerPrefab = PrefabUtility.SaveAsPrefabAsset(markerGo, markerPrefabPath);
            Object.DestroyImmediate(markerGo);

            // 7. Create Test Scene
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Setup Camera
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.orthographic = false;
            cam.fieldOfView = 60;
            camGo.transform.position = new Vector3(0, 15, -18);
            camGo.transform.rotation = Quaternion.Euler(35, 0, 0);

            // Setup Directional Light
            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

            // Setup MapManager
            GameObject managerGo = new GameObject("MapManager");
            MapManager manager = managerGo.AddComponent<MapManager>();

            SerializedObject soManager = new SerializedObject(manager);
            soManager.FindProperty("config").objectReferenceValue = config;
            soManager.FindProperty("nodePrefab").objectReferenceValue = nodePrefab.GetComponent<MapNodeView>();
            soManager.FindProperty("pathPrefab").objectReferenceValue = pathPrefab.GetComponent<MapPathRenderer>();
            soManager.FindProperty("playerMarkerPrefab").objectReferenceValue = markerPrefab.GetComponent<PlayerMarker>();
            soManager.ApplyModifiedProperties();

            // Save Scene
            string scenePath = "Assets/Scenes/MapTestScene.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TawanOS MapEngine] Setup Complete! Test Scene saved at: {scenePath}");
        }

        private static NodeProfileSO CreateOrGetProfile(string path, NodeType type, string title, string desc, Color color)
        {
            NodeProfileSO profile = AssetDatabase.LoadAssetAtPath<NodeProfileSO>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<NodeProfileSO>();
                profile.type = type;
                profile.title = title;
                profile.description = desc;
                profile.baseColor = color;
                profile.hoverColor = color * 1.3f;
                AssetDatabase.CreateAsset(profile, path);
            }
            return profile;
        }
    }
}
#endif
