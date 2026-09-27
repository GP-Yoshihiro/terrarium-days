using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Reproducible randomness for breeding (§7). Each roll gets its own generator built from
    /// the colony's seed, a stream, an id and an index, so outcomes do not depend on the order
    /// things were processed in (offline catch-up vs live play) or on saving in between.
    /// </summary>
    public static class BreedingRandom
    {
        public const int PairingStream = 1;
        public const int ClutchStream = 2;

        /// <summary>The colony's breeding seed, from its calendar epoch (so it needs no save field).</summary>
        public static int SeedOf(DateTimeOffset epochUtc)
        {
            var ticks = epochUtc.UtcTicks;
            return unchecked((int)ticks ^ (int)(ticks >> 32));
        }

        public static Random For(int seed, int stream, int id, int index)
        {
            unchecked
            {
                var h = (uint)seed;
                h = Mix(h ^ ((uint)stream * 0x9E3779B9u));
                h = Mix(h ^ ((uint)id * 0x85EBCA6Bu));
                h = Mix(h ^ ((uint)index * 0xC2B2AE35u));
                return new Random((int)(h & 0x7FFFFFFFu));
            }
        }

        /// <summary>A uniform value in [min, max).</summary>
        public static double Uniform(Random random, double min, double max) => min + random.NextDouble() * (max - min);

        private static uint Mix(uint h)
        {
            unchecked
            {
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
