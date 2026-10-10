using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.GameFlow
{
    /// <summary>Builds the title scene (New Game / Continue / Quit) and makes it the first scene in the build.</summary>
    public static class MainMenuSetupTool
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        // New Game asks this before it wipes the saved run. There is no mode choice: every run is the full 7-floor map,
        // because the 4-floor map was too short and too easy, passing too few places to make a run fun.
        private const string ConfirmQuestion = "เริ่มเกมใหม่จริงหรือ?\n<size=75%><color=#a89c8a>ถ้าเริ่มใหม่ เซฟล่าสุดจะหายไป</color></size>";
        private const string ConfirmYesLabel = "ยืนยัน";
        private const string TablePath = "Assets/ProjectAsset/CombatDemo/Table.fbx";
        private const string VolumeProfilePath = "Assets/ProjectAsset/CombatDemo/CombatDressingVolume.asset";
        private const string LogoPath = "Assets/Art/Logo/KhwanLogo.png";

        /// <summary>Makes sure the PNG imports as a Sprite (a fresh copy defaults to Texture) and loads it.</summary>
        private static Sprite EnsureSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static readonly Color Gold = new Color(0.93f, 0.78f, 0.45f);
        private static readonly Color Parchment = new Color(0.96f, 0.92f, 0.82f);
        private static readonly Color Muted = new Color(0.66f, 0.61f, 0.54f);

        [MenuItem("Tools/TawanOS/Main Menu/Setup Main Menu Scene")]
        private static void SetupMainMenu_Menu()
        {
            if (TawanOS.EditorTools.SetupGuard.Confirm("Setup Main Menu Scene")) SetupMainMenu();
        }

        public static void SetupMainMenu()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Setup in Play Mode", "Please exit Play Mode before running the Setup Tool.", "OK");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. 3D Perspective Camera framed to show Scarecrow on the right and UI on the left
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.015f, 0.015f);
            camGo.tag = "MainCamera";
            cam.orthographic = false;
            cam.fieldOfView = 48f;
            cam.nearClipPlane = 0.05f;
            camGo.transform.SetPositionAndRotation(new Vector3(-1.8f, 4.2f, -9.2f), Quaternion.Euler(20f, 10f, 0f));
            var uac = cam.GetUniversalAdditionalCameraData();
            if (uac != null) uac.renderPostProcessing = true;

            // 2. 3D Stage with Scarecrow, ritual cords, red flickering light, and fog
            var stageGo = new GameObject("MainMenuStage");
            var tableFbx = AssetDatabase.LoadAssetAtPath<GameObject>(TablePath);
            MainMenuStage.BuildStage(stageGo, tableFbx);

            // 3. Post-Processing Volume for atmosphere
            var volGo = new GameObject("MainMenuVolume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile != null) vol.sharedProfile = profile;

            // 4. UI EventSystem
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // 5. Canvas and horror layout
            BuildCanvas();

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterAsFirstScene(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[MainMenuSetupTool] Built {ScenePath} (first scene in Build Settings).</color>");
        }

        private static void BuildCanvas()
        {
            var titleFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MN-RueangLao SDF.asset")
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Charm-Bold SDF.asset");
            var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/EkkamaiVibe SDF.asset")
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Sarabun-Regular SDF.asset");
            var panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var canvasGo = new GameObject("MainMenuCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var menu = canvasGo.AddComponent<MainMenuUI>();
            var root = canvasGo.transform;

            // No solid background image covering the screen so 3D scene is visible

            var group = Rect("Menu", root, Vector2.zero, Vector2.one).gameObject.AddComponent<CanvasGroup>();
            menu.menuGroup = group;
            var g = group.transform;

            // Logo box 38% wide x 43% tall (aspect kept); the subtitle shares its x range and is centred under it
            var logo = Img("Logo", g, new Vector2(0.04f, 0.555f), new Vector2(0.42f, 0.985f), Color.white, null);
            logo.sprite = EnsureSprite(LogoPath);
            logo.preserveAspect = true;
            logo.enabled = logo.sprite != null;
            logo.raycastTarget = false;
            if (logo.sprite == null) Debug.LogWarning($"[MainMenuSetupTool] Logo not found at {LogoPath}.");
            var subtitle = Text("Subtitle", g, new Vector2(0.04f, 0.515f), new Vector2(0.42f, 0.555f), bodyFont, 30, Muted, TextAlignmentOptions.Center);
            subtitle.text = "เส้นทางของหมอธรรม";

            // Menu buttons: width <= 20% (0.05 to 0.24 = 19%), height >= 44 px (0.07 * 1080 = 75.6 px)
            menu.newGameButton = MenuButton("NewGameButton", g, 0.44f, "เริ่มเกมใหม่", bodyFont, panelSprite);
            menu.continueButton = MenuButton("ContinueButton", g, 0.34f, "เล่นต่อ", bodyFont, panelSprite);
            menu.continueInfoText = Text("ContinueInfo", g, new Vector2(0.05f, 0.28f), new Vector2(0.35f, 0.33f), bodyFont, 22, Muted, TextAlignmentOptions.TopLeft);
            menu.settingsButton = MenuButton("SettingsButton", g, 0.19f, "ตั้งค่า", bodyFont, panelSprite);
            menu.quitButton = MenuButton("QuitButton", g, 0.09f, "ออกจากเกม", bodyFont, panelSprite);
            AddCollectionButton(menu, bodyFont, panelSprite);

            // Overwrite confirmation
            var dim = Img("ConfirmPanel", root, Vector2.zero, Vector2.one, new Color(0, 0, 0, 0.7f), null);
            menu.confirmPanel = dim.gameObject;
            var box = Img("Window", dim.transform, new Vector2(0.32f, 0.36f), new Vector2(0.68f, 0.64f), new Color(0.13f, 0.075f, 0.06f), panelSprite);
            box.gameObject.AddComponent<Outline>().effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.5f);
            var question = Text("Question", box.transform, new Vector2(0.06f, 0.42f), new Vector2(0.94f, 0.92f), bodyFont, 30, Parchment, TextAlignmentOptions.Center);
            question.text = ConfirmQuestion;
            menu.confirmYesButton = DialogButton("YesButton", box.transform, new Vector2(0.08f, 0.1f), new Vector2(0.46f, 0.34f), ConfirmYesLabel, new Color(0.55f, 0.15f, 0.1f), bodyFont, panelSprite);
            menu.confirmNoButton = DialogButton("NoButton", box.transform, new Vector2(0.54f, 0.1f), new Vector2(0.92f, 0.34f), "ยกเลิก", new Color(0.3f, 0.25f, 0.22f), bodyFont, panelSprite);
        }

        /// <summary>
        /// Takes the mode choice (full / short map) out of the MainMenu scene that is already built, without rebuilding
        /// the rest of it: removes the ModePanel and words the New Game confirmation as "start again? the last save is
        /// lost" with ยืนยัน / ยกเลิก. Safe to run again.
        /// </summary>
        [MenuItem("Tools/TawanOS/Main Menu/Remove Mode Selection")]
        private static void RemoveModeSelectionMenu()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Setup in Play Mode", "Please exit Play Mode before running the Setup Tool.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RemoveModeSelection();
        }

        /// <summary>-executeMethod TawanOS.GameFlow.MainMenuSetupTool.RemoveModeSelectionBatch</summary>
        public static void RemoveModeSelectionBatch()
        {
            EditorApplication.Exit(RemoveModeSelection() ? 0 : 1);
        }

        public static bool RemoveModeSelection()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var menu = Object.FindFirstObjectByType<MainMenuUI>();
            if (menu == null || menu.confirmPanel == null || menu.confirmYesButton == null)
            {
                Debug.LogError($"[MainMenuSetupTool] {ScenePath} has no MainMenuUI with a confirm panel - run Setup Main Menu Scene first.");
                return false;
            }

            int removed = 0;
            foreach (var t in menu.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == "ModePanel")
                {
                    Object.DestroyImmediate(t.gameObject);
                    removed++;
                }
            }

            var question = menu.confirmPanel.transform.Find("Window/Question");
            if (question != null && question.TryGetComponent<TextMeshProUGUI>(out var questionText)) questionText.text = ConfirmQuestion;
            var yesLabel = menu.confirmYesButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (yesLabel != null) yesLabel.text = ConfirmYesLabel;

            EditorUtility.SetDirty(menu);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"<color=green>[MainMenuSetupTool] Mode selection removed from {ScenePath} (ModePanel x{removed}); New Game asks ยืนยัน / ยกเลิก.</color>");
            return true;
        }

        /// <summary>
        /// Adds the Settings button to the MainMenu scene that is already built, without rebuilding the rest of it
        /// (other setup tools rebuild their whole scene). Does nothing when the button is already there.
        /// </summary>
        [MenuItem("Tools/TawanOS/Main Menu/Add Settings Button")]
        public static void AddSettingsButtonToScene()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Setup in Play Mode", "Please exit Play Mode before running the Setup Tool.", "OK");
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var menu = Object.FindFirstObjectByType<MainMenuUI>();
            if (menu == null || menu.quitButton == null)
            {
                Debug.LogError($"[MainMenuSetupTool] {ScenePath} has no MainMenuUI with a quit button - run Setup Main Menu Scene first.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            if (menu.settingsButton != null)
            {
                Debug.Log("[MainMenuSetupTool] Settings button already exists.");
                return;
            }

            var sarabun = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Sarabun-Regular SDF.asset");
            var panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            AddSettingsButton(menu, sarabun, panelSprite);

            EditorUtility.SetDirty(menu);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"<color=green>[MainMenuSetupTool] Added the Settings button to {ScenePath}.</color>");
        }

        /// <summary>
        /// Adds the สมุดการ์ด (card collection) button to the MainMenu scene that is already built, without
        /// rebuilding the rest of it. Does nothing when the button is already there.
        /// </summary>
        [MenuItem("Tools/TawanOS/Main Menu/Add Card Collection Button")]
        public static void AddCollectionButtonToScene()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Setup in Play Mode", "Please exit Play Mode before running the Setup Tool.", "OK");
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var menu = Object.FindFirstObjectByType<MainMenuUI>();
            if (menu == null || menu.settingsButton == null || menu.quitButton == null)
            {
                Debug.LogError($"[MainMenuSetupTool] {ScenePath} has no MainMenuUI with Settings and Quit buttons - run Setup Main Menu Scene first.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            if (menu.collectionButton != null)
            {
                Debug.Log("[MainMenuSetupTool] Card collection button already exists.");
                return;
            }

            // Same font and sprite as the buttons already there
            var settingsLabel = menu.settingsButton.GetComponentInChildren<TextMeshProUGUI>(true);
            var font = settingsLabel != null ? settingsLabel.font : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Sarabun-Regular SDF.asset");
            var panelSprite = menu.settingsButton.GetComponent<Image>().sprite;
            AddCollectionButton(menu, font, panelSprite);

            EditorUtility.SetDirty(menu);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"<color=green>[MainMenuSetupTool] Added the card collection button to {ScenePath}.</color>");
        }

        // Five rows: the column is laid out again, 0.09 apart (the save line under Continue gets a little more),
        // with the collection between Continue and Settings
        private static void AddCollectionButton(MainMenuUI menu, TMP_FontAsset font, Sprite sprite)
        {
            var settings = (RectTransform)menu.settingsButton.transform;
            var button = MenuButton("CollectionButton", settings.parent, 0f, "ตำราไสยเวท", font, sprite);
            button.transform.SetSiblingIndex(settings.GetSiblingIndex());
            menu.collectionButton = button;

            SetRow(menu.newGameButton, 0.48f);
            SetRow(menu.continueButton, 0.38f);
            if (menu.continueInfoText != null)
            {
                var info = menu.continueInfoText.rectTransform;
                info.anchorMin = new Vector2(info.anchorMin.x, 0.325f);
                info.anchorMax = new Vector2(info.anchorMax.x, 0.375f);
            }
            SetRow(menu.collectionButton, 0.24f);
            SetRow(menu.settingsButton, 0.15f);
            SetRow(menu.quitButton, 0.06f);
        }

        // Moves a button to row y and keeps its height
        private static void SetRow(Button button, float y)
        {
            if (button == null) return;
            var rt = (RectTransform)button.transform;
            float h = rt.anchorMax.y - rt.anchorMin.y;
            rt.anchorMin = new Vector2(rt.anchorMin.x, y);
            rt.anchorMax = new Vector2(rt.anchorMax.x, y + h);
        }

        // Takes the quit button's slot and moves Quit one row down
        private static void AddSettingsButton(MainMenuUI menu, TMP_FontAsset font, Sprite sprite)
        {
            var quit = (RectTransform)menu.quitButton.transform;
            var button = MenuButton("SettingsButton", quit.parent, quit.anchorMin.y, "ตั้งค่า", font, sprite);
            button.transform.SetSiblingIndex(quit.GetSiblingIndex());
            quit.anchorMin -= new Vector2(0f, 0.12f);
            quit.anchorMax -= new Vector2(0f, 0.12f);
            menu.settingsButton = button;
        }

        private static Button MenuButton(string name, Transform parent, float y, string label, TMP_FontAsset font, Sprite sprite)
        {
            var img = Img(name, parent, new Vector2(0.05f, y), new Vector2(0.24f, y + 0.07f), Color.white, sprite);
            var button = img.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.2f, 0.12f, 0.09f, 0.95f);
            colors.highlightedColor = new Color(0.5f, 0.2f, 0.12f, 1f);
            colors.pressedColor = new Color(0.62f, 0.25f, 0.14f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.12f, 0.09f, 0.08f, 0.6f);
            button.colors = colors;
            var text = Text("Label", img.transform, Vector2.zero, Vector2.one, font, 32, Parchment, TextAlignmentOptions.MidlineLeft);
            text.margin = new Vector4(20, 0, 0, 0);
            text.text = label;
            text.fontStyle = FontStyles.Bold;
            text.raycastTarget = false;
            return button;
        }

        private static Button DialogButton(string name, Transform parent, Vector2 min, Vector2 max, string label, Color color, TMP_FontAsset font, Sprite sprite)
        {
            var img = Img(name, parent, min, max, color, sprite);
            var button = img.gameObject.AddComponent<Button>();
            var text = Text("Label", img.transform, Vector2.zero, Vector2.one, font, 30, Parchment, TextAlignmentOptions.Center);
            text.text = label;
            text.raycastTarget = false;
            return button;
        }

        private static void RegisterAsFirstScene(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == scenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static Image Img(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color, Sprite sprite)
        {
            var img = Rect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<Image>();
            img.color = color;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
        {
            var t = Rect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = string.Empty;
            return t;
        }
    }
}
