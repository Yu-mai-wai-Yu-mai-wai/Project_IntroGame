using System;
using System.Collections.Generic;

namespace TawanOS.CardEngine
{
    [Serializable]
    public class CombatStateData
    {
        public int playerKhwan;
        public int maxPlayerKhwan;
        public int currentMerit;
        public int maxMerit;
        public int currentCorruption;
        public int corruptionThreshold;
        public int enemyKhwan;
        public int maxEnemyKhwan;
        public int incenseCurrency;
        public int turnNumber;
        public CombatPhase currentPhase;

        public List<CardInstance> drawPile = new List<CardInstance>();
        public List<CardInstance> handCards = new List<CardInstance>();
        public List<CardInstance> discardPile = new List<CardInstance>();
        public List<CardInstance> activeAmulets = new List<CardInstance>();
        public List<CardInstance> activeFamiliars = new List<CardInstance>();

        public CombatStateData()
        {
            playerKhwan = 50;
            maxPlayerKhwan = 50;
            currentMerit = 1;
            maxMerit = 6;
            currentCorruption = 0;
            corruptionThreshold = 9;
            enemyKhwan = 30;
            maxEnemyKhwan = 30;
            incenseCurrency = 0;
            turnNumber = 1;
            currentPhase = CombatPhase.BattleInit;
        }
    }
}
