using System;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // When an ability fires. Passive abilities never fire: they are keywords read while fighting.
    public enum AbilityTrigger
    {
        OnPlay,         // เมื่อใช้การ์ด (อาคมร่ายทันที / เครื่องราง-บริวารลงสนาม)
        OnTurnStart,    // เมื่อเริ่มเทิร์นของเจ้าของ (ขณะการ์ดอยู่บนสนาม)
        OnRoundEnd,     // เมื่อจบรอบ (ขณะการ์ดอยู่บนสนาม)
        OnDeath,        // เมื่อการ์ดใบนี้ถูกทำลาย
        OnKill,         // เมื่อการ์ดใบนี้สังหารการ์ดฝ่ายตรงข้ามได้
        Passive,        // คำสำคัญที่มีผลตลอดเวลา
        Aura            // มีผลเฉพาะตอนการ์ดอยู่บนสนาม การ์ดพัง/ออกจากสนามแล้วผลหายไป (เช่น +1 ขวัญ -> -1 ขวัญคืน)
    }

    public enum AbilityTarget
    {
        Self,
        FriendlyCard,           // การ์ดฝั่งเรา 1 ใบ (ระบบเลือกให้อัตโนมัติ)
        AllFriendly,
        AllFriendlyExceptSelf,
        EnemyCard,              // การ์ดฝั่งตรงข้าม 1 ใบ (ระบบเลือกให้อัตโนมัติ)
        AllEnemy,
        AllEnemyFamiliars,
        Adjacent,               // การ์ดซ้าย-ขวาของใบนี้
        Opposite,               // การ์ดฝั่งตรงข้ามในช่องเดียวกัน
        Killer                  // การ์ดที่สังหารใบนี้ (ใช้กับ OnDeath)
    }

    public enum AbilityEffect
    {
        // --- Card effects (use the ability's target) ---
        BuffKhwan,              // เพิ่มขวัญสูงสุดและขวัญปัจจุบัน +value
        HealKhwan,              // ฟื้นฟูขวัญ +value (ไม่เกินขวัญสูงสุด)
        GrantArmor,             // เกราะ +value (ดูดซับดาเมจก่อนขวัญ)
        BuffAttack,             // สะเทือนขวัญถาวร +value (ติดลบ = ลด)
        DamageCard,             // ทำดาเมจ value (ignoreArmor = ไม่สนเกราะ)
        DestroyBelowKhwan,      // ทำลายการ์ดที่ขวัญน้อยกว่า value (ไม่สนเกราะ)
        ApplyStatus,            // มอบสถานะ status จำนวน value หน่วย นาน duration เทิร์น
        CleanseLatest,          // ล้างสถานะอัปมงคลล่าสุด 1 หน่วย
        CleanseAll,             // ล้างสถานะอัปมงคลทั้งหมด

        // --- Side / hand effects (target ignored) ---
        FlipOmens,              // กลับอัปมงคลเป็นสิริมงคล (ฝั่งเรา) หรือสิริมงคลเป็นอัปมงคล (ฝั่งตรงข้าม)
        Purify,                 // ชำระล้าง: กันสถานะอัปมงคลของผู้เล่น value ครั้ง
        DrawCards,              // จั่วการ์ด value ใบ
        GainMerit,              // ได้กุศล value (ไม่เกินค่ากุศลสูงสุด)
        SummonRandomFamiliar,   // สุ่มบริวารระดับทั่วไปขึ้นมือ
        ReturnFromGraveyard,    // สุ่มการ์ดจากกองสุสานขึ้นมือ
        GiveCardToHand,         // ได้การ์ด relatedCard ขึ้นมือ
        ThreadFormation,        // ถ้ามีการ์ดชื่อเดียวกันบนสนามตั้งแต่ 2 ใบ: การ์ดฝั่งเรา +value ขวัญสูงสุด/สะเทือนขวัญ (ครั้งเดียวต่อใบ) และอายุขลังเหลือ duration
        DetonateWithOpposite,   // ระเบิดตัวเอง ทำลายการ์ดตรงข้ามในช่องเดียวกัน

        // --- Passive keywords (trigger = Passive) ---
        Taunt,                  // ยั่วยุ: ศัตรูต้องโจมตีใบนี้ก่อน
        Overhead,               // ตีข้ามหัว: โจมตีข้ามการ์ดตรงหน้าไปที่ผู้เล่นโดยตรง
        OverheadMagnet,         // รับดาเมจตีข้ามหัวแทนผู้เล่น
        MultiStrike,            // โจมตีการ์ดด้านหน้าพร้อมกัน value ใบ
        AdjacentAttackAura,     // การ์ดที่อยู่ติดกันได้สะเทือนขวัญ +value

        // --- Added later: kept at the end so saved card assets keep their effect numbers ---
        RaiseCorruptionCap,     // เพิ่มมลทินสูงสุดของเจ้าของ +value (ตลอดการต่อสู้)
        ReflectDamage,          // Passive: สะท้อนดาเมจที่ตีเข้ามาใส่ผู้โจมตีเท่ากัน
        CritChanceBonus         // Passive: บริวารฝั่งเราได้อัตราคริเพิ่ม value หน่วยละ 0.1% (25 = 2.5%)
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
