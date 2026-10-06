using TawanOS.CardEngine;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EventEngine
{
    // One-line effect row: type popup + only the field that type uses (amount / relic id / enemy).
    [CustomPropertyDrawer(typeof(EventEffect))]
    public class EventEffectDrawer : PropertyDrawer
    {
        // Same order as EventEffectType
        public static readonly string[] TypeLabels =
        {
            "— None —",
            "Heal",
            "Take Damage",
            "Change Max HP",
            "Gain Incense",
            "Lose Incense",
            "Gain Relic",
            "Start Combat",
            "Card Reward",
        };

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var type = property.FindPropertyRelative("type");
            var amount = property.FindPropertyRelative("amount");
            var relicId = property.FindPropertyRelative("relicId");
            var enemy = property.FindPropertyRelative("enemy");

            EditorGUI.BeginProperty(position, label, property);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var typeRect = new Rect(position.x, position.y, Mathf.Min(150f, position.width * 0.45f), position.height);
            var valueRect = new Rect(typeRect.xMax + 4f, position.y, position.width - typeRect.width - 4f, position.height);

            type.enumValueIndex = EditorGUI.Popup(typeRect, type.enumValueIndex, TypeLabels);

            switch ((EventEffectType)type.enumValueIndex)
            {
                case EventEffectType.None:
                case EventEffectType.CardReward:
                    break;
                case EventEffectType.GainRelic:
                    relicId.stringValue = EditorGUI.TextField(valueRect, relicId.stringValue);
                    if (string.IsNullOrEmpty(relicId.stringValue)) Placeholder(valueRect, "Relic name");
                    break;
                case EventEffectType.StartCombat:
                    enemy.objectReferenceValue = EditorGUI.ObjectField(valueRect, enemy.objectReferenceValue, typeof(EnemyProfileSO), false);
                    break;
                default:
                    float oldLabelWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 48f;
                    amount.intValue = EditorGUI.IntField(valueRect, "Amount", amount.intValue);
                    EditorGUIUtility.labelWidth = oldLabelWidth;
                    break;
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        private static void Placeholder(Rect rect, string text)
        {
            var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.5f, 0.5f, 0.5f) } };
            GUI.Label(new Rect(rect.x + 4f, rect.y, rect.width, rect.height), text, style);
        }

        /// <summary>Short description used by the story outline.</summary>
        public static string Describe(EventEffect effect)
        {
            switch (effect.type)
            {
                case EventEffectType.Heal: return $"+{effect.amount} HP";
                case EventEffectType.TakeDamage: return $"-{effect.amount} HP";
                case EventEffectType.ChangeMaxHp: return $"{(effect.amount >= 0 ? "+" : "")}{effect.amount} Max HP";
                case EventEffectType.GainIncense: return $"+{effect.amount} Incense";
                case EventEffectType.LoseIncense: return $"-{effect.amount} Incense";
                case EventEffectType.GainRelic: return $"Relic: {(string.IsNullOrEmpty(effect.relicId) ? "?" : effect.relicId)}";
                case EventEffectType.StartCombat: return $"Fight {(effect.enemy != null ? effect.enemy.enemyName : "?")}";
                case EventEffectType.CardReward: return "Pick a card";
                default: return null;
            }
        }
    }
}
