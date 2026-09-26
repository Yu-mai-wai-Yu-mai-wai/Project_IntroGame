#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TawanOS.CardEngine
{
    // Card Data inspector: build a card here and see it the way the game draws it.
    //  - Live preview with the same frame and text layout as the in-game 3D card (CardFaceLayout)
    //  - Thai labels, only the fields that matter for the card's type
    //  - "Use in game": the card catalog (pit / random summons) and the player / enemy decks
    [CustomEditor(typeof(CardDataSO))]
    public class CardDataSOEditor : Editor
    {
        private const string CardFolder = "Assets/CardEngineData/Cards/Custom";
        private const string CardPrefabPath = "Assets/CardEngineData/Prefabs/CardCube3DPrefab.prefab";

        private static bool showPreview = true;
        private static bool showAdvanced;

        private Sprite whiteFrame, blackFrame;

        private void OnEnable()
        {
            // The same default frames the game uses (set on the card prefab)
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            var view = prefab != null ? prefab.GetComponent<CardView3D>() : null;
            if (view != null)
            {
                whiteFrame = view.defaultWhiteFrame;
                blackFrame = view.defaultBlackFrame;
            }
        }

        public override void OnInspectorGUI()
        {
            var card = (CardDataSO)target;
            serializedObject.Update();

            DrawToolbar();
            EditorGUILayout.Space(4);

            showPreview = EditorGUILayout.Foldout(showPreview, "พรีวิวการ์ด (หน้าตาเดียวกับในเกม)", true, EditorStyles.foldoutHeader);
            if (showPreview) DrawPreview(card);

            EditorGUILayout.Space(8);
            EditorGUI.BeginChangeCheck();
            DrawFields(card);
            bool changed = EditorGUI.EndChangeCheck();

            serializedObject.ApplyModifiedProperties();
            if (changed) RefreshCardViews(card);

            EditorGUILayout.Space(8);
            DrawUseInGame(card);
        }

        // 3D cards in the open scene / Prefab Mode that show this Card Data redraw right away
        private static void RefreshCardViews(CardDataSO card)
        {
            var views = new List<CardView3D>(Object.FindObjectsByType<CardView3D>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            views.AddRange(UnityEditor.SceneManagement.StageUtility.GetCurrentStageHandle().FindComponentsOfType<CardView3D>());
            foreach (var view in views)
            {
                if (view != null && view.cardDataAsset == card) view.RefreshEditorPreview();
            }
        }

        // ---------------------------------------------------------------- toolbar

        private static void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("+ สร้างการ์ดใหม่", EditorStyles.toolbarButton, GUILayout.Width(110))) CreateNewCard();
            if (GUILayout.Button("เปิดโฟลเดอร์การ์ด", EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                var folder = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.IsValidFolder(CardFolder) ? CardFolder : "Assets/CardEngineData/Cards");
                if (folder != null) EditorGUIUtility.PingObject(folder);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        [MenuItem("Tools/TawanOS/Card Engine/New Card")]
        public static void CreateNewCard()
        {
            if (!AssetDatabase.IsValidFolder(CardFolder))
            {
                Directory.CreateDirectory(CardFolder);
                AssetDatabase.Refresh();
            }

            string path = AssetDatabase.GenerateUniqueAssetPath(CardFolder + "/Card_New.asset");
            var card = CreateInstance<CardDataSO>();
            card.cardId = Path.GetFileNameWithoutExtension(path);
            card.cardNameThai = "การ์ดใหม่";
            card.cardType = CardType.Familiar;
            card.magicSchool = MagicSchool.WhiteMagic;
            card.meritCost = 1;

            AssetDatabase.CreateAsset(card, path);
            CardCatalogAutoRegister.AddToCatalog(card);
            AssetDatabase.SaveAssets();
            Selection.activeObject = card;
            EditorGUIUtility.PingObject(card);
        }

        // ---------------------------------------------------------------- fields

        private void DrawFields(CardDataSO card)
        {
            Section("ข้อมูลการ์ด");
            Prop("cardNameThai", "ชื่อการ์ด");
            Prop("cardNameEng", "ชื่ออังกฤษ");
            Prop("cardId", "รหัสการ์ด");
            Prop("magicSchool", "สาย");
            Prop("cardType", "ประเภท");
            Prop("customTypeText", "ข้อความประเภท", "เว้นว่าง = \"" + AutoTypeText(card) + "\"");
            Prop("descriptionFormat", "คำอธิบายความสามารถ", "ข้อความในกล่องล่างของการ์ด ({0} = ค่า Base Value)");

            Section("ค่าร่าย");
            if (card.magicSchool == MagicSchool.WhiteMagic) Prop("meritCost", "กุศล");
            else Prop("corruptionGain", "มลทิน");

            if (card.cardType == CardType.Familiar)
            {
                Section("ค่าบริวาร");
                Prop("familiarHealth", "ขวัญ (HP)");
                Prop("familiarDamage", "สะเทือนขวัญ (ATK)");
            }
            else if (card.cardType == CardType.Amulet)
            {
                Section("ค่าเครื่องราง");
                Prop("familiarHealth", "ขวัญ (HP)", "0 = ตีทำลายไม่ได้");
                Prop("durability", "อายุขลัง", "จำนวนรอบก่อนหมดอายุ (0 = อยู่จนกว่าจะพัง)");
            }

            Section("ความสามารถ");
            EditorGUILayout.HelpBox(
                "แต่ละความสามารถ: เลือก 'ทำงานเมื่อ' (Trigger), 'เป้าหมาย' (Target) และ 'ผล' (Effect)\n" +
                "ค่า (Value) = จำนวนหน่วย, ระยะเวลา (Duration) = จำนวนเทิร์นของสถานะ\n" +
                "เครื่องรางที่ให้ผลค้างบนสนาม ให้ใช้ 'ขณะอยู่บนสนาม (ออร่า)' ผลจะหายเมื่อเครื่องรางพัง",
                MessageType.None);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("abilities"), new GUIContent("ความสามารถ"), true);

            Section("ขนาดตัวหนังสือบนการ์ด");
            Prop("fontSizeAll", "ทั้งหมด", "คูณกับขนาดแต่ละส่วนด้านล่าง (1 = ปกติ)");
            Prop("nameFontSize", "ชื่อการ์ด");
            Prop("typeFontSize", "ประเภท / สาย");
            Prop("costFontSize", "ค่าร่าย");
            Prop("statFontSize", "สะเทือนขวัญ / ขวัญ");
            Prop("descriptionFontSize", "คำอธิบาย", "ยังย่อเองถ้าข้อความยาวเกินกล่อง");

            Section("รูป");
            Prop("cardBackground", "กรอบ / พื้นหลัง", "เว้นว่าง = ใช้กรอบตามสาย (ทอง = มนต์ขาว, แดง = มนต์ดำ)");
            Prop("artwork", "ภาพประกอบ", "วางในช่องภาพของกรอบ");

            EditorGUILayout.Space(4);
            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "ขั้นสูง (ระบบเก่า / เสียง / เอฟเฟกต์)", true);
            if (showAdvanced)
            {
                EditorGUI.indentLevel++;
                Prop("baseValue", "Base Value", "ใช้แทน {0} ในคำอธิบาย และอาคมแบบเก่าที่ไม่มีความสามารถ");
                Prop("targetType", "Target Type (ระบบเก่า)");
                Prop("frameBorder", "Frame Border (ระบบเก่า)");
                Prop("sfxPlay", "เสียงตอนลงการ์ด");
                Prop("vfxPrefab", "เอฟเฟกต์ตอนลงการ์ด");
                EditorGUI.indentLevel--;
            }
        }

        private static void Section(string title)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private void Prop(string name, string label, string tooltip = null)
        {
            var p = serializedObject.FindProperty(name);
            if (p == null) return;
            EditorGUILayout.PropertyField(p, new GUIContent(label, tooltip ?? p.tooltip), true);
        }

        private static string AutoTypeText(CardDataSO card)
        {
            string saved = card.customTypeText;
            card.customTypeText = "";
            string auto = card.GetFormattedTypeText();
            card.customTypeText = saved;
            return auto;
        }

        // ---------------------------------------------------------------- preview

        private void DrawPreview(CardDataSO card)
        {
            const float width = 230f;
            float height = width * 1312f / 939f;

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Rect r = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Frame: the card's own background, else the default frame of its school
            Sprite frame = card.cardBackground != null
                ? card.cardBackground
                : CardFaceLayout.DefaultFrame(whiteFrame, blackFrame, card.magicSchool);
            if (frame != null) DrawSprite(r, frame, ScaleMode.StretchToFill);
            else EditorGUI.DrawRect(r, card.magicSchool == MagicSchool.WhiteMagic ? new Color(0.85f, 0.8f, 0.55f) : new Color(0.35f, 0.1f, 0.15f));

            if (card.artwork != null) DrawSprite(BoxRect(r, CardFaceLayout.Artwork), card.artwork, ScaleMode.ScaleToFit);

            Color text = frame != null ? Color.white : (card.magicSchool == MagicSchool.WhiteMagic ? new Color(0.12f, 0.08f, 0.05f) : new Color(0.95f, 0.9f, 0.85f));
            Color typeColor = frame != null ? new Color(0.95f, 0.72f, 0.72f) : text;

            string cost = card.magicSchool == MagicSchool.WhiteMagic ? card.meritCost.ToString() : card.corruptionGain.ToString();
            float Fs(CardFaceLayout.Text part) => CardFaceLayout.FontScale(card, part);
            DrawText(r, CardFaceLayout.Cost.Scaled(Fs(CardFaceLayout.Text.Cost)), cost, text, FontStyle.Bold);
            DrawText(r, CardFaceLayout.Name.Scaled(Fs(CardFaceLayout.Text.Name)), string.IsNullOrEmpty(card.cardNameThai) ? "ชื่อการ์ด" : card.cardNameThai, text, FontStyle.Bold);
            DrawText(r, CardFaceLayout.Type.Scaled(Fs(CardFaceLayout.Text.Type)), card.GetFormattedTypeText().Replace(" • ", "  "), typeColor, FontStyle.Normal);

            if (card.cardType == CardType.Familiar) DrawText(r, CardFaceLayout.Attack.Scaled(Fs(CardFaceLayout.Text.Stat)), card.familiarDamage.ToString(), text, FontStyle.Bold);
            if (card.cardType == CardType.Familiar || (card.cardType == CardType.Amulet && card.familiarHealth > 0))
            {
                DrawText(r, CardFaceLayout.Khwan.Scaled(Fs(CardFaceLayout.Text.Stat)), card.familiarHealth.ToString(), text, FontStyle.Bold);
            }

            string description;
            try { description = string.Format(card.descriptionFormat ?? "", card.baseValue); }
            catch { description = card.descriptionFormat ?? ""; }
            DrawText(r, CardFaceLayout.Description, description, text, FontStyle.Normal, wrap: true,
                maxFont: Mathf.RoundToInt(13 * Fs(CardFaceLayout.Text.Description)));
        }

        private static Rect BoxRect(Rect card, CardFaceLayout.Box box)
        {
            float w = box.size.x * card.width, h = box.size.y * card.height;
            float cx = card.x + (box.center.x + 0.5f) * card.width;
            float cy = card.y + (0.5f - box.center.y) * card.height;
            return new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);
        }

        private static void DrawText(Rect card, CardFaceLayout.Box box, string text, Color color, FontStyle style, bool wrap = false, int maxFont = 0)
        {
            if (string.IsNullOrEmpty(text)) return;
            Rect rect = BoxRect(card, box);
            int size = Mathf.Max(8, Mathf.RoundToInt(rect.height * 0.75f));
            if (maxFont > 0) size = Mathf.Min(size, maxFont);

            var gs = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = size,
                fontStyle = style,
                wordWrap = wrap,
                clipping = TextClipping.Overflow,
                normal = { textColor = color },
            };
            GUI.Label(rect, text, gs);
        }

        private static void DrawSprite(Rect rect, Sprite sprite, ScaleMode mode)
        {
            if (sprite == null || sprite.texture == null) return;
            var tex = sprite.texture;
            Rect uv = sprite.textureRect;
            uv = new Rect(uv.x / tex.width, uv.y / tex.height, uv.width / tex.width, uv.height / tex.height);

            if (mode == ScaleMode.ScaleToFit)
            {
                float aspect = sprite.rect.width / sprite.rect.height;
                if (rect.width / rect.height > aspect)
                {
                    float w = rect.height * aspect;
                    rect = new Rect(rect.center.x - w * 0.5f, rect.y, w, rect.height);
                }
                else
                {
                    float h = rect.width / aspect;
                    rect = new Rect(rect.x, rect.center.y - h * 0.5f, rect.width, h);
                }
            }
            GUI.DrawTextureWithTexCoords(rect, tex, uv, true);
        }

        // ---------------------------------------------------------------- use in game

        private static void DrawUseInGame(CardDataSO card)
        {
            Section("ใช้ในเกม");

            var catalog = CardCatalogAutoRegister.LoadCatalog();
            if (catalog == null)
            {
                EditorGUILayout.HelpBox("ไม่พบ Resources/CardCatalog", MessageType.Warning);
            }
            else if (catalog.cards.Contains(card))
            {
                EditorGUILayout.LabelField("อยู่ในคลังการ์ดของเกมแล้ว (สุ่มจากหลุมจั่ว / อัญเชิญได้)", EditorStyles.miniLabel);
            }
            else if (GUILayout.Button("เพิ่มเข้าคลังการ์ดของเกม"))
            {
                CardCatalogAutoRegister.AddToCatalog(card);
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("จำนวนในเด็ค", EditorStyles.miniBoldLabel);

            foreach (string guid in AssetDatabase.FindAssets("t:DeckConfigSO"))
            {
                var deck = AssetDatabase.LoadAssetAtPath<DeckConfigSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (deck != null) DeckRow("ผู้เล่น: " + deck.name, deck, deck.startingCards, card);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:EnemyProfileSO"))
            {
                var enemy = AssetDatabase.LoadAssetAtPath<EnemyProfileSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (enemy != null && enemy.deck != null && enemy.deck.Count > 0) DeckRow("ศัตรู: " + enemy.name, enemy, enemy.deck, card);
            }
        }

        private static void DeckRow(string label, Object owner, List<CardDataSO> list, CardDataSO card)
        {
            int count = list.FindAll(c => c == card).Count;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.MinWidth(120));
            if (GUILayout.Button("-", GUILayout.Width(24)) && count > 0)
            {
                Undo.RecordObject(owner, "Remove card from deck");
                list.Remove(card);
                EditorUtility.SetDirty(owner);
            }
            GUILayout.Label(count.ToString(), EditorStyles.boldLabel, GUILayout.Width(24));
            if (GUILayout.Button("+", GUILayout.Width(24)))
            {
                Undo.RecordObject(owner, "Add card to deck");
                list.Add(card);
                EditorUtility.SetDirty(owner);
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
