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

        public double MaxOfflineProgressHours { get; set; } = 12d;
        public double OfflineProgressStepMinutes { get; set; } = 1d;
    }
}
