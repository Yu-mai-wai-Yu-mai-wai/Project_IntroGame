using System.Collections.Generic;
using TawanOS.CardEngine;
using TawanOS.MapEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.EventEngine
{
    /// <summary>
    /// Builds the story-event scene (Simulated Universe "Occurrence" style), a starter set of events,
    /// the event catalog and the Event map-node profile.
    /// </summary>
    public static class EventEngineSetupTool
    {
        private const string BaseFolder = "Assets/EventEngineData";
        private const string EventsFolder = BaseFolder + "/Events";
        private const string CatalogPath = BaseFolder + "/EventCatalog.asset";
        private const string ScenePath = "Assets/Scenes/EventScene.unity";
        private const string MapConfigPath = "Assets/MapEngineData/Profiles/DefaultMapConfig.asset";
        private const string EventNodeProfilePath = "Assets/MapEngineData/Profiles/EventProfile.asset";
        private const string EventIconPath = "Assets/MapEngineData/Icons/event-question.png";
        private const string PraiEnemyPath = "Assets/CardEngineData/Enemies/PraiGhostProfile.asset";

        private static readonly Color Gold = new Color(0.93f, 0.78f, 0.45f);
        private static readonly Color Parchment = new Color(0.96f, 0.92f, 0.82f);
        private static readonly Color Muted = new Color(0.62f, 0.58f, 0.52f);
        private static readonly Color Teal = new Color(0.55f, 0.9f, 0.85f);

        [MenuItem("Tools/TawanOS/Event Engine/Setup Event Scene & Sample Events")]
        public static void SetupEventScene()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cannot Setup in Play Mode", "Please exit Play Mode before running the Setup Tool.", "OK");
                return;
            }

            EnsureFolder("Assets", "EventEngineData");
            EnsureFolder(BaseFolder, "Events");

            var catalog = CreateOrGetCatalog();
            SetupEventNodeProfile();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.035f, 0.05f);
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0, 0, -10);

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            var view = BuildEventCanvas();

            var managerGo = new GameObject("EventManager");
            var manager = managerGo.AddComponent<EventManager>();
            manager.catalog = catalog;
            manager.view = view;

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterSceneInBuild(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=green>[EventEngineSetupTool] Built {ScenePath} with {catalog.events.Count} events.</color>");
        }

        // ------------------------------------------------------------------ Data

        private static EventCatalogSO CreateOrGetCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EventCatalogSO>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<EventCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var prai = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(PraiEnemyPath);
            var samples = new[]
            {
                CreateOrGetEvent("Event_SpiritHouse", BuildSpiritHouse),
                CreateOrGetEvent("Event_WanderingShaman", BuildWanderingShaman),
                CreateOrGetEvent("Event_WellVoice", e => BuildWellVoice(e, prai)),
                CreateOrGetEvent("Event_GhostGamble", BuildGhostGamble),
            };

            catalog.events.RemoveAll(e => e == null);
            foreach (var evt in samples)
                if (!catalog.events.Contains(evt)) catalog.events.Add(evt);

            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        /// <summary>Existing event assets are left untouched so designer edits survive re-running the tool.</summary>
        private static EventDataSO CreateOrGetEvent(string fileName, System.Action<EventDataSO> build)
        {
            string path = $"{EventsFolder}/{fileName}.asset";
            var evt = AssetDatabase.LoadAssetAtPath<EventDataSO>(path);
            if (evt != null) return evt;

            evt = ScriptableObject.CreateInstance<EventDataSO>();
            evt.pages.Clear();
            build(evt);
            AssetDatabase.CreateAsset(evt, path);
            return evt;
        }

        private static void BuildSpiritHouse(EventDataSO e)
        {
            e.eventId = "spirit_house";
            e.title = "ศาลพระภูมิร้าง";
            e.pages.Add(Page("start",
                "ริมทางมีศาลพระภูมิหลังเล็กตั้งเอียงอยู่ใต้ต้นไทร ธูปที่ปักไว้มอดดับไปนานแล้ว " +
                "แต่พวงมาลัยบนหลังคากลับยังสดราวกับเพิ่งมีคนนำมาวาง\n\nลมเย็นพัดผ่าน... เหมือนมีใครกำลังรอคำตอบจากคุณ",
                PaidChoice(30, 0, "จุดธูปถวาย", "เจ้าที่อาจตอบแทนน้ำใจ",
                    Outcome("ควันธูปลอยขึ้นตรง ความอบอุ่นแผ่ซ่านไปทั่วร่าง ในกระถางธูปมีพระเครื่ององค์เล็กวางอยู่",
                        Effect(EventEffectType.Heal, 15), Effect(EventEffectType.GainIncense, 25))),
                Choice("ไหว้แล้วขอพร", "ผลลัพธ์ไม่แน่นอน",
                    Outcome("เสียงกระดิ่งลมดังกังวาน จิตใจคุณสงบลง", 1, Effect(EventEffectType.Heal, 8)),
                    Outcome("ศาลสั่นเบา ๆ ราวกับไม่พอใจ ความหนาวเย็นแทรกเข้ามาในกระดูก", 1, Effect(EventEffectType.TakeDamage, 6))),
                Choice("หยิบธูปในกระถางไป", "ได้ธูป แต่...",
                    Outcome("คุณกวาดธูปที่ยังไม่จุดใส่ย่าม ขณะเดินจากมา ได้ยินเสียงหัวเราะแหบแห้งไล่หลัง",
                        Effect(EventEffectType.GainIncense, 40), Effect(EventEffectType.ChangeMaxHp, -4))),
                Choice("เดินผ่านไป", null,
                    Outcome("คุณพนมมือไหว้เบา ๆ แล้วเดินต่อไปโดยไม่หันกลับไปมอง"))));
        }

        private static void BuildWanderingShaman(EventDataSO e)
        {
            e.eventId = "wandering_shaman";
            e.title = "หมอผีเร่ร่อน";
            e.speakerName = "ตาหมอเหลือง";
            e.pages.Add(Page("start",
                "ชายชราสักยันต์เต็มแผ่นหลังนั่งผิงไฟอยู่ข้างทาง ข้างตัวมีย่ามเก่าที่ส่งเสียงกรุ๊งกริ๊งทุกครั้งที่ลมพัด\n\n" +
                "\"เอ็งก็เดินทางสายเดียวกับข้าสินะ... นั่งก่อน ข้ามีของดีจะให้ดู\"",
                Choice("นั่งลงฟัง", "ดูของในย่าม",
                    Outcome("คุณนั่งลงข้างกองไฟ ตาหมอค่อย ๆ หยิบของออกมาทีละชิ้น", 1, null, "trade")),
                Choice("ปฏิเสธแล้วเดินต่อ", null,
                    Outcome("\"ตามใจเอ็ง ทางข้างหน้ามันมืดนะ\" เสียงหัวเราะของเขาจางหายไปกับสายลม"))));
            e.pages.Add(Page("trade",
                "\"ตะกรุดดอกนี้ลงอาคมด้วยเลือด ข้าจะแลกด้วยเลือดของเอ็ง... หรือถ้าเอ็งมีธูป ข้ามีน้ำมนต์ต่อชีวิตให้\"",
                PaidChoice(0, 8, "แลกด้วยเลือด", "รับตะกรุด",
                    Outcome("ตาหมอกรีดปลายนิ้วคุณหยดลงบนตะกรุด ตัวอักษรขอมบนแผ่นโลหะเรืองแสงวาบ",
                        Effect(EventEffectType.CardReward, 0))),
                PaidChoice(40, 0, "แลกด้วยธูป", "เพิ่ม HP สูงสุด",
                    Outcome("น้ำมนต์เย็นเฉียบไหลผ่านลำคอ คุณรู้สึกแข็งแรงขึ้นกว่าเดิม",
                        Effect(EventEffectType.ChangeMaxHp, 8))),
                Choice("ไม่แลกอะไร", null,
                    Outcome("\"ไม่เป็นไร วาสนาคนเราไม่เท่ากัน\" ตาหมอเก็บของลงย่ามอย่างเงียบ ๆ"))));
        }

        private static void BuildWellVoice(EventDataSO e, EnemyProfileSO prai)
        {
            e.eventId = "well_voice";
            e.title = "เสียงร้องจากบ่อน้ำ";
            e.minFloor = 2;
            e.pages.Add(Page("start",
                "กลางลานวัดร้างมีบ่อน้ำเก่าถูกปิดด้วยแผ่นไม้ผุ ๆ จากก้นบ่อมีเสียงหญิงสาวร้องขอความช่วยเหลือ\n\n" +
                "\"ช่วยด้วย... ฉันตกลงมานานแล้ว ได้โปรด...\"",
                Choice("ดึงเธอขึ้นมา", "เสี่ยง: อาจไม่ใช่คน",
                    Outcome("คุณหย่อนเชือกลงไป หญิงสาวปีนขึ้นมาพร้อมยิ้มขอบคุณ ก่อนจะมอบปิ่นปักผมเงินให้แล้วหายไปในความมืด", 3,
                        Effect(EventEffectType.GainIncense, 40)),
                    Outcome("มือที่คว้าเชือกขึ้นมาเย็นเฉียบและเปียกชุ่ม ใบหน้าซีดขาวโผล่พ้นปากบ่อพร้อมรอยยิ้มฉีกถึงใบหู!", 2,
                        new EventEffect { type = EventEffectType.StartCombat, enemy = prai })),
                PaidChoice(10, 0, "จุดธูปส่งวิญญาณ", "ปลอบวิญญาณ",
                    Outcome("เสียงร้องค่อย ๆ เงียบลง แทนที่ด้วยเสียงกระซิบขอบคุณ ความอ่อนล้าของคุณหายไป",
                        Effect(EventEffectType.Heal, 10))),
                Choice("ปิดฝาบ่อให้แน่น", null,
                    Outcome("คุณลากแผ่นไม้มาทับปากบ่อ เสียงร้องกลายเป็นเสียงกรีดร้องโหยหวนก่อนจะเงียบไป"))));
        }

        private static void BuildGhostGamble(EventDataSO e)
        {
            e.eventId = "ghost_gamble";
            e.title = "วงไพ่สัมภเวสี";
            e.speakerName = "เจ้ามือไร้หน้า";
            e.repeatable = true;
            e.pages.Add(Page("start",
                "ใต้ศาลาริมน้ำ เงาดำหลายตนนั่งล้อมวงไพ่อยู่ใต้แสงตะเกียงสีเขียว เจ้ามือที่ไม่มีใบหน้ากวักมือเรียกคุณ\n\n" +
                "\"มาสิ... ลงสักตา ชนะเอาไปสองเท่า แพ้ก็แค่... เสียนิดหน่อย\"",
                PaidChoice(20, 0, "ลงธูป 20 ดอก", "ชนะได้ 50 ดอก",
                    Outcome("ไพ่ใบสุดท้ายพลิกออก เป็นของคุณ! เหล่าเงาดำส่งเสียงฮือฮา", 45, Effect(EventEffectType.GainIncense, 50)),
                    Outcome("เจ้ามือหัวเราะเสียงแหลม กวาดธูปของคุณไปจนหมดกอง", 55)),
                PaidChoice(0, 10, "ลงด้วยลมหายใจ", "ชนะได้ของวิเศษ",
                    Outcome("เจ้ามือนิ่งไปครู่หนึ่ง ก่อนยื่นไพ่ใบหนึ่งให้คุณ \"เอาไป... มันเลือกเอ็งแล้ว\"", 1, Effect(EventEffectType.CardReward, 0)),
                    Outcome("คุณแพ้ ความหนาวเย็นดูดพลังออกจากร่างจนเข่าอ่อน", 1)),
                Choice("ไม่เล่น", null,
                    Outcome("เงาดำทั้งวงหันมามองคุณพร้อมกัน ก่อนจะกลับไปสนใจไพ่ในมือเหมือนเดิม"))));
        }

        private static EventPage Page(string id, string body, params EventChoice[] choices)
        {
            return new EventPage { pageId = id, body = body, choices = new List<EventChoice>(choices) };
        }

        private static EventChoice Choice(string label, string hint, params EventOutcome[] outcomes)
        {
            return PaidChoice(0, 0, label, hint, outcomes);
        }

        private static EventChoice PaidChoice(int incenseCost, int hpCost, string label, string hint, params EventOutcome[] outcomes)
        {
            return new EventChoice
            {
                label = label,
                hint = hint,
                incenseCost = incenseCost,
                hpCost = hpCost,
                outcomes = new List<EventOutcome>(outcomes),
            };
        }

        private static EventOutcome Outcome(string text, params EventEffect[] effects)
        {
            return Outcome(text, 1, effects);
        }

        private static EventOutcome Outcome(string text, int weight, params EventEffect[] effects)
        {
            return new EventOutcome { resultText = text, weight = weight, effects = new List<EventEffect>(effects ?? new EventEffect[0]) };
        }

        private static EventOutcome Outcome(string text, int weight, EventEffect[] effects, string nextPageId)
        {
            var o = Outcome(text, weight, effects);
            o.nextPageId = nextPageId;
            return o;
        }

        private static EventEffect Effect(EventEffectType type, int amount)
        {
            return new EventEffect { type = type, amount = amount };
        }

        private static EventEffect Relic(string relicId)
        {
            return new EventEffect { type = EventEffectType.GainRelic, relicId = relicId };
        }

        // ------------------------------------------------------------------ Map node

        private static void SetupEventNodeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<NodeProfileSO>(EventNodeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<NodeProfileSO>();
                profile.type = NodeType.Event;
                // Same crimson ink-stamp palette as the other map nodes (MapEngineSetupTool)
                profile.title = "Unknown Omen";
                profile.description = "A strange occurrence on the path. Your choices shape what you gain... or lose.";
                profile.baseColor = new Color(0.3529f, 0.0941f, 0.0627f, 1f);
                profile.hoverColor = new Color(0.5568f, 0.1490f, 0.0941f, 1f);
                profile.visitedColor = new Color(0.1725f, 0.0784f, 0.0627f, 1f);
                AssetDatabase.CreateAsset(profile, EventNodeProfilePath);
            }

            if (profile.icon == null)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(EventIconPath);
                if (tex != null)
                {
                    var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    sprite.name = "event-question.png";
                    AssetDatabase.AddObjectToAsset(sprite, profile);
                    profile.icon = sprite;
                    EditorUtility.SetDirty(profile);
                }
            }

            var config = AssetDatabase.LoadAssetAtPath<MapConfigSO>(MapConfigPath);
            if (config != null && !config.nodeProfiles.Contains(profile))
            {
                config.nodeProfiles.RemoveAll(p => p != null && p.type == NodeType.Event);
                config.nodeProfiles.Add(profile);
                EditorUtility.SetDirty(config);
            }
        }

        // ------------------------------------------------------------------ UI

        private static EventViewUI BuildEventCanvas()
        {
            var charm = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/MN-RueangLao SDF.asset")
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Charm-Bold SDF.asset");
            var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/EkkamaiVibe SDF.asset")
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Sarabun-Regular SDF.asset");
            var sarabun = bodyFont;
            var panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            var canvasGo = new GameObject("EventCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var view = canvasGo.AddComponent<EventViewUI>();

            var root = canvasGo.transform;
            Img("Background", root, Vector2.zero, Vector2.one, new Color(0.07f, 0.045f, 0.06f), null);

            // Run status (top-left)
            var status = Rect("RunStatus", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -80), new Vector2(700, -24));
            view.hpText = Text("HPText", status, new Vector2(0, 0), new Vector2(0.45f, 1), sarabun, 30, new Color(0.95f, 0.45f, 0.4f), TextAlignmentOptions.MidlineLeft);
            view.incenseText = Text("IncenseText", status, new Vector2(0.45f, 0), new Vector2(1, 1), sarabun, 30, Gold, TextAlignmentOptions.MidlineLeft);

            var tag = Text("OccurrenceTag", root, new Vector2(1, 1), new Vector2(1, 1), sarabun, 26, Muted, TextAlignmentOptions.MidlineRight);
            tag.rectTransform.offsetMin = new Vector2(-400, -80);
            tag.rectTransform.offsetMax = new Vector2(-40, -24);
            tag.text = "เหตุการณ์";

            // Illustration frame (left)
            var frame = Img("IllustrationFrame", root, new Vector2(0.04f, 0.08f), new Vector2(0.52f, 0.88f), new Color(0.12f, 0.085f, 0.095f), panelSprite);
            frame.gameObject.AddComponent<Outline>().effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.45f);
            var placeholder = Text("Placeholder", frame.transform, Vector2.zero, Vector2.one, charm, 260, new Color(1f, 1f, 1f, 0.06f), TextAlignmentOptions.Center);
            placeholder.text = "?";
            var illus = Img("Illustration", frame.transform, Vector2.zero, Vector2.one, Color.white, null);
            illus.rectTransform.offsetMin = new Vector2(16, 16);
            illus.rectTransform.offsetMax = new Vector2(-16, -16);
            illus.raycastTarget = false;
            view.illustration = illus;

            // Narration panel (right)
            var panel = Rect("NarrationPanel", root, new Vector2(0.56f, 0.06f), new Vector2(0.96f, 0.9f), Vector2.zero, Vector2.zero);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            view.titleText = Text("Title", panel, Vector2.zero, Vector2.one, charm, 64, Gold, TextAlignmentOptions.BottomLeft);
            Layout(view.titleText.gameObject, 90);

            var divider = Img("Divider", panel, Vector2.zero, Vector2.one, new Color(Gold.r, Gold.g, Gold.b, 0.5f), null);
            Layout(divider.gameObject, 3);

            view.speakerText = Text("Speaker", panel, Vector2.zero, Vector2.one, sarabun, 30, Teal, TextAlignmentOptions.MidlineLeft);
            view.speakerText.fontStyle = FontStyles.Bold;
            Layout(view.speakerText.gameObject, 42);

            // Clickable body area: click to finish the typewriter
            var bodyArea = Img("BodyArea", panel, Vector2.zero, Vector2.one, new Color(0, 0, 0, 0), null);
            Layout(bodyArea.gameObject, 200, flexible: 1);
            view.skipTypingButton = bodyArea.gameObject.AddComponent<Button>();
            view.skipTypingButton.transition = Selectable.Transition.None;
            view.bodyText = Text("BodyText", bodyArea.transform, Vector2.zero, Vector2.one, sarabun, 31, Parchment, TextAlignmentOptions.TopLeft);
            view.bodyText.lineSpacing = 12;
            view.bodyText.raycastTarget = false;

            view.effectText = Text("EffectSummary", panel, Vector2.zero, Vector2.one, sarabun, 30, new Color(0.6f, 0.95f, 0.55f), TextAlignmentOptions.MidlineLeft);
            view.effectText.fontStyle = FontStyles.Bold;
            Layout(view.effectText.gameObject, 44);

            var choices = Rect("Choices", panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var choiceLayout = choices.gameObject.AddComponent<VerticalLayoutGroup>();
            choiceLayout.spacing = 12;
            choiceLayout.childControlWidth = true;
            choiceLayout.childControlHeight = true;
            choiceLayout.childForceExpandWidth = true;
            choiceLayout.childForceExpandHeight = false;
            view.choiceContainer = choices;
            view.choiceGroup = choices.gameObject.AddComponent<CanvasGroup>();
            view.choiceTemplate = BuildChoiceTemplate(choices, sarabun, panelSprite, knobSprite);

            return view;
        }

        private static EventChoiceButton BuildChoiceTemplate(Transform parent, TMP_FontAsset font, Sprite panelSprite, Sprite knobSprite)
        {
            var bg = Img("ChoiceTemplate", parent, Vector2.zero, Vector2.one, Color.white, panelSprite);
            Layout(bg.gameObject, 92);

            var button = bg.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.17f, 0.12f, 0.12f, 0.95f);
            colors.highlightedColor = new Color(0.34f, 0.25f, 0.16f, 1f);
            colors.pressedColor = new Color(0.45f, 0.34f, 0.2f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.12f, 0.1f, 0.1f, 0.7f);
            button.colors = colors;

            var badge = Img("IndexBadge", bg.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Color(Gold.r, Gold.g, Gold.b, 0.85f), knobSprite);
            badge.rectTransform.sizeDelta = new Vector2(48, 48);
            badge.rectTransform.anchoredPosition = new Vector2(44, 0);
            badge.raycastTarget = false;

            var choice = bg.gameObject.AddComponent<EventChoiceButton>();
            choice.button = button;
            choice.indexText = Text("Index", badge.transform, Vector2.zero, Vector2.one, font, 26, new Color(0.15f, 0.1f, 0.08f), TextAlignmentOptions.Center);
            choice.indexText.fontStyle = FontStyles.Bold;

            choice.labelText = Text("Label", bg.transform, new Vector2(0, 0.42f), new Vector2(1, 1), font, 30, Parchment, TextAlignmentOptions.BottomLeft);
            choice.labelText.rectTransform.offsetMin = new Vector2(88, 0);
            choice.labelText.rectTransform.offsetMax = new Vector2(-220, -6);

            choice.hintText = Text("Hint", bg.transform, new Vector2(0, 0), new Vector2(1, 0.42f), font, 22, Muted, TextAlignmentOptions.TopLeft);
            choice.hintText.fontStyle = FontStyles.Italic;
            choice.hintText.rectTransform.offsetMin = new Vector2(88, 6);
            choice.hintText.rectTransform.offsetMax = new Vector2(-220, 0);

            choice.costText = Text("Cost", bg.transform, new Vector2(1, 0), new Vector2(1, 1), font, 24, Gold, TextAlignmentOptions.MidlineRight);
            choice.costText.rectTransform.offsetMin = new Vector2(-210, 0);
            choice.costText.rectTransform.offsetMax = new Vector2(-24, 0);

            foreach (var t in bg.GetComponentsInChildren<TextMeshProUGUI>()) t.raycastTarget = false;
            return choice;
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

        private static void Layout(GameObject go, float preferredHeight, float flexible = 0)
        {
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.minHeight = flexible > 0 ? 0 : preferredHeight;
            le.flexibleHeight = flexible;
        }

        // ------------------------------------------------------------------ Helpers

        private static void RegisterSceneInBuild(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == scenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
