#if UNITY_EDITOR
using System.Collections.Generic;
using TawanOS.CardEngine;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EventEngine
{
    /// <summary>
    /// Adds the หมอกดำ events written in the team's story sheet (Google Sheet, tab "story", Oct 2026) that the game
    /// does not have yet: ดวงไฟประหลาด and หน้ากากตาโขน. วอดส์ is already in (Event_Wods); the sheet's untitled
    /// เงาตะคุ่ม row was left out by the team.
    /// Each becomes an EventDataSO in Assets/EventEngineData/Events and joins the EventCatalog's random pool.
    /// Only missing events are made: an event already there (perhaps tuned by hand) is left as it is.
    /// Menu: Tools/TawanOS/Event Engine/Add Story Sheet Events.
    /// Batch: -executeMethod TawanOS.EventEngine.StoryTabEventsTool.AddFromCli
    /// </summary>
    public static class StoryTabEventsTool
    {
        private const string Folder = "Assets/EventEngineData/Events";
        private const string WodsPath = "Assets/EventEngineData/Events/Event_Wods.asset"; // its illustration is reused
        private const string KrasuePath = "Assets/CardEngineData/Enemies/NewEnemyProfile.asset";

        [MenuItem("Tools/TawanOS/Event Engine/Add Story Sheet Events")]
        private static void Add_Menu() => Add();

        public static void AddFromCli()
        {
            if (!Add()) EditorApplication.Exit(1);
        }

        public static bool Add()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EventCatalogSO>(EventDataSOEditor.CatalogPath);
            var krasue = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(KrasuePath);
            if (catalog == null || krasue == null)
            {
                Debug.LogError($"[StoryTabEventsTool] Missing the catalog ({EventDataSOEditor.CatalogPath}) or an enemy profile.");
                return false;
            }
            // A first picture, the one วอดส์ shows (the team gives each event its own afterwards)
            var wods = AssetDatabase.LoadAssetAtPath<EventDataSO>(WodsPath);
            var art = wods != null ? wods.illustration : null;

            catalog.events.RemoveAll(e => e == null); // entries whose asset was deleted
            int made = 0;
            made += Make(catalog, "Event_StrangeLight", e => StrangeLight(e, krasue), art);
            made += Make(catalog, "Event_TaKhonMask", TaKhonMask, art);

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[StoryTabEventsTool] {made} story events made; the catalog has {catalog.events.Count} events.</color>");
            return true;
        }

        private static int Make(EventCatalogSO catalog, string assetName, System.Action<EventDataSO> fill, Sprite art)
        {
            string path = $"{Folder}/{assetName}.asset";
            var evt = AssetDatabase.LoadAssetAtPath<EventDataSO>(path);
            int made = 0;
            if (evt == null)
            {
                evt = ScriptableObject.CreateInstance<EventDataSO>();
                evt.speakerName = "ขวัญ";
                evt.illustration = art;
                evt.minFloor = 0;
                evt.repeatable = false;
                fill(evt);
                AssetDatabase.CreateAsset(evt, path);
                made = 1;
            }
            if (!catalog.events.Contains(evt)) catalog.events.Add(evt);
            return made;
        }

        // ---------------------------------------------------------------- the events

        // ป่าหลังวัด: a red light floating in circles
        private static void StrangeLight(EventDataSO e, EnemyProfileSO krasue)
        {
            e.eventId = "strange_light";
            e.title = "ดวงไฟประหลาด";
            e.pages = new List<EventPage>
            {
                Page("start",
                    "คุณได้เดินมาแถววัดและต้องหยุดชะงักเมื่อเห็นแสงประหลาด เมื่อมองดูดี ๆ มันมีลักษณะเป็นดวงไฟสีแดงสดดวงไม่ใหญ่มาก "
                    + "ลอยวนอยู่ภายในป่าหลังวัด มันลอยช้า ๆ เป็นวงกลม ไม่ส่งเสียง",
                    Choice("โยนหินใส่", "เสี่ยง: อาจไม่ใช่แค่แสงไฟ",
                        Outcome("ดวงไฟหยุดนิ่ง ก่อนพุ่งเข้าหาคุณ เผยให้เห็นหัวผู้หญิงที่มีไส้ห้อยระย้า... ผีเข้าโจมตี!",
                            Fight(krasue))),
                    Choice("แกล้งทำเป็นไม่เห็นและเดินผ่าน", null,
                        Outcome("คุณก้มหน้าเดินผ่านไปเงียบ ๆ ดวงไฟยังลอยวนอยู่อย่างนั้น ไม่มีอะไรเกิดขึ้น"))),
            };
        }

        // The boy whose mask went missing, in a village that has long been empty
        private static void TaKhonMask(EventDataSO e)
        {
            e.eventId = "ta_khon_mask";
            e.title = "หน้ากากตาโขน";
            e.pages = new List<EventPage>
            {
                Page("start",
                    "คุณเดินเข้ามาในหมู่บ้าน ก็เห็นเหล่าเด็ก ๆ กำลังนั่งจับกลุ่มทำหน้ากากตาโขนกันอย่างครึกครื้น\n"
                    + "แต่แล้วคุณก็เหลือบไปเห็นเด็กชายคนหนึ่งนั่งอยู่คนเดียวใต้ต้นไม้บริเวณชายป่า\n"
                    + "คุณเดินไปหาด้วยความสงสัยว่าทำไมเด็กชายคนนั้นถึงแยกออกมานั่งคนเดียว แล้วจึงได้รู้ว่า "
                    + "หน้ากากที่เด็กชายทำไว้หายไป เขาจึงไม่มีหน้ากากตาโขนให้ทำต่อเหมือนเด็กคนอื่น\n\n"
                    + "คุณจะช่วยเด็กชายคนนี้อย่างไร",
                    Choice("พาเด็กชายไปหาซื้อของเพื่อทำอันใหม่", null,
                        Outcome("เมื่อไปถึงร้านขายของชำ คุณบอกว่าต้องการของสำหรับทำหน้ากากตาโขนให้เด็กชายที่พามา "
                            + "แต่ลุงเจ้าของร้านกลับบอกว่าหมู่บ้านแห่งนี้ร้างไปนานแล้ว ไม่มีเทศกาลแล้ว ส่วนตนแค่กลับมาเอาของ "
                            + "พอคุณหันไปมองรอบข้างก็พบว่าหมู่บ้านที่ครึกครื้นเมื่อครู่กลับไร้วี่แววของผู้คน")),
                    Choice("บอกว่างั้นปีนี้ก็ไม่ต้องทำ มายืนดูขบวนแห่กับพี่ข้างนอก", null,
                        Outcome("เด็กชายตอบตกลงและเดินตามคุณไปยืนรอชมขบวนแห่ริมทาง บรรยากาศเต็มไปด้วยความครึกครื้น "
                            + "แต่แล้วสายตาของคุณก็สะดุดเข้ากับหน้ากากตาโขนลวดลายแปลกตาในขบวน เด็กชายกระซิบข้างหูคุณว่า "
                            + "\"นั่นไงครับ... หน้ากากของผม\" เมื่อคุณหันกลับไป ร่างของเขาก็จางหายไป "
                            + "และร่างที่สวมหน้ากากในขบวนนั้นก็หยุดเดิน แล้วค่อย ๆ หันกลับมาจ้องมองคุณ...", next: "parade")),
                    Choice("พาน้องไปตามหาหน้ากากตาโขนที่หายไป", "อาจได้รับการ์ด",
                        Outcome("คุณพาน้องเดินกลับไปตามหาหน้ากาก เริ่มจากใต้ต้นไม้ที่พบกัน เด็กชายเดินนำคุณลึกเข้าไปในป่าที่มืดครึ้ม "
                            + "และเย็นลงอย่างน่าประหลาด จนไปถึงศาลร้างเก่าแก่กลางป่า คุณพบหน้ากากตาโขนสภาพสมบูรณ์วางอยู่บนแท่นหิน "
                            + "ทันทีที่คุณก้มลงเก็บหน้ากากขึ้นมา เด็กชายก็ยิ้มให้และพูดว่า \"ขอบคุณนะพี่ที่มาส่ง...\"",
                            new EventEffect { type = EventEffectType.CardReward }))),
                Page("parade",
                    "ร่างสวมหน้ากากตาโขนยืนนิ่งอยู่กลางขบวน จ้องมองมาที่คุณ",
                    Choice("รีบเดินหันหลังกลับออกจากบริเวณงาน", null,
                        Outcome("เมื่อคุณออกห่างจากงานได้สักระยะแล้วหันกลับไปมอง ร่างสวมหน้ากากก็มาปรากฏตัวดักอยู่ตรงหน้า "
                            + "แล้วค่อย ๆ ถอดหน้ากากออก เผยให้เห็นใบหน้าเด็กชายที่ยิ้มให้ ก่อนจะหายไปต่อหน้าต่อตา "
                            + "และหมู่บ้านที่เต็มไปด้วยผู้คนก็กลับกลายเป็นหมู่บ้านร้าง")),
                    Choice("ยืนนิ่งอยู่กับที่ รอดูว่าร่างนั้นจะทำอะไรต่อ", null,
                        Outcome("ร่างนั้นก้าวมาหยุดตรงหน้า ก้มลงวางหน้ากากตาโขนไว้ที่เท้าของคุณ พร้อมเสียงเด็กชายดังก้องในหัวว่า "
                            + "\"ขอบคุณที่จำหน้ากากผมได้ครับ\" ก่อนที่ร่างนั้นจะสลายหายไปพร้อมกับผู้คนในบริเวณนั้น"))),
            };
        }

        // ---------------------------------------------------------------- builders

        private static EventPage Page(string id, string body, params EventChoice[] choices)
        {
            return new EventPage { pageId = id, body = body, choices = new List<EventChoice>(choices) };
        }

        private static EventChoice Choice(string label, string hint, params EventOutcome[] outcomes)
        {
            return new EventChoice { label = label, hint = hint ?? "", outcomes = new List<EventOutcome>(outcomes) };
        }

        private static EventOutcome Outcome(string text, params EventEffect[] effects)
        {
            return new EventOutcome { resultText = text, effects = new List<EventEffect>(effects) };
        }

        private static EventOutcome Outcome(string text, string next)
        {
            return new EventOutcome { resultText = text, nextPageId = next };
        }

        private static EventEffect Fight(EnemyProfileSO enemy)
        {
            return new EventEffect { type = EventEffectType.StartCombat, enemy = enemy };
        }
    }
}
#endif
