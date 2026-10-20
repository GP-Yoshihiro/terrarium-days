using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Event sale odds (§10.2, phase 6 spec §2.4). P is the chance over the whole event; one
    /// serve action gets a = 1 − (1 − P)^(1/N) so that N serves give exactly P, and a browse
    /// action gets a × 0.6. Pure: the caller supplies the base value and the random numbers.
    /// </summary>
    public static class EventOdds
    {
        public static readonly double K = Math.Log(3d) / 0.3d;
        public const double MaxChance = 0.98d;
        public const double BrowseFactor = 0.6d;
        public static readonly int[] TolerancePercents = { 0, 5, 10, 15, 20 };

        public static bool IsValidTolerance(int percent) => Array.IndexOf(TolerancePercents, percent) >= 0;

        /// <summary>The price the odds see: price × (1 − d/2).</summary>
        public static double EffectivePrice(long price, int tolerancePercent) => price * (1d - tolerancePercent / 200d);

        public static double SaleChance(long price, int tolerancePercent, long baseValue, EventKind kind)
        {
            if (price <= 0 || baseValue <= 0)
            {
                return 0d;
            }

            var r = EffectivePrice(price, tolerancePercent) / baseValue;
            return Math.Min(MaxChance, EventSchedule.SaleCap(kind) / (1d + Math.Exp(K * (r - 1d))));
        }

        public static double PerActionChance(double eventChance, int actionCount) =>
            actionCount <= 0 ? 0d : 1d - Math.Pow(1d - eventChance, 1d / actionCount);

        public static double ActionChance(double eventChance, int actionCount, EventActionKind kind) =>
            PerActionChance(eventChance, actionCount) * (kind == EventActionKind.Browse ? BrowseFactor : 1d);

        /// <summary>Effective price × P to the nearest 100 yen (0 when it cannot sell).</summary>
        public static long ExpectedValue(long price, int tolerancePercent, double chance) =>
            (long)Math.Round(EffectivePrice(price, tolerancePercent) * chance / 100d, MidpointRounding.AwayFromZero) * 100L;

        /// <summary>What the buyer pays: price × (1 − d × u) to the nearest 100 yen, u in [0, 1].</summary>
        public static long SaleAmount(long price, int tolerancePercent, double u) =>
            MarketPrice.RoundYen(price * (1d - tolerancePercent / 100d * u));
    }
}
