using System.Collections.Generic;
using TawanOS.MapEngine;
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
    /// วัด (Game Designer, 11 Oct): a map node where Khwan goes back to full for free.
    /// Setup Temple Scene builds TempleScene and adds it to Build Settings (rebuilds the scene, so it is guarded).
    /// Add Temple Node To Map creates TempleProfile (icon วัด.png), puts it in DefaultMapConfig, and corrects the
    /// เมรุ node text, which read as if the เมรุ healed (สวดชุบขวัญ there means upgrading a card). Safe to run again.
    /// Batch: -executeMethod TawanOS.GameFlow.TempleSetupTool.SetupBatch (does both)
    /// </summary>
    public static class TempleSetupTool
    {
        private const string ScenePath = "Assets/Scenes/TempleScene.unity";
        private const string ProfilePath = "Assets/MapEngineData/Profiles/TempleProfile.asset";
        private const string RestProfilePath = "Assets/MapEngineData/Profiles/RestProfile.asset";
        private const string MapConfigPath = "Assets/MapEngineData/Profiles/DefaultMapConfig.asset";
        private const string IconPath = "Assets/Art/MapIcons/วัด 1.png";

        public const string Title = "วัด";
        public const string NodeDescription = "ลานวัดสงบกลางป่า กราบพระเพื่อฟื้นขวัญเต็มหลอด ไม่เสียธูป";
        public const string MeruDescription = "ไฟเมรุไม่เคยดับ เผาการ์ดออกจากสำรับ หรือสวดชุบขวัญให้การ์ดแกร่งขึ้น (อัปเกรดการ์ด)";

        private const string Story =
            "ลานวัดเก่ากลางป่า แสงเทียนหน้าพระประธานยังไม่ดับ\n" +
            "เสียงสวดมนต์แผ่วเบาลอยมาจากโบสถ์\n" +
            "ผีร้ายย่างกรายเข้ามาในเขตนี้ไม่ได้";

        private static readonly Color Gold = new Color(0.93f, 0.78f, 0.45f);
        private static readonly Color Parchment = new Color(0.96f, 0.92f, 0.82f);
        private static readonly Color Muted = new Color(0.66f, 0.61f, 0.54f);
        private static readonly Color Candle = new Color(0.62f, 0.42f, 0.14f);
        private static readonly Color ButtonBrown = new Color(0.3f, 0.2f, 0.16f);

        [MenuItem("Tools/TawanOS/Temple/Setup Temple Scene")]
        private static void SetupTempleScene_Menu()
        {
            if (TawanOS.EditorTools.SetupGuard.Confirm("Setup Temple Scene")) SetupTempleScene();
        }

        [MenuItem("Tools/TawanOS/Temple/Add Temple Node To Map")]
        private static void AddTempleNodeMenu()
        {
            AddTempleNode();
        }

        public static void SetupBatch()
        {
            bool ok = AddTempleNode() && SetupTempleScene();
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool AddTempleNode()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            var config = AssetDatabase.LoadAssetAtPath<MapConfigSO>(MapConfigPath);
            var rest = AssetDatabase.LoadAssetAtPath<NodeProfileSO>(RestProfilePath);
            if (icon == null || config == null || rest == null)
            {
                Debug.LogError($"[TempleSetupTool] Missing: icon {icon != null}, map config {config != null}, rest profile {rest != null}.");
                return false;
            }

            var profile = AssetDatabase.LoadAssetAtPath<NodeProfileSO>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<NodeProfileSO>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            profile.type = NodeType.Temple;
            profile.icon = icon;
            // Same colours as every other node
            profile.baseColor = rest.baseColor;
            profile.hoverColor = rest.hoverColor;
            profile.visitedColor = rest.visitedColor;
            profile.title = Title;
            profile.description = NodeDescription;
            EditorUtility.SetDirty(profile);

            if (!config.nodeProfiles.Contains(profile))
            {
                config.nodeProfiles.Add(profile);
                EditorUtility.SetDirty(config);
            }

            rest.description = MeruDescription;
            EditorUtility.SetDirty(rest);

            AssetDatabase.SaveAssets();
            Debug.Log($"[TempleSetupTool] TempleProfile ready, {config.nodeProfiles.Count} node profiles in {MapConfigPath}.");
            return true;
        }

        public static bool SetupTempleScene()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Setup in Play Mode", "Please exit Play Mode before running the Setup Tool.", "OK");
                return false;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.03f);
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0, 0, -10);

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            var view = BuildCanvas();

            var managerGo = new GameObject("TempleManager");
            var manager = managerGo.AddComponent<TempleManager>();
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
            Debug.Log($"<color=green>[TempleSetupTool] Built {ScenePath}.</color>");
            return true;
        }

        private static TempleViewUI BuildCanvas()
        {
            var charm = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MN-RueangLao SDF.asset")
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Charm-Bold SDF.asset");
            var body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/EkkamaiVibe SDF.asset")
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Sarabun-Regular SDF.asset");
            var panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var canvasGo = new GameObject("TempleCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var view = canvasGo.AddComponent<TempleViewUI>();
            var root = canvasGo.transform;

            R.Img("Background", root, Vector2.zero, Vector2.one, new Color(0.07f, 0.05f, 0.04f), null);
            // Candlelight at the bottom: the altar
            R.Img("CandleGlow", root, new Vector2(0f, 0f), new Vector2(1f, 0.35f), new Color(Candle.r, Candle.g, Candle.b, 0.3f), null);

            view.titleText = R.Text("Title", root, new Vector2(0.1f, 0.86f), new Vector2(0.9f, 0.97f), charm, 84, Gold, TextAlignmentOptions.Center);
            view.titleText.text = Title;
            view.khwanText = R.Text("KhwanStatus", root, new Vector2(0.7f, 0.02f), new Vector2(0.98f, 0.08f), body, 26, Muted, TextAlignmentOptions.MidlineRight);

            view.storyText = R.Text("Story", root, new Vector2(0.15f, 0.6f), new Vector2(0.85f, 0.84f), body, 32, Parchment, TextAlignmentOptions.Center);
            view.storyText.text = Story;
            view.storyText.textWrappingMode = TextWrappingModes.Normal;
            R.AutoSize(view.storyText, 18, 32);

            // Choice: one big button, and a way out that only shows when Khwan is already full
            var choice = R.Rect("ChoicePanel", root, new Vector2(0.3f, 0.12f), new Vector2(0.7f, 0.56f));
            view.choicePanel = choice.gameObject;
            var prayBg = R.Img("PrayButton", choice, new Vector2(0.05f, 0.3f), new Vector2(0.95f, 1f), ButtonBrown, panelSprite);
            view.prayButton = prayBg.gameObject.AddComponent<Button>();
            var prayTitle = R.Text("Title", prayBg.transform, new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.9f), charm, 56, Gold, TextAlignmentOptions.Center);
            prayTitle.text = "กราบพระ";
            view.prayHintText = R.Text("Hint", prayBg.transform, new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.45f), body, 26, Parchment, TextAlignmentOptions.Center);
            view.prayHintText.text = "ฟื้นขวัญเต็มหลอด";
            prayTitle.raycastTarget = view.prayHintText.raycastTarget = false;
            view.leaveButton = SmallButton("LeaveButton", choice, new Vector2(0.3f, 0f), new Vector2(0.7f, 0.18f), body, panelSprite, "เดินทางต่อ");

            var result = R.Rect("ResultPanel", root, new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.7f));
            view.resultPanel = result.gameObject;
            view.resultText = R.Text("ResultText", result, new Vector2(0f, 0.4f), new Vector2(1f, 1f), body, 40, Parchment, TextAlignmentOptions.Center);
            R.AutoSize(view.resultText, 20, 40);
            view.continueButton = SmallButton("ContinueButton", result, new Vector2(0.4f, 0f), new Vector2(0.6f, 0.16f), body, panelSprite, "เดินทางต่อ");
            result.gameObject.SetActive(false);
            return view;
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
