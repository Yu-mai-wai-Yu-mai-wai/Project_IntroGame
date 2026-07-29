#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TawanOS.MapEngine
{
    public class MapEngineSetupTool
    {
        [MenuItem("Tools/TawanOS/Map Engine/Setup Test Scene & Profiles")]
        [MenuItem("Window/TawanOS Map Engine Setup")]
        public static void SetupTestSceneAndProfiles()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Setup in Play Mode", "Please exit Play Mode before running the Setup Tool.", "OK");
                return;
            }
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
            var enemyProfile = CreateOrGetProfile(baseFolder + "/Profiles/EnemyProfile.asset", NodeType.MinorEnemy, "Minor Enemy", "A basic monster blocking your path.", Color.red, "crossed-swords.png");
            var eliteProfile = CreateOrGetProfile(baseFolder + "/Profiles/EliteProfile.asset", NodeType.EliteEnemy, "Elite Enemy", "A powerful elite monster guarding rare rewards.", new Color(1f, 0.2f, 0.4f), "alien-skull.png");
            var restProfile = CreateOrGetProfile(baseFolder + "/Profiles/RestProfile.asset", NodeType.RestSite, "Rest Site", "Rest by the campfire to heal or upgrade cards.", Color.green, "campfire.png");
            var treasureProfile = CreateOrGetProfile(baseFolder + "/Profiles/TreasureProfile.asset", NodeType.Treasure, "Treasure Chest", "A mystery chest filled with relics and gold.", Color.yellow, "chest.png");
            var storeProfile = CreateOrGetProfile(baseFolder + "/Profiles/StoreProfile.asset", NodeType.Store, "Shop Merchant", "Buy cards, relics, or remove unwanted cards.", Color.cyan, "shopping-cart.png");
            var bossProfile = CreateOrGetProfile(baseFolder + "/Profiles/BossProfile.asset", NodeType.Boss, "Boss", "The final boss of this act!", new Color(0.6f, 0.1f, 0.8f), "dragon-head.png");

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
            else
            {
                if (config.nodeProfiles == null) config.nodeProfiles = new List<NodeProfileSO>();
                config.nodeProfiles.RemoveAll(p => p == null);
                if (enemyProfile != null && !config.nodeProfiles.Contains(enemyProfile)) config.nodeProfiles.Add(enemyProfile);
                if (eliteProfile != null && !config.nodeProfiles.Contains(eliteProfile)) config.nodeProfiles.Add(eliteProfile);
                if (restProfile != null && !config.nodeProfiles.Contains(restProfile)) config.nodeProfiles.Add(restProfile);
                if (treasureProfile != null && !config.nodeProfiles.Contains(treasureProfile)) config.nodeProfiles.Add(treasureProfile);
                if (storeProfile != null && !config.nodeProfiles.Contains(storeProfile)) config.nodeProfiles.Add(storeProfile);
                if (bossProfile != null && !config.nodeProfiles.Contains(bossProfile)) config.nodeProfiles.Add(bossProfile);
                EditorUtility.SetDirty(config);
            }

            // 4. Create Node Prefab
            GameObject nodeGo = new GameObject("NodePrefab");
            var nodeView = nodeGo.AddComponent<MapNodeView>();
            var col2d = nodeGo.AddComponent<BoxCollider2D>();
            col2d.size = new Vector2(1.5f, 1.5f);

            GameObject bgGo = new GameObject("Background");
            bgGo.transform.SetParent(nodeGo.transform, false);
            var bgSr = bgGo.AddComponent<SpriteRenderer>();
            bgSr.sortingOrder = 0;

            GameObject iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(nodeGo.transform, false);
            var iconSr = iconGo.AddComponent<SpriteRenderer>();
            iconSr.sortingOrder = 1;
            
            nodeView.backgroundRenderer = bgSr;
            nodeView.iconRenderer = iconSr;

            string nodePrefabPath = baseFolder + "/Prefabs/MapNodePrefab.prefab";
            GameObject nodePrefab = PrefabUtility.SaveAsPrefabAsset(nodeGo, nodePrefabPath);
            Object.DestroyImmediate(nodeGo);

            // 5. Create Path Prefab
            GameObject pathGo = new GameObject("PathPrefab");
            var pathView = pathGo.AddComponent<MapPathRenderer>();
            var lr = pathGo.GetComponent<LineRenderer>();
            lr.startWidth = 0.12f;
            lr.endWidth = 0.12f;
            Shader lineShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") 
                             ?? Shader.Find("Sprites/Default") 
                             ?? Shader.Find("Unlit/Color")
                             ?? Shader.Find("GUI/Text Shader");
            if (lineShader != null) lr.material = new Material(lineShader);

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

            // Setup EventSystem
            GameObject eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            var inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType != null)
            {
                eventSystemGo.AddComponent(inputModuleType);
            }
            else
            {
                eventSystemGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Setup Camera
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UnityEngine.EventSystems.Physics2DRaycaster>();
            camGo.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
            var scrollCtrl = camGo.AddComponent<MapScrollController>();
            scrollCtrl.config = config;
            cam.orthographic = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.11f, 0.09f, 0.08f, 1f);
            cam.fieldOfView = 60;
            camGo.transform.position = new Vector3(0, 0, -18);
            camGo.transform.rotation = Quaternion.identity;

            // Setup Directional Light
            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

            // Setup Background Sprite (Parchment Art scaled to fit full map)
            GameObject bgMapGo = new GameObject("MapBackground");
            var bgMapSr = bgMapGo.AddComponent<SpriteRenderer>();
            bgMapSr.sortingOrder = -10;

            float totalMapHeight = config.totalFloors * config.floorSpacingY;
            float totalMapWidth = config.mapWidth * config.columnSpacingX;
            bgMapGo.transform.position = new Vector3(0, totalMapHeight * 0.5f, 5f);
            bgMapGo.transform.localScale = new Vector3(Mathf.Max(6.0f, totalMapWidth * 0.45f), (totalMapHeight / 4.0f) + 6.0f, 1f);
            
            Texture2D bgTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/MapEngineData/Textures/MapBackground.jpg");
            if (bgTex != null)
            {
                Sprite bgSprite = Sprite.Create(bgTex, new Rect(0, 0, bgTex.width, bgTex.height), new Vector2(0.5f, 0.5f), 100f);
                bgMapSr.sprite = bgSprite;
            }

            // Setup MapManager
            GameObject managerGo = new GameObject("MapManager");
            MapManager manager = managerGo.AddComponent<MapManager>();
            manager.config = config;
            manager.nodePrefab = nodePrefab.GetComponent<MapNodeView>();
            manager.pathPrefab = pathPrefab.GetComponent<MapPathRenderer>();
            manager.playerMarkerPrefab = markerPrefab.GetComponent<PlayerMarker>();
            manager.scrollController = scrollCtrl;

            // Setup UI Canvas & Legend UI Panel & Reset Button
            GameObject canvasGo = new GameObject("UICanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // Node Explanation UI Legend Panel
            GameObject legendGo = new GameObject("MapLegendUI");
            legendGo.transform.SetParent(canvasGo.transform, false);
            legendGo.AddComponent<MapLegendUI>();

            GameObject btnGo = new GameObject("ResetButton");
            btnGo.transform.SetParent(canvasGo.transform, false);
            var btnImage = btnGo.AddComponent<UnityEngine.UI.Image>();
            btnImage.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);
            var btn = btnGo.AddComponent<UnityEngine.UI.Button>();
            
            RectTransform btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1f, 1f);
            btnRect.anchorMax = new Vector2(1f, 1f);
            btnRect.pivot = new Vector2(1f, 1f);
            btnRect.anchoredPosition = new Vector2(-20f, -20f);
            btnRect.sizeDelta = new Vector2(160f, 50f);

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(btnGo.transform, false);
            var text = textGo.AddComponent<UnityEngine.UI.Text>();
            text.text = "Reset Map 🔄 (R)";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, manager.ResetAndRegenerate);

            // Save Scene
            string scenePath = "Assets/Scenes/MapTestScene.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TawanOS MapEngine] Setup Complete! Test Scene saved at: {scenePath}");
        }

        private static NodeProfileSO CreateOrGetProfile(string path, NodeType type, string title, string desc, Color color, string iconName = null)
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

            if (!string.IsNullOrEmpty(iconName))
            {
                string iconPath = $"Assets/MapEngineData/Icons/{iconName}";
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
                if (tex != null)
                {
                    Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    sprite.name = iconName;
                    AssetDatabase.AddObjectToAsset(sprite, profile);
                    profile.icon = sprite;
                    EditorUtility.SetDirty(profile);
                }
            }

            return profile;
        }
    }
}
#endif
