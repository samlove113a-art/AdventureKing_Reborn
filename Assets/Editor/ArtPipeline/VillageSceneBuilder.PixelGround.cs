using System.IO;
using AdventureKing.Gameplay;
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

        /// <summary>All ten Chapter 1 maps with the pixel-art look (ground, decks, props, panorama).
        ///   Unity -batchmode -quit -executeMethod AdventureKing.ArtPipeline.VillageSceneBuilder.BuildChapter1AllPixelFromCommandLine</summary>
        public static void BuildChapter1AllPixelFromCommandLine()
        {
            try
            {
                EnsureLayer(OneWayLayer, "OneWayPlatform");
                BuildCh1Village();
                BuildCh1GreenMeadow();
                BuildCh1GlowForest();
                foreach (Ch1MapData.Ch1Map m in Ch1MapData.All) BuildCh1Generated(m);
                RegisterBuildScenes();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>Only the generated maps with an index >= env AK_FROM_MAP (default 6): a long full rebuild can be resumed.</summary>
        public static void BuildChapter1GeneratedFromMapFromCommandLine()
        {
            try
            {
                int from = int.TryParse(System.Environment.GetEnvironmentVariable("AK_FROM_MAP"), out int f) ? f : 6;
                EnsureLayer(OneWayLayer, "OneWayPlatform");
                foreach (Ch1MapData.Ch1Map m in Ch1MapData.All) if (m.Index >= from) BuildCh1Generated(m);
                RegisterBuildScenes();
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>"ch1_m07" -> "07"; null for the hub, the meadow and the halls.</summary>
        private static string PixelMapNo => SceneTag != null && SceneTag.StartsWith("ch1_m") && SceneTag.Length == 7 ? SceneTag.Substring(5) : null;

        private static bool UsePixelGround => PixelMapNo != null && File.Exists($"{PixelGroundDir}/ground_top_m{PixelMapNo}.png") && File.Exists($"{PixelGroundDir}/ground_earth_m{PixelMapNo}.png");

        /// <summary>Uses the map's pixel-art panorama (Meshy, cleaned by Tools/world) instead of the procedural painting. True = applied.</summary>
        private static bool TryPixelPanorama()
        {
            if (PixelMapNo == null) return false;
            string path = $"{WorldDir}/Generated/c1_m{PixelMapNo}_pixel_panorama_v2.png";
            if (!File.Exists(path)) return false;
            var bd = GameObject.Find("Backdrop_Painting");
            if (!bd) throw new System.InvalidOperationException("Backdrop_Painting not found after FinishWorld");
            bd.GetComponent<MeshRenderer>().sharedMaterial = CutoutMat(path, 0f, fogAmount: 0f, cutoff: 0f);
            return true;
        }

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

        private const string PixelPropDir = WorldDir + "/PixelProps";

        private static Sprite PixelFile(string path, SpriteAlignment align, bool fullRect, bool repeat)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); ti = (TextureImporter)AssetImporter.GetAtPath(path); }
            var st = new TextureImporterSettings();
            ti.ReadTextureSettings(st);
            bool dirty = ti.textureType != TextureImporterType.Sprite || !Mathf.Approximately(ti.spritePixelsPerUnit, PixelPpu) || ti.filterMode != FilterMode.Point
                         || ti.textureCompression != TextureImporterCompression.Uncompressed || ti.mipmapEnabled || st.spriteAlignment != (int)align
                         || (st.spriteMeshType == SpriteMeshType.FullRect) != fullRect || (ti.wrapMode == TextureWrapMode.Repeat) != repeat;
            if (dirty)
            {
                ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.spritePixelsPerUnit = PixelPpu;
                ti.filterMode = FilterMode.Point; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.mipmapEnabled = false; ti.alphaIsTransparency = true;
                ti.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                ti.ReadTextureSettings(st);
                st.spriteMeshType = fullRect ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
                st.spriteAlignment = (int)align;
                ti.SetTextureSettings(st);
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>The pixel-art replacement of a Map 1 Meshy prop (or null = keep the 3D one).</summary>
        private static GameObject PixelProp(Transform parent, string meshyName, float x, float z, float scale, bool flip, float y, Color? tint)
        {
            if (!UsePixelGround) return null;
            string file = meshyName switch
            {
                "ch1_mushroom_house" => "ch1_house", "ch1_giant_mushroom_tree" => "ch1_tree", "ch1_crystal_fountain" => "ch1_fountain",
                "ch1_mushroom_stall" => "ch1_stall", "ch1_mushroom_lamp" => "ch1_lamp", "crates" => "ch1_crates", "planter" => "ch1_planter",
                "ch1_flower_bush" => "ch1_flower_bush", "ch1_wood_fence" => "ch1_fence", "banyan_tree" => "ch1_banyan", "windmill" => "ch1_windmill", _ => null,
            };
            string path = $"{PixelPropDir}/{file}.png";
            if (file == null || !File.Exists(path)) return null;
            Sprite s = PixelFile(path, SpriteAlignment.BottomCenter, false, false);
            var g = new GameObject(meshyName + "_pixel");
            g.transform.SetParent(parent, false);
            g.transform.position = new Vector3(x, y - 0.12f, z);                 // sink the roots / feet a little into the grass
            g.transform.localScale = Vector3.one * scale;
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.flipX = flip; sr.sharedMaterial = Chibi2DArt.PixelMaterial();
            if (tint.HasValue) sr.color = tint.Value;
            sr.sortingOrder = -10 - Mathf.RoundToInt(z);                         // farther = further back; the ground lip (-5) overlaps the foot
            return g;
        }

        // heights (metres) the pixel prop sprites were made at; Prop() callers pass the wanted height, C1Prop() callers pass a multiplier
        private static readonly System.Collections.Generic.Dictionary<string, float> PixelBaseHeight = new()
        {
            ["crates"] = 1.3f, ["planter"] = 1.0f, ["banyan_tree"] = 6f, ["windmill"] = 6f,
        };

        private static GameObject PixelPropByHeight(Transform parent, string name, float x, float z, float height, bool flip, float y)
        {
            if (!UsePixelGround || !PixelBaseHeight.TryGetValue(name, out float baseH)) return null;
            return PixelProp(parent, name, x, z, height / baseH, flip, y, null);
        }

        /// <summary>Pixel-art deck: a tiled plank strip on the walking height + wooden posts down to the ground. The collider is the same as the 3D deck.</summary>
        private static void PixelDeck(Transform art, Transform solids, TownLayout.Deck d)
        {
            Solid(solids, d.Name, d.XMin, d.XMax, d.Top, 0.4f, true);
            Sprite planks = PixelFile($"{PixelGroundDir}/deck_planks.png", SpriteAlignment.TopLeft, true, true);
            Sprite post = PixelFile($"{PixelGroundDir}/deck_post.png", SpriteAlignment.TopCenter, true, true);
            Material mat = Chibi2DArt.PixelMaterial();
            var root = Group(art, "Deck_" + d.Name);
            float w = d.XMax - d.XMin, plankH = planks.rect.height / PixelPpu;
            var g = new GameObject("Planks");
            g.transform.SetParent(root, false);
            g.transform.position = new Vector3(d.XMin, d.Top + 0.03f, 0.1f);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = planks; sr.drawMode = SpriteDrawMode.Tiled; sr.tileMode = SpriteTileMode.Continuous; sr.size = new Vector2(w, plankH);
            sr.sharedMaterial = mat; sr.sortingOrder = -3;
            float top = d.Top + 0.03f - plankH, drop = Mathf.Max(0f, top - 0.02f);
            int posts = Mathf.Max(2, Mathf.RoundToInt(w / 6f) + 1);
            for (int i = 0; i < posts && drop > 0.2f; i++)
            {
                var p = new GameObject("Post");
                p.transform.SetParent(root, false);
                p.transform.position = new Vector3(Mathf.Lerp(d.XMin + 0.4f, d.XMax - 0.4f, i / (posts - 1f)), top, 0.25f);
                var ps = p.AddComponent<SpriteRenderer>();
                ps.sprite = post; ps.drawMode = SpriteDrawMode.Tiled; ps.tileMode = SpriteTileMode.Continuous; ps.size = new Vector2(post.rect.width / PixelPpu, drop);
                ps.sharedMaterial = mat; ps.sortingOrder = -4;
            }
        }

        /// <summary>Tiled grass lip on top of a tiled earth face; the grass line of the lip sits on y = 0 (the walking line).</summary>
        private static void PixelGround(Transform art, float minX, float maxX)
        {
            Sprite top = PixelSprite("ground_top_m" + PixelMapNo), earthFace = PixelSprite("ground_earth_m" + PixelMapNo);
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
