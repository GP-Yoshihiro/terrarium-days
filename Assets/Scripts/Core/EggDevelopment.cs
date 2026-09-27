using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Egg development at a temperature (§8 days to hatch, §5.1 middle third). Phase 4 uses it
    /// for nest-box eggs at room temperature (§7.7); phase 5 adds the incubator.
    /// </summary>
    public static class EggDevelopment
    {
        /// <summary>Below this an egg does not develop and, given long enough, dies (§5.1).</summary>
        public const double MinDevelopingC = 24d;

        private const double MiddleThirdStart = 100d / 3d;
        private const double MiddleThirdEnd = 200d / 3d;

        /// <summary>§8: 60 days at 28 ℃, 52 at 30 ℃, 45 at 32 ℃; straight lines between, continued beyond.</summary>
        public static double DaysToHatch(double celsius) =>
            celsius <= 30d ? 52d + (30d - celsius) * 4d : 52d - (celsius - 30d) * 3.5d;

        public static void Apply(Egg egg, double gameDays, double celsius, CareTuning care)
        {
            if (gameDays <= 0d || egg.Failed || !egg.Fertile)
            {
                return;
            }

            if (celsius < MinDevelopingC)
            {
                egg.ColdGameDays += gameDays;
                if (egg.ColdGameDays >= care.EggColdFailGameDays)
                {
                    egg.Failure = EggFailure.Cold;
                }

                return;
            }

            var daysToHatch = DaysToHatch(celsius);
            var before = egg.DevelopmentPercent;
            var after = Math.Min(100d, before + gameDays / daysToHatch * 100d);
            egg.DevelopmentPercent = after;

            var overlap = Math.Min(after, MiddleThirdEnd) - Math.Max(before, MiddleThirdStart);
            if (overlap > 0d)
            {
                var days = overlap * daysToHatch / 100d;
                egg.MiddleThirdGameDays += days;
                egg.MiddleThirdTemperatureSum += celsius * days;
            }
        }
    }
}
