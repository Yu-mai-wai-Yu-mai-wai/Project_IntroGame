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
        [MenuItem("Tools/TawanOS/Map Engine/Clean Player UI in Scene")]
        public static void CleanPlayerUIInScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MapTestScene.unity");
            var canvas = GameObject.Find("UICanvas");
            if (canvas != null)
            {
                var resetBtn = canvas.transform.Find("ResetButton");
                if (resetBtn != null)
                {
                    Undo.DestroyObjectImmediate(resetBtn.gameObject);
                    Debug.Log("[MapEngineSetupTool] Removed ResetButton from UICanvas.");
                }
                var status = canvas.transform.Find("MapPlayerStatusUI");
                if (status == null)
                {
                    var statusGo = new GameObject("MapPlayerStatusUI");
                    statusGo.transform.SetParent(canvas.transform, false);
                    statusGo.AddComponent<MapPlayerStatusUI>();
                    Debug.Log("[MapEngineSetupTool] Added MapPlayerStatusUI to UICanvas.");
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[MapEngineSetupTool] Clean Player UI in Scene finished.");
        }

        [MenuItem("Tools/TawanOS/Map Engine/Setup Test Scene & Profiles")]
        [MenuItem("Window/TawanOS Map Engine Setup")]
        private static void SetupTestSceneAndProfiles_Menu()
        {
            if (TawanOS.EditorTools.SetupGuard.Confirm("Setup Test Scene & Profiles")) SetupTestSceneAndProfiles();
        }

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

            // The seven node types on the map (Elite is no longer rolled; วัด took its place, Game Designer 11 Oct).
            // Names and descriptions are MapLegendUI's, the same text the node pop-up shows.
            var enemyProfile = CreateOrGetProfile(baseFolder + "/Profiles/EnemyProfile.asset", NodeType.MinorEnemy, crimsonBase, "ป่า.png");
            var templeProfile = CreateOrGetProfile(baseFolder + "/Profiles/TempleProfile.asset", NodeType.Temple, crimsonBase, "วัด 1.png");
            var restProfile = CreateOrGetProfile(baseFolder + "/Profiles/RestProfile.asset", NodeType.RestSite, crimsonBase, "เมรุ 1.png");
            var treasureProfile = CreateOrGetProfile(baseFolder + "/Profiles/TreasureProfile.asset", NodeType.Treasure, crimsonBase, "กองของเซ่น.png");
            var storeProfile = CreateOrGetProfile(baseFolder + "/Profiles/StoreProfile.asset", NodeType.Store, crimsonBase, "ศาลตายาย.png");
            var bossProfile = CreateOrGetProfile(baseFolder + "/Profiles/BossProfile.asset", NodeType.Boss, crimsonBase, "BossIcon.png");
            var eventProfile = CreateOrGetProfile(baseFolder + "/Profiles/EventProfile.asset", NodeType.Event, crimsonBase, "หมอกดำ.png");

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
                config.totalFloors = 8;
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
                config.nodeProfiles.Add(templeProfile);
                config.nodeProfiles.Add(restProfile);
                config.nodeProfiles.Add(treasureProfile);
                config.nodeProfiles.Add(storeProfile);
                config.nodeProfiles.Add(bossProfile);
                config.nodeProfiles.Add(eventProfile);

                AssetDatabase.CreateAsset(config, configPath);
            }
            else
            {
                if (config.nodeProfiles == null) config.nodeProfiles = new List<NodeProfileSO>();
                config.nodeProfiles.RemoveAll(p => p == null);
                if (enemyProfile != null && !config.nodeProfiles.Contains(enemyProfile)) config.nodeProfiles.Add(enemyProfile);
                config.nodeProfiles.RemoveAll(p => p.type == NodeType.EliteEnemy);
                if (templeProfile != null && !config.nodeProfiles.Contains(templeProfile)) config.nodeProfiles.Add(templeProfile);
                if (restProfile != null && !config.nodeProfiles.Contains(restProfile)) config.nodeProfiles.Add(restProfile);
                if (treasureProfile != null && !config.nodeProfiles.Contains(treasureProfile)) config.nodeProfiles.Add(treasureProfile);
                if (storeProfile != null && !config.nodeProfiles.Contains(storeProfile)) config.nodeProfiles.Add(storeProfile);
                if (bossProfile != null && !config.nodeProfiles.Contains(bossProfile)) config.nodeProfiles.Add(bossProfile);
                if (eventProfile != null && !config.nodeProfiles.Contains(eventProfile)) config.nodeProfiles.Add(eventProfile);
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

                // The cyan GlassPointLight was removed in the final-asset pass: the glass must not glow.
                // ponytail: this tool still builds the marker from the demo hands.fbx; the shipped prefab was rebuilt from
                // NavigateFinal/MapNavigate.fbx (Hand + GlassCup). Re-running this tool regresses the marker to the demo model.

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
            string mapNavigatePath = "Assets/ProjectAsset/NavigateFinal/MapNavigate.fbx";
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

            // Player Status UI
            GameObject statusGo = new GameObject("MapPlayerStatusUI");
            statusGo.transform.SetParent(canvasGo.transform, false);
            statusGo.AddComponent<MapPlayerStatusUI>();

            // Save Scene
            string scenePath = "Assets/Scenes/MapTestScene.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TawanOS MapEngine] Setup Complete! Test Scene saved at: {scenePath}");
        }

        private static NodeProfileSO CreateOrGetProfile(string path, NodeType type, Color color, string iconName = null)
        {
            string title = MapLegendUI.GetNodeTitle(type);
            string desc = MapLegendUI.GetNodeDescription(type);
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
                // The node icons in use (Game Designer, 15 Oct)
                string iconPath = $"Assets/Art/MapIcons/{iconName}";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                if (sprite != null)
                {
                    profile.icon = sprite;
                    EditorUtility.SetDirty(profile);
                }
                else Debug.LogWarning($"[MapEngineSetupTool] No icon at {iconPath}; {profile.name} keeps its icon.");
            }

            return profile;
        }
    }
}
#endif
