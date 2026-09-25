using System.Collections.Generic;
using TerrariumDays.Core;

namespace TerrariumDays.UI
{
    /// <summary>One sprite library per morph look, shared by the cage view and home thumbnails.</summary>
    public static class MorphSprites
    {
        private static readonly Dictionary<string, PetSpriteLibrary> byKey = new Dictionary<string, PetSpriteLibrary>();
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
    }
}
