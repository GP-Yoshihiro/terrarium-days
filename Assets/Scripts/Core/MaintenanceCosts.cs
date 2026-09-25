namespace TerrariumDays.Core
{
    public static class MaintenanceCosts
    {
        public static long FeedCost(GrowthStage stage, EconomyTuning tuning)
        {
            switch (stage)
            {
                case GrowthStage.Juvenile:
                    return tuning.FeedCostJuvenile;
                case GrowthStage.Adult:
                    return tuning.FeedCostAdult;
                default:
                    return tuning.FeedCostBaby;
            }
        }

        public static long MonthlyElectricity(int cages, int incubators, EconomyTuning tuning) =>
            cages * tuning.ElectricityPerCagePerMonth + incubators * tuning.ElectricityPerIncubatorPerMonth;
    }
}
