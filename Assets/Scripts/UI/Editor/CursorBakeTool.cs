#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    // Bakes the cursor art (Assets/Art/Cursor/Normal.svg) into a small PNG under Resources, set up as a cursor
    // texture, for PointerCursor to load in a build. Re-run after the SVG changes.
    public static class CursorBakeTool
    {
        private const string SourcePath = "Assets/Art/Cursor/Normal.svg";
        private const string OutputPath = "Assets/Resources/Cursor/cursor_normal.png";
        private const int Height = 48; // pixels on screen; the width follows the art's shape

        [MenuItem("Tools/TawanOS/UI/Bake Cursor")]
        private static void Bake_Menu()
        {
            Bake();
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

        // Batch: -executeMethod TawanOS.EditorTools.CursorBakeTool.Bake
        public static void Bake()
        {
            if (!File.Exists(SourcePath))
            {
                Debug.LogError($"[CursorBakeTool] {SourcePath} not found.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            // The art has many gradients, so it is rendered by Unity's own SVG importer: switched to a texture at the
            // cursor's size for a moment, its pixels copied, and the importer set back as it was
            var svgImporter = AssetImporter.GetAtPath(SourcePath);
            var so = new SerializedObject(svgImporter);
            int oldType = so.FindProperty("m_SvgType").intValue;
            int oldSize = so.FindProperty("m_TextureSize").intValue, oldW = so.FindProperty("m_TextureWidth").intValue, oldH = so.FindProperty("m_TextureHeight").intValue;
            int oldSamples = so.FindProperty("m_SampleCount").intValue;
            SetSvg(so, 2, Height, Height, Height, 8); // 2 = Texture2D; the aspect ratio is kept, so the width follows

            Texture2D tex = null;
            var rendered = AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
            if (rendered != null)
            {
                var rt = RenderTexture.GetTemporary(rendered.width, rendered.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(rendered, rt);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D(rendered.width, rendered.height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, rendered.width, rendered.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
            SetSvg(new SerializedObject(svgImporter), oldType, oldSize, oldW, oldH, oldSamples);
            if (tex == null)
            {
                Debug.LogError("[CursorBakeTool] The SVG importer gave no texture.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            // A cursor is drawn square (Windows stretches it to its own square size), so the art goes on a square
            // sheet, kept to its shape in the top-left corner where the hotspot is (Game Designer, 25 Oct: it looked squeezed)
            int side = Mathf.Max(tex.width, tex.height);
            var square = new Texture2D(side, side, TextureFormat.RGBA32, false);
            square.SetPixels32(new Color32[side * side]);
            square.SetPixels(0, side - tex.height, tex.width, tex.height, tex.GetPixels());
            square.Apply();
            Object.DestroyImmediate(tex);
            tex = square;
            int width = tex.width;

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllBytes(OutputPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(OutputPath);
            importer.textureType = TextureImporterType.Cursor;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            Debug.Log($"<color=green>[CursorBakeTool] {OutputPath} baked ({width}x{Height}).</color>");
        }
    }
}
#endif
