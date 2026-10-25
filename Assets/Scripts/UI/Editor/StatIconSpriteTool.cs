using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// Builds the TextMeshPro sprite assets that put the designer's สะเทือนขวัญ and ขวัญ icons inside card text
    /// (Game Designer, 25 Oct). Each icon is its own file, rendered from its SVG at its own shape (no square cell, so
    /// nothing is cropped): Art/UI/StatIconAttack.png + Resources/Sprite Assets/StatIconAttack.asset, and the same for
    /// StatIconKhwan. The art is white so the text's colour tints it: &lt;sprite="StatIconAttack" index=0 tint=1&gt;.
    /// </summary>
    public static class StatIconSpriteTool
    {
        private const string ArtFolder = "Assets/Art/UI/";
        private const string AssetFolder = "Assets/Resources/Sprite Assets/";
        private static readonly string[] Icons = { "StatIconAttack", "StatIconKhwan" };
        private const int Height = 96; // pixels of art height; the width follows the icon's shape

        [MenuItem("Tools/TawanOS/UI/Build Stat Icon Sprites")]
        public static void BuildFromMenu() => Build();

        // Batch: -executeMethod TawanOS.EditorTools.StatIconSpriteTool.Run
        public static void Run()
        {
            EditorApplication.Exit(Build() ? 0 : 1);
        }

        public static bool Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Sprite Assets")) AssetDatabase.CreateFolder("Assets/Resources", "Sprite Assets");
            bool ok = true;
            foreach (var icon in Icons) ok &= BuildOne(icon);
            // The old shared sheet (both icons in square cells) is no longer used
            AssetDatabase.DeleteAsset(AssetFolder + "StatIcons.asset");
            AssetDatabase.DeleteAsset(ArtFolder + "StatIcons.png");
            AssetDatabase.SaveAssets();
            return ok;
        }

        private static bool BuildOne(string icon)
        {
            string svgPath = ArtFolder + icon + ".svg", pngPath = ArtFolder + icon + ".png", assetPath = AssetFolder + icon + ".asset";
            var art = RenderSvg(svgPath);
            if (art == null)
            {
                Debug.LogError($"[StatIconSpriteTool] Could not render {svgPath}");
                return false;
            }
            // White, keeping only the art's shape (its alpha), so tint=1 colours it like the text
            var px = art.GetPixels32();
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, px[i].a);
            art.SetPixels32(px);
            art.Apply();
            int w = art.width, h = art.height;
            System.IO.File.WriteAllBytes(pngPath, art.EncodeToPNG());
            Object.DestroyImmediate(art);
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(pngPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true; // drawn at text size: mipmaps keep thin parts from breaking up
                importer.filterMode = FilterMode.Trilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None; // keep its own size: a power-of-two stretch distorts it
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);

            AssetDatabase.DeleteAsset(assetPath);
            var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = icon;
            asset.spriteSheet = sheet;
            AssetDatabase.CreateAsset(asset, assetPath);
            // A new asset reads as an old-format one and "upgrades" from a sprite list it does not have: mark it current
            var version = new SerializedObject(asset);
            version.FindProperty("m_Version").stringValue = "1.1.0";
            version.ApplyModifiedPropertiesWithoutUndo();

            var material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = icon + " Material" };
            material.SetTexture(ShaderUtilities.ID_MainTex, sheet);
            AssetDatabase.AddObjectToAsset(material, asset);
            asset.material = material;

            // The whole picture, sitting on the baseline a little below the letters' top, a small gap after it
            var glyph = new TMP_SpriteGlyph(0, new GlyphMetrics(w, h, 0, h * 0.86f, w + h * 0.1f), new GlyphRect(0, 0, w, h), 1.15f, 0);
            asset.spriteGlyphTable.Add(glyph);
            asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xFFFE, glyph) { name = icon, scale = 1f });

            var so = new SerializedObject(asset);
            var face = so.FindProperty("m_FaceInfo");
            if (face != null)
            {
                var pointSize = face.FindPropertyRelative("m_PointSize");
                if (pointSize.propertyType == SerializedPropertyType.Float) pointSize.floatValue = h; else pointSize.intValue = h;
                face.FindPropertyRelative("m_Scale").floatValue = 1f;
                face.FindPropertyRelative("m_LineHeight").floatValue = h;
                face.FindPropertyRelative("m_AscentLine").floatValue = h * 0.86f;
                face.FindPropertyRelative("m_DescentLine").floatValue = -h * 0.14f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            asset.UpdateLookupTables();
            EditorUtility.SetDirty(asset);
            Debug.Log($"[StatIconSpriteTool] Built {assetPath} ({w}x{h})");
            return true;
        }

        // Unity's SVG importer renders the art: switched to a texture for a moment, its pixels copied, set back
        private static Texture2D RenderSvg(string svgPath)
        {
            var importer = AssetImporter.GetAtPath(svgPath);
            if (importer == null) return null;
            var so = new SerializedObject(importer);
            int oldType = so.FindProperty("m_SvgType").intValue, oldSize = so.FindProperty("m_TextureSize").intValue;
            int oldW = so.FindProperty("m_TextureWidth").intValue, oldH = so.FindProperty("m_TextureHeight").intValue;
            int oldSamples = so.FindProperty("m_SampleCount").intValue;
            SetSvg(so, 2, Height, Height, Height, 8); // 2 = Texture2D; the art keeps its shape

            Texture2D art = null;
            var rendered = AssetDatabase.LoadAssetAtPath<Texture2D>(svgPath);
            if (rendered != null)
            {
                var rt = RenderTexture.GetTemporary(rendered.width, rendered.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(rendered, rt);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                art = new Texture2D(rendered.width, rendered.height, TextureFormat.RGBA32, false);
                art.ReadPixels(new Rect(0, 0, rendered.width, rendered.height), 0, 0);
                art.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
            SetSvg(new SerializedObject(importer), oldType, oldSize, oldW, oldH, oldSamples);
            return art;
        }

        private static void SetSvg(SerializedObject so, int type, int size, int w, int h, int samples)
        {
            so.FindProperty("m_SvgType").intValue = type;
            so.FindProperty("m_TextureSize").intValue = size;
            so.FindProperty("m_TextureWidth").intValue = w;
            so.FindProperty("m_TextureHeight").intValue = h;
            so.FindProperty("m_SampleCount").intValue = samples;
            so.ApplyModifiedPropertiesWithoutUndo();
            ((AssetImporter)so.targetObject).SaveAndReimport();
        }
    }
}
