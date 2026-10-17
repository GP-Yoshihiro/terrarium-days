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
        private static readonly SpriteCachePolicy policy = new SpriteCachePolicy();
        private static HashSet<string> keysInUse = new HashSet<string>();
        private static PetSpriteLibrary baseLibrary;

        /// <summary>Number of recoloured looks currently cached (libraries and thumbnails share one policy).</summary>
        public static int CachedCount => policy.Count;

        /// <summary>The cache key of the look this pet currently shows.</summary>
        public static string KeyFor(PetState pet) => MorphAppearance.PaletteFor(pet.Genotype, pet.Stage).Key;

        /// <summary>
        /// Tells the cache which looks are on screen right now; those are never destroyed, even when
        /// that leaves the cache over its capacity. Call before rendering so a render cannot evict
        /// a sprite it is about to show.
        /// </summary>
        public static void SetKeysInUse(IEnumerable<string> keys)
        {
            keysInUse = keys == null ? new HashSet<string>() : new HashSet<string>(keys);
        }

        /// <summary>Drops every recoloured look (used by tests to start from an empty cache).</summary>
        public static void ClearCache()
        {
            keysInUse = new HashSet<string>();
            foreach (var key in new List<string>(byKey.Keys))
            {
                policy.Remove(key);
                byKey[key].ReleaseRecoloured();
            }

            foreach (var thumbnail in thumbnails.Values)
            {
                DestroyObject(thumbnail);
            }

            foreach (var key in new List<string>(thumbnails.Keys))
            {
                policy.Remove(key);
            }

            byKey.Clear();
            thumbnails.Clear();
        }

        private static void TrimCache()
        {
            foreach (var key in policy.Evict(keysInUse))
            {
                if (byKey.TryGetValue(key, out var library))
                {
                    library.ReleaseRecoloured();
                    byKey.Remove(key);
                }

                if (thumbnails.TryGetValue(key, out var thumbnail))
                {
                    DestroyObject(thumbnail);
                    thumbnails.Remove(key);
                }
            }
        }

        /// <summary>Destroys a runtime-made object: Destroy in play mode, DestroyImmediate in edit mode.</summary>
        internal static void DestroyObject(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
        }

        public static PetSpriteLibrary Base => baseLibrary ?? (baseLibrary = PetSpriteLibrary.LoadFromResources());

        public static PetSpriteLibrary For(PetState pet)
        {
            var palette = MorphAppearance.PaletteFor(pet.Genotype, pet.Stage);
            var created = false;
            if (!byKey.TryGetValue(palette.Key, out var library))
            {
                library = Base.WithPalette(palette);
                byKey[palette.Key] = library;
                created = true;
            }

            policy.Touch(palette.Key);
            if (created)
            {
                TrimCache();
            }

            return library;
        }

        /// <summary>
        /// The idle frame cropped to the pet's body (see ThumbnailCrop), so home/list
        /// thumbnails are not mostly transparent padding. Cached per morph.
        /// </summary>
        public static Sprite Thumbnail(PetState pet)
        {
            var key = KeyFor(pet);
            if (thumbnails.TryGetValue(key, out var cached) && cached != null)
            {
                policy.Touch(key);
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
            policy.Touch(key);
            TrimCache();
            return sprite;
        }
    }
}
