using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    [CreateAssetMenu(fileName = "NewCardData", menuName = "TawanOS/CardEngine/Card Data")]
    public class CardDataSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("รหัสการ์ด (ว่าง = ใช้ชื่อไฟล์)")]
        public string cardId = "";
        public string cardNameThai = "";
        public string cardNameEng = "";
        [TextArea(2, 4)]
        public string descriptionFormat = "";

        [Header("Classification")]
        public MagicSchool magicSchool = MagicSchool.WhiteMagic;
        public CardType cardType = CardType.Incantation;
        public TargetType targetType = TargetType.SingleEnemy;
        [Tooltip("ข้อความประเภทบนการ์ด เช่น 'บริวาร • มนต์ดำ' (เว้นว่าง = สร้างจากประเภทและสายอัตโนมัติ)")]
        public string customTypeText = "";

        [Header("Costs & Thresholds")]
        [Range(0, 6)] public int meritCost = 1;
        [Range(0, 9)] public int corruptionGain = 0;

        [Header("Primary Combat Values")]
        public int baseValue = 0;
        [Tooltip("อายุขลังของเครื่องราง (0 = อยู่บนสนามจนกว่าจะพัง)")]
        public int durability = 0;
        [Tooltip("ขวัญ (HP) ของบริวาร / เครื่องราง (เครื่องราง 0 = ตีไม่ได้)")]
        public int familiarHealth = 1;
        [Tooltip("สะเทือนขวัญ (ATK) ของบริวาร")]
        public int familiarDamage = 1;

        [Header("Face Text Sizes (x 1 = default)")]
        [Tooltip("ขนาดตัวหนังสือทั้งหมดบนการ์ด (คูณกับขนาดแต่ละส่วนด้านล่าง)")]
        [Range(0.3f, 3f)] public float fontSizeAll = 1f;
        [Tooltip("ชื่อการ์ด")]
        [Range(0.3f, 3f)] public float nameFontSize = 1f;
        [Tooltip("ประเภท / สาย")]
        [Range(0.3f, 3f)] public float typeFontSize = 1f;
        [Tooltip("ค่าร่ายในวงกลม")]
        [Range(0.3f, 3f)] public float costFontSize = 1f;
        [Tooltip("สะเทือนขวัญ และ ขวัญ")]
        [Range(0.3f, 3f)] public float statFontSize = 1f;
        [Tooltip("คำอธิบายความสามารถ (ยังย่อเองถ้าข้อความยาวเกินกล่อง)")]
        [Range(0.3f, 3f)] public float descriptionFontSize = 1f;

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

        // "ประเภท • สาย" as on the sheet cards, e.g. "บริวาร • มนต์ดำ"
        public string GetFormattedTypeText()
        {
            if (!string.IsNullOrEmpty(customTypeText))
                return customTypeText;

            string typeStr = cardType switch
            {
                CardType.Incantation => "อาคม",
                CardType.Amulet => "เครื่องราง",
                CardType.Familiar => "บริวาร",
                _ => cardType.ToString()
            };
            string schoolStr = magicSchool == MagicSchool.WhiteMagic ? "มนต์ขาว" : "มนต์ดำ";

            return $"{typeStr} • {schoolStr}";
        }
    }
}
