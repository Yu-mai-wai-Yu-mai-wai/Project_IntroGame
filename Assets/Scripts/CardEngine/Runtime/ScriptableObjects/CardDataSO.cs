using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    [CreateAssetMenu(fileName = "NewCardData", menuName = "TawanOS/CardEngine/Card Data")]
    public class CardDataSO : ScriptableObject
    {
        [Header("Identity")]
        public string cardId = "card_001";
        public string cardNameThai = "มีดหมอปราบมาร";
        public string cardNameEng = "Exorcist Knife";
        [TextArea(2, 4)]
        public string descriptionFormat = "สร้างความเสียหาย {0} หน่วย";

        [Header("Classification")]
        public MagicSchool magicSchool = MagicSchool.WhiteMagic;
        public CardType cardType = CardType.Incantation;
        public TargetType targetType = TargetType.SingleEnemy;
        [Tooltip("ข้อความระบุ 'ประเภท • รูปแบบ' เช่น 'อาคม • โจมตี' (หากเว้นว่าง ระบบจะสร้างอัตโนมัติ)")]
        public string customTypeText = "";

        [Header("Costs & Thresholds")]
        [Range(0, 6)] public int meritCost = 1;
        [Range(0, 9)] public int corruptionGain = 0;

        [Header("Primary Combat Values")]
        public int baseValue = 8;
        public int durability = 3;
        public int familiarHealth = 10;
        public int familiarDamage = 4;

        [Header("Abilities")]
        [Tooltip("ความสามารถของการ์ด (ว่าง = การ์ดแบบเดิมที่ใช้ baseValue/targetType)")]
        public List<CardAbility> abilities = new List<CardAbility>();

        [Header("Visuals & Audio")]
        [Tooltip("ภาพพื้นหลังการ์ดเต็มใบที่ Art วาด (วางพื้นหลังได้เลย)")]
        public Sprite cardBackground;
        [Tooltip("รูปภาพประกอบการ์ดตรงกลาง")]
        public Sprite artwork;
        public Sprite frameBorder;
        public AudioClip sfxPlay;
        public GameObject vfxPrefab;

        public string GetFormattedTypeText()
        {
            if (!string.IsNullOrEmpty(customTypeText))
                return customTypeText;

            string typeStr = cardType switch
            {
                CardType.Incantation => "อาคม",
                CardType.Amulet => "เครื่องราง",
                CardType.Familiar => "ภูติรับใช้",
                _ => cardType.ToString()
            };

            string targetStr = targetType switch
            {
                TargetType.SingleEnemy => "ศัตรูเดี่ยว",
                TargetType.AllEnemies => "ศัตรูทั้งหมด",
                TargetType.Self => "ตนเอง",
                TargetType.FriendlyMinion => "บริวารฝ่ายเรา",
                TargetType.NoTarget => "ไร้เป้าหมาย",
                _ => targetType.ToString()
            };

            return $"{typeStr} • {targetStr}";
        }
    }
}
