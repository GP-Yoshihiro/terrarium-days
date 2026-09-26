using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>Every price and running cost in yen, in one place (§6.3).</summary>
    public sealed class EconomyTuning
    {
        public long StartingMoney { get; set; } = 50000;

        public long FeedCostBaby { get; set; } = 30;
        public long FeedCostJuvenile { get; set; } = 50;
        public long FeedCostAdult { get; set; } = 80;

        public long ElectricityPerCagePerMonth { get; set; } = 300;
        public long ElectricityPerIncubatorPerMonth { get; set; } = 500;

        // Shop (§9, §8).
        public long SmallCagePrice { get; set; } = 3000;
        public long StandardCagePrice { get; set; } = 6000;
        public long LargeCagePrice { get; set; } = 10000;
        public long RackPrice { get; set; } = 8000;
        public int MaxRacks { get; set; } = 4;
        public long NestBoxPrice { get; set; } = 1500;
        public long StandardIncubatorPrice { get; set; } = 15000;
        public long LuxuryIncubatorPrice { get; set; } = 40000;

        /// <summary>Per decor id (see DecorItems).</summary>
        public IReadOnlyDictionary<string, long> DecorPrices { get; set; } = new Dictionary<string, long>
        {
            ["rock_01"] = 1000,
            ["water_dish_01"] = 1000,
            ["plant_01"] = 1500,
            ["driftwood_01"] = 2000,
            ["heat_lamp_01"] = 3000,
        };

        /// <summary>Shop animals sell at market × this (§9).</summary>
        public double ShopMarkup { get; set; } = 1.2d;

        /// <summary>Wholesale pays market × this (§9).</summary>
        public double WholesaleRate { get; set; } = 0.4d;

        public long CagePrice(CageSize size) =>
            size == CageSize.Small ? SmallCagePrice : size == CageSize.Large ? LargeCagePrice : StandardCagePrice;
    }
}
