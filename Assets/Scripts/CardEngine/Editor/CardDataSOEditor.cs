#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TawanOS.CardEngine
{
    [CustomEditor(typeof(CardDataSO))]
    public class CardDataSOEditor : Editor
    {
        private bool showLivePreview = true;

        public override void OnInspectorGUI()
        {
            CardDataSO card = (CardDataSO)target;

            // Top Quick Actions Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("+ สร้างการ์ดใหม่ (New Card)", EditorStyles.toolbarButton, GUILayout.Width(170)))
            {
                CreateNewCardAsset();
            }
            if (GUILayout.Button("📁 เปิดโฟลเดอร์การ์ด", EditorStyles.toolbarButton))
            {
                var folder = AssetDatabase.LoadAssetAtPath<Object>("Assets/CardEngineData/Cards");
                if (folder != null) Selection.activeObject = folder;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // Live Preview Card (WYSIWYG Inspector)
            showLivePreview = EditorGUILayout.Foldout(showLivePreview, "🃏 LIVE CARD PREVIEW (พรีวิวการ์ดแบบเรียลไทม์)", true, EditorStyles.foldoutHeader);
            if (showLivePreview)
            {
                DrawLiveCardPreview(card);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("⚙️ คุณสมบัติการ์ด (Card Properties)", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(card);
                Repaint();
            }
        }

        private void DrawLiveCardPreview(CardDataSO card)
        {
            float cardWidth = 220f;
            float cardHeight = 310f;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            Rect cardRect = GUILayoutUtility.GetRect(cardWidth, cardHeight);

            // 1. Base Card Frame & Border
            bool isWhiteMagic = card.magicSchool == MagicSchool.WhiteMagic;
            Color borderColor = isWhiteMagic ? new Color(0.9f, 0.75f, 0.35f) : new Color(0.85f, 0.25f, 0.35f);
            Color cardBgColor = isWhiteMagic ? new Color(0.12f, 0.10f, 0.08f) : new Color(0.10f, 0.06f, 0.10f);

            EditorGUI.DrawRect(cardRect, borderColor); // Outer border
            Rect innerRect = new Rect(cardRect.x + 3, cardRect.y + 3, cardRect.width - 6, cardRect.height - 6);
            EditorGUI.DrawRect(innerRect, cardBgColor); // Inner background

            // 2. Header Bar: Name Thai & Eng
            Rect headerRect = new Rect(innerRect.x + 6, innerRect.y + 6, innerRect.width - 12, 34);
            EditorGUI.DrawRect(headerRect, new Color(0.2f, 0.18f, 0.22f, 0.8f));

            GUIStyle nameThaiStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(headerRect.x, headerRect.y + 1, headerRect.width, 18), card.cardNameThai, nameThaiStyle);

            GUIStyle nameEngStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                normal = { textColor = new Color(0.75f, 0.75f, 0.75f) }
            };
            GUI.Label(new Rect(headerRect.x, headerRect.y + 17, headerRect.width, 14), card.cardNameEng, nameEngStyle);

            // 3. Cost Gem Badge (Top-Left)
            Rect costRect = new Rect(cardRect.x - 6, cardRect.y - 6, 32, 32);
            Color costBg = isWhiteMagic ? new Color(0.95f, 0.75f, 0.1f) : new Color(0.85f, 0.15f, 0.15f);
            EditorGUI.DrawRect(costRect, costBg);
            GUIStyle costStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                normal = { textColor = Color.black }
            };
            string costText = isWhiteMagic ? $"{card.meritCost}" : $"+{card.corruptionGain}";
            GUI.Label(costRect, costText, costStyle);

            // 4. Artwork Box (Center)
            Rect artRect = new Rect(innerRect.x + 10, innerRect.y + 46, innerRect.width - 20, 130);
            EditorGUI.DrawRect(artRect, new Color(0.06f, 0.05f, 0.07f)); // Art backing

            if (card.artwork != null && card.artwork.texture != null)
            {
                GUI.DrawTexture(artRect, card.artwork.texture, ScaleMode.ScaleToFit);
            }
            else
            {
                GUIStyle placeholderStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
                GUI.Label(artRect, "🖼️ ไม่มีรูปภาพ\n(ลาก Sprite มาใส่ในช่อง Artwork)", placeholderStyle);
            }

            // 5. Stat Badge (Familiar HP/ATK or Amulet Durability)
            if (card.cardType == CardType.Familiar)
            {
                Rect statBadge = new Rect(innerRect.x + 10, artRect.yMax - 22, innerRect.width - 20, 20);
                EditorGUI.DrawRect(statBadge, new Color(0, 0, 0, 0.75f));
                GUIStyle statStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    normal = { textColor = new Color(0.4f, 0.9f, 0.4f) }
                };
                GUI.Label(statBadge, $"ขวัญ (HP): {card.familiarHealth}  |  โจมตี (ATK): {card.familiarDamage}", statStyle);
            }
            else if (card.cardType == CardType.Amulet)
            {
                Rect statBadge = new Rect(innerRect.x + 10, artRect.yMax - 22, innerRect.width - 20, 20);
                EditorGUI.DrawRect(statBadge, new Color(0, 0, 0, 0.75f));
                GUIStyle statStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11,
                    normal = { textColor = new Color(0.4f, 0.7f, 1f) }
                };
                GUI.Label(statBadge, $"ความคงทน: {card.durability} ครั้ง", statStyle);
            }

            // 6. Description Text Box (Bottom)
            Rect descRect = new Rect(innerRect.x + 8, innerRect.y + 184, innerRect.width - 16, 114);
            EditorGUI.DrawRect(descRect, new Color(0.16f, 0.14f, 0.18f, 0.9f));

            string formattedDesc = "";
            try
            {
                formattedDesc = string.Format(card.descriptionFormat ?? "", card.baseValue);
            }
            catch
            {
                formattedDesc = card.descriptionFormat ?? "";
            }

            GUIStyle descStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
            };
            GUI.Label(new Rect(descRect.x + 6, descRect.y + 6, descRect.width - 12, descRect.height - 12), formattedDesc, descStyle);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static void CreateNewCardAsset()
        {
            string folder = "Assets/CardEngineData/Cards";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(folder + "/Card_NewCustomCard.asset");
            CardDataSO newCard = ScriptableObject.CreateInstance<CardDataSO>();
            newCard.cardId = "card_" + System.Guid.NewGuid().ToString().Substring(0, 8);
            newCard.cardNameThai = "การ์ดอาคมใหม่";
            newCard.cardNameEng = "New Arcane Card";
            newCard.descriptionFormat = "สร้างความเสียหาย {0} หน่วย";
            newCard.baseValue = 10;
            newCard.meritCost = 1;

            AssetDatabase.CreateAsset(newCard, uniquePath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = newCard;
            EditorGUIUtility.PingObject(newCard);
            Debug.Log($"<color=green>[CardDataSOEditor] สร้างการ์ดใหม่สำเร็จ: {uniquePath}</color>");
        }
    }
}
#endif
