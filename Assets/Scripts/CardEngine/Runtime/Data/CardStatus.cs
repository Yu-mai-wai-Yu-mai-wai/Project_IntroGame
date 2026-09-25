using System;

namespace TawanOS.CardEngine
{
    // Statuses that sit on a single card on the board (the player/enemy-wide ones are StatusEffectType)
    public enum CardStatusType
    {
        // อัปมงคล (debuffs)
        Phawa,          // ผวา: สะเทือนขวัญลด 1 ต่อหน่วย
        PhiAm,          // ผีอำ: โจมตีไม่ได้
        DoneKhong,      // โดนของ: เสียขวัญทุกรอบเท่าจำนวนหน่วย แล้วลดลง 1 หน่วย (ไม่สนเกราะ)
        FireBreak,      // ไฟแตก: เสียขวัญ 2 ต่อหน่วยทุกรอบ (ไม่สนเกราะ)
        Blinded,        // ผีบังตา: โจมตีพลาด 50%
        Karma,          // กรรมตามสนอง: ดาเมจที่ทำได้สะท้อนกลับใส่ตัวเองเท่ากัน

        // เป็นกลาง
        Taunt,          // ยั่วยุ

        // สิริมงคล (blessings)
        Might,          // สะเทือนขวัญ +1 ต่อหน่วย
        Protect,        // คุ้มภัย: ดาเมจที่ได้รับลด 1 ต่อหน่วย
        Vital           // ฟื้นขวัญ 1 ต่อหน่วยทุกรอบ
    }

    [Serializable]
    public class CardStatus
    {
        public CardStatusType type;
        public int stacks;
        public int duration;

        public CardStatus(CardStatusType type, int stacks, int duration)
        {
            this.type = type;
            this.stacks = stacks;
            this.duration = duration;
        }

        public static bool IsDebuff(CardStatusType type)
        {
            return type == CardStatusType.Phawa || type == CardStatusType.PhiAm || type == CardStatusType.DoneKhong
                || type == CardStatusType.FireBreak || type == CardStatusType.Blinded || type == CardStatusType.Karma;
        }

        public static bool IsBlessing(CardStatusType type)
        {
            return type == CardStatusType.Might || type == CardStatusType.Protect || type == CardStatusType.Vital;
        }

        // ผ้ายันต์กลับด้าน: อัปมงคล -> สิริมงคล
        public static CardStatusType ToBlessing(CardStatusType debuff)
        {
            switch (debuff)
            {
                case CardStatusType.DoneKhong: return CardStatusType.Vital;
                case CardStatusType.FireBreak:
                case CardStatusType.Phawa: return CardStatusType.Might;
                default: return CardStatusType.Protect;
            }
        }

        // สิริมงคล -> อัปมงคล
        public static CardStatusType ToDebuff(CardStatusType blessing)
        {
            switch (blessing)
            {
                case CardStatusType.Vital: return CardStatusType.DoneKhong;
                case CardStatusType.Might: return CardStatusType.Phawa;
                default: return CardStatusType.PhiAm;
            }
        }
    }
}
