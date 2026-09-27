using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>Why a pairing cannot start (§7.1). None = it can.</summary>
    public enum PairingProblem
    {
        None,
        NotFound,
        OutOfSeason,
        NotMaleAndFemale,
        SexUnknown,
        Weak,
        TooYoung,
        TooLight,
        Gravid,
        AlreadyPairing
    }

    /// <summary>Breeding conditions and odds (§7.1–7.6). Pure: no Unity, no clock of its own.</summary>
    public static class BreedingRules
    {
        /// <summary>The weight factor reaches 1 this many grams above the female minimum.</summary>
        public const double WeightFactorSpanGrams = 10d;

        public const double WeightFactorFloor = 0.85d;
        public const double HealthFactorFloor = 0.5d;

        public static bool IsBreedingSeason(GameDate date, CareTuning care) =>
            date.Month >= care.BreedingSeasonFirstMonth && date.Month <= care.BreedingSeasonLastMonth;

        /// <summary>The season (game year) a date belongs to, or -1 outside the breeding season.</summary>
        public static int SeasonOf(GameDate date, CareTuning care) => IsBreedingSeason(date, care) ? date.Year : -1;

        /// <summary>
        /// Whether this animal can take the given role now: sex known and right, not weak, old
        /// and heavy enough. The season, gravid state and the room are checked by BreedingService.
        /// </summary>
        public static PairingProblem CheckAnimal(PetState pet, Sex role, DateTimeOffset nowUtc, CareTuning care)
        {
            if (pet == null)
            {
                return PairingProblem.NotFound;
            }

            if (!pet.SexKnown)
            {
                return PairingProblem.SexUnknown;
            }

            if (pet.Sex != role)
            {
                return PairingProblem.NotMaleAndFemale;
            }

            if (pet.Weak)
            {
                return PairingProblem.Weak;
            }

            var female = role == Sex.Female;
            var minAge = female ? care.FemaleBreedingMinAgeMonths : care.MaleBreedingMinAgeMonths;
            if (GrowthModel.AgeMonths(pet, nowUtc) < minAge)
            {
                return PairingProblem.TooYoung;
            }

            var minWeight = female ? care.FemaleBreedingMinWeightGrams : care.MaleBreedingMinWeightGrams;
            return pet.WeightGrams < minWeight ? PairingProblem.TooLight : PairingProblem.None;
        }

        /// <summary>0.5 at health 0 up to 1 at health 100, from the less healthy of the two.</summary>
        public static double HealthFactor(PetState male, PetState female)
        {
            var health = Math.Min(male.Health, female.Health);
            return HealthFactorFloor + (1d - HealthFactorFloor) * health / 100d;
        }

        /// <summary>0.85 at the female minimum weight up to 1 at 10 g above it.</summary>
        public static double WeightFactor(PetState female, CareTuning care)
        {
            var above = (female.WeightGrams - care.FemaleBreedingMinWeightGrams) / WeightFactorSpanGrams;
            return WeightFactorFloor + (1d - WeightFactorFloor) * Math.Max(0d, Math.Min(1d, above));
        }

        /// <summary>§7.3: base 70% × compatibility × health × weight, at most CareTuning.MaxMatingSuccess.</summary>
        public static double MatingSuccess(Compatibility compatibility, PetState male, PetState female, CareTuning care) =>
            Math.Min(care.MaxMatingSuccess, care.BaseMatingSuccess * PersonalityTraits.MatingSuccessMultiplier(compatibility)
                * HealthFactor(male, female) * WeightFactor(female, care));

        /// <summary>§7.4: 4–8 clutches, ±1 by compatibility, at least 1.</summary>
        public static (int Min, int Max) ClutchRange(Compatibility compatibility, CareTuning care)
        {
            var delta = PersonalityTraits.ClutchDelta(compatibility);
            return (Math.Max(1, care.MinClutches + delta), Math.Max(1, care.MaxClutches + delta));
        }

        /// <summary>§7.6: 90%, or 75% for a bad match.</summary>
        public static double FertilityFor(Compatibility compatibility, CareTuning care) =>
            compatibility == Compatibility.Bad ? care.BadMatchFertility : care.Fertility;
    }

    /// <summary>
    /// What the pairing screen shows before starting (§7.2), from what the player knows: an
    /// unknown personality means an unknown match, estimated as a normal one.
    /// </summary>
    public sealed class BreedingForecast
    {
        private BreedingForecast()
        {
        }

        public bool CompatibilityKnown { get; private set; }

        public Compatibility Compatibility { get; private set; }

        public double SuccessChance { get; private set; }

        public int MinClutches { get; private set; }

        public int MaxClutches { get; private set; }

        public List<MorphOdds> Offspring { get; private set; }

        public static BreedingForecast For(PetState male, PetState female, CareTuning care)
        {
            var known = male.PersonalityKnown && female.PersonalityKnown;
            var compatibility = known ? PersonalityTraits.CompatibilityOf(male.Personality, female.Personality) : Compatibility.Normal;
            var clutches = BreedingRules.ClutchRange(compatibility, care);
            return new BreedingForecast
            {
                CompatibilityKnown = known,
                Compatibility = compatibility,
                SuccessChance = BreedingRules.MatingSuccess(compatibility, male, female, care),
                MinClutches = clutches.Min,
                MaxClutches = clutches.Max,
                Offspring = GeneticsCalculator.PredictVisualOdds(female.Genotype, female.Known, male.Genotype, male.Known),
            };
        }
    }
}
