namespace TerrariumDays.Core
{
    /// <summary>
    /// Single source of truth for care effect amounts and offline-progress rates.
    /// See Terrarium_Days_仕様書.md sections 6 and 7.2.
    /// </summary>
    public sealed class CareTuning
    {
        public double FeedHungerAmount { get; set; } = 30d;
        public double RefreshWaterHydrationAmount { get; set; } = 30d;
        public double CleanCleanlinessAmount { get; set; } = 40d;

        public double HungerDecayPerHour { get; set; } = 4d;
        public double HydrationDecayPerHour { get; set; } = 4d;
        public double CleanlinessDecayPerHour { get; set; } = 3d;

        public double HealthDecayPerHour { get; set; } = 5d;
        public double HealthRecoveryPerHour { get; set; } = 2d;

        public double HealthyCareThreshold { get; set; } = 40d;
        public double LowCareThreshold { get; set; } = 20d;

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
    }
}
