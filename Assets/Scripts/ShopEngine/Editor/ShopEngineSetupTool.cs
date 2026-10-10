using System.Collections.Generic;
using TawanOS.CardEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.ShopEngine
{
    /// <summary>
    /// Builds the spirit-house shop scene (ศาลตายาย) and its ShopConfig asset.
    /// Prices are in incense (ธูป), the run currency.
    /// </summary>
    public static class ShopEngineSetupTool
    {
        private const string BaseFolder = "Assets/ShopEngineData";
        private const string ConfigPath = BaseFolder + "/ShopConfig.asset";
        private const string ScenePath = "Assets/Scenes/ShopScene.unity";
        private const string StarterDeckPath = "Assets/CardEngineData/Decks/StarterDeckConfig.asset";
        private const string IconFolder = "Assets/ProjectAsset/MapNavigate/";

        private static readonly Color Gold = new Color(0.93f, 0.78f, 0.45f);
        private static readonly Color Parchment = new Color(0.96f, 0.92f, 0.82f);
        private static readonly Color Muted = new Color(0.66f, 0.61f, 0.54f);
        private static readonly Color Crimson = new Color(0.55f, 0.15f, 0.1f);

        [MenuItem("Tools/TawanOS/Shop Engine/Setup Shop Scene (Spirit House)")]
        private static void SetupShopScene_Menu()
        {
            if (TawanOS.EditorTools.SetupGuard.Confirm("Setup Shop Scene (Spirit House)")) SetupShopScene();
        }

        public static void SetupShopScene()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Setup in Play Mode", "Please exit Play Mode before running the Setup Tool.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder(BaseFolder)) AssetDatabase.CreateFolder("Assets", "ShopEngineData");
            var config = CreateOrGetConfig();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.035f, 0.035f);
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0, 0, -10);

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            var view = BuildShopCanvas();

            var managerGo = new GameObject("ShopManager");
            var manager = managerGo.AddComponent<ShopManager>();
            manager.config = config;
            manager.view = view;

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterSceneInBuild(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=green>[ShopEngineSetupTool] Built {ScenePath}. Tune prices and stock in {ConfigPath}.</color>");
        }

        // ------------------------------------------------------------------ Data

        /// <summary>An existing config is left untouched so tuning survives re-running the tool.</summary>
        private static ShopConfigSO CreateOrGetConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<ShopConfigSO>(ConfigPath);
            if (config != null) return config;

            config = ScriptableObject.CreateInstance<ShopConfigSO>();
            config.starterDeck = AssetDatabase.LoadAssetAtPath<DeckConfigSO>(StarterDeckPath);
            config.shrineImage = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "ShrineIcon.png");
            config.blessingIcon = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "TempleIcon.png");
            config.removalIcon = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "GraveyardIcon.png");

            config.greetingLines = new List<string>
            {
                "จุดธูปบอกกล่าวก่อนสิ แล้วค่อยเลือกของ",
                "ผู้เดินทางยามวิกาล... ธูปในมือเจ้ามีพอหรือไม่",
                "ศาลนี้คุ้มครองคนมีสัจจะ ของดีมีไว้ให้คนที่ถวายด้วยใจ",
            };
            config.thanksLines = new List<string>
            {
                "ควันธูปลอยตรง เจ้าที่รับไว้แล้ว",
                "ดี... ของชิ้นนี้จะคุ้มครองเจ้า",
                "สาธุ ขอให้เดินทางปลอดภัย",
            };
            config.notEnoughIncenseLines = new List<string>
            {
                "ธูปแค่นี้ ไม่พอให้ข้าลืมตาดูหรอก",
                "ไปหาธูปมาเพิ่มก่อนเถิด",
                "ของศักดิ์สิทธิ์ ไม่ใช่ของให้เปล่า",
            };

            var offering = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "OfferingIcon.png");
            config.amulets = new List<ShopAmulet>
            {
                new ShopAmulet { relicId = "takrut_tone", displayName = "ตะกรุดโทน", description = "แผ่นโลหะลงอาคม ป้องกันคุณไสย", icon = offering, price = 70 },
                new ShopAmulet { relicId = "pha_yan", displayName = "ผ้ายันต์แดง", description = "ผ้ายันต์ลงเลขยันต์ คุ้มกันภูตผี", icon = offering, price = 60 },
                new ShopAmulet { relicId = "phra_khrueang", displayName = "พระเครื่องเก่า", description = "พระเนื้อผงอายุนับร้อยปี เสริมบุญบารมี", icon = offering, price = 90 },
                new ShopAmulet { relicId = "mai_khru", displayName = "ไม้ครูหมอธรรม", description = "ไม้เท้าสืบทอดจากครูบาอาจารย์", icon = offering, price = 80 },
                new ShopAmulet { relicId = "nam_mon", displayName = "น้ำมนต์ในบาตร", description = "น้ำมนต์เย็นเฉียบ ชำระสิ่งอัปมงคล", icon = offering, price = 50 },
            };

            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        // ------------------------------------------------------------------ UI

        private static ShopViewUI BuildShopCanvas()
        {
            var charm = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MN-RueangLao SDF.asset")
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Charm-Bold SDF.asset");
            var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/EkkamaiVibe SDF.asset")
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Sarabun-Regular SDF.asset");
            var sarabun = bodyFont;
            var panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var canvasGo = new GameObject("ShopCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var view = canvasGo.AddComponent<ShopViewUI>();
            var root = canvasGo.transform;

            Img("Background", root, Vector2.zero, Vector2.one, new Color(0.075f, 0.045f, 0.04f), null);

            // Run status (top bar)
            var status = Rect("RunStatus", root, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -84), new Vector2(-40, -20));
            view.incenseText = Text("IncenseText", status, new Vector2(0, 0), new Vector2(0.2f, 1), charm, 46, Gold, TextAlignmentOptions.MidlineLeft);
            view.hpText = Text("HPText", status, new Vector2(0.2f, 0), new Vector2(0.38f, 1), sarabun, 30, new Color(0.95f, 0.45f, 0.4f), TextAlignmentOptions.MidlineLeft);
            view.deckText = Text("DeckText", status, new Vector2(0.38f, 0), new Vector2(0.56f, 1), sarabun, 30, Parchment, TextAlignmentOptions.MidlineLeft);

            // Shrine and guardian (left)
            var shrine = Img("ShrinePanel", root, new Vector2(0.025f, 0.1f), new Vector2(0.3f, 0.88f), new Color(0.13f, 0.075f, 0.06f), panelSprite);
            shrine.gameObject.AddComponent<Outline>().effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.45f);
            view.shrineNameText = Text("ShrineName", shrine.transform, new Vector2(0, 0.88f), new Vector2(1, 0.98f), charm, 58, Gold, TextAlignmentOptions.Center);
            view.shrineImage = Img("ShrineImage", shrine.transform, new Vector2(0.12f, 0.46f), new Vector2(0.88f, 0.87f), Color.white, null);
            view.shrineImage.raycastTarget = false;
            view.guardianNameText = Text("GuardianName", shrine.transform, new Vector2(0.06f, 0.38f), new Vector2(0.94f, 0.45f), sarabun, 30, new Color(0.55f, 0.9f, 0.85f), TextAlignmentOptions.MidlineLeft);
            view.guardianNameText.fontStyle = FontStyles.Bold;
            view.speechText = Text("Speech", shrine.transform, new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.38f), sarabun, 28, Parchment, TextAlignmentOptions.TopLeft);
            view.speechText.fontStyle = FontStyles.Italic;
            view.resultText = Text("Result", shrine.transform, new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.14f), sarabun, 26, new Color(0.6f, 0.95f, 0.55f), TextAlignmentOptions.MidlineLeft);
            view.resultText.fontStyle = FontStyles.Bold;

            // Shelves (right)
            Label("CardsLabel", root, new Vector2(0.33f, 0.84f), new Vector2(0.97f, 0.9f), charm, "การ์ดเวท");
            view.cardRow = Row("CardRow", root, new Vector2(0.33f, 0.47f), new Vector2(0.97f, 0.84f), 18);

            Label("AmuletsLabel", root, new Vector2(0.33f, 0.37f), new Vector2(0.64f, 0.43f), charm, "เครื่องราง");
            view.amuletRow = Row("AmuletRow", root, new Vector2(0.33f, 0.1f), new Vector2(0.64f, 0.37f), 14);

            Label("ServicesLabel", root, new Vector2(0.66f, 0.37f), new Vector2(0.97f, 0.43f), charm, "บริการของเจ้าที่");
            view.serviceRow = Row("ServiceRow", root, new Vector2(0.66f, 0.1f), new Vector2(0.97f, 0.37f), 14);

            view.itemTemplate = BuildItemTemplate(view.cardRow, sarabun, panelSprite);

            // Leave button
            var leave = Img("LeaveButton", root, new Vector2(0.82f, 0.018f), new Vector2(0.97f, 0.085f), Crimson, panelSprite);
            view.leaveButton = leave.gameObject.AddComponent<Button>();
            var leaveText = Text("Label", leave.transform, Vector2.zero, Vector2.one, sarabun, 32, Parchment, TextAlignmentOptions.Center);
            leaveText.text = "กราบลา";
            leaveText.fontStyle = FontStyles.Bold;

            BuildRemovalPanel(root, view, sarabun, panelSprite);
            return view;
        }

        private static ShopItemView BuildItemTemplate(Transform parent, TMP_FontAsset font, Sprite panelSprite)
        {
            var bg = Img("ItemTemplate", parent, Vector2.zero, Vector2.one, Color.white, panelSprite);
            var button = bg.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.17f, 0.1f, 0.08f, 0.96f);
            colors.highlightedColor = new Color(0.32f, 0.2f, 0.12f, 1f);
            colors.pressedColor = new Color(0.45f, 0.3f, 0.16f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.12f, 0.09f, 0.08f, 0.8f);
            button.colors = colors;

            var item = bg.gameObject.AddComponent<ShopItemView>();
            item.button = button;
            item.icon = Img("Icon", bg.transform, new Vector2(0.08f, 0.5f), new Vector2(0.92f, 0.95f), Color.white, null);
            item.icon.raycastTarget = false;

            item.titleText = Text("Title", bg.transform, new Vector2(0.05f, 0.39f), new Vector2(0.95f, 0.5f), font, 24, Parchment, TextAlignmentOptions.Center);
            item.titleText.fontStyle = FontStyles.Bold;
            AutoSize(item.titleText, 14, 24);
            item.subtitleText = Text("Subtitle", bg.transform, new Vector2(0.05f, 0.31f), new Vector2(0.95f, 0.39f), font, 17, Muted, TextAlignmentOptions.Center);
            AutoSize(item.subtitleText, 11, 17);
            item.descriptionText = Text("Description", bg.transform, new Vector2(0.07f, 0.14f), new Vector2(0.93f, 0.31f), font, 17, Parchment, TextAlignmentOptions.Top);
            AutoSize(item.descriptionText, 10, 17);

            var priceBar = Img("PriceBar", bg.transform, new Vector2(0, 0), new Vector2(1, 0.13f), new Color(0, 0, 0, 0.35f), null);
            priceBar.raycastTarget = false;
            item.priceText = Text("Price", priceBar.transform, Vector2.zero, Vector2.one, font, 24, Gold, TextAlignmentOptions.Center);
            item.priceText.fontStyle = FontStyles.Bold;

            var sold = Img("SoldOverlay", bg.transform, Vector2.zero, Vector2.one, new Color(0.04f, 0.02f, 0.02f, 0.78f), panelSprite);
            sold.raycastTarget = false;
            var soldText = Text("Label", sold.transform, Vector2.zero, Vector2.one, font, 30, Gold, TextAlignmentOptions.Center);
            soldText.text = "ถวายแล้ว";
            soldText.fontStyle = FontStyles.Bold;
            item.soldOverlay = sold.gameObject;
            sold.gameObject.SetActive(false);

            foreach (var t in bg.GetComponentsInChildren<TextMeshProUGUI>(true)) t.raycastTarget = false;
            return item;
        }

        private static void BuildRemovalPanel(Transform root, ShopViewUI view, TMP_FontAsset font, Sprite panelSprite)
        {
            var dim = Img("RemovalPanel", root, Vector2.zero, Vector2.one, new Color(0, 0, 0, 0.75f), null);
            view.removalPanel = dim.gameObject;

            var panel = Img("Window", dim.transform, new Vector2(0.18f, 0.1f), new Vector2(0.82f, 0.9f), new Color(0.13f, 0.075f, 0.06f), panelSprite);
            panel.gameObject.AddComponent<Outline>().effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.5f);

            view.removalTitleText = Text("Title", panel.transform, new Vector2(0.04f, 0.88f), new Vector2(0.96f, 0.97f), font, 32, Gold, TextAlignmentOptions.MidlineLeft);
            view.removalTitleText.fontStyle = FontStyles.Bold;

            // Scrollable grid of deck cards
            var scrollRt = Rect("Scroll", panel.transform, new Vector2(0.04f, 0.13f), new Vector2(0.96f, 0.86f), Vector2.zero, Vector2.zero);
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30f;
            var viewport = Rect("Viewport", scrollRt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(360, 72);
            grid.spacing = new Vector2(12, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperCenter;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            view.removalGrid = content;

            var entry = Img("EntryTemplate", content, Vector2.zero, Vector2.one, Color.white, panelSprite);
            var entryButton = entry.gameObject.AddComponent<Button>();
            var colors = entryButton.colors;
            colors.normalColor = new Color(0.22f, 0.13f, 0.1f, 1f);
            colors.highlightedColor = new Color(0.5f, 0.18f, 0.12f, 1f);
            colors.pressedColor = new Color(0.62f, 0.22f, 0.14f, 1f);
            colors.selectedColor = colors.normalColor;
            entryButton.colors = colors;
            var entryText = Text("Label", entry.transform, Vector2.zero, Vector2.one, font, 24, Parchment, TextAlignmentOptions.Center);
            entryText.raycastTarget = false;
            view.removalEntryTemplate = entryButton;

            var cancel = Img("CancelButton", panel.transform, new Vector2(0.38f, 0.025f), new Vector2(0.62f, 0.1f), new Color(0.3f, 0.25f, 0.22f), panelSprite);
            view.removalCancelButton = cancel.gameObject.AddComponent<Button>();
            var cancelText = Text("Label", cancel.transform, Vector2.zero, Vector2.one, font, 28, Parchment, TextAlignmentOptions.Center);
            cancelText.text = "ยกเลิก";
        }

        // ------------------------------------------------------------------ Helpers

        private static RectTransform Row(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, float spacing)
        {
            var row = Rect(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return row;
        }

        private static void Label(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, TMP_FontAsset font, string text)
        {
            var t = Text(name, parent, anchorMin, anchorMax, font, 40, Gold, TextAlignmentOptions.BottomLeft);
            t.text = text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        private static Image Img(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color, Sprite sprite)
        {
            var rt = Rect(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            var img = rt.gameObject.AddComponent<Image>();
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
            var rt = Rect(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = string.Empty;
            return t;
        }

        private static void AutoSize(TextMeshProUGUI text, float min, float max)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = min;
            text.fontSizeMax = max;
        }

        private static void RegisterSceneInBuild(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == scenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
