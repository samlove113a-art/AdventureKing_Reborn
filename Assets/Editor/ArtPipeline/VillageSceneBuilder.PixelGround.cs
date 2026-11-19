using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>
    /// Pixel-art Dark Fantasy ground (Tools/world/make_pixel_ground.py -> Assets/Art/World/PixelGround): a tiled grass lip + a tiled earth face,
    /// same 101 px per metre as the heroes, instead of the glossy 3D ground blocks. Pilot on Map 1 (SceneTag "ch1_m01"); the collider is unchanged.
    ///   Unity -batchmode -quit -executeMethod AdventureKing.ArtPipeline.VillageSceneBuilder.BuildChapter1Map1FromCommandLine
    /// </summary>
    public static partial class VillageSceneBuilder
    {
        private const string PixelGroundDir = WorldDir + "/PixelGround";
        private const float PixelPpu = 101.05f;

        public static void BuildChapter1Map1FromCommandLine()
        {
            try
            {
                EnsureLayer(OneWayLayer, "OneWayPlatform");
                BuildCh1Village();
                RegisterBuildScenes();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static bool UsePixelGround => SceneTag == "ch1_m01" && File.Exists($"{PixelGroundDir}/ground_top.png") && File.Exists($"{PixelGroundDir}/ground_earth.png");

        private static Sprite PixelSprite(string file)
        {
            string path = $"{PixelGroundDir}/{file}.png";
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); ti = (TextureImporter)AssetImporter.GetAtPath(path); }
            bool dirty = ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single || !Mathf.Approximately(ti.spritePixelsPerUnit, PixelPpu)
                         || ti.filterMode != FilterMode.Point || ti.textureCompression != TextureImporterCompression.Uncompressed || ti.wrapMode != TextureWrapMode.Repeat || ti.mipmapEnabled;
            if (dirty)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = PixelPpu;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                var st = new TextureImporterSettings();
                ti.ReadTextureSettings(st);
                st.spriteMeshType = SpriteMeshType.FullRect;     // Tiled draw mode needs a full rectangle
                st.spriteAlignment = (int)SpriteAlignment.TopLeft;
                ti.SetTextureSettings(st);
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Tiled grass lip on top of a tiled earth face; the grass line of the lip sits on y = 0 (the walking line).</summary>
        private static void PixelGround(Transform art, float minX, float maxX)
        {
            Sprite top = PixelSprite("ground_top"), earthFace = PixelSprite("ground_earth");
            Material mat = Chibi2DArt.PixelMaterial();
            float x0 = minX - 14f, x1 = maxX + 14f, w = x1 - x0;
            float grassLinePx = 18f;                                     // grass surface = 9 art cells below the top of the lip tile
            float topY = grassLinePx / PixelPpu, lipH = top.rect.height / PixelPpu;
            var root = new GameObject("PixelGround");
            root.transform.SetParent(art, false);

            SpriteRenderer Make(string name, Sprite s, float y, float height, int order)
            {
                var g = new GameObject(name);
                g.transform.SetParent(root.transform, false);
                g.transform.position = new Vector3(x0, y, 0.35f);          // TopLeft pivot: the sprite hangs down and right from here
                var sr = g.AddComponent<SpriteRenderer>();
                sr.sprite = s;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.tileMode = SpriteTileMode.Continuous;
                sr.size = new Vector2(w, height);
                sr.sharedMaterial = mat;
                sr.sortingOrder = order;
                return sr;
            }
            Make("EarthFace", earthFace, topY - lipH + 0.02f, 6f, -6);
            Make("GrassLip", top, topY, lipH, -5);
        }
    }
}
