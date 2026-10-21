using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    /// <summary>
    /// The default cursor (Project Settings > Player, set in 1a7dc6b) was the Eyes sprite, which Unity cannot use as a
    /// cursor ("Failed to set the cursor ... not CPU accessible" in every play, so the combat smoke test exited 1).
    /// Copies it to Assets/Art/MainMenu/Generated/EyesCursor.png imported as a Cursor texture and makes that the
    /// default cursor; the Eyes sprite the main menu uses stays as it is.
    /// Menu: Tools/TawanOS/Fix Cursor Texture. Batch: -executeMethod TawanOS.EditorTools.CursorTextureFixTool.Run
    /// </summary>
    public static class CursorTextureFixTool
    {
        private const string SourcePath = "Assets/Art/MainMenu/Generated/Eyes.png";
        private const string CursorPath = "Assets/Art/MainMenu/Generated/EyesCursor.png";

        [MenuItem("Tools/TawanOS/Fix Cursor Texture")]
        public static void RunFromMenu() { Fix(); }

        public static void Run() { EditorApplication.Exit(Fix() ? 0 : 1); }

        private static bool Fix()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(CursorPath) == null && !AssetDatabase.CopyAsset(SourcePath, CursorPath))
            {
                Debug.LogError($"[CursorTextureFixTool] Could not copy {SourcePath} to {CursorPath}.");
                return false;
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(CursorPath);
            importer.textureType = TextureImporterType.Cursor;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            PlayerSettings.defaultCursor = AssetDatabase.LoadAssetAtPath<Texture2D>(CursorPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CursorTextureFixTool] Default cursor is now {CursorPath} (a Cursor texture).");
            return PlayerSettings.defaultCursor != null;
        }
    }
}
