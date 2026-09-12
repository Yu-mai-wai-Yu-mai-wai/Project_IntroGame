using UnityEngine;

namespace TawanOS.CardEngine
{
    [CreateAssetMenu(fileName = "NewStatusEffect", menuName = "TawanOS/CardEngine/Status Effect")]
    public class StatusEffectSO : ScriptableObject
    {
        public StatusEffectType statusType;
        public string effectName = "ขวัญผวา";
        [TextArea(2, 4)]
        public string description = "ลดพลังโจมตีลง 25%";
        public Sprite icon;
        public bool isDebuff = true;
        public int defaultDuration = 2;
    }
}
