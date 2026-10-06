using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace TawanOS.StoryEngine
{
    /// <summary>
    /// Builds StoryScene (visual-novel layout: picture/video background, text box, skip button) and a starter
    /// intro story. The intro asset is only created when missing, so re-running keeps the team's edits.
    /// </summary>
    public static class StorySetupTool
    {
        private const string BaseFolder = "Assets/StoryEngineData";
        private const string IntroStoryPath = BaseFolder + "/Story_Intro.asset";
        private const string ScenePath = "Assets/Scenes/StoryScene.unity";
        private const string IconFolder = "Assets/ProjectAsset/MapNavigate/";

        private static readonly Color Gold = new Color(0.93f, 0.78f, 0.45f);
        private static readonly Color Parchment = new Color(0.96f, 0.92f, 0.82f);

        [MenuItem("Tools/TawanOS/Story/Setup Story Scene & Intro")]
        public static void SetupStoryScene()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Setup in Play Mode", "Please exit Play Mode before running the Setup Tool.", "OK");
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            try
            {
                if (!AssetDatabase.IsValidFolder(BaseFolder)) AssetDatabase.CreateFolder("Assets", "StoryEngineData");
                var intro = CreateOrGetIntroStory();

                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var camGo = new GameObject("Main Camera");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                camGo.tag = "MainCamera";
                camGo.transform.position = new Vector3(0, 0, -10);

                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

                var player = BuildCanvas();
                player.defaultStory = intro;

                EditorSceneManager.SaveScene(scene, ScenePath);
                RegisterSceneInBuild(ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=green>[StorySetupTool] Built {ScenePath} with intro story {IntroStoryPath}.</color>");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        private static StoryDataSO CreateOrGetIntroStory()
        {
            var existing = AssetDatabase.LoadAssetAtPath<StoryDataSO>(IntroStoryPath);
            if (existing != null) return existing;

            // Placeholder text and pictures: the team replaces them in the inspector
            var temple = Icon("TempleIcon.png");
            var graveyard = Icon("GraveyardIcon.png");
            var shrine = Icon("ShrineIcon.png");

            var story = ScriptableObject.CreateInstance<StoryDataSO>();
            story.storyId = "intro";
            story.pages = new List<StoryPage>
            {
                new StoryPage { blackScreen = true, text = "คืนนั้น ลมหนาวพัดผ่านหมู่บ้านริมป่าช้า..." },
                new StoryPage { background = temple, text = "เด็กน้อยคนหนึ่งล้มป่วยไม่ได้สติ ผู้เฒ่าบอกว่าขวัญของเด็กหลุดหายไปกับความมืด" },
                new StoryPage { speaker = "แม่", text = "ขวัญเอ้ย ขวัญมา... ได้โปรดช่วยลูกฉันด้วยเถิด หมอธรรม" },
                new StoryPage { background = graveyard, text = "ร่องรอยของขวัญมุ่งไปทางป่าช้าหลังวัด ที่ซึ่งผีร้ายเฝ้ารออยู่" },
                new StoryPage { background = shrine, speaker = "หมอธรรม", text = "อาคมขาว เครื่องรางดำ... ข้าจะใช้ทุกอย่างที่มี เพื่อพาขวัญกลับคืนมา" },
                new StoryPage { blackScreen = true, text = "จุดธูป แล้วออกเดินทาง" },
            };
            AssetDatabase.CreateAsset(story, IntroStoryPath);
            return story;
        }

        private static Sprite Icon(string file)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + file);
        }

        private static StoryPlayer BuildCanvas()
        {
            var charm = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Charm-Bold SDF.asset");
            var sarabun = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Sarabun-Regular SDF.asset");
            var panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            var canvasGo = new GameObject("StoryCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var player = canvasGo.AddComponent<StoryPlayer>();

            Img("Black", canvasGo.transform, Vector2.zero, Vector2.one, Color.black, null);

            var group = Rect("Story", canvasGo.transform, Vector2.zero, Vector2.one).gameObject.AddComponent<CanvasGroup>();
            player.rootGroup = group;
            var root = group.transform;

            // Picture and video backgrounds each have two stacked layers that StoryPlayer cross-fades
            var bg = Rect("Background", root, Vector2.zero, Vector2.one);
            player.background.a = Picture("BackgroundA", bg, Vector2.zero, Vector2.one);
            player.background.b = Picture("BackgroundB", bg, Vector2.zero, Vector2.one);

            var video = Rect("BackgroundVideo", root, Vector2.zero, Vector2.one);
            video.gameObject.AddComponent<RectMask2D>();
            player.backgroundVideo.a = VideoSurface("VideoA", video);
            player.backgroundVideo.b = VideoSurface("VideoB", video);

            // Clicking anywhere advances; everything drawn above it ignores raycasts except the skip button
            var advance = Img("AdvanceArea", root, Vector2.zero, Vector2.one, new Color(0, 0, 0, 0), null);
            player.advanceButton = advance.gameObject.AddComponent<Button>();
            player.advanceButton.transition = Selectable.Transition.None;

            var box = Img("TextBox", root, new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.27f), new Color(0.07f, 0.04f, 0.04f, 0.88f), panelSprite);
            box.raycastTarget = false;
            box.gameObject.AddComponent<Outline>().effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.45f);
            player.bodyText = Text("Body", box.transform, Vector2.zero, Vector2.one, sarabun, 38, Parchment, TextAlignmentOptions.TopLeft);
            player.bodyText.margin = new Vector4(48, 36, 80, 28);

            var marker = Img("NextIndicator", box.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), Gold, null);
            marker.raycastTarget = false;
            marker.rectTransform.sizeDelta = new Vector2(18, 18);
            marker.rectTransform.anchoredPosition = new Vector2(-40, 36);
            marker.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            player.nextIndicator = marker;

            var plate = Img("SpeakerPlate", root, new Vector2(0.08f, 0.27f), new Vector2(0.3f, 0.335f), new Color(0.35f, 0.1f, 0.07f, 0.95f), panelSprite);
            plate.raycastTarget = false;
            player.speakerPlate = plate.gameObject;
            player.speakerText = Text("Speaker", plate.transform, Vector2.zero, Vector2.one, charm, 40, Gold, TextAlignmentOptions.Center);

            var skip = Img("SkipButton", root, new Vector2(0.86f, 0.92f), new Vector2(0.97f, 0.975f), new Color(0.2f, 0.12f, 0.09f, 0.8f), panelSprite);
            player.skipButton = skip.gameObject.AddComponent<Button>();
            var skipLabel = Text("Label", skip.transform, Vector2.zero, Vector2.one, sarabun, 28, Parchment, TextAlignmentOptions.Center);
            skipLabel.text = "ข้าม";

            return player;
        }

        private static Image Picture(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var img = Img(name, parent, anchorMin, anchorMax, new Color(1f, 1f, 1f, 0f), null);
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        // RawImage + VideoPlayer; StoryPlayer gives each its RenderTexture at runtime.
        // EnvelopeParent fills the screen at the clip's aspect, cropping the overflow.
        private static RawImage VideoSurface(string name, Transform parent)
        {
            var raw = Rect(name, parent, Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
            raw.color = new Color(1f, 1f, 1f, 0f);
            raw.raycastTarget = false;
            var fitter = raw.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;
            var vp = raw.gameObject.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.isLooping = true;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.audioOutputMode = VideoAudioOutputMode.None;
            return raw;
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
            t.raycastTarget = false;
            t.text = string.Empty;
            return t;
        }
    }
}
