using System;
using System.Globalization;

namespace TerrariumDays.Core
{
    /// <summary>
    /// The breeding room's temperature (§7.7): the current outdoor temperature at the player's
    /// location clamped to 18–30 ℃, or 24 ℃ when there is no usable weather (location off,
    /// failed, offline). Pure, so eggs and breeding can be tested without Unity.
    /// </summary>
    public static class RoomTemperature
    {
        public const double MinC = 18d;
        public const double MaxC = 30d;
        public const double FallbackC = 24d;

        public static bool IsUsable(WeatherReport report) =>
            !double.IsNaN(report.TemperatureC) && !double.IsInfinity(report.TemperatureC);

        public static double From(WeatherReport? report)
        {
            if (!report.HasValue || !IsUsable(report.Value))
            {
                return FallbackC;
            }

            return Math.Max(MinC, Math.Min(MaxC, report.Value.TemperatureC));
        }

        /// <summary>"室温27℃", or with no weather "室温24℃（天気を取得できないため）".</summary>
        public static string Label(double celsius, bool measured)
        {
            var text = "室温" + Math.Round(celsius, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "℃";
            return measured ? text : text + "（天気を取得できないため）";
        }
    }

    /// <summary>The room temperature the colony uses right now; the UI feeds it each weather result.</summary>
    public sealed class RoomClimate
    {
        public double TemperatureC { get; private set; } = RoomTemperature.FallbackC;

        /// <summary>False while the 24 ℃ fallback is in use.</summary>
        public bool Measured { get; private set; }

        public void Update(WeatherReport? report)
        {
            TemperatureC = RoomTemperature.From(report);
            Measured = report.HasValue && RoomTemperature.IsUsable(report.Value);
        }
    }
}
