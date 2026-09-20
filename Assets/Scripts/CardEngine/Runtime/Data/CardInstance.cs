using System;
using UnityEngine;

namespace TawanOS.CardEngine
{
    [Serializable]
    public class CardInstance
    {
        public string instanceId;
        public string cardId;
        public string cardNameThai;
        public string cardNameEng;
        public MagicSchool magicSchool;
        public CardType cardType;
        public int meritCost;
        public int corruptionGain;
        public int baseValue;
        public int currentDurability;
        public int familiarHealth;
        public int familiarDamage;
        public TargetType targetType;
        public Sprite cardBackground;
        public Sprite artwork;
        public Sprite frameBorder;
        public string customTypeText;
        public string descriptionFormat;

        public CardInstance()
        {
            instanceId = Guid.NewGuid().ToString();
        }

        public CardInstance(CardDataSO template)
        {
            instanceId = Guid.NewGuid().ToString();
            if (template != null)
            {
                cardId = template.cardId;
                cardNameThai = template.cardNameThai;
                cardNameEng = template.cardNameEng;
                magicSchool = template.magicSchool;
                cardType = template.cardType;
                meritCost = template.meritCost;
                corruptionGain = template.corruptionGain;
                baseValue = template.baseValue;
                currentDurability = template.durability;
                familiarHealth = template.familiarHealth;
                familiarDamage = template.familiarDamage;
                targetType = template.targetType;
                cardBackground = template.cardBackground != null ? template.cardBackground : template.frameBorder;
                artwork = template.artwork;
                frameBorder = template.frameBorder;
                customTypeText = template.GetFormattedTypeText();
                descriptionFormat = template.descriptionFormat;
            }
        }

        public string GetFormattedTypeText()
        {
            if (!string.IsNullOrEmpty(customTypeText)) return customTypeText;

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
