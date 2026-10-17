using System;

namespace TerrariumDays.Core
{
    /// <summary>What candling shows (phase 5 spec §3).</summary>
    public enum CandlingStage
    {
        Unknown,
        Infertile,
        Early,
        Middle,
        Late,
        AboutToHatch,
        Dead
    }

    public static class Candling
    {
        /// <summary>
        /// Dead eggs always show as dead; otherwise nothing is known before 7 game days;
        /// then infertile, or the development band (≤33 early, ≤66 middle, ≤95 late, above: about to hatch).
        /// </summary>
        public static CandlingStage StageOf(Egg egg, DateTimeOffset gameNowUtc, CareTuning care)
        {
            if (egg.Failed)
            {
                return CandlingStage.Dead;
            }

            if (GameCalendar.GameDaysBetween(egg.LaidAtUtc, gameNowUtc) < care.CandlingRevealGameDays)
            {
                return CandlingStage.Unknown;
            }

            if (!egg.Fertile)
            {
                return CandlingStage.Infertile;
            }

            var development = egg.DevelopmentPercent;
            if (development <= 33d)
            {
                return CandlingStage.Early;
            }

            if (development <= 66d)
            {
                return CandlingStage.Middle;
            }

            return development <= 95d ? CandlingStage.Late : CandlingStage.AboutToHatch;
        }
    }

    /// <summary>Temperature-dependent sex (parent spec §5.1): the middle third's average temperature.</summary>
    public static class HatchSex
    {
        /// <summary>Female chance: ≤28℃ 95%, 29℃ 70%, 30℃ 60%, ≥31℃ 25%, straight lines between.</summary>
        public static double FemaleChance(double celsius)
        {
            if (celsius <= 28d)
            {
                return 0.95d;
            }

            if (celsius <= 29d)
            {
                return Lerp(0.95d, 0.70d, celsius - 28d);
            }

            if (celsius <= 30d)
            {
                return Lerp(0.70d, 0.60d, celsius - 29d);
            }

            if (celsius <= 31d)
            {
                return Lerp(0.60d, 0.25d, celsius - 30d);
            }

            return 0.25d;
        }

        /// <summary>The middle third's average temperature, or <paramref name="fallbackC"/> when nothing was recorded.</summary>
        public static double MiddleThirdAverage(Egg egg, double fallbackC) =>
            egg.MiddleThirdGameDays > 0d ? egg.MiddleThirdTemperatureSum / egg.MiddleThirdGameDays : fallbackC;

        /// <summary>One draw from <paramref name="random"/>.</summary>
        public static Sex Decide(Egg egg, double fallbackC, Random random) =>
            random.NextDouble() < FemaleChance(MiddleThirdAverage(egg, fallbackC)) ? Sex.Female : Sex.Male;

        private static double Lerp(double from, double to, double t) => from + (to - from) * t;
    }
}
