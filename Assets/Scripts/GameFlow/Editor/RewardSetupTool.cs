using System.Collections.Generic;
using TawanOS.CardEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.GameFlow
{
    /// <summary>Builds the victory / card reward scene and its CardRewardConfig asset.</summary>
    public static class RewardSetupTool
    {
        private const string BaseFolder = "Assets/GameFlowData";
        private const string ConfigPath = BaseFolder + "/CardRewardConfig.asset";
        private const string ScenePath = "Assets/Scenes/RewardScene.unity";
        private const string StarterDeckPath = "Assets/CardEngineData/Decks/StarterDeckConfig.asset";
        private const string CardPrefabPath = "Assets/CardEngineData/Prefabs/CardCube3DPrefab.prefab";

        private static readonly Color Gold = new Color(0.93f, 0.78f, 0.45f);
        private static readonly Color Parchment = new Color(0.96f, 0.92f, 0.82f);
        private static readonly Color Muted = new Color(0.66f, 0.61f, 0.54f);

        [MenuItem("Tools/TawanOS/Rewards/Setup Reward Scene")]
        private static void SetupRewardScene_Menu()
        {
            if (TawanOS.EditorTools.SetupGuard.Confirm("Setup Reward Scene")) SetupRewardScene();
        }

        public static void SetupRewardScene()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Setup in Play Mode", "Please exit Play Mode before running the Setup Tool.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder(BaseFolder)) AssetDatabase.CreateFolder("Assets", "GameFlowData");
            var config = AssetDatabase.LoadAssetAtPath<CardRewardConfigSO>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<CardRewardConfigSO>();
                config.starterDeck = AssetDatabase.LoadAssetAtPath<DeckConfigSO>(StarterDeckPath);
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.035f, 0.04f);
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0, 0, -10);

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            var view = BuildCanvas();

            var managerGo = new GameObject("RewardManager");
            var manager = managerGo.AddComponent<RewardManager>();
            manager.config = config;
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
            Debug.Log($"<color=green>[RewardSetupTool] Built {ScenePath}. Tune rewards in {ConfigPath}.</color>");
        }

        private static RewardViewUI BuildCanvas()
        {
            var charm = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MN-RueangLao SDF.asset")
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Charm-Bold SDF.asset");
            var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/EkkamaiVibe SDF.asset")
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Sarabun-Regular SDF.asset");
            var sarabun = bodyFont;
            var panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var canvasGo = new GameObject("RewardCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var view = canvasGo.AddComponent<RewardViewUI>();
            var root = canvasGo.transform;

            Img("Background", root, Vector2.zero, Vector2.one, new Color(0.07f, 0.045f, 0.05f), null);

            view.titleText = Text("Title", root, new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.95f), charm, 84, Gold, TextAlignmentOptions.Center);
            view.incenseText = Text("Incense", root, new Vector2(0.3f, 0.74f), new Vector2(0.7f, 0.82f), charm, 52, Gold, TextAlignmentOptions.Center);
            view.promptText = Text("Prompt", root, new Vector2(0.2f, 0.68f), new Vector2(0.8f, 0.74f), sarabun, 32, Parchment, TextAlignmentOptions.Center);

            var row = Rect("CardRow", root, new Vector2(0.12f, 0.18f), new Vector2(0.88f, 0.66f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            view.cardRow = row;
            view.cardTemplate = BuildCardTemplate(row, sarabun, charm, panelSprite);

            var skip = Img("SkipButton", root, new Vector2(0.4f, 0.05f), new Vector2(0.6f, 0.12f), new Color(0.3f, 0.24f, 0.2f), panelSprite);
            view.skipButton = skip.gameObject.AddComponent<Button>();
            view.skipLabel = Text("Label", skip.transform, Vector2.zero, Vector2.one, sarabun, 28, Parchment, TextAlignmentOptions.Center);

            view.deckText = Text("DeckStatus", root, new Vector2(0.62f, 0.05f), new Vector2(0.97f, 0.12f), sarabun, 26, Muted, TextAlignmentOptions.MidlineRight);
            return view;
        }

        // The card face drawn like the card in play: frame, artwork and face text on CardFaceLayout's boxes
        internal static RewardCardView BuildCardTemplate(Transform parent, TMP_FontAsset font, TMP_FontAsset titleFont, Sprite panelSprite)
        {
            const float cardWidth = 320f;
            var root = Rect("CardTemplate", parent, Vector2.zero, Vector2.one);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = cardWidth;
            le.preferredHeight = cardWidth * 1312f / 939f; // the card design's shape
            root.gameObject.AddComponent<CanvasGroup>();

            var card = root.gameObject.AddComponent<RewardCardView>();

            // Gold glow behind the card, shown on the picked one
            var highlight = Img("PickedHighlight", root, Vector2.zero, Vector2.one, Gold, panelSprite);
            highlight.rectTransform.offsetMin = new Vector2(-10f, -10f);
            highlight.rectTransform.offsetMax = new Vector2(10f, 10f);
            highlight.raycastTarget = false;
            card.pickedHighlight = highlight.gameObject;

            card.frame = Img("Frame", root, Vector2.zero, Vector2.one, Color.white, null);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = card.frame;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.95f, 0.85f);
            colors.pressedColor = new Color(0.85f, 0.78f, 0.65f);
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            button.colors = colors;
            card.button = button;

            card.artwork = Img("Artwork", root, Vector2.zero, Vector2.one, Color.white, null);
            card.artwork.preserveAspect = true;
            card.artwork.raycastTarget = false;
            RewardCardView.PlaceOnFace(card.artwork.rectTransform, CardFaceLayout.Artwork);

            var pink = new Color(0.95f, 0.72f, 0.72f);
            card.costText = FaceText("Cost", root, titleFont, CardFaceLayout.Cost, 44, Color.white);
            card.nameText = FaceText("Name", root, titleFont, CardFaceLayout.Name, 35, Color.white);
            card.typeText = FaceText("Type", root, font, CardFaceLayout.Type, 15, pink);
            card.attackText = FaceText("Attack", root, titleFont, CardFaceLayout.Attack, 24, Color.white);
            card.khwanText = FaceText("Khwan", root, titleFont, CardFaceLayout.Khwan, 24, Color.white);
            card.descriptionText = FaceText("Description", root, font, CardFaceLayout.Description, 16, Color.white);
            card.descriptionText.textWrappingMode = TextWrappingModes.Normal;

            // Default frames come from the 3D card prefab, so a card without its own background matches play
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            var view3D = prefab != null ? prefab.GetComponent<CardView3D>() : null;
            if (view3D != null)
            {
                card.defaultWhiteFrame = view3D.defaultWhiteFrame;
                card.defaultBlackFrame = view3D.defaultBlackFrame;
            }

            foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true)) t.raycastTarget = false;
            return card;
        }

        private static TextMeshProUGUI FaceText(string name, Transform parent, TMP_FontAsset font, CardFaceLayout.Box box, float maxSize, Color color)
        {
            var t = Text(name, parent, Vector2.zero, Vector2.one, font, maxSize, color, TextAlignmentOptions.Center);
            RewardCardView.PlaceOnFace(t.rectTransform, box);
            AutoSize(t, 6, maxSize);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        internal static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
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

        internal static Image Img(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color, Sprite sprite)
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

        internal static TextMeshProUGUI Text(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
        {
            var t = Rect(name, parent, anchorMin, anchorMax).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = string.Empty;
            return t;
        }

        internal static void AutoSize(TextMeshProUGUI text, float min, float max)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = min;
            text.fontSizeMax = max;
        }
    }
}
