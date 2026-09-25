using System;

namespace TerrariumDays.Core
{
    /// <summary>One per animal (§5.2).</summary>
    public enum Personality
    {
        Calm,
        Shy,
        Curious,
        Bold,
        Glutton
    }

    public enum Compatibility
    {
        Good,
        Normal,
        Bad
    }

    /// <summary>The effects of each personality (§5.2). Pure data so it is EditMode tested.</summary>
    public static class PersonalityTraits
    {
        public static readonly Personality[] All = (Personality[])Enum.GetValues(typeof(Personality));

        /// <summary>Chance of taking each parent's personality at hatching; the rest is random.</summary>
        public const double InheritChance = 0.2d;

        private const Compatibility G = Compatibility.Good;
        private const Compatibility N = Compatibility.Normal;
        private const Compatibility B = Compatibility.Bad;

        // Rows: male; columns: female. Order: Calm, Shy, Curious, Bold, Glutton.
        private static readonly Compatibility[,] CompatibilityTable =
        {
            { G, G, G, G, G },
            { G, N, N, B, N },
            { G, N, G, N, G },
            { G, B, N, B, N },
            { G, N, G, N, N },
        };

        public static string Label(Personality personality)
        {
            switch (personality)
            {
                case Personality.Shy:
                    return "臆病";
                case Personality.Curious:
                    return "好奇心旺盛";
                case Personality.Bold:
                    return "気が強い";
                case Personality.Glutton:
                    return "食いしん坊";
                default:
                    return "おっとり";
            }
        }

        public static double ThreatMultiplier(Personality p) =>
            p == Personality.Calm ? 0.5d : p == Personality.Shy || p == Personality.Bold ? 1.5d : 1d;

        public static double WalkFrequencyMultiplier(Personality p) => p == Personality.Curious ? 1.3d : 1d;

        /// <summary>How much longer it stays tucked by its shelter (sleep lengths).</summary>
        public static double HideMultiplier(Personality p) => p == Personality.Shy ? 1.5d : 1d;

        /// <summary>Length of food-refusal periods (pre-shed and pre-growth).</summary>
        public static double FastMultiplier(Personality p) =>
            p == Personality.Shy ? 1.5d : p == Personality.Glutton ? 0.5d : 1d;

        public static double WeightGainMultiplier(Personality p) =>
            p == Personality.Glutton ? 1.2d : p == Personality.Bold ? 1.1d : 1d;

        public static double PriceMultiplier(Personality p)
        {
            switch (p)
            {
                case Personality.Calm:
                    return 1.1d;
                case Personality.Shy:
                    return 0.9d;
                case Personality.Curious:
                    return 1.05d;
                default:
                    return 1d;
            }
        }

        public static bool IsEagerEater(Personality p) => p == Personality.Glutton;

        public static Compatibility CompatibilityOf(Personality male, Personality female) =>
            CompatibilityTable[(int)male, (int)female];

        public static double MatingSuccessMultiplier(Compatibility c) =>
            c == Compatibility.Good ? 1.2d : c == Compatibility.Bad ? 0.6d : 1d;

        public static int ClutchDelta(Compatibility c) =>
            c == Compatibility.Good ? 1 : c == Compatibility.Bad ? -1 : 0;

        public static Personality Inherit(Personality mother, Personality father, Random random)
        {
            var roll = random.NextDouble();
            if (roll < InheritChance)
            {
                return mother;
            }

            if (roll < InheritChance * 2d)
            {
                return father;
            }

            return Roll(random);
        }

        public static Personality Roll(Random random) => All[random.Next(All.Length)];

        /// <summary>A copy of the shared behaviour tuning with this personality's effects applied.</summary>
        public static PetBehaviourTuning BehaviourTuningFor(PetBehaviourTuning baseline, Personality p)
        {
            var tuning = baseline.Clone();
            var threat = ThreatMultiplier(p);
            tuning.WakeStartleChance = Math.Min(1d, baseline.WakeStartleChance * threat);
            tuning.ThreatTapCount = Math.Max(2, (int)Math.Round(baseline.ThreatTapCount / threat));

            var walk = (float)WalkFrequencyMultiplier(p);
            tuning.LivelyIdleMinSeconds = baseline.LivelyIdleMinSeconds / walk;
            tuning.LivelyIdleMaxSeconds = baseline.LivelyIdleMaxSeconds / walk;
            tuning.NormalIdleMinSeconds = baseline.NormalIdleMinSeconds / walk;
            tuning.NormalIdleMaxSeconds = baseline.NormalIdleMaxSeconds / walk;
            tuning.SluggishIdleMinSeconds = baseline.SluggishIdleMinSeconds / walk;
            tuning.SluggishIdleMaxSeconds = baseline.SluggishIdleMaxSeconds / walk;

            var hide = (float)HideMultiplier(p);
            tuning.MorningSleepMinSeconds = baseline.MorningSleepMinSeconds * hide;
            tuning.MorningSleepMaxSeconds = baseline.MorningSleepMaxSeconds * hide;
            tuning.DaySleepMinSeconds = baseline.DaySleepMinSeconds * hide;
            tuning.DaySleepMaxSeconds = baseline.DaySleepMaxSeconds * hide;
            tuning.NightSleepMinSeconds = baseline.NightSleepMinSeconds * hide;
            tuning.NightSleepMaxSeconds = baseline.NightSleepMaxSeconds * hide;

            if (IsEagerEater(p))
            {
                // Strikes at food almost at once.
                tuning.PreStrikeTailWagSeconds = baseline.PreStrikeTailWagSeconds * 0.5f;
            }

            return tuning;
        }
    }
}
