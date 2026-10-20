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

        private void OnPreprocessTexture()
        {
            if (!IsPixelArt(assetPath))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = assetPath.StartsWith("Assets/Resources/Candling/")
                || assetPath.StartsWith("Assets/Resources/Event/")
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
                // 9-slice: 4 art px at the 2x output scale = 8 px on every side.
                importer.spriteBorder = new Vector4(8, 8, 8, 8);
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
