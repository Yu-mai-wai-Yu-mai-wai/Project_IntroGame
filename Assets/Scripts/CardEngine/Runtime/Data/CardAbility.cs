using System;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // When an ability fires. Passive abilities never fire: they are keywords read while fighting.
    public enum AbilityTrigger
    {
        [InspectorName("เมื่อลงการ์ด")] OnPlay,         // เมื่อใช้การ์ด (อาคมร่ายทันที / เครื่องราง-บริวารลงสนาม)
        [InspectorName("เมื่อเริ่มเทิร์น (อยู่บนสนาม)")] OnTurnStart,    // เมื่อเริ่มเทิร์นของเจ้าของ (ขณะการ์ดอยู่บนสนาม)
        [InspectorName("เมื่อจบรอบ (อยู่บนสนาม)")] OnRoundEnd,     // เมื่อจบรอบ (ขณะการ์ดอยู่บนสนาม)
        [InspectorName("เมื่อถูกทำลาย")] OnDeath,        // เมื่อการ์ดใบนี้ถูกทำลาย
        [InspectorName("เมื่อสังหารศัตรูได้")] OnKill,         // เมื่อการ์ดใบนี้สังหารการ์ดฝ่ายตรงข้ามได้
        [InspectorName("คำสำคัญติดตัว (Passive)")] Passive,        // คำสำคัญที่มีผลตลอดเวลา
        [InspectorName("ขณะอยู่บนสนาม (ออร่า)")] Aura            // มีผลเฉพาะตอนการ์ดอยู่บนสนาม การ์ดพัง/ออกจากสนามแล้วผลหายไป (เช่น +1 ขวัญ -> -1 ขวัญคืน)
    }

    public enum AbilityTarget
    {
        [InspectorName("ตัวเอง")] Self,
        [InspectorName("การ์ดฝั่งเรา 1 ใบ (ผู้เล่นเลือก)")] FriendlyCard,           // การ์ดฝั่งเรา 1 ใบ (ระบบเลือกให้อัตโนมัติ)
        [InspectorName("การ์ดฝั่งเราทั้งหมด")] AllFriendly,
        [InspectorName("การ์ดฝั่งเราทั้งหมด ยกเว้นตัวเอง")] AllFriendlyExceptSelf,
        [InspectorName("การ์ดฝั่งตรงข้าม 1 ใบ (ผู้เล่นเลือก)")] EnemyCard,              // การ์ดฝั่งตรงข้าม 1 ใบ (ระบบเลือกให้อัตโนมัติ)
        [InspectorName("การ์ดฝั่งตรงข้ามทั้งหมด")] AllEnemy,
        [InspectorName("บริวารฝั่งตรงข้ามทั้งหมด")] AllEnemyFamiliars,
        [InspectorName("การ์ดซ้าย-ขวาของใบนี้")] Adjacent,               // การ์ดซ้าย-ขวาของใบนี้
        [InspectorName("การ์ดช่องตรงข้าม")] Opposite,               // การ์ดฝั่งตรงข้ามในช่องเดียวกัน
        [InspectorName("ผู้ที่สังหารใบนี้")] Killer                  // การ์ดที่สังหารใบนี้ (ใช้กับ OnDeath)
    }

    public enum AbilityEffect
    {
        // --- Card effects (use the ability's target) ---
        [InspectorName("เพิ่มขวัญ (ขวัญสูงสุด)")] BuffKhwan,              // เพิ่มขวัญสูงสุดและขวัญปัจจุบัน +value
        [InspectorName("ฟื้นฟูขวัญ")] HealKhwan,              // ฟื้นฟูขวัญ +value (ไม่เกินขวัญสูงสุด)
        [InspectorName("ให้เกราะ")] GrantArmor,             // เกราะ +value (ดูดซับดาเมจก่อนขวัญ)
        [InspectorName("เพิ่ม/ลด สะเทือนขวัญ")] BuffAttack,             // สะเทือนขวัญถาวร +value (ติดลบ = ลด)
        [InspectorName("ทำดาเมจ")] DamageCard,             // ทำดาเมจ value (ignoreArmor = ไม่สนเกราะ)
        [InspectorName("ทำลายการ์ดที่ขวัญน้อยกว่า ค่า")] DestroyBelowKhwan,      // ทำลายการ์ดที่ขวัญน้อยกว่า value (ไม่สนเกราะ)
        [InspectorName("มอบสถานะ")] ApplyStatus,            // มอบสถานะ status จำนวน value หน่วย นาน duration เทิร์น
        [InspectorName("ล้างอัปมงคลล่าสุด")] CleanseLatest,          // ล้างสถานะอัปมงคลล่าสุด 1 หน่วย
        [InspectorName("ล้างอัปมงคลทั้งหมด")] CleanseAll,             // ล้างสถานะอัปมงคลทั้งหมด

        // --- Side / hand effects (target ignored) ---
        [InspectorName("กลับอัปมงคล/สิริมงคล")] FlipOmens,              // กลับอัปมงคลเป็นสิริมงคล (ฝั่งเรา) หรือสิริมงคลเป็นอัปมงคล (ฝั่งตรงข้าม)
        [InspectorName("ชำระล้าง (กันอัปมงคล)")] Purify,                 // ชำระล้าง: กันสถานะอัปมงคลของผู้เล่น value ครั้ง
        [InspectorName("จั่วการ์ด")] DrawCards,              // จั่วการ์ด value ใบ
        [InspectorName("ได้กุศล")] GainMerit,              // ได้กุศล value (ไม่เกินค่ากุศลสูงสุด)
        [InspectorName("สุ่มบริวารขึ้นมือ")] SummonRandomFamiliar,   // สุ่มบริวารระดับทั่วไปขึ้นมือ
        [InspectorName("สุ่มการ์ดจากกองสุสานขึ้นมือ")] ReturnFromGraveyard,    // สุ่มการ์ดจากกองสุสานขึ้นมือ
        [InspectorName("ได้การ์ด (Related Card) ขึ้นมือ")] GiveCardToHand,         // ได้การ์ด relatedCard ขึ้นมือ
        [InspectorName("สายสิญจน์ (2 ใบขึ้นไป)")] ThreadFormation,        // ถ้ามีการ์ดชื่อเดียวกันบนสนามตั้งแต่ 2 ใบ: การ์ดฝั่งเรา +value ขวัญสูงสุด/สะเทือนขวัญ (ครั้งเดียวต่อใบ) และอายุขลังเหลือ duration
        [InspectorName("ทำลายการ์ดช่องตรงข้าม")] DetonateWithOpposite,   // ระเบิดตัวเอง ทำลายการ์ดตรงข้ามในช่องเดียวกัน

        // --- Passive keywords (trigger = Passive) ---
        [InspectorName("ยั่วยุ")] Taunt,                  // ยั่วยุ: ศัตรูต้องโจมตีใบนี้ก่อน
        [InspectorName("ตีข้ามหัว")] Overhead,               // ตีข้ามหัว: โจมตีข้ามการ์ดตรงหน้าไปที่ผู้เล่นโดยตรง
        [InspectorName("รับดาเมจตีข้ามหัว")] OverheadMagnet,         // รับดาเมจตีข้ามหัวแทนผู้เล่น
        [InspectorName("ตีหลายใบพร้อมกัน")] MultiStrike,            // โจมตีการ์ดด้านหน้าพร้อมกัน value ใบ
        [InspectorName("การ์ดข้างๆ ได้สะเทือนขวัญ")] AdjacentAttackAura,     // การ์ดที่อยู่ติดกันได้สะเทือนขวัญ +value

        // --- Added later: kept at the end so saved card assets keep their effect numbers ---
        [InspectorName("เพิ่มมลทินสูงสุด")] RaiseCorruptionCap,     // เพิ่มมลทินสูงสุดของเจ้าของ +value (ตลอดการต่อสู้)
        [InspectorName("สะท้อนดาเมจ")] ReflectDamage,          // Passive: สะท้อนดาเมจที่ตีเข้ามาใส่ผู้โจมตีเท่ากัน
        [InspectorName("เพิ่มอัตราคริ (ค่า x 0.1%)")] CritChanceBonus         // Passive: บริวารฝั่งเราได้อัตราคริเพิ่ม value หน่วยละ 0.1% (25 = 2.5%)
    }

    [Serializable]
    public class CardAbility
    {
        public AbilityTrigger trigger = AbilityTrigger.OnPlay;
        public AbilityEffect effect = AbilityEffect.HealKhwan;
        public AbilityTarget target = AbilityTarget.Self;
        public CardStatusType status = CardStatusType.Phawa;
        public int value = 1;
        public int duration = 0;
        public bool ignoreArmor = false;
        [Tooltip("ใช้กับ GiveCardToHand")]
        public CardDataSO relatedCard;
    }
}
