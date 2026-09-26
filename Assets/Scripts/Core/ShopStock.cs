using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>One animal for sale. <see cref="Animal"/> becomes the colony's own once bought.</summary>
    public sealed class ShopOffer
    {
        public int OfferId { get; set; }
        public PetState Animal { get; set; }
    }

    /// <summary>This game month's animals for sale (§9). Prices are computed live from the market.</summary>
    public sealed class ShopStock
    {
        public const int NeverStocked = -1;

        public int Seed { get; set; }

        public int StockMonthIndex { get; set; } = NeverStocked;

        public List<ShopOffer> Offers { get; set; } = new List<ShopOffer>();

        /// <summary>Replaces every offer when the game month differs from the stocked one. Same (Seed, month) → same stock.</summary>
        public bool EnsureStocked(int monthIndex, DateTimeOffset nowUtc, CareTuning care)
        {
            if (StockMonthIndex == monthIndex)
            {
                return false;
            }

            StockMonthIndex = monthIndex;
            Offers = ShopStockGenerator.Generate(new Random(ShopStockGenerator.SeedFor(Seed, monthIndex)), nowUtc, care);
            return true;
        }
    }
}
