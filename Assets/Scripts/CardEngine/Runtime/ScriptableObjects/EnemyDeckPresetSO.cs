using System.Collections.Generic;
using UnityEngine;

namespace TawanOS.CardEngine
{
    /// <summary>
    /// A deck an enemy can be given in place of its own (EnemyProfileSO.deck). Which preset an enemy gets is decided
    /// by how far into the map the fight is (TawanOS.GameFlow.EnemyDeckProgressionSO). Edit the cards freely.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnemyDeckPreset", menuName = "TawanOS/CardEngine/Enemy Deck Preset")]
    public class EnemyDeckPresetSO : ScriptableObject
    {
        public string presetName = "สำรับผี";
        [TextArea] public string notes;
        public List<CardDataSO> cards = new List<CardDataSO>();
    }
}
