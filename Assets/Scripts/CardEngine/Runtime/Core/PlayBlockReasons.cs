namespace TawanOS.CardEngine
{
    /// <summary>
    /// The Thai text shown to the player when a card cannot be played (plan task H1). Pure functions with no
    /// scene dependencies so the wording and the phase rules can be tested on their own.
    /// </summary>
    public static class PlayBlockReasons
    {
        public const string NoTarget = "ไม่มีเป้าหมายให้ใช้อาคมนี้";
        public const string BoardFull = "กระดานเต็ม ลงบริวารหรือเครื่องรางเพิ่มไม่ได้";
        public const string HandFull = "มือเต็ม จั่วการ์ดเพิ่มไม่ได้";

        /// <summary>Null when a card of this type may be played in this phase, otherwise the reason.</summary>
        public static string ForPhase(TurnPhase phase, CardType type)
        {
            bool isBoardCard = type == CardType.Familiar || type == CardType.Amulet;

            switch (phase)
            {
                case TurnPhase.PlayerBoard:
                    return isBoardCard ? null : "อาคมใช้ได้ในเฟสร่ายอาคม กด Space เพื่อไปเฟสนั้น";
                case TurnPhase.PlayerSpell:
                    return isBoardCard ? "เฟสนี้ร่ายอาคมได้อย่างเดียว บริวารและเครื่องรางลงได้ในเฟสก่อนหน้าเท่านั้น" : null;
                case TurnPhase.EnemyBoard:
                case TurnPhase.EnemySpell:
                    return "ตอนนี้เป็นตาของศัตรู รอให้ถึงตาคุณ";
                case TurnPhase.Clash:
                    return "การ์ดกำลังตีกัน รอให้จบก่อน";
                default:
                    return "กำลังเปลี่ยนเทิร์น รอสักครู่";
            }
        }

        public static string NotEnoughMerit(int cost, int have)
        {
            return $"กุศลไม่พอ ต้องใช้ {cost} แต่มี {have}";
        }

        public static string OwnerLabel(TurnPhase phase)
        {
            switch (phase)
            {
                case TurnPhase.PlayerBoard:
                case TurnPhase.PlayerSpell:
                    return "ตาของคุณ";
                case TurnPhase.EnemyBoard:
                case TurnPhase.EnemySpell:
                    return "ตาของศัตรู";
                case TurnPhase.Clash:
                    return "การ์ดตีกัน";
                case TurnPhase.Draw:
                    return "จั่วการ์ด";
                case TurnPhase.End:
                    return "จบเทิร์น";
                default:
                    return "";
            }
        }

        /// <summary>What the player should do in this phase, empty when there is nothing to do.</summary>
        public static string HintFor(TurnPhase phase)
        {
            switch (phase)
            {
                case TurnPhase.PlayerBoard:
                    return "ลากการ์ดลงช่อง · กด Space เมื่อลงเสร็จ";
                case TurnPhase.PlayerSpell:
                    return "ลากการ์ดอาคมไปใช้ · กด Space เพื่อจบเทิร์น";
                case TurnPhase.EnemyBoard:
                case TurnPhase.EnemySpell:
                    return "รอศัตรู ยังเล่นการ์ดไม่ได้";
                default:
                    return "";
            }
        }
    }
}
