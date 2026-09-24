using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class CareStatusPresentationTests
    {
        private CareTuning tuning;

        [SetUp]
        public void SetUp()
        {
            tuning = new CareTuning();
        }

        [TestCase(100d, StatusBarLevel.Good)]
        [TestCase(40d, StatusBarLevel.Good)]
        [TestCase(39.999d, StatusBarLevel.Warning)]
        [TestCase(20d, StatusBarLevel.Warning)]
        [TestCase(19.999d, StatusBarLevel.Critical)]
        [TestCase(0d, StatusBarLevel.Critical)]
        public void LevelFor_UsesCareTuningsOwnThresholds(double value, StatusBarLevel expectedLevel)
        {
            Assert.That(CareStatusPresentation.LevelFor(value, tuning), Is.EqualTo(expectedLevel));
        }
    }
}
