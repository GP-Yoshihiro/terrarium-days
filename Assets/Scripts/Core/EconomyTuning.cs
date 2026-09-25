namespace TerrariumDays.Core
{
    /// <summary>Every price and running cost in yen, in one place.</summary>
    public sealed class EconomyTuning
    {
        public long StartingMoney { get; set; } = 50000;

        public long FeedCostBaby { get; set; } = 30;
        public long FeedCostJuvenile { get; set; } = 50;
        public long FeedCostAdult { get; set; } = 80;

        public long ElectricityPerCagePerMonth { get; set; } = 300;
        public long ElectricityPerIncubatorPerMonth { get; set; } = 500;
    }
}
