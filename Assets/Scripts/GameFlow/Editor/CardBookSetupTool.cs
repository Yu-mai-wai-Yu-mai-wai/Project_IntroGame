using TawanOS.CardEngine;
using UnityEditor;
using UnityEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Makes Resources/CardBook.asset (<see cref="CardBookStyleSO"/>, the look of the ตำราไสยเวท book) from the
    /// pictures in Assets/Art/Book and the card back, importing them as sprites. Keeps any values already tuned on
    /// the asset; only empty picture slots are filled. Safe to run again.
    /// </summary>
    public static class CardBookSetupTool
    {
        private const string AssetPath = "Assets/Resources/CardBook.asset";
        private const string ArtFolder = "Assets/Art/Book";
        private const string CardBackPath = "Assets/Art/Cards/FramedCard/back.png";

        [MenuItem("Tools/TawanOS/Main Menu/Create Card Book Style")]
        private static void CreateMenu()
        {
            Create();
        }

        /// <summary>-executeMethod TawanOS.GameFlow.CardBookSetupTool.CreateBatch</summary>
        public static void CreateBatch()
        {
            EditorApplication.Exit(Create() ? 0 : 1);
        }

        public static bool Create()
        {
            var style = AssetDatabase.LoadAssetAtPath<CardBookStyleSO>(AssetPath);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<CardBookStyleSO>();
                AssetDatabase.CreateAsset(style, AssetPath);
            }

            if (style.spread == null) style.spread = Sprite(ArtFolder + "/Pages.png");
            if (style.page == null) style.page = Sprite(ArtFolder + "/Page.png");
            if (style.cardBack == null) style.cardBack = Sprite(CardBackPath);
            EditorUtility.SetDirty(style);
            AssetDatabase.SaveAssets();

            bool ok = style.spread != null && style.page != null && style.cardBack != null;
            if (ok) Debug.Log($"[CardBookSetupTool] {AssetPath} ready.");
            else Debug.LogError($"[CardBookSetupTool] Missing art: spread={style.spread != null} page={style.page != null} cardBack={style.cardBack != null}");
            return ok;
        }

        private static Sprite Sprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer
                && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
