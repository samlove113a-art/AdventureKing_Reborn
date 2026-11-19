using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>Re-imports the HUD icon pictures (Assets/Art/UI/Hud/*.png, Meshy 2D PNG composited on the Meshy medallion frame) as pixel art: Point filter, no compression, no mipmaps.
    ///   Unity -batchmode -nographics -quit -executeMethod AdventureKing.ArtPipeline.HudIconImport.ImportFromCommandLine</summary>
    public static class HudIconImport
    {
        public static void ImportFromCommandLine() { Import(); EditorApplication.Exit(0); }

        [MenuItem("AdventureKing/Art/Re-import HUD icons as pixel art")]
        public static void Import()
        {
            foreach (string file in Directory.GetFiles("Assets/Art/UI/Hud", "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (name.StartsWith("portrait_") || name.StartsWith("avatar_")) continue;       // hero busts / avatar masks keep their own import
                string path = file.Replace('\\', '/');
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                if (ti == null) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
            }
            foreach (string file in Directory.GetFiles("Assets/Art/UI/Skills", "*.png"))      // skill / action button pictures: pixel art, Point, uncompressed
            {
                var si = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
                if (si == null) continue;
                si.textureType = TextureImporterType.Sprite; si.filterMode = FilterMode.Point; si.mipmapEnabled = false; si.alphaIsTransparency = true; si.textureCompression = TextureImporterCompression.Uncompressed;
                si.SaveAndReimport();
            }
            // bag / shop item slots: the Meshy slot frame, 9-sliced (the rim keeps its width, the centre stretches)
            var cell = (TextureImporter)AssetImporter.GetAtPath("Assets/Art/UI/Skin/item_cell.png");
            if (cell != null)
            {
                cell.textureType = TextureImporterType.Sprite; cell.filterMode = FilterMode.Point; cell.mipmapEnabled = false; cell.textureCompression = TextureImporterCompression.Uncompressed;
                cell.spriteBorder = new Vector4(20f, 20f, 20f, 20f);
                cell.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
        }
    }
}
