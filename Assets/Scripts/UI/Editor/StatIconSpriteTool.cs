using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Builds the TextMeshPro sprite asset that puts the card face's skull (สะเทือนขวัญ) and blood drop (ขวัญ) inside
    /// text (Game Designer, 25 Oct): Resources/Sprite Assets/StatIcons.asset from Art/UI/StatIcons.png (two 100 px cells).
    /// TMP finds it by name: &lt;sprite="StatIcons" name="atk"&gt; / name="khwan" (see StatIconText).
    /// </summary>
    public static class StatIconSpriteTool
    {
        private const string SheetPath = "Assets/Art/UI/StatIcons.png";
        private const string AssetPath = "Assets/Resources/Sprite Assets/StatIcons.asset";
        private const int Cell = 100;

        [MenuItem("Tools/TawanOS/UI/Build Stat Icon Sprites")]
        public static void BuildFromMenu() => Build();

        // Batch: -executeMethod TawanOS.EditorTools.StatIconSpriteTool.Run
        public static void Run()
        {
            EditorApplication.Exit(Build() ? 0 : 1);
        }

        public static bool Build()
        {
            if (AssetImporter.GetAtPath(SheetPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
            if (sheet == null)
            {
                Debug.LogError($"[StatIconSpriteTool] {SheetPath} not found");
                return false;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources/Sprite Assets")) AssetDatabase.CreateFolder("Assets/Resources", "Sprite Assets");
            var old = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(AssetPath);
            if (old != null) AssetDatabase.DeleteAsset(AssetPath);

            var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = "StatIcons";
            asset.spriteSheet = sheet;
            AssetDatabase.CreateAsset(asset, AssetPath);
            // A new asset reads as an old-format one and "upgrades" from a sprite list it does not have: mark it current
            var version = new SerializedObject(asset);
            version.FindProperty("m_Version").stringValue = "1.1.0";
            version.ApplyModifiedPropertiesWithoutUndo();

            var material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "StatIcons Material" };
            material.SetTexture(ShaderUtilities.ID_MainTex, sheet);
            AssetDatabase.AddObjectToAsset(material, asset);
            asset.material = material;

            string[] names = { "atk", "khwan" };
            for (int i = 0; i < names.Length; i++)
            {
                // A little below the line's top, as wide as it is tall, with a small gap after it
                var glyph = new TMP_SpriteGlyph((uint)i, new GlyphMetrics(Cell, Cell, 0, Cell * 0.82f, Cell * 1.06f),
                    new GlyphRect(i * Cell, 0, Cell, Cell), 1f, 0);
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, glyph) { name = names[i], scale = 1f });
            }

            // The sprites are sized against the text: a line's worth at the font's size
            var so = new SerializedObject(asset);
            var face = so.FindProperty("m_FaceInfo");
            if (face != null)
            {
                var pointSize = face.FindPropertyRelative("m_PointSize");
                if (pointSize.propertyType == SerializedPropertyType.Float) pointSize.floatValue = Cell; else pointSize.intValue = Cell;
                face.FindPropertyRelative("m_Scale").floatValue = 1f;
                face.FindPropertyRelative("m_LineHeight").floatValue = Cell;
                face.FindPropertyRelative("m_AscentLine").floatValue = Cell * 0.82f;
                face.FindPropertyRelative("m_DescentLine").floatValue = -Cell * 0.18f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            asset.UpdateLookupTables();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[StatIconSpriteTool] Built {AssetPath}");
            return true;
        }
    }
}
