using System;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Statuses that sit on a single card on the board (the player/enemy-wide ones are StatusEffectType)
    public enum CardStatusType
    {
        // อัปมงคล (debuffs)
        [InspectorName("ผวา")] Phawa,          // ผวา: สะเทือนขวัญลด 1 ต่อหน่วย
        [InspectorName("ผีอำ")] PhiAm,          // ผีอำ: โจมตีไม่ได้
        [InspectorName("โดนของ")] DoneKhong,      // โดนของ: เสียขวัญทุกรอบเท่าจำนวนหน่วย แล้วลดลง 1 หน่วย (ไม่สนเกราะ)
        [InspectorName("ไฟแตก")] FireBreak,      // ไฟแตก: เสียขวัญ 2 ต่อหน่วยทุกรอบ (ไม่สนเกราะ)
        [InspectorName("ผีบังตา")] Blinded,        // ผีบังตา: โจมตีพลาด 50%
        [InspectorName("กรรมตามสนอง")] Karma,          // กรรมตามสนอง: ดาเมจที่ทำได้สะท้อนกลับใส่ตัวเองเท่ากัน

        // เป็นกลาง
        [InspectorName("ยั่วยุ")] Taunt,          // ยั่วยุ

        // สิริมงคล (blessings)
        [InspectorName("ฮึกเหิม")] Might,          // สะเทือนขวัญ +1 ต่อหน่วย
        [InspectorName("คุ้มภัย")] Protect,        // คุ้มภัย: ดาเมจที่ได้รับลด 1 ต่อหน่วย
        [InspectorName("ฟื้นขวัญ")] Vital           // ฟื้นขวัญ 1 ต่อหน่วยทุกรอบ
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
