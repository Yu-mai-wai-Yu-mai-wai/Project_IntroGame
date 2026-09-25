using System;
using System.Collections.Generic;
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

        // Board state. maxKhwan is 0 for cards that have no Khwan (they cannot be damaged or destroyed).
        public int maxKhwan;
        public int armor;
        public List<CardAbility> abilities = new List<CardAbility>();
        public List<CardStatus> statuses = new List<CardStatus>();

        [NonSerialized] public CardDataSO source;
        [NonSerialized] public CardInstance lastAttacker;
        [NonSerialized] public bool deathHandled;
        [NonSerialized] public bool pendingRemoval;
        [NonSerialized] public bool formationBuffed;

        // The board column (0..4) this card sits in; -1 when it is not on the board. Columns are fixed:
        // a card keeps its column when others die. Set before playing to ask for a specific column.
        [NonSerialized] public int boardSlot = -1;

        // What the auras in play currently add to this card (EffectResolver.RefreshAuras), so it can be taken back
        [NonSerialized] public int auraKhwan;
        [NonSerialized] public int auraAttack;

        public bool IsDead
        {
            get
            {
                if (pendingRemoval) return true;
                if (cardType == CardType.Familiar) return familiarHealth <= 0;
                return maxKhwan > 0 && familiarHealth <= 0;
            }
        }

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
                maxKhwan = template.familiarHealth;
                abilities = template.abilities != null ? new List<CardAbility>(template.abilities) : new List<CardAbility>();
                source = template;
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
