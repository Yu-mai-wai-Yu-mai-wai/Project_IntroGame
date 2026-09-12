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
        public Sprite artwork;
        public Sprite frameBorder;
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
                artwork = template.artwork;
                frameBorder = template.frameBorder;
                descriptionFormat = template.descriptionFormat;
            }
        }
    }
}
