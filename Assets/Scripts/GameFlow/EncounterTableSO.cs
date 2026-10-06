using System.Collections.Generic;
using UnityEngine;
using TawanOS.CardEngine;
using TawanOS.MapEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Which enemies a map node can spawn. Loaded with Resources.Load so it works in a player build
    /// (the old lookup used AssetDatabase paths, which only exist in the Editor). Plan task A1.
    /// The asset lives at Assets/Resources/EncounterTable.asset; create or refill it with
    /// Tools/TawanOS/Game Flow/Create Encounter Table.
    /// </summary>
    [CreateAssetMenu(fileName = "EncounterTable", menuName = "TawanOS/Game Flow/Encounter Table")]
    public class EncounterTableSO : ScriptableObject
    {
        public const string ResourceName = "EncounterTable";

        public List<EnemyProfileSO> minor = new List<EnemyProfileSO>();
        public List<EnemyProfileSO> elite = new List<EnemyProfileSO>();
        public List<EnemyProfileSO> boss = new List<EnemyProfileSO>();

        private static EncounterTableSO cached;

        public static EncounterTableSO Load()
        {
            if (cached == null) cached = Resources.Load<EncounterTableSO>(ResourceName);
            return cached;
        }

        /// <summary>A random enemy for the node type, or null when that list is empty.</summary>
        public EnemyProfileSO Pick(NodeType nodeType)
        {
            List<EnemyProfileSO> list;
            switch (nodeType)
            {
                case NodeType.Boss: list = boss; break;
                case NodeType.EliteEnemy: list = elite; break;
                default: list = minor; break;
            }

            // Skip empty slots so a half-filled list never returns null while another entry is valid
            var valid = list.FindAll(e => e != null);
            if (valid.Count == 0) return null;
            return valid[Random.Range(0, valid.Count)];
        }
    }
}
