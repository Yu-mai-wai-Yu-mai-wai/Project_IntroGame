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

            // Logo enlarged to ~38% of the screen width (0.04 to 0.42 = 38%)
            var logo = Img("Logo", g, new Vector2(0.04f, 0.62f), new Vector2(0.42f, 0.97f), Color.white, null);
            logo.sprite = EnsureSprite(LogoPath);
            logo.preserveAspect = true;
            logo.enabled = logo.sprite != null;
            logo.raycastTarget = false;
            if (logo.sprite == null) Debug.LogWarning($"[MainMenuSetupTool] Logo not found at {LogoPath}.");
            var subtitle = Text("Subtitle", g, new Vector2(0.045f, 0.54f), new Vector2(0.40f, 0.61f), bodyFont, 30, Muted, TextAlignmentOptions.TopLeft);
            subtitle.text = "เส้นทางของหมอธรรม";

            // Menu buttons: width <= 20% (0.05 to 0.24 = 19%), height >= 44 px (0.07 * 1080 = 75.6 px)
            menu.newGameButton = MenuButton("NewGameButton", g, 0.44f, "เริ่มเกมใหม่", bodyFont, panelSprite);
            menu.continueButton = MenuButton("ContinueButton", g, 0.34f, "เล่นต่อ", bodyFont, panelSprite);
            menu.continueInfoText = Text("ContinueInfo", g, new Vector2(0.05f, 0.28f), new Vector2(0.35f, 0.33f), bodyFont, 22, Muted, TextAlignmentOptions.TopLeft);
            menu.settingsButton = MenuButton("SettingsButton", g, 0.19f, "ตั้งค่า", bodyFont, panelSprite);
            menu.quitButton = MenuButton("QuitButton", g, 0.09f, "ออกจากเกม", bodyFont, panelSprite);

            // Overwrite confirmation
            var dim = Img("ConfirmPanel", root, Vector2.zero, Vector2.one, new Color(0, 0, 0, 0.7f), null);
            menu.confirmPanel = dim.gameObject;
            var box = Img("Window", dim.transform, new Vector2(0.32f, 0.36f), new Vector2(0.68f, 0.64f), new Color(0.13f, 0.075f, 0.06f), panelSprite);
            box.gameObject.AddComponent<Outline>().effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.5f);
            var question = Text("Question", box.transform, new Vector2(0.06f, 0.42f), new Vector2(0.94f, 0.92f), bodyFont, 30, Parchment, TextAlignmentOptions.Center);
            question.text = "เริ่มเกมใหม่?\n<size=75%><color=#a89c8a>เกมที่เล่นค้างไว้จะถูกลบ</color></size>";
            menu.confirmYesButton = DialogButton("YesButton", box.transform, new Vector2(0.08f, 0.1f), new Vector2(0.46f, 0.34f), "เริ่มใหม่", new Color(0.55f, 0.15f, 0.1f), bodyFont, panelSprite);
            menu.confirmNoButton = DialogButton("NoButton", box.transform, new Vector2(0.54f, 0.1f), new Vector2(0.92f, 0.34f), "ยกเลิก", new Color(0.3f, 0.25f, 0.22f), bodyFont, panelSprite);

            // Mode selection panel (Phase A10)
            var modeDim = Img("ModePanel", root, Vector2.zero, Vector2.one, new Color(0, 0, 0, 0.7f), null);
            menu.modePanel = modeDim.gameObject;
            var modeBox = Img("Window", modeDim.transform, new Vector2(0.30f, 0.30f), new Vector2(0.70f, 0.70f), new Color(0.13f, 0.075f, 0.06f), panelSprite);
            modeBox.gameObject.AddComponent<Outline>().effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.5f);
            var modeTitle = Text("Title", modeBox.transform, new Vector2(0.06f, 0.68f), new Vector2(0.94f, 0.94f), titleFont ?? bodyFont, 34, Parchment, TextAlignmentOptions.Center);
            modeTitle.text = "เลือกโหมดการเล่น\n<size=70%><color=#a89c8a>กำหนดระยะเวลาเส้นทางหมอธรรม</color></size>";
            menu.fullModeButton = DialogButton("FullModeButton", modeBox.transform, new Vector2(0.08f, 0.38f), new Vector2(0.46f, 0.62f), "เล่นเต็ม (7 ชั้น)", new Color(0.55f, 0.15f, 0.1f), bodyFont, panelSprite);
            menu.shortModeButton = DialogButton("ShortModeButton", modeBox.transform, new Vector2(0.54f, 0.38f), new Vector2(0.92f, 0.62f), "เล่นสั้น (4 ชั้น)", new Color(0.2f, 0.35f, 0.25f), bodyFont, panelSprite);
            menu.modeCancelButton = DialogButton("CancelButton", modeBox.transform, new Vector2(0.31f, 0.10f), new Vector2(0.69f, 0.30f), "ยกเลิก", new Color(0.3f, 0.25f, 0.22f), bodyFont, panelSprite);
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
