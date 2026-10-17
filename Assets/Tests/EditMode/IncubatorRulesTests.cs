using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class IncubatorRulesTests
    {
        private readonly CareTuning care = new CareTuning();

        [TestCase(IncubatorModel.Simple, 6)]
        [TestCase(IncubatorModel.Standard, 12)]
        [TestCase(IncubatorModel.Luxury, 24)]
        public void Capacity_MatchesTheModel(IncubatorModel model, int expected)
        {
            Assert.AreEqual(expected, IncubatorRules.Capacity(model));
        }

        [TestCase(20, 24)]
        [TestCase(24, 24)]
        [TestCase(30, 30)]
        [TestCase(34, 34)]
        [TestCase(40, 34)]
        public void ClampSetting_KeepsTheRange(int input, int expected)
        {
            Assert.AreEqual(expected, IncubatorRules.ClampSetting(input));
        }

        [Test]
        public void ClampHumidity_KeepsZeroToHundred()
        {
            Assert.AreEqual(0d, IncubatorRules.ClampHumidity(-5d));
            Assert.AreEqual(100d, IncubatorRules.ClampHumidity(120d));
            Assert.AreEqual(55.5d, IncubatorRules.ClampHumidity(55.5d));
        }

        [Test]
        public void RealTemperature_Simple_IsPulledTenPercentTowardTheRoom()
        {
            Assert.AreEqual(27.4d, IncubatorRules.RealTemperature(IncubatorModel.Simple, 28, 22d, new QueuedRandom(), care), 1e-9);
        }

        [Test]
        public void RealTemperature_Simple_DriftIsCappedAtTwoDegrees()
        {
            Assert.AreEqual(26d, IncubatorRules.RealTemperature(IncubatorModel.Simple, 28, 0d, new QueuedRandom(), care), 1e-9);
            Assert.AreEqual(30d, IncubatorRules.RealTemperature(IncubatorModel.Simple, 28, 60d, new QueuedRandom(), care), 1e-9);
        }

        [Test]
        public void RealTemperature_Standard_StaysWithinHalfADegree()
        {
            for (var day = 0; day < 100; day++)
            {
                var random = BreedingRandom.For(1234, BreedingRandom.IncubatorTemperatureStream, 1, day);
                var celsius = IncubatorRules.RealTemperature(IncubatorModel.Standard, 28, 10d, random, care);
                Assert.That(celsius, Is.InRange(27.5d, 28.5d));
            }
        }

        [Test]
        public void RealTemperature_Standard_IsTheSameForTheSameDay()
        {
            var first = IncubatorRules.RealTemperature(IncubatorModel.Standard, 28, 22d,
                BreedingRandom.For(99, BreedingRandom.IncubatorTemperatureStream, 2, 5), care);
            var second = IncubatorRules.RealTemperature(IncubatorModel.Standard, 28, 22d,
                BreedingRandom.For(99, BreedingRandom.IncubatorTemperatureStream, 2, 5), care);
            Assert.AreEqual(first, second);
        }

        [Test]
        public void RealTemperature_Luxury_IsExactlyTheSetting()
        {
            Assert.AreEqual(28d, IncubatorRules.RealTemperature(IncubatorModel.Luxury, 28, 10d, new QueuedRandom(0.9d), care));
        }

        [Test]
        public void DailyFailure_LowHumidity_DriesOnALowFirstRoll()
        {
            Assert.AreEqual(EggFailure.Dried, IncubatorRules.DailyFailure(59d, 28d, new QueuedRandom(0.05d, 0.99d), care));
            Assert.AreEqual(EggFailure.None, IncubatorRules.DailyFailure(59d, 28d, new QueuedRandom(0.2d, 0.99d), care));
        }

        [Test]
        public void DailyFailure_HighHumidity_MoldsOnALowFirstRoll()
        {
            Assert.AreEqual(EggFailure.Mold, IncubatorRules.DailyFailure(96d, 28d, new QueuedRandom(0.05d, 0.99d), care));
            Assert.AreEqual(EggFailure.None, IncubatorRules.DailyFailure(96d, 28d, new QueuedRandom(0.2d, 0.99d), care));
        }

        [Test]
        public void DailyFailure_TheEdgesOfTheSafeRangeAreSafe()
        {
            Assert.AreEqual(EggFailure.None, IncubatorRules.DailyFailure(60d, 28d, new QueuedRandom(0d, 0.99d), care));
            Assert.AreEqual(EggFailure.None, IncubatorRules.DailyFailure(95d, 28d, new QueuedRandom(0d, 0.99d), care));
            Assert.AreEqual(EggFailure.None, IncubatorRules.DailyFailure(80d, 28d, new QueuedRandom(0d, 0.99d), care));
        }

        [Test]
        public void DailyFailure_From33Degrees_FivePercentHeat()
        {
            Assert.AreEqual(EggFailure.Heat, IncubatorRules.DailyFailure(80d, 33d, new QueuedRandom(0.99d, 0.04d), care));
            Assert.AreEqual(EggFailure.None, IncubatorRules.DailyFailure(80d, 33d, new QueuedRandom(0.99d, 0.06d), care));
            Assert.AreEqual(EggFailure.None, IncubatorRules.DailyFailure(80d, 32.9d, new QueuedRandom(0.99d, 0d), care));
        }

        [Test]
        public void DailyFailure_From35Degrees_FifteenPercentHeat()
        {
            Assert.AreEqual(EggFailure.Heat, IncubatorRules.DailyFailure(80d, 35d, new QueuedRandom(0.99d, 0.14d), care));
            Assert.AreEqual(EggFailure.None, IncubatorRules.DailyFailure(80d, 35d, new QueuedRandom(0.99d, 0.16d), care));
        }
    }
}
