using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.Gameplay;

namespace TerrariumDays.Tests
{
    public sealed class CareServiceTests
    {
        private CareTuning tuning;
        private CareService service;

        [SetUp]
        public void SetUp()
        {
            tuning = new CareTuning();
            service = new CareService(tuning);
        }

        [Test]
        public void Feed_IncreasesOnlyHungerByTheTunedAmount()
        {
            var state = new PetState { Hunger = 50d, Hydration = 50d, Cleanliness = 50d, Health = 90d, Growth = 10d };

            service.Feed(state);

            Assert.That(state.Hunger, Is.EqualTo(50d + tuning.FeedHungerAmount));
            Assert.That(state.Hydration, Is.EqualTo(50d));
            Assert.That(state.Cleanliness, Is.EqualTo(50d));
            Assert.That(state.Health, Is.EqualTo(90d));
            Assert.That(state.Growth, Is.EqualTo(10d));
        }

        [Test]
        public void RefreshWater_IncreasesOnlyHydrationByTheTunedAmount()
        {
            var state = new PetState { Hunger = 50d, Hydration = 50d, Cleanliness = 50d, Health = 90d, Growth = 10d };

            service.RefreshWater(state);

            Assert.That(state.Hydration, Is.EqualTo(50d + tuning.RefreshWaterHydrationAmount));
            Assert.That(state.Hunger, Is.EqualTo(50d));
            Assert.That(state.Cleanliness, Is.EqualTo(50d));
            Assert.That(state.Health, Is.EqualTo(90d));
            Assert.That(state.Growth, Is.EqualTo(10d));
        }

        [Test]
        public void Clean_IncreasesOnlyCleanlinessByTheTunedAmount()
        {
            var state = new PetState { Hunger = 50d, Hydration = 50d, Cleanliness = 50d, Health = 90d, Growth = 10d };

            service.Clean(state);

            Assert.That(state.Cleanliness, Is.EqualTo(50d + tuning.CleanCleanlinessAmount));
            Assert.That(state.Hunger, Is.EqualTo(50d));
            Assert.That(state.Hydration, Is.EqualTo(50d));
            Assert.That(state.Health, Is.EqualTo(90d));
            Assert.That(state.Growth, Is.EqualTo(10d));
        }

        [Test]
        public void RepeatedCareActions_NeverExceedTheMaximumStatusValue()
        {
            var state = new PetState { Hunger = 90d, Hydration = 90d, Cleanliness = 90d };

            service.Feed(state);
            service.Feed(state);
            service.RefreshWater(state);
            service.RefreshWater(state);
            service.Clean(state);
            service.Clean(state);

            Assert.That(state.Hunger, Is.EqualTo(100d));
            Assert.That(state.Hydration, Is.EqualTo(100d));
            Assert.That(state.Cleanliness, Is.EqualTo(100d));
        }
    }
}
