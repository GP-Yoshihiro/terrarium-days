namespace TerrariumDays.Core
{
    /// <summary>
    /// Per-event demand for a morph (§10.1): 0.8–1.3 from the event's seed, and ×1.15 at big
    /// events for morphs whose base price is 15,000 yen or more. Events arrive in phase 6;
    /// until then callers use <see cref="None"/>.
    /// </summary>
    public static class EventDemand
    {
        public const double None = 1d;
        public const double Min = 0.8d;
        public const double Max = 1.3d;
        public const double BigEventBoost = 1.15d;
        public const long BigEventBoostFromBasePrice = 15000;

        public static double For(int eventSeed, Genotype genotype, bool isBigEvent)
        {
            var demand = Min + (Max - Min) * Unit(eventSeed, MorphNamer.VisualName(genotype));
            return isBigEvent && MarketPrice.BasePrice(genotype) >= BigEventBoostFromBasePrice ? demand * BigEventBoost : demand;
        }

        /// <summary>
        /// A number in [0, 1) fixed by (seed, key) on every platform (string.GetHashCode is not):
        /// FNV-1a over the seed and the key's UTF-16 units, finished with SplitMix64.
        /// </summary>
        public static double Unit(int seed, string key)
        {
            unchecked
            {
                var h = 14695981039346656037UL;
                h = (h ^ (uint)seed) * 1099511628211UL;
                foreach (var c in key)
                {
                    h = (h ^ c) * 1099511628211UL;
                }

                h ^= h >> 30;
                h *= 0xBF58476D1CE4E5B9UL;
                h ^= h >> 27;
                h *= 0x94D049BB133111EBUL;
                h ^= h >> 31;
                return (h >> 11) * (1d / (1UL << 53));
            }
        }
    }
}
