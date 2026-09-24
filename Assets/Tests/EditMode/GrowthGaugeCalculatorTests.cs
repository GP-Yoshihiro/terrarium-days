using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class GrowthGaugeCalculatorTests
    {
        [TestCase(0d, 0d)]
        [TestCase(25d, 50d)]
        [TestCase(49.999d, 99.998d)]
        [TestCase(50d, 0d)]
        [TestCase(75d, 50d)]
        [TestCase(99.999d, 99.998d)]
        [TestCase(100d, 100d)]
        public void PercentWithinStage_MapsGrowthToTheCurrentStageBand(double growth, double expectedPercent)
        {
            Assert.That(GrowthGaugeCalculator.PercentWithinStage(growth), Is.EqualTo(expectedPercent).Within(1e-6));
        }

        [Test]
        public void PercentWithinStage_ClampsOutOfRangeInput()
        {
            Assert.That(GrowthGaugeCalculator.PercentWithinStage(-10d), Is.EqualTo(0d));
            Assert.That(GrowthGaugeCalculator.PercentWithinStage(150d), Is.EqualTo(100d));
        }
    }
}
