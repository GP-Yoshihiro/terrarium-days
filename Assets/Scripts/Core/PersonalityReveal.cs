using System;

namespace TerrariumDays.Core
{
    /// <summary>A bought animal's personality becomes known a while after purchase (§5.2, §9).</summary>
    public static class PersonalityReveal
    {
        public static bool ApplyIfDue(PetState pet, DateTimeOffset nowUtc)
        {
            if (pet.PersonalityKnown || !pet.PersonalityRevealAtUtc.HasValue || pet.PersonalityRevealAtUtc.Value > nowUtc)
            {
                return false;
            }

            pet.PersonalityKnown = true;
            pet.PersonalityRevealAtUtc = null;
            return true;
        }

        public static string Message(PetState pet) =>
            $"{pet.Name}の性格は「{PersonalityTraits.Label(pet.Personality)}」のようです";
    }
}
