using System.Collections.Generic;
using TawanOS.CardEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using R = TawanOS.GameFlow.RewardSetupTool;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Builds the เมรุ scene (upgrade one card) and adds it to Build Settings: the screen (the upgrade panel that
    /// slides up from the bottom when the furnace door is clicked), then the เมรุ model and its stage
    /// (<see cref="MeruStageSetupTool"/>).
    /// </summary>
    public static class MeruSetupTool
    {
        private const string ScenePath = "Assets/Scenes/MeruScene.unity";
        private const string StarterDeckPath = "Assets/CardEngineData/Decks/StarterDeckConfig.asset";

        private static readonly Color Gold = new Color(0.93f, 0.78f, 0.45f);
        private static readonly Color Parchment = new Color(0.96f, 0.92f, 0.82f);
        private static readonly Color Muted = new Color(0.66f, 0.61f, 0.54f);
        private static readonly Color Ember = new Color(0.55f, 0.16f, 0.08f);
        private static readonly Color ButtonBrown = new Color(0.3f, 0.2f, 0.16f);

        [MenuItem("Tools/TawanOS/Meru/Setup Meru Scene")]
        private static void SetupMeruScene_Menu()
        {
            if (TawanOS.EditorTools.SetupGuard.Confirm("Setup Meru Scene")) SetupMeruScene();
        }

        public static void SetupMeruScene()
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
            cam.backgroundColor = new Color(0.05f, 0.03f, 0.03f);
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0, 0, -10);

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            var view = BuildCanvas();

            var managerGo = new GameObject("MeruManager");
            var manager = managerGo.AddComponent<MeruManager>();
            manager.starterDeck = AssetDatabase.LoadAssetAtPath<DeckConfigSO>(StarterDeckPath);
            manager.view = view;

            EditorSceneManager.SaveScene(scene, ScenePath);
            MeruStageSetupTool.Setup();
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=green>[MeruSetupTool] Built {ScenePath}. Set each card's upgrade in its Card Data (อัพเกรด (เมรุ)).</color>");
        }

        internal static MeruViewUI BuildCanvas()
        {
            var charm = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MN-RueangLao SDF.asset");
            var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/EkkamaiVibe SDF.asset");
            var panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var canvasGo = new GameObject("MeruCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<CanvasGroup>();
            var view = canvasGo.AddComponent<MeruViewUI>();
            var root = canvasGo.transform;

            view.upgradeColumn = BuildPanel("UpgradePanel", root, charm, bodyFont, panelSprite);
            // One hidden card the panel copies; it lives in the panel's grid
            view.cardTemplate = R.BuildCardTemplate(view.upgradeColumn.cardGrid, bodyFont, charm, panelSprite);

            view.leaveButton = SmallButton("LeaveButton", root, new Vector2(0.42f, 0.06f), new Vector2(0.58f, 0.12f), bodyFont, panelSprite, "จากไปเฉย ๆ");

            BuildResultPanel(view, root, charm, bodyFont, panelSprite);
            return view;
        }

        // The upgrade panel: dark and a little see-through with a soft top edge, the run deck as a scrolling grid six
        // cards across, a prompt, and its back and confirm buttons. MeruViewUI lays it over the middle of the screen
        // and slides it up from the bottom when the furnace door is clicked (Game Designer, 24 Oct)
        private static MeruViewUI.Column BuildPanel(string name, Transform root, TMP_FontAsset charm, TMP_FontAsset font, Sprite panelSprite)
        {
            var column = new MeruViewUI.Column();
            column.root = R.Rect(name, root, new Vector2(0.08f, 0f), new Vector2(0.92f, 0.8f));

            column.background = R.Img("Background", column.root, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.9f), null);
            column.background.raycastTarget = false;
            column.background.gameObject.AddComponent<TawanOS.UI.SoftEdgeImage>().SetSides(0f, 0f, 0.06f, 0f);

            var panel = R.Rect("Panel", column.root, Vector2.zero, Vector2.one);
            panel.offsetMin = new Vector2(24f, 0f);
            panel.offsetMax = new Vector2(-24f, 0f);
            column.panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            var panelTitle = R.Text("Title", panel, new Vector2(0f, 0.9f), new Vector2(1f, 0.98f), charm, 60, Gold, TextAlignmentOptions.Center);
            panelTitle.text = "อัพเกรดการ์ด";
            column.deckText = R.Text("DeckStatus", panel, new Vector2(0f, 0.86f), new Vector2(1f, 0.9f), font, 22, Muted, TextAlignmentOptions.Center);
            column.promptText = R.Text("Prompt", panel, new Vector2(0f, 0.81f), new Vector2(1f, 0.86f), font, 26, Parchment, TextAlignmentOptions.Center);
            R.AutoSize(column.promptText, 16, 26);

            // Scrollable grid of the run deck, six cards across
            var scrollBg = R.Img("DeckScroll", panel, new Vector2(0f, 0.13f), new Vector2(1f, 0.8f), Color.clear, null);
            var scroll = scrollBg.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            var viewport = R.Rect("Viewport", scrollBg.transform, Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = R.Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f));
            content.pivot = new Vector2(0.5f, 1f);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(180f, 180f * 1312f / 939f);
            grid.spacing = new Vector2(16f, 16f);
            grid.padding = new RectOffset(16, 16, 16, 16);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            grid.childAlignment = TextAnchor.UpperCenter;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            column.cardGrid = content;

            column.backButton = SmallButton("BackButton", panel, new Vector2(0.08f, 0.03f), new Vector2(0.44f, 0.1f), font, panelSprite, "ย้อนกลับ");
            column.confirmButton = SmallButton("ConfirmButton", panel, new Vector2(0.56f, 0.03f), new Vector2(0.92f, 0.1f), font, panelSprite, "อัพเกรด");
            column.confirmButton.GetComponent<Image>().color = Ember;
            column.confirmLabel = column.confirmButton.GetComponentInChildren<TextMeshProUGUI>();
            return column;
        }

        private static void BuildResultPanel(MeruViewUI view, Transform root, TMP_FontAsset charm, TMP_FontAsset sarabun, Sprite panelSprite)
        {
            var panel = R.Rect("ResultPanel", root, new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.85f));
            view.resultPanel = panel.gameObject;

            view.resultText = R.Text("ResultText", panel, new Vector2(0f, 0.86f), new Vector2(1f, 1f), sarabun, 36, Parchment, TextAlignmentOptions.Center);
            R.AutoSize(view.resultText, 20, 36);

            // The upgraded card, shown after the upgrade
            var holder = R.Rect("ResultCardHolder", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            holder.sizeDelta = new Vector2(320f, 320f * 1312f / 939f);
            holder.anchoredPosition = new Vector2(0f, 20f);
            view.resultCard = R.BuildCardTemplate(holder, sarabun, charm, panelSprite);
            view.resultCard.name = "ResultCard";

            view.continueButton = SmallButton("ContinueButton", panel, new Vector2(0.4f, 0f), new Vector2(0.6f, 0.1f), sarabun, panelSprite, "เดินทางต่อ");
            panel.gameObject.SetActive(false);
        }

        private static Button SmallButton(string name, Transform parent, Vector2 min, Vector2 max, TMP_FontAsset font, Sprite panelSprite, string label)
        {
            var bg = R.Img(name, parent, min, max, ButtonBrown, panelSprite);
            var button = bg.gameObject.AddComponent<Button>();
            var t = R.Text("Label", bg.transform, Vector2.zero, Vector2.one, font, 28, Parchment, TextAlignmentOptions.Center);
            t.text = label;
            t.raycastTarget = false;
            return button;
        }
    }
}
