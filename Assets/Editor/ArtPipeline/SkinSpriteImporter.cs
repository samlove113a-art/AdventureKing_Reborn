using UnityEditor;
using UnityEngine;

namespace AdventureKing.ArtPipeline
{
    /// <summary>Every picture under Resources/Fashion/Skins is a full-body skin frame (192 px cell, same scale and pivot as the hero strips): Sprite, Point, uncompressed, no mips.</summary>
    public sealed class SkinSpriteImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Fashion/Skins/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 101.052635f;
            ti.spriteAlignment = (int)SpriteAlignment.Custom;
            ti.spritePivot = new Vector2(0.5f, 0.052105263f);   // feet pivot, identical to the hero strips
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
