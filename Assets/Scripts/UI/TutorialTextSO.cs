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
            "คลิกขวาหรือกด Esc เพื่อยกเลิกการ์ดที่เลือก (ก่อนวางลงสนาม) การ์ดที่วางแล้วเอาคืนไม่ได้\nลงเสร็จแล้ว กดปุ่มจบช่วงเพื่อจบเฟส");
        [Tooltip("{first} = Merit on turn 1, {max} = most Merit")]
        public Hint merit = new Hint("กุศล",
            "การ์ดสายขาวใช้กุศล (ตัวเลขมุมซ้ายบนของการ์ด)\n" +
            "เทิร์นแรกมีกุศล {first} แต้ม เพิ่มเทิร์นละ 1 แต้ม สูงสุด {max} แต้ม\n" +
            "กุศลที่ไม่ได้ใช้จะไม่สะสมไปเทิร์นหน้า");
        public Hint spell = new Hint("ร่ายอาคม",
            "เฟสนี้ใช้การ์ดอาคม: คลิกการ์ด แล้วคลิกการ์ดเป้าหมาย\n" +
            "อาคมที่ไม่มีเป้าหมาย คลิกตรงไหนก็ได้เพื่อร่าย\nร่ายเสร็จแล้ว กดปุ่มจบเทิร์น");
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
            "คลิกขวาที่การ์ดใบไหนก็ได้เพื่อดูรายละเอียด\nกด W เพื่อดูกระดานจากมุมบน กด S เพื่อกลับมุมปกติ");

        [Header("Corruption")]
        [Tooltip("{threshold} = Corruption that sets off the curse, {khwanLoss} = khwan lost when it does")]
        public Hint corruption = new Hint("มลทิน",
            "การ์ดสายดำไม่ใช้กุศล แต่เพิ่มมลทิน (ตัวเลขมุมซ้ายบนของการ์ดสายดำ)\n" +
            "ถ้ามลทินถึง {threshold} คำสาปจะย้อนเข้าตัว: เสียขวัญ {khwanLoss} (ไม่ต่ำกว่า 1 แต่ถ้าเหลือ 1 อยู่แล้วจะแพ้) แล้วมลทินส่วนที่เกินจะยกไปนับต่อ");
        [Tooltip("{khwanLoss} = khwan lost")]
        public Hint backfire = new Hint("คำสาปย้อน!",
            "มลทินเต็มแล้ว คุณจึงเสียขวัญ {khwanLoss}\nถ้าขวัญเหลือ 1 แล้วมลทินเต็มอีก จะแพ้ทันที");

        [Header("Interactive tutorial (Game Designer, 25 Oct) - explanations")]
        public Hint tHand = new Hint("การ์ดในมือ",
            "นี่คือการ์ด 4 ใบแรกของคุณ\nกุมารทอง ตะกรุด พรายน้ำ และคาถาสาปแช่ง\nมาดูทีละส่วนกัน");
        [Tooltip("{cost} = the card's merit cost, {merit} = the player's merit now")]
        public Hint tCostMerit = new Hint("กุศล",
            "มุมซ้ายบนของการ์ดคือกุศลที่ต้องใช้ กุมารทองใช้ {cost}\nเทียบกับกุศลที่คุณมีตรงนี้ ({merit}) ถ้าพอก็ลงการ์ดได้");
        public Hint tAttackKhwan = new Hint("สะเทือนขวัญ และขวัญ",
            "ซ้ายคือสะเทือนขวัญ ดาเมจที่บริวารทำเวลาตี\nขวาคือขวัญ เลือดของบริวาร ถ้าเหลือ 0 บริวารจะตาย");
        public Hint tEnemyShield = new Hint("ศัตรูร่ายคาถากันภัย",
            "บริวารที่ได้คาถากันภัยจะกันดาเมจครั้งถัดไป\nและได้คุ้มกัน บริวารของคุณทุกตัวต้องตีมันก่อน ตีเข้าตัวศัตรูไม่ได้");
        [Tooltip("{cost} = the card's merit cost")]
        public Hint tCost = new Hint("ค่ากุศล",
            "มุมซ้ายบนคือกุศลที่ต้องใช้ลงการ์ดใบนี้\nกุมารทองใช้กุศล {cost}");
        [Tooltip("{merit} = the player's merit now")]
        public Hint tMerit = new Hint("กุศลของคุณ",
            "เทียบกับกุศลที่คุณมีตรงนี้ ถ้ากุศลพอก็ลงการ์ดได้\nตอนนี้คุณมีกุศล {merit}");
        public Hint tAttack = new Hint("สะเทือนขวัญ",
            "ตัวเลขนี้คือสะเทือนขวัญ\nดาเมจที่บริวารทำเวลาตี");
        public Hint tKhwan = new Hint("ขวัญของบริวาร",
            "ตัวเลขนี้คือขวัญ เลือดของบริวาร\nถ้าเหลือ 0 บริวารจะตาย");
        public Hint tSkill = new Hint("สกิลของการ์ด",
            "ข้างล่างคือสกิลของการ์ด\nคำที่ขีดเส้นใต้ เอาเมาส์ชี้เพื่อดูความหมาย\nคลิกขวาที่การ์ดเพื่อเปิดตำราไสยศาสตร์ ดูรายละเอียดเต็ม");
        public Hint tBoardTurn = new Hint("เทิร์นอัญเชิญ",
            "ตอนนี้เป็นเทิร์นอัญเชิญ วางบริวารและเครื่องรางได้\nศัตรูลงกุมารทองมาแล้ว 1 ตัว");
        public Hint tSlots = new Hint("ช่องบนกระดาน",
            "บริวารวางได้ 4 ช่อง\nช่องซ้ายสุดเป็นช่องของเครื่องราง");
        public Hint tLane = new Hint("เลนเดียวกัน",
            "บริวารจะตีตัวที่อยู่ตรงข้ามในเลนเดียวกัน\nกุมารทองของคุณกับของศัตรูจะตีกันเอง");
        public Hint tAmulet = new Hint("เครื่องราง",
            "เครื่องรางช่วยทั้งกระดาน\nตะกรุดทำให้บริวารของคุณทุกตัวตี +1");
        [Tooltip("{threshold} = corruption limit, {khwanLoss} = khwan lost on a backfire")]
        public Hint tCorruption = new Hint("มลทิน",
            "การ์ดสายมนต์ดำไม่ใช้กุศล แต่เพิ่มมลทิน\nถ้ามลทินถึง {threshold} จะโดนคำสาปย้อน เสียขวัญ {khwanLoss}");
        public Hint tEnemySpell = new Hint("เทิร์นท่องคาถา",
            "ตอนนี้เป็นเทิร์นท่องคาถา ทั้งคุณและศัตรูใช้การ์ดอาคมได้\nลองใช้คาถาสาปแช่งใส่กุมารทองของศัตรู");
        public Hint tYourSpell = new Hint("ถึงตาคุณร่ายอาคม",
            "ระหว่างเทิร์นท่องคาถา คุณใช้การ์ดอาคมได้\nลองใช้คาถาสาปแช่งใส่กุมารทองของศัตรู");
        public Hint tCursed = new Hint("ติดสาป",
            "กุมารทองของศัตรูติดสาปแล้ว ดูสัญลักษณ์บนการ์ด\nคลิกขวาที่การ์ดเพื่ออ่านว่าสาปทำอะไร");
        public Hint tClash = new Hint("ต่อสู้",
            "จบเทิร์นแล้ว บริวารทั้งสองฝั่งจะตีกันทีละเลน");
        public Hint tFamiliarHit = new Hint("บริวารตีกัน",
            "บริวารที่อยู่ตรงข้ามกันตีกันเอง\nขวัญของบริวารลดลงตามสะเทือนขวัญของอีกฝั่ง");
        public Hint tFaceHit = new Hint("ตีเข้าศัตรู",
            "ถ้าไม่มีตัวไหนบังหน้า\nดาเมจจะเข้าขวัญของศัตรูโดยตรง");
        public Hint tFaceBlocked = new Hint("ตีเข้าศัตรู",
            "ถ้าเลนไหนไม่มีตัวบังหน้า ดาเมจจะเข้าขวัญของศัตรูโดยตรง\nแต่เทิร์นนี้กุมารทองของศัตรูได้คุ้มกันจากคาถากันภัย\nบริวารของคุณจึงต้องตีมันก่อน");
        public Hint tPit = new Hint("กองกลาง",
            "ทุกเทิร์นจะมีการ์ด 4 ใบในกองกลาง\nคุณกับศัตรูผลัดกันหยิบคนละ 1 ใบ ใบที่เหลือจะถูกทิ้ง\n\nตอนซูม กด W / S เลื่อนไปดูแถวศัตรู / แถวคุณได้ กด Space อีกครั้งเพื่อออกจากซูม");
        public Hint tSummary = new Hint("สรุปปุ่ม",
            "ทุกเทิร์น: กองกลาง → อัญเชิญ (วางบริวาร / เครื่องราง) → ท่องคาถา (ใช้อาคม) → ต่อสู้\n\nW / S : มองจากด้านบน / กลับมุมปกติ\nSpace (ตอนอยู่ด้านบน) : ซูม แล้วใช้ W / S เลื่อนแถว\nคลิกขวาที่การ์ด : เปิดตำราไสยศาสตร์\nปุ่มจบช่วง / จบเทิร์น : ไปช่วงต่อไป\n\nทำให้ขวัญของเงาดำเหลือ 0 ลองเล่นเองจนจบฝันนี้");

        [Header("Interactive tutorial - things to do (the line at the top of the screen)")]
        public string doHoverKuman = "เอาเมาส์ชี้ที่การ์ด กุมารทอง";
        public string doPlaceKuman = "คลิกกุมารทอง แล้ววางในช่องที่ตรงกับกุมารทองของศัตรู";
        public string doPlaceTakrut = "คลิกตะกรุด แล้ววางในช่องเครื่องรางทางซ้าย";
        public string doPlacePrai = "ลองลงพรายน้ำในช่องว่าง";
        public string doEndBoard = "กดปุ่มจบช่วงลงบริวาร";
        public string doCast = "คลิกคาถาสาปแช่ง แล้วเลือกกุมารทองของศัตรู";
        public string doUp = "กด W เพื่อมองกระดานจากด้านบน";
        public string doDown = "กด S เพื่อกลับมุมปกติ";
        public string doUpAgain = "กด W อีกครั้ง";
        public string doZoom = "กด Space เพื่อซูมแถวของคุณ";
        public string doZoomEnemy = "กด W เพื่อเลื่อนไปดูแถวศัตรู";
        public string doZoomPlayer = "กด S เพื่อกลับมาแถวของคุณ";
        public string doUnzoom = "กด Space เพื่อออกจากซูม";
        public string doEndTurn = "กดปุ่มจบเทิร์น เพื่อเริ่มต่อสู้";
        public string doZoomPit = "กด Space เพื่อซูมกองกลาง";
        public string doPick = "คลิกเลือกการ์ด 1 ใบจากกองกลาง";
        public string nextButton = "ต่อไป";

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
