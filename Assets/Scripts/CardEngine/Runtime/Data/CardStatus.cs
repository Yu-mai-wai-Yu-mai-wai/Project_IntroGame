using System;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Statuses that sit on a single card on the board (the player/enemy-wide ones are StatusEffectType).
    // อัปมงคล = ผีบังตา and ผวา; สิริมงคล = ยั่วยุ and เกราะคุ้มภัย. The other values are not given by any
    // card; they stay so saved assets keep their numbers.
    public enum CardStatusType
    {
        [InspectorName("ผวา")] Phawa,          // อัปมงคล ผวา: สะเทือนขวัญลด 1 ต่อหน่วย
        [InspectorName("ผีอำ")] PhiAm,          // โจมตีไม่ได้
        [InspectorName("สาป")] DoneKhong,        // สาป (เดิม โดนของ): จบรอบเสียขวัญเท่าจำนวนหน่วย แล้วลดลง 1 หน่วย (ไม่สนเกราะ)
        [InspectorName("ไฟแตก")] FireBreak,      // เสียขวัญ 2 ต่อหน่วยทุกรอบ (ไม่สนเกราะ)
        [InspectorName("ผีบังตา")] Blinded,        // อัปมงคล ผีบังตา: ความสามารถของการ์ดใช้ไม่ได้ (ทริกเกอร์ คำสำคัญ ออร่า) ตีได้ปกติ
        [InspectorName("กรรมตามสนอง")] Karma,          // ดาเมจที่ทำได้สะท้อนกลับใส่ตัวเองเท่ากัน
        [InspectorName("คุ้มกัน")] Taunt,         // สิริมงคล คุ้มกัน (เดิม ยั่วยุ): ศัตรูต้องโจมตีใบนี้ก่อน
        [InspectorName("ฮึกเหิม")] Might,          // สะเทือนขวัญ +1 ต่อหน่วย
        [InspectorName("เกราะคุ้มภัย")] Protect,        // สิริมงคล เกราะคุ้มภัย: กันดาเมจได้ทั้งก้อน 1 ครั้งต่อหน่วย
        [InspectorName("ฟื้นขวัญ")] Vital           // ฟื้นขวัญ 1 ต่อหน่วยทุกรอบ
    }

    [Serializable]
    public class CardStatus
    {
        public CardStatusType type;
        public int stacks;
        public int duration;
        // Given by an aura (ตุ๊กตาคุณไสย): it lasts while the aura's card is on the board, so it does not tick
        // down and is not cleansed or flipped
        [NonSerialized] public bool fromAura;

        private static readonly CardStatusType[] Debuffs = { CardStatusType.Blinded, CardStatusType.Phawa };
        // Bad statuses a cleanse or an immunity covers: the omens plus สาป and ผีอำ (sheet 7)
        private static readonly CardStatusType[] Harmful = { CardStatusType.Blinded, CardStatusType.Phawa, CardStatusType.DoneKhong, CardStatusType.PhiAm };

        public static bool IsHarmful(CardStatusType type) => Array.IndexOf(Harmful, type) >= 0;
        private static readonly CardStatusType[] Blessings = { CardStatusType.Taunt, CardStatusType.Protect };

        public CardStatus(CardStatusType type, int stacks, int duration)
        {
            this.type = type;
            this.stacks = stacks;
            this.duration = duration;
        }

        // อัปมงคล
        public static bool IsDebuff(CardStatusType type)
        {
            return Array.IndexOf(Debuffs, type) >= 0;
        }

        // สิริมงคล
        public static bool IsBlessing(CardStatusType type)
        {
            return Array.IndexOf(Blessings, type) >= 0;
        }

        // ผ้ายันต์กลับด้าน: each flipped status becomes a random one from the other group
        public static CardStatusType RandomBlessing()
        {
            return Blessings[UnityEngine.Random.Range(0, Blessings.Length)];
        }

        public static CardStatusType RandomDebuff()
        {
            return Debuffs[UnityEngine.Random.Range(0, Debuffs.Length)];
        }
    }
}
