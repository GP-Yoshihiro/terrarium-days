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
        public double GrowthPerHour { get; set; } = 100d / 168d;

        public double HealthyCareThreshold { get; set; } = 40d;
        public double LowCareThreshold { get; set; } = 20d;

        // Food refusal (拒食) and shedding (脱皮). Leopard geckos stop eating in the days before a
        // shed and around growth spurts; they are not ill, so hunger falls slower and low hunger
        // neither hurts health nor blocks growth while they refuse food.
        public double ShedIntervalDays { get; set; } = 60d;
        public double PreShedDays { get; set; } = 2d;
        /// <summary>Percent of the current stage's gauge after which the pet fasts before growing.</summary>
        public double PreGrowthGaugePercent { get; set; } = 90d;
        public double AnorexiaHungerDecayMultiplier { get; set; } = 0.3d;

        public double MaxOfflineProgressHours { get; set; } = 12d;
        public double OfflineProgressStepMinutes { get; set; } = 1d;
    }
}
