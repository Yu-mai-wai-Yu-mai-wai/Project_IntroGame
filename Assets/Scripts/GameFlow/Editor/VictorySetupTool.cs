#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TawanOS.UI;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Builds the Victory scene (Plan Task A3) for defeating the Boss and adds it to Build Settings.
    /// Menu: Tools > TawanOS > Game Flow > Setup Victory Scene
    /// </summary>
    public static class VictorySetupTool
    {
        public const string ScenePath = "Assets/Scenes/VictoryScene.unity";

        private static readonly Color Gold = new Color(0.93f, 0.78f, 0.45f);
        private static readonly Color Parchment = new Color(0.96f, 0.92f, 0.82f);
        private static readonly Color Crimson = new Color(0.55f, 0.16f, 0.08f);
        private static readonly Color MutedText = new Color(0.75f, 0.70f, 0.65f);
        private static readonly Color DarkBg = new Color(0.05f, 0.035f, 0.04f);

        [MenuItem("Tools/TawanOS/Game Flow/Setup Victory Scene")]
        public static void SetupVictoryScene()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Setup in Play Mode", "Please exit Play Mode before running the Setup Tool.", "OK");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Camera
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = DarkBg;
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0, 0, -10);

            // 2. EventSystem
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // 3. Canvas & UI
            var view = BuildCanvas();

            // 4. Save Scene
            EditorSceneManager.SaveScene(scene, ScenePath);

            // 5. Add to Build Settings
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=green>[VictorySetupTool] Built {ScenePath} and added to Build Settings successfully.</color>");
        }

        private static VictoryViewUI BuildCanvas()
        {
            var charm = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Charm-Bold SDF.asset");
            var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/KorKorTor SDF.asset")
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Sarabun-Regular SDF.asset");
            var sarabun = bodyFont;

            var canvasGo = new GameObject("VictoryCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
            var view = canvasGo.AddComponent<VictoryViewUI>();
            var root = canvasGo.transform;

            // Background
            CreateImage("Background", root, Vector2.zero, Vector2.one, DarkBg);

            // Ember glow at top/bottom
            CreateImage("AuraGlow", root, new Vector2(0f, 0.4f), new Vector2(1f, 1f), new Color(Crimson.r, Crimson.g, Crimson.b, 0.25f));

            // Content Panel / Group
            var panelGo = new GameObject("ContentGroup", typeof(RectTransform), typeof(CanvasGroup));
            panelGo.transform.SetParent(root, false);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.15f, 0.1f);
            panelRect.anchorMax = new Vector2(0.85f, 0.9f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            view.contentGroup = panelGo.GetComponent<CanvasGroup>();

            // Decorative Border Box
            var border = CreateImage("Border", panelGo.transform, Vector2.zero, Vector2.one, Crimson);
            var innerBg = CreateImage("InnerBg", panelGo.transform, Vector2.zero, Vector2.one, new Color(0.08f, 0.05f, 0.05f, 0.95f));
            innerBg.rectTransform.offsetMin = new Vector2(4, 4);
            innerBg.rectTransform.offsetMax = new Vector2(-4, -4);

            // Title "สู่สุขคติ"
            view.titleText = CreateText("Title", panelGo.transform,
                new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.95f),
                charm != null ? charm : sarabun, 84, Gold, TextAlignmentOptions.Center);
            view.titleText.text = "สู่สุขคติ";

            // Subtitle "ชัยชนะอันสมบูรณ์เหนือวิญญาณร้าย"
            view.subtitleText = CreateText("Subtitle", panelGo.transform,
                new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.78f),
                sarabun, 36, Parchment, TextAlignmentOptions.Center);
            view.subtitleText.text = "ชัยชนะอันสมบูรณ์เหนือวิญญาณร้าย";

            // Epilogue Story
            view.epilogueText = CreateText("Epilogue", panelGo.transform,
                new Vector2(0.08f, 0.40f), new Vector2(0.92f, 0.66f),
                sarabun, 28, MutedText, TextAlignmentOptions.Center);
            view.epilogueText.text =
                "เจ้าได้สะกดวิญญาณร้ายแห่งป่าช้า และรวบรวมขวัญที่กระเจิดกระเจิงกลับคืนสู่ร่างได้สำเร็จ\n" +
                "ควันธูปจางหาย สายหมอกมืดมิดเริ่มคลี่คลาย การเดินทางในคืนอันยาวนานสิ้นสุดลงแล้ว...";
            view.epilogueText.textWrappingMode = TextWrappingModes.Normal;

            // Run Stats Line
            view.statsText = CreateText("Stats", panelGo.transform,
                new Vector2(0.05f, 0.28f), new Vector2(0.95f, 0.38f),
                sarabun, 26, Gold, TextAlignmentOptions.Center);
            view.statsText.text = "ขวัญคงเหลือ: 50/50   •   ธูปสะสม: 0   •   สำรับการ์ด: 0 ใบ";

            // Main Menu Button
            var btnGo = new GameObject("MainMenuButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(panelGo.transform, false);
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.35f, 0.12f);
            btnRect.anchorMax = new Vector2(0.65f, 0.22f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;

            var btnImg = btnGo.GetComponent<Image>();
            btnImg.color = Crimson;

            var btnInnerGo = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            btnInnerGo.transform.SetParent(btnGo.transform, false);
            var innerRect = btnInnerGo.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(2, 2);
            innerRect.offsetMax = new Vector2(-2, -2);
            btnInnerGo.GetComponent<Image>().color = new Color(0.18f, 0.10f, 0.10f);

            view.mainMenuButton = btnGo.GetComponent<Button>();
            view.mainMenuButtonLabel = CreateText("Label", btnInnerGo.transform,
                Vector2.zero, Vector2.one, sarabun, 28, Parchment, TextAlignmentOptions.Center);
            view.mainMenuButtonLabel.text = "กลับสู่หน้าจอหลัก";

            return view;
        }

        private static Image CreateImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, TMP_FontAsset font, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            return tmp;
        }
    }
}
#endif
