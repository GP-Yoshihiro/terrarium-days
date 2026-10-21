using UnityEditor;
using UnityEngine;

namespace TerrariumDays.Editor
{
    /// <summary>
    /// Imports the generated pixel-art sprites (tools/sprites/gecko_sprites.py) crisp:
    /// point filtering, no mipmaps, no compression, so frames never blur or shimmer.
    /// </summary>
    public sealed class PixelArtTexturePostprocessor : AssetPostprocessor
    {
        private static readonly string[] PixelArtFolders =
        {
            "Assets/Resources/Gecko/",
            "Assets/Resources/Effects/",
            "Assets/Resources/Candling/",
            "Assets/Resources/Event/",
        };

        public override uint GetVersion() => 4;

        private void OnPreprocessTexture()
        {
            if (!IsPixelArt(assetPath))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            // Event visitor and Candling frames are code-loaded textures: keep them Default like the geckos (proven on device). The rest are Sprites for USS/9-slice.
            importer.textureType = (assetPath.StartsWith("Assets/Resources/Event/") && !assetPath.StartsWith("Assets/Resources/Event/visitor_"))
                ? TextureImporterType.Sprite
                : TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (assetPath == "Assets/Resources/Event/bubble.png")
            {
                // 9-slice (left, bottom, right, top): 8 px borders at output scale; left is 20 so the tail (logical x 5..9) is never stretched.
                importer.spriteBorder = new Vector4(20, 8, 8, 8);
            }
        }

        private static bool IsPixelArt(string path)
        {
            foreach (var folder in PixelArtFolders)
            {
                if (path.StartsWith(folder))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
