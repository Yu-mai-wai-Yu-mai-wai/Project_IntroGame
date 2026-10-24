#if UNITY_EDITOR
using TawanOS.GameFlow;
using UnityEditor;
using UnityEngine;

namespace TawanOS.EditorTools
{
    // Imports Art/CutScenes/Lost.PNG as a sprite and points Resources/GameOverArt at it (the Game Over screen's picture)
    public static class GameOverArtSetupTool
    {
        private const string ArtPath = "Assets/Art/CutScenes/Lost.PNG";
        private const string AssetPath = "Assets/Resources/GameOverArt.asset";

        [MenuItem("Tools/TawanOS/Game Over/Set Up Defeat Art")]
        private static void Setup_Menu() => Setup();

        // Batch: -executeMethod TawanOS.EditorTools.GameOverArtSetupTool.Setup
        public static void Setup()
        {
            var importer = AssetImporter.GetAtPath(ArtPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[GameOverArtSetupTool] {ArtPath} not found.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }

            var art = AssetDatabase.LoadAssetAtPath<GameOverArtSO>(AssetPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<GameOverArtSO>();
                AssetDatabase.CreateAsset(art, AssetPath);
            }
            art.defeatArt = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[GameOverArtSetupTool] {AssetPath} -> {ArtPath} ({(art.defeatArt != null ? "ok" : "no sprite")}).</color>");
        }
    }
}
#endif
