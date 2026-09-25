using System;
using System.Globalization;
using UnityEngine;

namespace TerrariumDays.Core
{
    /// <summary>Current weather at the player's location (from Open-Meteo).</summary>
    public readonly struct WeatherReport
    {
        public readonly double TemperatureC;
        public readonly double HumidityPercent;
        public readonly int WeatherCode;

        public WeatherReport(double temperatureC, double humidityPercent, int weatherCode)
        {
            TemperatureC = temperatureC;
            HumidityPercent = humidityPercent;
            WeatherCode = weatherCode;
        }

        public string ConditionLabel => WeatherCodes.Label(WeatherCode);

        public string ToDisplayText()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1:0.0}℃ 湿度{2:0}%",
                ConditionLabel, TemperatureC, HumidityPercent);
        }
    }

    /// <summary>WMO weather interpretation codes (as used by Open-Meteo) → Japanese labels.</summary>
    public static class WeatherCodes
    {
        public static string Label(int code)
        {
            switch (code)
            {
                case 0: return "快晴";
                case 1: return "晴れ";
                case 2: return "晴れ時々くもり";
                case 3: return "くもり";
                case 45:
                case 48: return "霧";
                case 51:
                case 53:
                case 55:
                case 56:
                case 57: return "霧雨";
                case 61:
                case 63:
                case 65:
                case 66:
                case 67: return "雨";
                case 71:
                case 73:
                case 75:
                case 77: return "雪";
                case 80:
                case 81:
                case 82: return "にわか雨";
                case 85:
                case 86: return "にわか雪";
                case 95:
                case 96:
                case 99: return "雷雨";
                default: return "不明";
            }
        }
    }

    /// <summary>Parses the "current" block of an Open-Meteo forecast response.</summary>
    public static class OpenMeteo
    {
        public const string Endpoint = "https://api.open-meteo.com/v1/forecast";

        [Serializable]
        private sealed class Response
        {
            public Current current;
        }

        [Serializable]
        private sealed class Current
        {
            public double temperature_2m = double.NaN;
            public double relative_humidity_2m = double.NaN;
            public int weather_code = -1;
        }

        /// <summary>
        /// Request URL. Coordinates are rounded to 2 decimals (about 1 km) so the exact
        /// position never leaves the device.
        /// </summary>
        public static string CurrentWeatherUrl(double latitude, double longitude)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0}?latitude={1:0.00}&longitude={2:0.00}&current=temperature_2m,relative_humidity_2m,weather_code&timezone=auto",
                Endpoint, latitude, longitude);
        }

        public static bool TryParse(string json, out WeatherReport report)
        {
            report = default;
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            try
            {
                var response = JsonUtility.FromJson<Response>(json);
                var current = response?.current;
                if (current == null || double.IsNaN(current.temperature_2m) || double.IsNaN(current.relative_humidity_2m)
                    || current.weather_code < 0)
                {
                    return false;
                }

                report = new WeatherReport(current.temperature_2m, current.relative_humidity_2m, current.weather_code);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
