using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class CareTuningTests
    {
        [Test]
        public void Constructor_SetsDocumentedDefaultTuningValues()
        {
            var tuning = new CareTuning();

            Assert.That(tuning.FeedHungerAmount, Is.EqualTo(30d));
            Assert.That(tuning.RefreshWaterHydrationAmount, Is.EqualTo(30d));
            Assert.That(tuning.CleanCleanlinessAmount, Is.EqualTo(40d));

            Assert.That(tuning.HungerDecayPerHour, Is.EqualTo(4d));
            Assert.That(tuning.HydrationDecayPerHour, Is.EqualTo(4d));
            Assert.That(tuning.CleanlinessDecayPerHour, Is.EqualTo(3d));

            Assert.That(tuning.HealthDecayPerHour, Is.EqualTo(5d));
            Assert.That(tuning.HealthRecoveryPerHour, Is.EqualTo(2d));
            Assert.That(tuning.FeedWeightGainGrams, Is.EqualTo(2.2d));

            Assert.That(tuning.HealthyCareThreshold, Is.EqualTo(40d));
            Assert.That(tuning.LowCareThreshold, Is.EqualTo(20d));

            Assert.That(tuning.MaxOfflineProgressHours, Is.EqualTo(12d));
            Assert.That(tuning.OfflineProgressStepMinutes, Is.EqualTo(1d));
        }
    }
}
