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
        [InspectorName("ขณะอยู่บนสนาม (ออร่า)")] Aura,           // มีผลเฉพาะตอนการ์ดอยู่บนสนาม การ์ดพัง/ออกจากสนามแล้วผลหายไป (เช่น +1 ขวัญ -> -1 ขวัญคืน)
        [InspectorName("เมื่อตีโดนการ์ด")] OnHit           // เมื่อบริวารใบนี้ตีโดนการ์ด (เป้าหมาย "การ์ดที่ถูกตี")
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
        [InspectorName("ผู้ที่สังหารใบนี้")] Killer,                 // การ์ดที่สังหารใบนี้ (ใช้กับ OnDeath)
        [InspectorName("การ์ดทุกใบบนสนาม (ทั้งสองฝั่ง)")] AllOnBoard,
        [InspectorName("การ์ดที่ถูกตี")] StruckCard,             // การ์ดที่ใบนี้เพิ่งตีโดน (ใช้กับ OnHit)
        [InspectorName("การ์ดใดก็ได้ 1 ใบ (ผู้เล่นเลือก)")] AnyCard                 // การ์ด 1 ใบจากฝั่งไหนก็ได้ (ระบบเลือกให้: สิริมงคลลงฝั่งเรา อัปมงคล/ดาเมจลงฝั่งตรงข้าม)
    }

    public enum AbilityEffect
    {
        // --- Card effects (use the ability's target) ---
        // "(ไม่ใช้แล้ว)": no card uses it since sheet 7 and the game ignores it; kept so saved assets keep their numbers
        [InspectorName("เพิ่มขวัญ (ขวัญสูงสุด)")] BuffKhwan,              // เพิ่มขวัญสูงสุดและขวัญปัจจุบัน +value
        [InspectorName("ฟื้นฟูขวัญ")] HealKhwan,              // ฟื้นฟูขวัญ +value (ไม่เกินขวัญสูงสุด)
        [InspectorName("(ไม่ใช้แล้ว) ให้เกราะ")] GrantArmor,             // เกราะ +value (ดูดซับดาเมจก่อนขวัญ)
        [InspectorName("เพิ่ม/ลด สะเทือนขวัญ")] BuffAttack,             // สะเทือนขวัญถาวร +value (ติดลบ = ลด)
        [InspectorName("ทำดาเมจ")] DamageCard,             // ทำดาเมจ value (ignoreArmor = ไม่สนเกราะ)
        [InspectorName("(ไม่ใช้แล้ว) ทำลายการ์ดที่ขวัญไม่เกิน ค่า")] DestroyBelowKhwan,      // ทำลายการ์ดที่ขวัญไม่เกิน value (ไม่สนเกราะ)
        [InspectorName("มอบสถานะ")] ApplyStatus,            // มอบสถานะ status จำนวน value หน่วย นาน duration เทิร์น
        [InspectorName("(ไม่ใช้แล้ว) ล้างอัปมงคลล่าสุด")] CleanseLatest,          // ล้างสถานะอัปมงคลล่าสุด 1 หน่วย
        [InspectorName("ล้างอัปมงคลทั้งหมด")] CleanseAll,             // ล้างสถานะอัปมงคลทั้งหมด

        // --- Side / hand effects (target ignored) ---
        [InspectorName("(ไม่ใช้แล้ว) กลับอัปมงคล/สิริมงคล")] FlipOmens,              // กลับอัปมงคลเป็นสิริมงคล (ฝั่งเรา) หรือสิริมงคลเป็นอัปมงคล (ฝั่งตรงข้าม)
        [InspectorName("(ไม่ใช้แล้ว) ชำระล้าง (กันอัปมงคล)")] Purify,                 // ชำระล้าง: กันสถานะอัปมงคลของผู้เล่น value ครั้ง
        [InspectorName("จั่วการ์ด")] DrawCards,              // จั่วการ์ด value ใบ
        [InspectorName("(ไม่ใช้แล้ว) ได้กุศล")] GainMerit,              // ได้กุศล value (ไม่เกินค่ากุศลสูงสุด)
        [InspectorName("(ไม่ใช้แล้ว) สุ่มบริวารขึ้นมือ")] SummonRandomFamiliar,   // สุ่มบริวารระดับทั่วไปขึ้นมือ
        [InspectorName("(ไม่ใช้แล้ว) สุ่มการ์ดจากกองสุสานขึ้นมือ")] ReturnFromGraveyard,    // สุ่มการ์ดจากกองสุสานขึ้นมือ
        [InspectorName("ได้การ์ด (Related Card) ขึ้นมือ")] GiveCardToHand,         // ได้การ์ด relatedCard ขึ้นมือ
        [InspectorName("(ไม่ใช้แล้ว) สายสิญจน์ (2 ใบขึ้นไป)")] ThreadFormation,        // ถ้ามีการ์ดชื่อเดียวกันบนสนามตั้งแต่ 2 ใบ: การ์ดฝั่งเรา +value ขวัญสูงสุด/สะเทือนขวัญ (ครั้งเดียวต่อใบ) และอายุขลังเหลือ duration
        [InspectorName("(ไม่ใช้แล้ว) ทำลายการ์ดช่องตรงข้าม")] DetonateWithOpposite,   // ระเบิดตัวเอง ทำลายการ์ดตรงข้ามในช่องเดียวกัน

        // --- Passive keywords (trigger = Passive) ---
        [InspectorName("คุ้มกัน")] Taunt,                 // คุ้มกัน (เดิม ยั่วยุ): ศัตรูต้องโจมตีใบนี้ก่อน
        [InspectorName("บิน")] Overhead,                  // บิน (เดิม ตีข้ามหัว): โจมตีข้ามการ์ดตรงหน้าไปที่ผู้เล่นโดยตรง
        [InspectorName("ดักบิน")] OverheadMagnet,          // ดักบิน: รับการโจมตีของบริวารบินแทนผู้เล่น
        [InspectorName("(ไม่ใช้แล้ว) ตีหลายใบพร้อมกัน")] MultiStrike,            // โจมตีการ์ดด้านหน้าพร้อมกัน value ใบ
        [InspectorName("การ์ดข้างๆ ได้สะเทือนขวัญ")] AdjacentAttackAura,     // การ์ดที่อยู่ติดกันได้สะเทือนขวัญ +value

        // --- Added later: kept at the end so saved card assets keep their effect numbers ---
        [InspectorName("(ไม่ใช้แล้ว) เพิ่มมลทินสูงสุด")] RaiseCorruptionCap,     // เพิ่มมลทินสูงสุดของเจ้าของ +value (ตลอดการต่อสู้)
        [InspectorName("(ไม่ใช้แล้ว) สะท้อนดาเมจ")] ReflectDamage,          // Passive: สะท้อนดาเมจที่ตีเข้ามาใส่ผู้โจมตีเท่ากัน
        [InspectorName("(ไม่ใช้แล้ว) เพิ่มอัตราคริ (ค่า x 0.1%)")] CritChanceBonus,        // Passive: บริวารฝั่งเราได้อัตราคริเพิ่ม value หน่วยละ 0.1% (25 = 2.5%)
        [InspectorName("(ไม่ใช้แล้ว) สุ่มบริวารมนต์ดำลงสนาม")] SummonToBoard,          // สร้างบริวารมนต์ดำแบบสุ่มบนช่องว่างของฝั่งเรา (ตีไม่ได้ในรอบนี้)

        // --- Sheet 7 (Narathorn, 21 Oct 2026). EffectResolver.Sheet7.cs. "ฮีล" = the owner's Khwan ---
        [InspectorName("ฮีล (ขวัญผู้เล่น)")] HealPlayer,             // ฮีลขวัญเจ้าของ value (ไม่เกินขวัญสูงสุด)
        [InspectorName("ฮีลต่อบริวารเรา 1 ตัว")] HealPerAlly,         // ฮีล value ต่อบริวารฝั่งเรา 1 ตัว
        [InspectorName("ทุกครั้งที่ฮีล: ใบนี้โจมตี")] AttackOnHeal,    // Passive: โจมตีทันทีเมื่อเจ้าของฮีล (value ครั้งต่อเทิร์น)
        [InspectorName("ทุกครั้งที่ฮีล: ศัตรูเสียขวัญ")] HealHurtsEnemy, // Passive: ศัตรูเสียขวัญเท่าที่ฮีล (สูงสุด value ต่อเทิร์น)
        [InspectorName("ฮีลทุกครั้ง +ค่า")] HealBonus,                // Passive: การฮีลของเจ้าของ +value
        [InspectorName("จบรอบ: ฮีลแล้วจั่ว / ไม่ฮีลก็ฮีล")] EndRoundHealOrDraw, // Passive: ถ้าเทิร์นนี้ฮีล จั่ว 1 ไม่งั้นฮีล value (duration 1 = ทั้งสองอย่าง)
        [InspectorName("บริวารเราตาย: ศัตรูเสียขวัญ")] AllyDeathHurtsEnemy, // Passive: ศัตรูเสียขวัญ value (ใช้ได้ duration ครั้ง แล้วพัง)
        [InspectorName("บริวารเราตัวแรกที่ตาย: เรียก Related ลงช่องนั้น")] AllyDeathSummon, // Passive: ครั้งละเทิร์น
        [InspectorName("บริวารเราตัวอื่นตาย: ใบนี้ตี +ค่า")] AllyDeathBuff, // Passive: ตี +value ขวัญ +duration ถาวร
        [InspectorName("สังเวย: ได้ตีของตัวนั้น")] SacrificeForAttack, // เมื่อลง: สังเวยบริวารเรา 1 ตัว ได้ตีเพิ่ม (value 1 = ได้ขวัญด้วย)
        [InspectorName("สังเวย: ฮีลตามขวัญ")] SacrificeForHeal,     // สังเวยบริวารเรา 1 ตัว ฮีลตามขวัญ (value 1 = ศัตรูเสียขวัญเท่ากันด้วย)
        [InspectorName("คืนชีพบริวารที่ตายล่าสุด")] ReviveLast,       // คืนชีพ value ตัว ฮีล duration ต่อตัว ตายอีกจะหายจากไฟต์
        [InspectorName("ส่งบริวารกลับขึ้นมือ (ติดสาป = ทำลาย)")] ReturnToHand,
        [InspectorName("ตีบริวารที่ติดสาปแรงขึ้น")] CursedBonusDamage, // Passive: +value (duration 1 = สองเท่า)
        [InspectorName("สาปบนบริวารศัตรูไม่ลดลง")] CurseNoDecay,      // Passive: value = ฮีลเมื่อบริวารศัตรูตายเพราะสาป
        [InspectorName("ย้ายสาปไปบริวารศัตรูช่องตรงข้าม")] MoveCurses,  // value = จั่วการ์ดเพิ่ม
        [InspectorName("บริวารลงฝั่งเรา: สาปบริวารศัตรูช่องตรงข้าม")] CurseOnAllyEnter, // Passive: สาป value
        [InspectorName("บริวารเราติดสาป/สถานะร้ายไม่ได้")] CurseImmunity, // Passive
        [InspectorName("บริวารเราจะตาย: เหลือขวัญ 1 แทน")] DeathSave,  // Passive: ใช้ได้ value ครั้ง แล้วพัง
        [InspectorName("เชื่อมเลน: ตัวหนึ่งตาย อีกตัวตายด้วย")] LinkLane, // เมื่อวาง: เลือกบริวาร 1 ตัว (value 1 = ฝั่งเราขึ้นมือแทนตาย)
        [InspectorName("ตี +ค่า ต่อมลทิน duration หน่วย")] AttackPerCorruption, // Aura
        [InspectorName("บริวารที่ขวัญสูงสุดถึง duration ตี +ค่า")] AttackIfSturdy, // Aura
        [InspectorName("ตี +ค่า ต่อบริวารเราตัวอื่น")] AttackPerAlly,  // Aura (ตัวเอง)
        [InspectorName("ทำลายเครื่องรางศัตรู")] DestroyEnemyAmulet,
        [InspectorName("บริวารบินของศัตรูบินข้ามไม่ได้")] BlockFlying,  // Passive
        [InspectorName("บริวารศัตรูตีบริวารเรา: ผู้ตีโดน ค่า")] Thorns, // Passive
        [InspectorName("ลงบริวารเราตัวอื่น: ใบนี้ขวัญ +ค่า")] GrowOnAllyPlayed, // Passive: ขวัญ +value ตี +duration
        [InspectorName("ทำดาเมจ ถ้าตายฮีล")] DamageHealOnKill,      // ดาเมจ value ถ้าตายฮีล duration
        [InspectorName("เรียก Related ลงช่องว่าง")] SummonRelated,     // เรียก relatedCard ลงช่องว่าง value ตัว
        [InspectorName("บริวารศัตรูที่ติดสาปตีไม่ได้เทิร์นนี้")] CursedCannotAttack,
        [InspectorName("ทะลวง: ดาเมจที่เกินเข้าขวัญศัตรู")] Pierce,       // Passive
        [InspectorName("จั่วการ์ด ใบที่ได้ลดราคา 1")] DrawCheaper         // จั่ว value ใบ แต่ละใบลดราคา 1 จนจบไฟต์ (ต่ำสุด 0)
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
