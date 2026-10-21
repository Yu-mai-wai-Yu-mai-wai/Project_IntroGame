using System;
using UnityEngine;

namespace TawanOS.UI
{
    /// <summary>
    /// Every word the tutorial hints (<see cref="TutorialCoach"/>) show, editable in the Inspector. Loaded from
    /// Resources/TutorialText.asset; when the asset is missing the defaults below are used (create the asset with
    /// Tools/TawanOS/UI/Create Tutorial Text).
    ///
    /// Words in {braces} are filled in with the game's real numbers when the hint shows; keep them as they are.
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialText", menuName = "TawanOS/UI/Tutorial Text")]
    public class TutorialTextSO : ScriptableObject
    {
        public const string ResourceName = "TutorialText";

        [Serializable]
        public class Hint
        {
            public string title;
            [TextArea(2, 6)] public string body;

            public Hint(string title, string body)
            {
                this.title = title;
                this.body = body;
            }
        }

        [Header("Start of the fight")]
        public Hint enemyKhwan = new Hint("ขวัญของศัตรู",
            "เงาดำบุกเข้ามาในฝันของขวัญ!\nทำให้ขวัญของศัตรูตรงนี้เหลือ 0 เพื่อชนะ");
        public Hint playerKhwan = new Hint("ขวัญของคุณ",
            "ถ้าขวัญของคุณหมดก่อน คุณจะแพ้\nนี่เป็นแค่ฝัน แพ้ก็ไม่เสียอะไร");

        [Header("Turn 1, by phase")]
        public Hint enemyFirst = new Hint("ศัตรูลงการ์ดก่อนเสมอ",
            "ทุกเทิร์น ศัตรูจะลงบริวารและเครื่องรางในแถวนี้ก่อน\nดูว่ามันวางการ์ดไว้ช่องไหน แล้วค่อยวางของคุณตอบโต้");
        [Tooltip("{boardCards} = familiars + amulets in the hand, {incantations} = incantations in the hand")]
        public Hint hand = new Hint("การ์ดในมือ",
            "มือนี้มีบริวาร / เครื่องราง {boardCards} ใบ และอาคม {incantations} ใบ ลองใช้ให้ครบในเทิร์นนี้\n" +
            "ตอนนี้เป็นเฟสลงบริวาร / เครื่องราง: คลิกการ์ดบริวารหรือเครื่องรางในมือเพื่อหยิบ");
        public Hint board = new Hint("กระดานของคุณ",
            "หยิบการ์ดแล้ว คลิกช่องว่างที่สว่างขึ้นในแถวนี้เพื่อวาง\n" +
            "คลิกขวาหรือกด Esc เพื่อยกเลิกการ์ดที่เลือก (ก่อนวางลงสนาม) การ์ดที่วางแล้วเอาคืนไม่ได้\nลงเสร็จแล้ว กด Space เพื่อจบเฟส");
        [Tooltip("{first} = Merit on turn 1, {max} = most Merit")]
        public Hint merit = new Hint("กุศล",
            "การ์ดสายขาวใช้กุศล (ตัวเลขมุมซ้ายบนของการ์ด)\n" +
            "เทิร์นแรกมีกุศล {first} แต้ม เพิ่มเทิร์นละ 1 แต้ม สูงสุด {max} แต้ม\n" +
            "กุศลที่ไม่ได้ใช้จะไม่สะสมไปเทิร์นหน้า");
        public Hint spell = new Hint("ร่ายอาคม",
            "เฟสนี้ใช้การ์ดอาคม: คลิกการ์ด แล้วคลิกการ์ดเป้าหมาย\n" +
            "อาคมที่ไม่มีเป้าหมาย คลิกตรงไหนก็ได้เพื่อร่าย\nร่ายเสร็จแล้ว กด Space");
        public Hint clash = new Hint("การ์ดตีกัน",
            "บริวารตีกันทีละคอลัมน์ จากซ้ายไปขวา\n" +
            "บริวารจะตีบริวารที่อยู่ช่องตรงข้าม ถ้าช่องตรงข้ามว่าง จะตีขวัญของฝ่ายตรงข้ามโดยตรง");

        [Header("Turn 2")]
        public Hint drawPile = new Hint("กองจั่ว",
            "การ์ดที่ยังไม่ได้จั่วอยู่ที่นี่ ทุกต้นเทิร์นจะจั่วการ์ดจากกองนี้");
        public Hint graveyard = new Hint("หลุมศพ",
            "การ์ดที่ใช้ไปหรือถูกทำลายของทั้งสองฝ่ายมาอยู่ที่นี่ คลิกเพื่อดู");
        public Hint centerPit = new Hint("หลุมกลาง",
            "ทุกเทิร์นจะมีการ์ด 4 ใบเปิดหน้าขึ้นมาจากหลุมกลาง คุณกับศัตรูได้เลือกคนละ 1 ใบ\n" +
            "ฝ่ายที่ได้เลือกก่อนสลับกันทุกเทิร์น ใบที่เหลือจะถูกทิ้ง");
        public Hint inspect = new Hint("ดูการ์ด",
            "คลิกขวาที่การ์ดใบไหนก็ได้เพื่อดูรายละเอียด\nกด C เพื่อดูกระดานจากมุมบน");

        [Header("Corruption")]
        [Tooltip("{threshold} = Corruption that sets off the curse, {khwanLoss} = khwan lost when it does")]
        public Hint corruption = new Hint("มลทิน",
            "การ์ดสายดำไม่ใช้กุศล แต่เพิ่มมลทิน (ตัวเลขมุมซ้ายบนของการ์ดสายดำ)\n" +
            "ถ้ามลทินถึง {threshold} คำสาปจะย้อนเข้าตัว: เสียขวัญ {khwanLoss} (ไม่ต่ำกว่า 1 แต่ถ้าเหลือ 1 อยู่แล้วจะแพ้) แล้วมลทินส่วนที่เกินจะยกไปนับต่อ");
        [Tooltip("{khwanLoss} = khwan lost")]
        public Hint backfire = new Hint("คำสาปย้อน!",
            "มลทินเต็มแล้ว คุณจึงเสียขวัญ {khwanLoss}\nถ้าขวัญเหลือ 1 แล้วมลทินเต็มอีก จะแพ้ทันที");

        [Header("End of the fight")]
        public Hint won = new Hint("ชนะฝันร้าย", "เงาดำสลายไป... ขวัญกำลังจะตื่น");
        public Hint lost = new Hint("ขวัญแตกในฝัน", "ไม่เป็นไร นี่เป็นแค่ฝัน... ขวัญกำลังจะตื่น");

        [Header("Buttons and prompts")]
        public string nextPrompt = "คลิกหรือกด Enter เพื่อไปต่อ";
        public string skipButton = "ข้ามบทสอน";

        [Header("Skip question")]
        public string skipQuestion = "ต้องการข้ามบทสอนเล่นไหม?";
        [TextArea(2, 4)] public string skipQuestionBody = "ขวัญจะตื่นจากฝันทันที และเรื่องจะดำเนินต่อ\nบทสอนนี้จะไม่กลับมาอีกใน run นี้";
        public string keepPlayingButton = "เล่นบทสอนต่อ (Esc)";
        public string confirmSkipButton = "ข้ามบทสอน";

        private static TutorialTextSO cached;

        public static TutorialTextSO Current
        {
            get
            {
                if (cached == null) cached = Resources.Load<TutorialTextSO>(ResourceName);
                if (cached == null)
                {
                    cached = CreateInstance<TutorialTextSO>();
                    cached.hideFlags = HideFlags.HideAndDontSave;
                }
                return cached;
            }
        }

        public static void ResetCache()
        {
            cached = null;
        }
    }
}
