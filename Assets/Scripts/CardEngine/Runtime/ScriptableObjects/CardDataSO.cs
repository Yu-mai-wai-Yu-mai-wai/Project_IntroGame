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

        [Header("Costs & Thresholds")]
        [Range(0, 6)] public int meritCost = 1;
        [Range(0, 9)] public int corruptionGain = 0;

        [Header("Primary Combat Values")]
        public int baseValue = 8;
        public int durability = 3;
        public int familiarHealth = 10;
        public int familiarDamage = 4;

        [Header("Visuals & Audio")]
        public Sprite artwork;
        public Sprite frameBorder;
        public AudioClip sfxPlay;
        public GameObject vfxPrefab;
    }
}
