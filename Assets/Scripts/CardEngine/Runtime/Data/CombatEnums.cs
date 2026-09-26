using System;
using UnityEngine;

namespace TawanOS.CardEngine
{
    public enum MagicSchool
    {
        [InspectorName("มนต์ขาว")] WhiteMagic,     // มนต์ขาว: ใช้ค่ากุศล (Merit)
        [InspectorName("มนต์ดำ")] BlackMagic      // มนต์ดำ: ไร้ค่าร่ายกุศล แต่เพิ่มมลทิน (Corruption)
    }

    public enum CardType
    {
        [InspectorName("อาคม")] Incantation,    // การ์ดอาคม: ส่งผลทันทีจากมือ
        [InspectorName("เครื่องราง")] Amulet,         // การ์ดเครื่องราง: วางลงช่องเครื่องราง ให้บัฟต่อเนื่อง
        [InspectorName("บริวาร")] Familiar        // การ์ดบริวาร: วางลงสนาม มี HP ขวัญ และ ATK สะเทือนขวัญ
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

    // One turn: Draw -> player plays familiars/amulets -> enemy does the same -> player casts
    // incantations -> enemy does the same -> board clash -> End, then the next turn starts.
    public enum TurnPhase
    {
        None,
        Draw,
        PlayerBoard,    // ผู้เล่นลงบริวาร / เครื่องราง
        EnemyBoard,     // ศัตรูลงบริวาร / เครื่องราง
        PlayerSpell,    // ผู้เล่นร่ายอาคม
        EnemySpell,     // ศัตรูร่ายอาคม
        Clash,          // การ์ดตีกัน
        End
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

    // How an enemy that owns a card deck weighs its options while playing cards
    public enum EnemyCardPlayStyle
    {
        Balanced,
        Aggressive,     // prefers damage cards and familiars
        Defensive,      // prefers shield cards
        Random
    }

    public enum StatusEffectType
    {
        KhwanPhawa,     // ขวัญผวา (ลดพลังโจมตี)
        KhumPhai,       // คุ้มภัย (เกราะป้องกัน)
        MontSaThon,     // มนต์สะท้อน (สะท้อนดาเมจ)
        BleedingCurse   // โดนของ (เสีย HP ทุกเทิร์น)
    }
}
