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

            // 1. Full Card Background (วาดภาพเต็มใบจากทีม Art หรือ Fallback)
            Sprite bgSprite = card.cardBackground != null ? card.cardBackground : card.frameBorder;
            if (bgSprite != null && bgSprite.texture != null)
            {
                GUI.DrawTexture(cardRect, bgSprite.texture, ScaleMode.StretchToFill);
            }
            else
            {
                // Fallback background matching Art team color palette
                Color baseBg = new Color(0.53f, 0.53f, 0.53f); // Clean card gray
                EditorGUI.DrawRect(cardRect, baseBg);
                // Top header darker gray
                EditorGUI.DrawRect(new Rect(cardRect.x, cardRect.y, cardRect.width, 46), new Color(0.44f, 0.44f, 0.44f));
                // Center art box light gray
                EditorGUI.DrawRect(new Rect(cardRect.x, cardRect.y + 46, cardRect.width, 134), new Color(0.84f, 0.84f, 0.84f));
                // Divider line
                EditorGUI.DrawRect(new Rect(cardRect.x + 10, cardRect.y + 180, cardRect.width - 20, 1.5f), new Color(0.75f, 0.75f, 0.75f));
            }

            // 2. Header Bar: Name Thai (Top-Left)
            Rect nameRect = new Rect(cardRect.x + 12, cardRect.y + 10, cardRect.width - 60, 30);
            GUIStyle nameThaiStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 15,
                normal = { textColor = Color.white }
            };
            GUI.Label(nameRect, string.IsNullOrEmpty(card.cardNameThai) ? "ชื่อการ์ด" : card.cardNameThai, nameThaiStyle);

            // 3. Cost Badge Circle (Top-Right "0")
            Rect costCircleRect = new Rect(cardRect.xMax - 42, cardRect.y + 6, 32, 32);
            bool isWhiteMagic = card.magicSchool == MagicSchool.WhiteMagic;
            string costStr = isWhiteMagic ? $"{card.meritCost}" : $"+{card.corruptionGain}";
            
            // Draw circle background if no background sprite
            if (bgSprite == null)
            {
                EditorGUI.DrawRect(costCircleRect, new Color(0.2f, 0.2f, 0.2f, 0.85f));
            }
            GUIStyle costStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                normal = { textColor = Color.white }
            };
            GUI.Label(costCircleRect, costStr, costStyle);

            // 4. Artwork Box ("รูป" - Center)
            Rect artRect = new Rect(cardRect.x + 8, cardRect.y + 48, cardRect.width - 16, 130);
            if (card.artwork != null && card.artwork.texture != null)
            {
                GUI.DrawTexture(artRect, card.artwork.texture, ScaleMode.ScaleToFit);
            }
            else
            {
                GUIStyle placeholderStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    fontSize = 11,
                    normal = { textColor = new Color(0.35f, 0.35f, 0.35f) }
                };
                GUI.Label(artRect, "รูป\n(ลาก Sprite มาใส่ในช่อง Artwork)", placeholderStyle);
            }

            // 5. Stat Badge (Familiar HP/ATK or Amulet Durability overlay if active)
            if (card.cardType == CardType.Familiar)
            {
                Rect statBadge = new Rect(artRect.x, artRect.yMax - 20, artRect.width, 20);
                EditorGUI.DrawRect(statBadge, new Color(0, 0, 0, 0.75f));
                GUIStyle statStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10,
                    normal = { textColor = new Color(0.4f, 0.9f, 0.4f) }
                };
                GUI.Label(statBadge, $"HP: {card.familiarHealth} | ATK: {card.familiarDamage}", statStyle);
            }
            else if (card.cardType == CardType.Amulet)
            {
                Rect statBadge = new Rect(artRect.x, artRect.yMax - 20, artRect.width, 20);
                EditorGUI.DrawRect(statBadge, new Color(0, 0, 0, 0.75f));
                GUIStyle statStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 10,
                    normal = { textColor = new Color(0.4f, 0.7f, 1f) }
                };
                GUI.Label(statBadge, $"ความคงทน: {card.durability} ครั้ง", statStyle);
            }

            // 6. Card Type • Subtype Divider ("ประเภท • รูปแบบ")
            Rect typeRect = new Rect(cardRect.x + 10, cardRect.y + 180, cardRect.width - 20, 24);
            GUIStyle typeStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = Color.white }
            };
            string typeDisplay = card.GetFormattedTypeText();
            GUI.Label(typeRect, string.IsNullOrEmpty(typeDisplay) ? "ประเภท • รูปแบบ" : typeDisplay, typeStyle);

            // 7. Description Box ("คำอธิบาย")
            Rect descRect = new Rect(cardRect.x + 14, cardRect.y + 208, cardRect.width - 28, 92);
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
                fontSize = 12,
                normal = { textColor = Color.white }
            };
            GUI.Label(descRect, string.IsNullOrEmpty(formattedDesc) ? "คำอธิบาย" : formattedDesc, descStyle);

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
            newCard.cardNameThai = "ชื่อการ์ดใหม่";
            newCard.cardNameEng = "New Card";
            newCard.customTypeText = "อาคม • โจมตีเดี่ยว";
            newCard.descriptionFormat = "สร้างความเสียหายสะเทือนขวัญ {0} หน่วย";
            newCard.baseValue = 10;
            newCard.meritCost = 1;

            // Auto-assign Art's Card_Template_Base if present
            Sprite defaultBg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/CardEngineData/Art/Card_Template_Base.png");
            if (defaultBg == null)
            {
                defaultBg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/CardEngineData/Cards/CardImg/Card_Template_Base.png");
            }
            if (defaultBg != null)
            {
                newCard.cardBackground = defaultBg;
            }

            AssetDatabase.CreateAsset(newCard, uniquePath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = newCard;
            EditorGUIUtility.PingObject(newCard);
            Debug.Log($"<color=green>[CardDataSOEditor] สร้างการ์ดใหม่สำเร็จ: {uniquePath}</color>");
        }
    }
}
#endif
