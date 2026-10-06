using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.GameFlow
{
    /// <summary>Builds the title scene (New Game / Continue / Quit) and makes it the first scene in the build.</summary>
    public static class MainMenuSetupTool
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private const string ShrineIconPath = "Assets/ProjectAsset/MapNavigate/ShrineIcon.png";

        private static readonly Color Gold = new Color(0.93f, 0.78f, 0.45f);
        private static readonly Color Parchment = new Color(0.96f, 0.92f, 0.82f);
        private static readonly Color Muted = new Color(0.66f, 0.61f, 0.54f);

        [MenuItem("Tools/TawanOS/Main Menu/Setup Main Menu Scene")]
        public static void SetupMainMenu()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Setup in Play Mode", "Please exit Play Mode before running the Setup Tool.", "OK");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.025f, 0.03f);
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0, 0, -10);

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            BuildCanvas();

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterAsFirstScene(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[MainMenuSetupTool] Built {ScenePath} (first scene in Build Settings).</color>");
        }

        private static void BuildCanvas()
        {
            var charm = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Charm-Bold SDF.asset");
            var sarabun = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Sarabun-Regular SDF.asset");
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

            Img("Background", root, Vector2.zero, Vector2.one, new Color(0.06f, 0.035f, 0.04f), null);
            var shrine = Img("ShrineArt", root, new Vector2(0.52f, 0.08f), new Vector2(0.96f, 0.92f), new Color(1f, 1f, 1f, 0.18f), null);
            shrine.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShrineIconPath);
            shrine.preserveAspect = true;
            shrine.enabled = shrine.sprite != null;
            shrine.raycastTarget = false;

            var group = Rect("Menu", root, Vector2.zero, Vector2.one).gameObject.AddComponent<CanvasGroup>();
            menu.menuGroup = group;
            var g = group.transform;

            menu.titleText = Text("Title", g, new Vector2(0.07f, 0.66f), new Vector2(0.6f, 0.88f), charm, 120, Gold, TextAlignmentOptions.BottomLeft);
            var subtitle = Text("Subtitle", g, new Vector2(0.075f, 0.6f), new Vector2(0.6f, 0.66f), sarabun, 30, Muted, TextAlignmentOptions.TopLeft);
            subtitle.text = "เส้นทางของหมอธรรม";

            menu.newGameButton = MenuButton("NewGameButton", g, 0.46f, "เริ่มเกมใหม่", sarabun, panelSprite);
            menu.continueButton = MenuButton("ContinueButton", g, 0.34f, "เล่นต่อ", sarabun, panelSprite);
            menu.continueInfoText = Text("ContinueInfo", g, new Vector2(0.075f, 0.285f), new Vector2(0.5f, 0.335f), sarabun, 22, Muted, TextAlignmentOptions.TopLeft);
            menu.quitButton = MenuButton("QuitButton", g, 0.16f, "ออกจากเกม", sarabun, panelSprite);

            // Overwrite confirmation
            var dim = Img("ConfirmPanel", root, Vector2.zero, Vector2.one, new Color(0, 0, 0, 0.7f), null);
            menu.confirmPanel = dim.gameObject;
            var box = Img("Window", dim.transform, new Vector2(0.32f, 0.36f), new Vector2(0.68f, 0.64f), new Color(0.13f, 0.075f, 0.06f), panelSprite);
            box.gameObject.AddComponent<Outline>().effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.5f);
            var question = Text("Question", box.transform, new Vector2(0.06f, 0.42f), new Vector2(0.94f, 0.92f), sarabun, 30, Parchment, TextAlignmentOptions.Center);
            question.text = "เริ่มเกมใหม่?\n<size=75%><color=#a89c8a>เกมที่เล่นค้างไว้จะถูกลบ</color></size>";
            menu.confirmYesButton = DialogButton("YesButton", box.transform, new Vector2(0.08f, 0.1f), new Vector2(0.46f, 0.34f), "เริ่มใหม่", new Color(0.55f, 0.15f, 0.1f), sarabun, panelSprite);
            menu.confirmNoButton = DialogButton("NoButton", box.transform, new Vector2(0.54f, 0.1f), new Vector2(0.92f, 0.34f), "ยกเลิก", new Color(0.3f, 0.25f, 0.22f), sarabun, panelSprite);
        }

        private static Button MenuButton(string name, Transform parent, float y, string label, TMP_FontAsset font, Sprite sprite)
        {
            var img = Img(name, parent, new Vector2(0.07f, y), new Vector2(0.32f, y + 0.09f), Color.white, sprite);
            var button = img.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.2f, 0.12f, 0.09f, 0.95f);
            colors.highlightedColor = new Color(0.5f, 0.2f, 0.12f, 1f);
            colors.pressedColor = new Color(0.62f, 0.25f, 0.14f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.12f, 0.09f, 0.08f, 0.6f);
            button.colors = colors;
            var text = Text("Label", img.transform, Vector2.zero, Vector2.one, font, 40, Parchment, TextAlignmentOptions.MidlineLeft);
            text.margin = new Vector4(32, 0, 0, 0);
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
