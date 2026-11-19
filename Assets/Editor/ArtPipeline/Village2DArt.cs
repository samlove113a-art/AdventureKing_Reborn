using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>Imports the Village 2D pixel-art pictures (Assets/Art/World/Village2D/*.png, Meshy 2D PNG output): Sprite, Point (no filter), uncompressed, pivot at the base (bottom-centre), hero scale.
    ///   Unity -batchmode -nographics -quit -executeMethod AdventureKing.ArtPipeline.Village2DArt.ImportFromCommandLine</summary>
    public static class Village2DArt
    {
        public const string Dir = "Assets/Art/World/Village2D";

        public static void ImportFromCommandLine() { Import(); EditorApplication.Exit(0); }

        [MenuItem("AdventureKing/Art/Import Village 2D art")]
        public static void Import()
        {
            foreach (string file in Directory.GetFiles(Dir, "*.png"))
            {
                string path = file.Replace('\\', '/');
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                if (ti == null) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 192f / 1.9f;
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                var st = new TextureImporterSettings(); ti.ReadTextureSettings(st); st.spriteAlignment = (int)SpriteAlignment.Custom; ti.SetTextureSettings(st);
                ti.spritePivot = new Vector2(0.5f, 0f);
                ti.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
        }
    }
}
