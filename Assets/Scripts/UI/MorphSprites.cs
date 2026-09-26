using System.Collections.Generic;
using TerrariumDays.Core;
using TerrariumDays.Gameplay;
using UnityEngine;

namespace TerrariumDays.UI
{
    /// <summary>One sprite library per morph look, shared by the cage view and home thumbnails.</summary>
    public static class MorphSprites
    {
        public const float ThumbnailPadding = 6f;

        private static readonly Dictionary<string, PetSpriteLibrary> byKey = new Dictionary<string, PetSpriteLibrary>();
        private static readonly Dictionary<string, Sprite> thumbnails = new Dictionary<string, Sprite>();
        private static PetSpriteLibrary baseLibrary;

        public static PetSpriteLibrary Base => baseLibrary ?? (baseLibrary = PetSpriteLibrary.LoadFromResources());

        public static PetSpriteLibrary For(PetState pet)
        {
            var palette = MorphAppearance.PaletteFor(pet.Genotype, pet.Stage);
            if (!byKey.TryGetValue(palette.Key, out var library))
            {
                library = Base.WithPalette(palette);
                byKey[palette.Key] = library;
            }

            return library;
        }

        /// <summary>
        /// The idle frame cropped to the pet's body (see ThumbnailCrop), so home/list
        /// thumbnails are not mostly transparent padding. Cached per morph.
        /// </summary>
        public static Sprite Thumbnail(PetState pet)
        {
            var key = MorphAppearance.PaletteFor(pet.Genotype, pet.Stage).Key;
            if (thumbnails.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var frame = For(pet).Frame(PetClip.Idle, 0);
            if (frame == null)
            {
                return null;
            }

            var footprint = new TerrariumArtLayout().Pet;
            var crop = ThumbnailCrop.BodyRect(footprint, ThumbnailPadding);
            // The idle frame is its own whole texture (not a shared atlas), so the crop
            // rect only needs scaling from the footprint's reference size to the texture's
            // actual pixel size (normally 1:1).
            var scale = frame.width / footprint.ImageWidth;
            var rect = new Rect(crop.X * scale, crop.Y * scale, crop.Width * scale, crop.Height * scale);
            var sprite = Sprite.Create(frame, rect, new Vector2(0.5f, 0.5f), 100f);
            thumbnails[key] = sprite;
            return sprite;
        }
    }
}
