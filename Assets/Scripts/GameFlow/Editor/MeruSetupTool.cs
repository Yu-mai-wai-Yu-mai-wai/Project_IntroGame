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
    /// Builds the เมรุ scene (burn or upgrade one card) and adds it to Build Settings: the screen (a burn strip on
    /// the left and an upgrade strip on the right, each widening into a column of the deck when chosen), then the
    /// เมรุ model and its stage (<see cref="MeruStageSetupTool"/>).
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

        // Room kept clear for the smoky edge of a column, on its side toward the furnace (reference pixels)
        private const float SmokePixels = 110f;

        internal static MeruViewUI BuildCanvas()
        {
            var charm = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MN-RueangLao SDF.asset")
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Charm-Bold SDF.asset");
            var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/EkkamaiVibe SDF.asset")
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Sarabun-Regular SDF.asset");
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

            view.burnColumn = BuildColumn("BurnColumn", root, true, "เผาการ์ด", "นำการ์ด 1 ใบออกจากสำรับไปตลอดกาล", "เผาทิ้ง",
                view.stripWidth, charm, bodyFont, panelSprite);
            view.upgradeColumn = BuildColumn("UpgradeColumn", root, false, "อัพเกรดการ์ด", "ชำระการ์ด 1 ใบในไฟให้แกร่งขึ้น", "อัพเกรด",
                view.stripWidth, charm, bodyFont, panelSprite);
            view.smokePixels = SmokePixels;
            // One hidden card the columns copy; it lives in the burn column's grid
            view.cardTemplate = R.BuildCardTemplate(view.burnColumn.cardGrid, bodyFont, charm, panelSprite);

            view.leaveButton = SmallButton("LeaveButton", root, new Vector2(0.42f, 0.06f), new Vector2(0.58f, 0.12f), bodyFont, panelSprite, "จากไปเฉย ๆ");

            BuildResultPanel(view, root, charm, bodyFont, panelSprite);
            return view;
        }

        // A black column down one side of the screen. Before choosing it is a narrow strip showing only its name and
        // what it does, and the whole strip is a button; once chosen it is wide and shows the deck as a scrolling
        // grid, a prompt, and its back and confirm buttons. Its background is mirrored on the left so the smoky edge
        // (drawn on the quad's left side) faces the furnace; everything else keeps clear of that edge.
        private static MeruViewUI.Column BuildColumn(string name, Transform root, bool left, string title, string hint,
            string confirm, float stripWidth, TMP_FontAsset charm, TMP_FontAsset font, Sprite panelSprite)
        {
            var column = new MeruViewUI.Column();
            column.root = left
                ? R.Rect(name, root, new Vector2(0f, 0f), new Vector2(stripWidth, 1f))
                : R.Rect(name, root, new Vector2(1f - stripWidth, 0f), new Vector2(1f, 1f));

            column.background = R.Img("Background", column.root, Vector2.zero, Vector2.one, Color.black, null);
            column.background.raycastTarget = false;
            if (left) column.background.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

            // The strip: a see-through button over the whole column, with the name and a line under it
            var strip = R.Img("Strip", column.root, Vector2.zero, Vector2.one, Color.clear, null);
            Inset(strip.rectTransform, left);
            column.chooseButton = strip.gameObject.AddComponent<Button>();
            column.chooseButton.transition = Selectable.Transition.None;
            column.stripGroup = strip.gameObject.AddComponent<CanvasGroup>();
            column.stripTitle = R.Text("Title", strip.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.64f), charm, 64, Gold, TextAlignmentOptions.Center);
            column.stripTitle.text = title;
            R.AutoSize(column.stripTitle, 36, 64);
            column.stripHint = R.Text("Hint", strip.transform, new Vector2(0f, 0.36f), new Vector2(1f, 0.5f), font, 24, Parchment, TextAlignmentOptions.Top);
            column.stripHint.text = hint;
            column.stripHint.textWrappingMode = TextWrappingModes.Normal;
            column.stripTitle.raycastTarget = column.stripHint.raycastTarget = false;

            // The column once chosen
            var panel = R.Rect("Panel", column.root, Vector2.zero, Vector2.one);
            Inset(panel, left);
            column.panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            var panelTitle = R.Text("Title", panel, new Vector2(0f, 0.9f), new Vector2(1f, 0.98f), charm, 60, Gold, TextAlignmentOptions.Center);
            panelTitle.text = title;
            column.deckText = R.Text("DeckStatus", panel, new Vector2(0f, 0.86f), new Vector2(1f, 0.9f), font, 22, Muted, TextAlignmentOptions.Center);
            column.promptText = R.Text("Prompt", panel, new Vector2(0f, 0.81f), new Vector2(1f, 0.86f), font, 26, Parchment, TextAlignmentOptions.Center);
            R.AutoSize(column.promptText, 16, 26);

            // Scrollable grid of the run deck, four cards across
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
            grid.constraintCount = 4;
            grid.childAlignment = TextAnchor.UpperCenter;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            column.cardGrid = content;

            column.backButton = SmallButton("BackButton", panel, new Vector2(0.08f, 0.03f), new Vector2(0.44f, 0.1f), font, panelSprite, "ย้อนกลับ");
            column.confirmButton = SmallButton("ConfirmButton", panel, new Vector2(0.56f, 0.03f), new Vector2(0.92f, 0.1f), font, panelSprite, confirm);
            column.confirmButton.GetComponent<Image>().color = Ember;
            column.confirmLabel = column.confirmButton.GetComponentInChildren<TextMeshProUGUI>();
            return column;
        }

        // Fills the column, less a margin on its outer side and the smoky edge on its side toward the furnace
        private static void Inset(RectTransform rt, bool left)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left ? 24f : SmokePixels + 10f, 0f);
            rt.offsetMax = new Vector2(left ? -(SmokePixels + 10f) : -24f, 0f);
        }

        private static void BuildResultPanel(MeruViewUI view, Transform root, TMP_FontAsset charm, TMP_FontAsset sarabun, Sprite panelSprite)
        {
            var panel = R.Rect("ResultPanel", root, new Vector2(0.1f, 0.05f), new Vector2(0.9f, 0.85f));
            view.resultPanel = panel.gameObject;

            view.resultText = R.Text("ResultText", panel, new Vector2(0f, 0.86f), new Vector2(1f, 1f), sarabun, 36, Parchment, TextAlignmentOptions.Center);
            R.AutoSize(view.resultText, 20, 36);

            // The card shown after the action (the burnt one, or the upgraded one)
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
