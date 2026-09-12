using System;
using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    [Serializable]
    public class EnemyMove
    {
        public EnemyIntent intent = EnemyIntent.Attack;
        public int baseValue = 6;
        public StatusEffectType statusEffect = StatusEffectType.KhwanPhawa;
        public int weight = 1;
        public string moveDescription = "โจมตีสะเทือนขวัญ";
    }

    [CreateAssetMenu(fileName = "NewEnemyProfile", menuName = "TawanOS/CardEngine/Enemy Profile")]
    public class EnemyProfileSO : ScriptableObject
    {
        [Header("Identity")]
        public string enemyId = "enemy_prai";
        public string enemyName = "ผีพราย";
        public Sprite portrait;

        [Header("Stats")]
        public int maxKhwan = 30;

        [Header("Moveset AI")]
        public List<EnemyMove> moves = new List<EnemyMove>();

        [Header("Boss Settings")]
        public bool isBoss = false;
        public EnemyProfileSO phase2Profile;
    }
}
