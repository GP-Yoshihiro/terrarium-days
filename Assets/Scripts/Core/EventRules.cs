using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TerrariumDays.Core
{
    /// <summary>Why an animal cannot be listed (spec ★A). Appended values only.</summary>
    public enum ListingProblem
    {
        None,
        NotFound,
        InTray,
        Gravid,
        Pairing,
        Weak
    }

    public static class EventRules
    {
        public static ListingProblem Check(Colony colony, PetState pet)
        {
            if (pet == null || colony.AnimalById(pet.Id) != pet)
            {
                return ListingProblem.NotFound;
            }

            if (colony.PairingOf(pet) != null)
            {
                return ListingProblem.Pairing;
            }

            if (colony.CageOf(pet) == null)
            {
                return ListingProblem.InTray;
            }

            if (pet.Gravid != null)
            {
                return ListingProblem.Gravid;
            }

            return pet.Weak ? ListingProblem.Weak : ListingProblem.None;
        }

        /// <summary>The §10.1 market price with this event's demand (the base value of the odds).</summary>
        public static long BaseValue(PetState pet, int demandSeed, EventKind kind) =>
            MarketPrice.For(pet, EventDemand.For(demandSeed, pet.Genotype, kind == EventKind.Big), pet.Weak);

        /// <summary>"レイプター♀" — the look plus the sex mark ("?" while unknown).</summary>
        public static string MorphLabel(PetState pet) =>
            MorphNamer.VisualName(pet.Genotype) + (!pet.SexKnown ? "?" : pet.Sex == Sex.Female ? "♀" : "♂");
    }

    /// <summary>
    /// Event seeds (spec §2.7). Built with <see cref="EventDemand.Unit"/> so they are the same on
    /// every platform; the actions and booths then use <see cref="BreedingRandom.For"/> streams 6 and 7.
    /// </summary>
    public static class EventRandom
    {
        public const int ActionStream = 6;
        public const int BoothStream = 7;

        public static int DemandSeed(int colonySeed, int number) =>
            ToSeed(EventDemand.Unit(colonySeed, "event-demand:" + number.ToString(CultureInfo.InvariantCulture)));

        public static int ListingSeed(int colonySeed, int number, IEnumerable<EventListing> listings)
        {
            var sorted = new List<EventListing>(listings);
            sorted.Sort((x, y) => x.AnimalId.CompareTo(y.AnimalId));
            var key = new StringBuilder("event-listing:").Append(number.ToString(CultureInfo.InvariantCulture)).Append('|');
            foreach (var listing in sorted)
            {
                key.Append(listing.AnimalId.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(listing.Price.ToString(CultureInfo.InvariantCulture)).Append(':')
                    .Append(listing.TolerancePercent.ToString(CultureInfo.InvariantCulture)).Append(';');
            }

            return ToSeed(EventDemand.Unit(colonySeed, key.ToString()));
        }

        public static Random ForAction(int listingSeed, int actionIndex, EventActionKind kind) =>
            BreedingRandom.For(listingSeed, ActionStream, actionIndex, (int)kind);

        public static Random ForBooth(int listingSeed, int actionIndex) =>
            BreedingRandom.For(listingSeed, BoothStream, actionIndex, 0);

        private static int ToSeed(double unit) => (int)(unit * int.MaxValue);
    }
}
