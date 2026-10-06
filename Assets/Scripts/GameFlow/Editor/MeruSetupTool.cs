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
    /// <summary>Builds the เมรุ scene (burn or upgrade one card) and adds it to Build Settings.</summary>
    public static class MeruSetupTool
    {
        private const string ScenePath = "Assets/Scenes/MeruScene.unity";
        private const string StarterDeckPath = "Assets/CardEngineData/Decks/StarterDeckConfig.asset";

        private static readonly Color Gold = new Color(0.93f, 0.78f, 0.45f);
        private static readonly Color Parchment = new Color(0.96f, 0.92f, 0.82f);
        private static readonly Color Muted = new Color(0.66f, 0.61f, 0.54f);
        private static readonly Color Ember = new Color(0.55f, 0.16f, 0.08f);
        private static readonly Color ButtonBrown = new Color(0.3f, 0.2f, 0.16f);

        private const string Story =
            "ท้ายวัดร้าง เมรุเก่ายังมีไฟลุกโชนอยู่ทั้งที่ไม่มีใครดูแล กลิ่นควันธูปปนเถ้ากระดูกลอยอบอวล\n" +
            "สัปเหร่อชราก้มหน้านั่งเฝ้าไฟ \"ไฟนี้เผาได้ทั้งสิ่งที่ไม่ต้องการ... และชำระสิ่งที่ยังไม่บริสุทธิ์\n" +
            "แต่คืนนี้ ไฟจะรับของจากเจ้าได้เพียงอย่างเดียว\"";

        [MenuItem("Tools/TawanOS/Meru/Setup Meru Scene")]
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

        private static MeruViewUI BuildCanvas()
        {
            var charm = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MN-RueangLao SDF.asset")
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Charm-Bold SDF.asset");
            var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/EkkamaiVibe SDF.asset")
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Sarabun-Regular SDF.asset");
            var sarabun = bodyFont;
            var panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var canvasGo = new GameObject("MeruCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var view = canvasGo.AddComponent<MeruViewUI>();
            var root = canvasGo.transform;

            R.Img("Background", root, Vector2.zero, Vector2.one, new Color(0.07f, 0.04f, 0.04f), null);
            // Ember glow at the bottom: the pyre
            R.Img("PyreGlow", root, new Vector2(0f, 0f), new Vector2(1f, 0.35f), new Color(Ember.r, Ember.g, Ember.b, 0.35f), null);

            view.titleText = R.Text("Title", root, new Vector2(0.1f, 0.86f), new Vector2(0.9f, 0.97f), charm, 84, Gold, TextAlignmentOptions.Center);
            view.titleText.text = "เมรุ";
            view.deckText = R.Text("DeckStatus", root, new Vector2(0.7f, 0.02f), new Vector2(0.98f, 0.08f), sarabun, 26, Muted, TextAlignmentOptions.MidlineRight);

            view.storyText = R.Text("Story", root, new Vector2(0.15f, 0.6f), new Vector2(0.85f, 0.84f), sarabun, 32, Parchment, TextAlignmentOptions.Center);
            view.storyText.text = Story;
            view.storyText.textWrappingMode = TextWrappingModes.Normal;
            R.AutoSize(view.storyText, 18, 32);

            BuildChoicePanel(view, root, charm, sarabun, panelSprite);
            BuildPickerPanel(view, root, charm, sarabun, panelSprite);
            BuildResultPanel(view, root, charm, sarabun, panelSprite);
            return view;
        }

        private static void BuildChoicePanel(MeruViewUI view, Transform root, TMP_FontAsset charm, TMP_FontAsset sarabun, Sprite panelSprite)
        {
            var panel = R.Rect("ChoicePanel", root, new Vector2(0.2f, 0.12f), new Vector2(0.8f, 0.56f));
            view.choicePanel = panel.gameObject;

            view.burnButton = BigButton("BurnButton", panel, new Vector2(0.02f, 0.3f), new Vector2(0.48f, 1f), charm, sarabun, panelSprite,
                "เผาการ์ดทิ้ง", "นำการ์ด 1 ใบออกจากสำรับไปตลอดกาล", out _);
            view.upgradeButton = BigButton("UpgradeButton", panel, new Vector2(0.52f, 0.3f), new Vector2(0.98f, 1f), charm, sarabun, panelSprite,
                "ชำระการ์ด", "", out view.upgradeHintText);

            view.leaveButton = SmallButton("LeaveButton", panel, new Vector2(0.38f, 0f), new Vector2(0.62f, 0.18f), sarabun, panelSprite, "จากไปเฉย ๆ");
        }

        private static void BuildPickerPanel(MeruViewUI view, Transform root, TMP_FontAsset charm, TMP_FontAsset sarabun, Sprite panelSprite)
        {
            var panel = R.Rect("PickerPanel", root, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.85f));
            view.pickerPanel = panel.gameObject;

            view.pickerTitleText = R.Text("PickerTitle", panel, new Vector2(0f, 0.9f), new Vector2(1f, 1f), sarabun, 36, Parchment, TextAlignmentOptions.Center);
            R.AutoSize(view.pickerTitleText, 20, 36);

            // Scrollable grid of the run deck
            var scrollBg = R.Img("DeckScroll", panel, new Vector2(0f, 0.12f), new Vector2(1f, 0.89f), new Color(0f, 0f, 0f, 0.35f), panelSprite);
            var scroll = scrollBg.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            var viewport = R.Rect("Viewport", scrollBg.transform, Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = R.Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f));
            content.pivot = new Vector2(0.5f, 1f);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(210f, 210f * 1312f / 939f);
            grid.spacing = new Vector2(24f, 24f);
            grid.padding = new RectOffset(24, 24, 24, 24);
            grid.childAlignment = TextAnchor.UpperCenter;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            view.cardGrid = content;
            view.cardTemplate = R.BuildCardTemplate(content, sarabun, charm, panelSprite);

            view.backButton = SmallButton("BackButton", panel, new Vector2(0.2f, 0f), new Vector2(0.4f, 0.09f), sarabun, panelSprite, "ย้อนกลับ");
            view.confirmButton = SmallButton("ConfirmButton", panel, new Vector2(0.6f, 0f), new Vector2(0.8f, 0.09f), sarabun, panelSprite, "ยืนยัน");
            view.confirmButton.GetComponent<Image>().color = Ember;
            view.confirmLabel = view.confirmButton.GetComponentInChildren<TextMeshProUGUI>();
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
        }

        private static Button BigButton(string name, Transform parent, Vector2 min, Vector2 max, TMP_FontAsset titleFont, TMP_FontAsset font,
            Sprite panelSprite, string title, string hint, out TextMeshProUGUI hintText)
        {
            var bg = R.Img(name, parent, min, max, ButtonBrown, panelSprite);
            var button = bg.gameObject.AddComponent<Button>();
            var t = R.Text("Title", bg.transform, new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.9f), titleFont, 56, Gold, TextAlignmentOptions.Center);
            t.text = title;
            hintText = R.Text("Hint", bg.transform, new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.45f), font, 26, Parchment, TextAlignmentOptions.Center);
            hintText.text = hint;
            hintText.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = hintText.raycastTarget = false;
            return button;
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
