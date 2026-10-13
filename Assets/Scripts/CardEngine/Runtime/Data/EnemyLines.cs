using System;
using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    /// <summary>
    /// What a ghost says during a fight (TawanOS.UI.EnemySpeech), one list per moment; a line is picked at random.
    /// A list left empty uses the shared lines in <see cref="Defaults"/>.
    /// </summary>
    [Serializable]
    public class EnemyLines
    {
        public enum Moment { CombatStart, PlayCard, Hurt, PlayerHurt, LowKhwan, PhaseChange, EnemyWins, EnemyLoses }

        [Tooltip("ตอนเริ่มการต่อสู้")] public List<string> combatStart = new List<string>();
        [Tooltip("ตอนศัตรูลงการ์ด (พูดบางครั้ง)")] public List<string> playCard = new List<string>();
        [Tooltip("ตอนศัตรูโดนตี (พูดบางครั้ง)")] public List<string> hurt = new List<string>();
        [Tooltip("ตอนตีผู้เล่นโดน (พูดบางครั้ง)")] public List<string> playerHurt = new List<string>();
        [Tooltip("ครั้งแรกที่ขวัญศัตรูเหลือน้อย")] public List<string> lowKhwan = new List<string>();
        [Tooltip("ตอนเข้าเฟสถัดไป (บอส)")] public List<string> phaseChange = new List<string>();
        [Tooltip("ตอนศัตรูชนะ (ผู้เล่นแพ้)")] public List<string> enemyWins = new List<string>();
        [Tooltip("ตอนศัตรูแพ้")] public List<string> enemyLoses = new List<string>();

        public List<string> For(Moment moment)
        {
            switch (moment)
            {
                case Moment.CombatStart: return combatStart;
                case Moment.PlayCard: return playCard;
                case Moment.Hurt: return hurt;
                case Moment.PlayerHurt: return playerHurt;
                case Moment.LowKhwan: return lowKhwan;
                case Moment.PhaseChange: return phaseChange;
                case Moment.EnemyWins: return enemyWins;
                default: return enemyLoses;
            }
        }

        /// <summary>A line for this moment: from <paramref name="own"/> when it has any, else from the shared ones.</summary>
        public static string Pick(EnemyLines own, Moment moment)
        {
            var list = own != null ? own.For(moment) : null;
            if (list == null || list.FindAll(l => !string.IsNullOrWhiteSpace(l)).Count == 0) list = Defaults.For(moment);
            var usable = list.FindAll(l => !string.IsNullOrWhiteSpace(l));
            return usable.Count > 0 ? usable[UnityEngine.Random.Range(0, usable.Count)] : null;
        }

        /// <summary>Lines any ghost can say.</summary>
        public static readonly EnemyLines Defaults = new EnemyLines
        {
            combatStart = new List<string> { "มาถึงที่นี่ได้... ก็อย่าหวังว่าจะได้กลับ", "ขวัญของเอ็ง... ข้าขอนะ", "กลิ่นคนเป็น... หอมจริง ๆ" },
            playCard = new List<string> { "รับไปซะ!", "หึ หึ หึ...", "ดูนี่สิ...", "เจ็บกว่านี้อีกเยอะ" },
            hurt = new List<string> { "อ๊ากก...!", "แค่นี้น่ะหรือ", "เอ็งกล้าดียังไง!" },
            playerHurt = new List<string> { "ขวัญหายแล้วสิ...", "สั่นแล้วล่ะสิ", "อีกนิดเดียว..." },
            lowKhwan = new List<string> { "ไม่... ข้ายังไม่ไปไหนทั้งนั้น!", "เอ็งจะต้องชดใช้!" },
            phaseChange = new List<string> { "ตอนนี้แหละ... ของจริง!", "เอ็งปลุกข้าขึ้นมาแล้ว..." },
            enemyWins = new List<string> { "ขวัญของเอ็ง... เป็นของข้าแล้ว", "อยู่กับข้าตรงนี้ตลอดไปนะ..." },
            enemyLoses = new List<string> { "ข้า... จะกลับมา...", "ไม่... ไม่จริง..." },
        };
    }
}
