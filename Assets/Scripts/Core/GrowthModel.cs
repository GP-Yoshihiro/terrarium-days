using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Growth by body weight and age, as breeders judge it. Pure so it is EditMode tested.
    /// </summary>
    public static class GrowthModel
    {
        public static GrowthStage? NextStage(GrowthStage stage)
        {
            switch (stage)
            {
                case GrowthStage.Baby:
                    return GrowthStage.Juvenile;
                case GrowthStage.Juvenile:
                    return GrowthStage.Adult;
                default:
                    return null;
            }
        }

        public static double MinWeightFor(GrowthStage stage, CareTuning tuning)
        {
            switch (stage)
            {
                case GrowthStage.Juvenile:
                    return tuning.JuvenileMinWeightGrams;
                case GrowthStage.Adult:
                    return tuning.AdultMinWeightGrams;
                default:
                    return tuning.HatchlingWeightGrams;
            }
        }

        /// <summary>Age in game months: one real day is one game month.</summary>
        public static double AgeMonths(PetState pet, DateTimeOffset nowUtc) =>
            Math.Max(0d, (nowUtc - pet.HatchedAtUtc).TotalDays);

        public static bool MeetsNextStage(PetState pet, DateTimeOffset nowUtc, CareTuning tuning)
        {
            var next = NextStage(pet.Stage);
            if (!next.HasValue || pet.WeightGrams < MinWeightFor(next.Value, tuning))
            {
                return false;
            }

            return next.Value != GrowthStage.Adult || AgeMonths(pet, nowUtc) >= tuning.AdultMinAgeMonths;
        }

        public static double WeightCap(PetState pet, CareTuning tuning)
        {
            if (!pet.SexKnown)
            {
                return tuning.UnknownSexWeightCapGrams;
            }

            return pet.Sex == Sex.Female ? tuning.FemaleWeightCapGrams : tuning.MaleWeightCapGrams;
        }

        public static double FeedGain(PetState pet, CareTuning tuning) =>
            tuning.FeedWeightGainGrams * Math.Max(0d, 1d - pet.WeightGrams / WeightCap(pet, tuning));

        public static double ProgressToNextStage(PetState pet, DateTimeOffset nowUtc, CareTuning tuning)
        {
            var next = NextStage(pet.Stage);
            if (!next.HasValue)
            {
                return 1d;
            }

            var from = MinWeightFor(pet.Stage, tuning);
            var to = MinWeightFor(next.Value, tuning);
            var byWeight = Clamp01((pet.WeightGrams - from) / (to - from));
            if (next.Value != GrowthStage.Adult)
            {
                return byWeight;
            }

            return Math.Min(byWeight, Clamp01(AgeMonths(pet, nowUtc) / tuning.AdultMinAgeMonths));
        }

        public static string StageLabel(GrowthStage stage)
        {
            switch (stage)
            {
                case GrowthStage.Juvenile:
                    return "ヤング";
                case GrowthStage.Adult:
                    return "アダルト";
                default:
                    return "ベビー";
            }
        }

        private static double Clamp01(double value) => value < 0d ? 0d : value > 1d ? 1d : value;
    }
}
