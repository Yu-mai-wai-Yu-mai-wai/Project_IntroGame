using System;

namespace TawanOS.CardEngine
{
    public enum MagicSchool
    {
        WhiteMagic,     // มนต์ขาว: ใช้ค่ากุศล (Merit)
        BlackMagic      // มนต์ดำ: ไร้ค่าร่ายกุศล แต่เพิ่มมลทิน (Corruption)
    }

    public enum CardType
    {
        Incantation,    // การ์ดอาคม: ส่งผลทันทีจากมือ
        Amulet,         // การ์ดเครื่องราง: วางลงช่องเครื่องราง ให้บัฟต่อเนื่อง
        Familiar        // การ์ดบริวาร: วางลงสนาม มี HP ขวัญ และ ATK สะเทือนขวัญ
    }

    public enum TargetType
    {
        Self,
        SingleEnemy,
        AllEnemies,
        FriendlyMinion,
        NoTarget
    }

    public enum CombatPhase
    {
        BattleInit,
        TurnStartDraw,
        PlayerTurn,
        PlayerCardResolving,
        EnemyIntentExecution,
        RoundEndStatusTick,
        Victory,
        Defeat
    }

    public enum EnemyIntent
    {
        Attack,
        HeavyAttack,
        Defend,
        DebuffCurse,
        BuffSelf,
        SummonMinion
    }

    public enum StatusEffectType
    {
        KhwanPhawa,     // ขวัญผวา (ลดพลังโจมตี)
        KhumPhai,       // คุ้มภัย (เกราะป้องกัน)
        MontSaThon,     // มนต์สะท้อน (สะท้อนดาเมจ)
        BleedingCurse   // โดนของ (เสีย HP ทุกเทิร์น)
    }
}
