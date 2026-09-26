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

            // 1. Create Node Profiles (Authentic Thai Crimson Ink Stamp on Paper Board)
            Color crimsonBase = new Color(0.3529f, 0.0941f, 0.0627f, 1f);
            Color crimsonHover = new Color(0.5568f, 0.1490f, 0.0941f, 1f);
            Color crimsonVisited = new Color(0.1725f, 0.0784f, 0.0627f, 1f);

            var enemyProfile = CreateOrGetProfile(baseFolder + "/Profiles/EnemyProfile.asset", NodeType.MinorEnemy, "Forest Shadow", "Vengeful spirits haunting the sacred path.", crimsonBase, "NarrowIcon.png");
            var eliteProfile = CreateOrGetProfile(baseFolder + "/Profiles/EliteProfile.asset", NodeType.EliteEnemy, "Ancient Shrine Guardian", "A cursed guardian of the ancient ruins holding powerful relics.", crimsonBase, "EliteIcon.png");
            var restProfile = CreateOrGetProfile(baseFolder + "/Profiles/RestProfile.asset", NodeType.RestSite, "Spirit Lantern", "Consecrated sanctuary to light incense and soothe your soul.", crimsonBase, "TempleIcon.png");
            var treasureProfile = CreateOrGetProfile(baseFolder + "/Profiles/TreasureProfile.asset", NodeType.Treasure, "Sacred Offering", "Ancient treasure chest left behind by past practitioners.", crimsonBase, "OfferingIcon.png");
            var storeProfile = CreateOrGetProfile(baseFolder + "/Profiles/StoreProfile.asset", NodeType.Store, "Bodhi Tree Merchant", "An enigmatic merchant trading talismans and sacred offerings.", crimsonBase, "ShrineIcon.png");
            var bossProfile = CreateOrGetProfile(baseFolder + "/Profiles/BossProfile.asset", NodeType.Boss, "The Sovereign Spirit", "The ancient overlord reigning over the temple grounds.", crimsonBase, "GraveyardIcon.png");

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
                config.seed = 737445;
                config.totalFloors = 7;
                config.mapWidth = 3;
                config.startingNodesCount = 3;
                config.pathCount = 3;
                config.extraPaths = 1;
                config.orientation = MapOrientation.LeftToRight;
                config.floorSpacingY = 3.9f;
                config.columnSpacingX = 2.1f;
                config.depthZOffset = 0f;
                config.nodePositionJitter = 0.2f;
                config.use3DTableMode = true;
                // Low, close framing so the canopy fills the top edge (Blender concept render).
                config.cameraHeightY = 3f;
                config.cameraAnglePitch = 28f;
                config.cameraZDistance = 5.5f;
                config.player3DScale = Vector3.one;
                config.player3DRotation = new Vector3(0, 180, 0);
                config.startNodeOffset = Vector3.zero;
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
            var col3d = nodeGo.AddComponent<SphereCollider>();
            if (col3d != null) col3d.radius = 0.8f;

            GameObject bgGo = new GameObject("Background");
            bgGo.transform.SetParent(nodeGo.transform, false);
            var bgSr = bgGo.AddComponent<SpriteRenderer>();
            if (bgSr != null) bgSr.sortingOrder = 0;

            GameObject iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(nodeGo.transform, false);
            var iconSr = iconGo.AddComponent<SpriteRenderer>();
            if (iconSr != null) iconSr.sortingOrder = 1;
            
            if (nodeView != null)
            {
                nodeView.backgroundRenderer = bgSr;
                nodeView.iconRenderer = iconSr;
            }
            nodeGo.transform.rotation = Quaternion.Euler(90f, 180f, 0f);

            string nodePrefabPath = baseFolder + "/Prefabs/MapNodePrefab.prefab";
            GameObject nodePrefab = PrefabUtility.SaveAsPrefabAsset(nodeGo, nodePrefabPath);
            Object.DestroyImmediate(nodeGo);

            // 5. Create Path Prefab
            GameObject pathGo = new GameObject("PathPrefab");
            var pathView = pathGo.AddComponent<MapPathRenderer>();
            var lr = pathGo.GetComponent<LineRenderer>() ?? pathGo.AddComponent<LineRenderer>();
            if (lr != null)
            {
                lr.alignment = LineAlignment.TransformZ;
                lr.startWidth = 0.07f;
                lr.endWidth = 0.07f;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                Shader lineShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") 
                                 ?? Shader.Find("Sprites/Default") 
                                 ?? Shader.Find("Unlit/Color")
                                 ?? Shader.Find("GUI/Text Shader");
                if (lineShader != null) lr.material = new Material(lineShader);
            }
            pathGo.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            string pathPrefabPath = baseFolder + "/Prefabs/MapPathPrefab.prefab";
            GameObject pathPrefab = PrefabUtility.SaveAsPrefabAsset(pathGo, pathPrefabPath);
            Object.DestroyImmediate(pathGo);

            // 6. Create 3D Player Marker Prefab (using hands.fbx)
            GameObject markerGo = new GameObject("PlayerMarkerPrefab");
            markerGo.transform.rotation = Quaternion.LookRotation(new Vector3(-1f, 0f, 0f));
            var markerComponent = markerGo.AddComponent<PlayerMarker>();

            string handsFbxPath = "Assets/ProjectAsset/hands.fbx";
            GameObject handsModelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(handsFbxPath);
            if (handsModelAsset != null)
            {
                GameObject handsInstance = (GameObject)PrefabUtility.InstantiatePrefab(handsModelAsset, markerGo.transform);
                handsInstance.name = "Hands3DModel";
                handsInstance.transform.localPosition = Vector3.zero;
                handsInstance.transform.localRotation = Quaternion.Euler(0, 180, 0);
                handsInstance.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);

                // Assign Materials if found
                var renderers = handsInstance.GetComponentsInChildren<Renderer>();
                Material handSkinMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ProjectAsset/Mat_HandSkin.mat");
                Material glassMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ProjectAsset/Mat_Glass.mat");
                Material fingernailMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ProjectAsset/Mat_Fingernail.mat");

                foreach (var r in renderers)
                {
                    if (r != null)
                    {
                        r.enabled = true;
                        if (r.sharedMaterials != null && r.sharedMaterials.Length > 0)
                        {
                            Material[] mats = new Material[r.sharedMaterials.Length];
                            for (int m = 0; m < mats.Length; m++)
                            {
                                string matName = r.sharedMaterials[m] != null ? r.sharedMaterials[m].name.ToLower() : "";
                                if (matName.Contains("glass")) mats[m] = glassMat ?? r.sharedMaterials[m];
                                else if (matName.Contains("nail")) mats[m] = fingernailMat ?? r.sharedMaterials[m];
                                else mats[m] = handSkinMat ?? r.sharedMaterials[m];
                            }
                            r.sharedMaterials = mats;
                        }
                    }
                }

                // Add a Glass Point Light to illuminate the spirit glass and hand
                GameObject markerLightGo = new GameObject("GlassPointLight");
                markerLightGo.transform.SetParent(markerGo.transform, false);
                markerLightGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                Light markerLight = markerLightGo.AddComponent<Light>();
                markerLight.type = LightType.Point;
                markerLight.range = 8f;
                markerLight.intensity = 3.5f;
                markerLight.color = new Color(0.4f, 0.9f, 1.0f); // Mystical Cyan Glow

                if (markerComponent != null)
                {
                    markerComponent.ApplyModelLocalTransform();
                }
            }
            else
            {
                var markerSr = markerGo.AddComponent<SpriteRenderer>();
                markerSr.color = Color.magenta;
            }

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

            // Setup Top-Down Angled 3D Camera looking down at Ouija Board Table (LeftToRight)
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UnityEngine.EventSystems.Physics2DRaycaster>();
            camGo.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
            var scrollCtrl = camGo.AddComponent<MapScrollController>();
            scrollCtrl.config = config;
            scrollCtrl.minScrollX = -18.6f;
            scrollCtrl.maxScrollX = 15.5f;
            cam.orthographic = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.03f, 0.03f, 1f);
            cam.fieldOfView = 55;
            camGo.transform.position = new Vector3(15.5f, 3f, 5.5f);
            camGo.transform.rotation = Quaternion.Euler(30f, 180f, 0f);

            // Setup Directional Light (Eerie Moonlight / Twilight Canopy looking from camera side)
            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.85f;
            light.color = new Color(0.95f, 0.88f, 0.78f, 1f); // Warm pale ivory
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;
            lightGo.transform.rotation = Quaternion.Euler(45f, 155f, 0f);

            // Setup Ritual Table Candle Light
            GameObject pointLightGo = new GameObject("Table Light");
            Light pLight = pointLightGo.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.range = 18f;
            pLight.intensity = 2.2f;
            pLight.color = new Color(1.0f, 0.85f, 0.62f, 1f); // Warm ritual candle amber
            pLight.shadows = LightShadows.Soft;
            pLight.shadowStrength = 0.8f;
            pointLightGo.transform.position = new Vector3(14.0f, 4.5f, 1.5f);

            // Setup 3D Environment (MapNavigate.fbx - PaperFloor + Sacred Forest Trees)
            string mapNavigatePath = "Assets/ProjectAsset/MapNavigate/MapNavigate.fbx";
            GameObject mapNavigateAsset = AssetDatabase.LoadAssetAtPath<GameObject>(mapNavigatePath);
            if (mapNavigateAsset != null)
            {
                GameObject envInstance = (GameObject)PrefabUtility.InstantiatePrefab(mapNavigateAsset);
                envInstance.name = "MapNavigateEnvironment";
                envInstance.transform.position = Vector3.zero;
                envInstance.transform.rotation = Quaternion.identity;
                envInstance.transform.localScale = Vector3.one;

                // Deactivate static placeholder String.* so procedural MapPathRenderer takes over,
                // but keep 3D stone pedestals Node.* active for hybrid placement
                foreach (Transform child in envInstance.GetComponentsInChildren<Transform>(true))
                {
                    if (child == envInstance.transform) continue;
                    string cName = child.name;
                    if (cName.StartsWith("String"))
                    {
                        child.gameObject.SetActive(false);
                    }
                    else if (cName.StartsWith("Node"))
                    {
                        child.gameObject.SetActive(true);
                    }
                }

                // Add TreeBillboardController for cylindrical billboarding of background trees
                var billboardCtrl = envInstance.AddComponent<TreeBillboardController>();
                billboardCtrl.targetCamera = cam;
                billboardCtrl.FindTrees();
                EditorUtility.SetDirty(billboardCtrl);
                EditorUtility.SetDirty(envInstance);
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
                profile.hoverColor = new Color(color.r * 1.5f, color.g * 1.5f, color.b * 1.5f, 1f);
                profile.visitedColor = new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 1f);
                AssetDatabase.CreateAsset(profile, path);
            }
            else
            {
                profile.title = title;
                profile.description = desc;
                profile.baseColor = color;
                profile.hoverColor = new Color(color.r * 1.5f, color.g * 1.5f, color.b * 1.5f, 1f);
                profile.visitedColor = new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 1f);
                EditorUtility.SetDirty(profile);
            }

            if (!string.IsNullOrEmpty(iconName))
            {
                string mapNavigateIconPath = $"Assets/ProjectAsset/MapNavigate/{iconName}";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(mapNavigateIconPath);
                if (sprite != null)
                {
                    profile.icon = sprite;
                    EditorUtility.SetDirty(profile);
                }
                else
                {
                    string fallbackIconPath = $"Assets/MapEngineData/Icons/{iconName}";
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(fallbackIconPath);
                    if (tex != null)
                    {
                        Sprite fallbackSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        fallbackSprite.name = iconName;
                        AssetDatabase.AddObjectToAsset(fallbackSprite, profile);
                        profile.icon = fallbackSprite;
                        EditorUtility.SetDirty(profile);
                    }
                }
            }

            return profile;
        }
    }
}
#endif
