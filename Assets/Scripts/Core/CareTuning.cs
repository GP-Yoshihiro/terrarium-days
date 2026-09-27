namespace TerrariumDays.Core
{
    /// <summary>
    /// Single source of truth for care effect amounts and offline-progress rates.
    /// See Terrarium_Days_仕様書.md sections 6 and 7.2.
    /// </summary>
    public sealed class CareTuning
    {
        public double FeedHungerAmount { get; set; } = 30d;

        /// <summary>Hunger at or above this is "full": Feed refuses (no charge, no weight, no hunger change).</summary>
        public double FeedFullThreshold { get; set; } = 95d;
        public double RefreshWaterHydrationAmount { get; set; } = 30d;
        public double CleanCleanlinessAmount { get; set; } = 40d;

        public double HungerDecayPerHour { get; set; } = 4d;
        public double HydrationDecayPerHour { get; set; } = 4d;
        public double CleanlinessDecayPerHour { get; set; } = 3d;

        public double HealthDecayPerHour { get; set; } = 5d;
        public double HealthRecoveryPerHour { get; set; } = 2d;

        public double HealthyCareThreshold { get; set; } = 40d;
        public double LowCareThreshold { get; set; } = 20d;

        /// <summary>§5.5: health at or below this makes the animal weak (衰弱).</summary>
        public double WeakHealthThreshold { get; set; } = 0d;

        /// <summary>§5.5: a weak animal recovers once health is back to this.</summary>
        public double WeakRecoveryHealth { get; set; } = 30d;

        // Body weight and growth stages (grams / game months). Stage-up needs these, then a
        // PreGrowthFastGameDays fast, then a shed.
        public double HatchlingWeightGrams { get; set; } = 3d;
        public double JuvenileMinWeightGrams { get; set; } = 15d;
        public double AdultMinWeightGrams { get; set; } = 40d;
        public double AdultMinAgeMonths { get; set; } = 10d;
        public double FeedWeightGainGrams { get; set; } = 2.2d;
        public double FemaleWeightCapGrams { get; set; } = 60d;
        public double MaleWeightCapGrams { get; set; } = 75d;
        public double UnknownSexWeightCapGrams { get; set; } = 65d;
        public double FastingWeightLossPerGameDay { get; set; } = 0.1d;
        public double PreGrowthFastGameDays { get; set; } = 3d;

        // Shedding, in game days.
        public double PreShedGameDays { get; set; } = 2d;
        public double YoungShedIntervalGameDays { get; set; } = 17.5d;
        public double AdultShedIntervalGameDays { get; set; } = 45d;

        public double AnorexiaHungerDecayMultiplier { get; set; } = 0.3d;

        public double MaxOfflineProgressHours { get; set; } = 12d;
        public double OfflineProgressStepMinutes { get; set; } = 1d;

        /// <summary>A bought animal's personality shows after it has been kept this many game days (§5.2, §9).</summary>
        public double PersonalityRevealGameDays { get; set; } = 7d;

        // Breeding (§7), in game months / grams / game days.
        public int BreedingSeasonFirstMonth { get; set; } = 3;
        public int BreedingSeasonLastMonth { get; set; } = 9;
        public double FemaleBreedingMinAgeMonths { get; set; } = 10d;
        public double FemaleBreedingMinWeightGrams { get; set; } = 45d;
        public double MaleBreedingMinAgeMonths { get; set; } = 8d;
        public double MaleBreedingMinWeightGrams { get; set; } = 40d;
        public double PairingGameDays { get; set; } = 3d;
        public double BaseMatingSuccess { get; set; } = 0.7d;
        public double MaxMatingSuccess { get; set; } = 0.95d;
        public double FirstClutchMinGameDays { get; set; } = 21d;
        public double FirstClutchMaxGameDays { get; set; } = 28d;
        public double ClutchIntervalMinGameDays { get; set; } = 14d;
        public double ClutchIntervalMaxGameDays { get; set; } = 28d;
        public int MinClutches { get; set; } = 4;
        public int MaxClutches { get; set; } = 8;
        public double SingleEggChance { get; set; } = 0.1d;
        public double ClutchWeightLossMinGrams { get; set; } = 3d;
        public double ClutchWeightLossMaxGrams { get; set; } = 5d;
        public double LayingStopWeightGrams { get; set; } = 40d;
        public double Fertility { get; set; } = 0.9d;
        public double BadMatchFertility { get; set; } = 0.75d;

        /// <summary>§7.7: an egg laid without a nest box dries out after this many game days.</summary>
        public double LooseEggDryGameDays { get; set; } = 2d;

        /// <summary>Game days below 24 ℃ (in total) that kill a nest-box egg (planner's ruling).</summary>
        public double EggColdFailGameDays { get; set; } = 3d;
    }
}
