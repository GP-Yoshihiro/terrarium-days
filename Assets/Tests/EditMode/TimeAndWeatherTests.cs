using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class TimeAndWeatherTests
    {
        private readonly PetBehaviourTuning tuning = new PetBehaviourTuning();

        [TestCase(4, 59, DayPhase.Night)]
        [TestCase(5, 0, DayPhase.Morning)]
        [TestCase(9, 59, DayPhase.Morning)]
        [TestCase(10, 0, DayPhase.Day)]
        [TestCase(15, 59, DayPhase.Day)]
        [TestCase(16, 0, DayPhase.Evening)]
        [TestCase(18, 59, DayPhase.Evening)]
        [TestCase(19, 0, DayPhase.Night)]
        [TestCase(0, 0, DayPhase.Night)]
        public void DayPhase_FollowsTheLocalClock(int hour, int minute, DayPhase expected)
        {
            var local = new DateTime(2026, 9, 24, hour, minute, 0, DateTimeKind.Local);

            Assert.That(DayPhaseClock.PhaseAt(local, tuning), Is.EqualTo(expected));
        }

        [Test]
        public void NightIsDarkerThanDusk_AndDaytimeIsUntinted()
        {
            DayPhaseClock.Tint(DayPhase.Night, tuning, out _, out _, out _, out var night);
            DayPhaseClock.Tint(DayPhase.Evening, tuning, out _, out _, out _, out var evening);
            DayPhaseClock.Tint(DayPhase.Day, tuning, out _, out _, out _, out var day);

            Assert.That(night, Is.GreaterThan(evening));
            Assert.That(day, Is.EqualTo(0f));
        }

        [Test]
        public void OpenMeteo_ParsesTheCurrentBlock()
        {
            const string json = "{\"latitude\":35.7,\"longitude\":139.7,\"current_units\":{\"temperature_2m\":\"°C\"}," +
                                "\"current\":{\"time\":\"2026-09-24T21:15\",\"interval\":900,\"temperature_2m\":22.4," +
                                "\"relative_humidity_2m\":78,\"weather_code\":61}}";

            Assert.That(OpenMeteo.TryParse(json, out var report), Is.True);
            Assert.That(report.TemperatureC, Is.EqualTo(22.4).Within(1e-9));
            Assert.That(report.HumidityPercent, Is.EqualTo(78d));
            Assert.That(report.ConditionLabel, Is.EqualTo("雨"));
            Assert.That(report.ToDisplayText(), Is.EqualTo("雨 22.4℃ 湿度78%"));
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("{\"error\":true,\"reason\":\"Latitude must be in range\"}")]
        [TestCase("{\"current\":{\"temperature_2m\":20.0}}")]
        public void OpenMeteo_RejectsErrorsAndIncompleteResponses(string json)
        {
            Assert.That(OpenMeteo.TryParse(json, out _), Is.False);
        }

        [Test]
        public void OpenMeteo_SendsOnlyCoarseCoordinates()
        {
            var url = OpenMeteo.CurrentWeatherUrl(35.681236, 139.767125);

            StringAssert.Contains("latitude=35.68&", url);
            StringAssert.Contains("longitude=139.77&", url);
            StringAssert.DoesNotContain("35.6812", url);
        }

        [TestCase(0, "快晴")]
        [TestCase(3, "くもり")]
        [TestCase(45, "霧")]
        [TestCase(73, "雪")]
        [TestCase(81, "にわか雨")]
        [TestCase(95, "雷雨")]
        [TestCase(12345, "不明")]
        public void WeatherCodes_MapToJapanese(int code, string expected)
        {
            Assert.That(WeatherCodes.Label(code), Is.EqualTo(expected));
        }

        [TestCase(12d, 18d)]
        [TestCase(18d, 18d)]
        [TestCase(26.4d, 26.4d)]
        [TestCase(30d, 30d)]
        [TestCase(35d, 30d)]
        public void RoomTemperature_ClampsTheOutdoorTemperature(double outdoor, double room)
        {
            Assert.That(RoomTemperature.From(new WeatherReport(outdoor, 50d, 1)), Is.EqualTo(room).Within(1e-9));
        }

        [Test]
        public void RoomTemperature_WithoutUsableWeather_Is24()
        {
            Assert.That(RoomTemperature.From(null), Is.EqualTo(24d));
            Assert.That(RoomTemperature.From(new WeatherReport(double.NaN, 50d, 1)), Is.EqualTo(24d));
        }

        [Test]
        public void RoomClimate_FollowsTheLatestReportAndFallsBackWhenItIsLost()
        {
            var room = new RoomClimate();
            Assert.That((room.TemperatureC, room.Measured), Is.EqualTo((24d, false)));

            room.Update(new WeatherReport(31d, 60d, 0));
            Assert.That((room.TemperatureC, room.Measured), Is.EqualTo((30d, true)));

            room.Update(null);
            Assert.That((room.TemperatureC, room.Measured), Is.EqualTo((24d, false)));

            room.Update(new WeatherReport(double.NaN, 60d, 0));
            Assert.That((room.TemperatureC, room.Measured), Is.EqualTo((24d, false)));
        }

        [Test]
        public void RoomTemperatureLabel_SaysWhenItIsTheFallback()
        {
            Assert.That(RoomTemperature.Label(27.4d, true), Is.EqualTo("室温27℃"));
            Assert.That(RoomTemperature.Label(24d, false), Is.EqualTo("室温24℃（天気を取得できないため）"));
        }
    }
}
