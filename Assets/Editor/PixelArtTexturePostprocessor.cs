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
        };

        private void OnPreprocessTexture()
        {
            if (!IsPixelArt(assetPath))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
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
