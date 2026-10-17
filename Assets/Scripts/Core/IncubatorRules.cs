using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Incubator numbers (phase 5 spec §2.1, §3): capacity, the setting range, the real
    /// temperature each model holds, and the once-per-game-day egg failure check.
    /// </summary>
    public static class IncubatorRules
    {
        public const int MinSetC = 24;
        public const int MaxSetC = 34;
        public const int DefaultSetC = 28;
        public const double DefaultHumidityPercent = 80d;
        public const double MaxHumidityPercent = 100d;

        public static int Capacity(IncubatorModel model)
        {
            switch (model)
            {
                case IncubatorModel.Standard:
                    return 12;
                case IncubatorModel.Luxury:
                    return 24;
                default:
                    return 6;
            }
        }

        public static int ClampSetting(int celsius) => Math.Max(MinSetC, Math.Min(MaxSetC, celsius));

        public static double ClampHumidity(double percent) => Math.Max(0d, Math.Min(MaxHumidityPercent, percent));

        /// <summary>
        /// The temperature the eggs actually get. Simple: pulled toward the room (10%, at most ±2℃).
        /// Standard: set ± 0.5℃ (one draw from <paramref name="random"/>, seeded per game day).
        /// Luxury: exactly the setting.
        /// </summary>
        public static double RealTemperature(IncubatorModel model, int setC, double roomC, Random random, CareTuning care)
        {
            switch (model)
            {
                case IncubatorModel.Simple:
                    var drift = (roomC - setC) * care.SimpleIncubatorRoomPull;
                    drift = Math.Max(-care.SimpleIncubatorMaxDriftC, Math.Min(care.SimpleIncubatorMaxDriftC, drift));
                    return setC + drift;
                case IncubatorModel.Standard:
                    return setC + BreedingRandom.Uniform(random, -care.StandardIncubatorJitterC, care.StandardIncubatorJitterC);
                default:
                    return setC;
            }
        }

        /// <summary>
        /// One game day's check for one live fertile egg. Always draws twice (humidity, then heat)
        /// so the stream stays aligned whatever the conditions.
        /// </summary>
        public static EggFailure DailyFailure(double humidityPercent, double realC, Random random, CareTuning care)
        {
            var humidityRoll = random.NextDouble();
            var heatRoll = random.NextDouble();

            if (humidityPercent < care.HumidityDryBelowPercent && humidityRoll < care.HumidityDailyFailChance)
            {
                return EggFailure.Dried;
            }

            if (humidityPercent > care.HumidityMoldAbovePercent && humidityRoll < care.HumidityDailyFailChance)
            {
                return EggFailure.Mold;
            }

            var heatChance = realC >= care.SevereHeatFromC ? care.SevereHeatDailyFailChance
                : realC >= care.HeatRiskFromC ? care.HeatDailyFailChance
                : 0d;
            return heatRoll < heatChance ? EggFailure.Heat : EggFailure.None;
        }
    }
}
