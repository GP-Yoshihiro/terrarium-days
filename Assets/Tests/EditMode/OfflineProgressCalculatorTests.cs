using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class OfflineProgressCalculatorTests
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        private CareTuning tuning;
        private OfflineProgressCalculator calculator;

        [SetUp]
        public void SetUp()
        {
            tuning = new CareTuning();
            calculator = new OfflineProgressCalculator(tuning);
        }

        [Test]
        public void Apply_WithZeroElapsedTime_ChangesNothingAndReturnsNoEvent()
        {
            var state = new PetState();
            var before = (state.Hunger, state.Hydration, state.Cleanliness, state.Health, state.WeightGrams);

            var result = calculator.Apply(state, Epoch, Epoch);

            Assert.That(result.AppliedElapsed, Is.EqualTo(TimeSpan.Zero));
            Assert.That(result.NewGrowthStage, Is.Null);
            Assert.That((state.Hunger, state.Hydration, state.Cleanliness, state.Health, state.WeightGrams), Is.EqualTo(before));
        }

        [Test]
        public void Apply_WithDeviceClockMovedBackward_TreatsElapsedAsZero()
        {
            var state = new PetState();
            var before = (state.Hunger, state.Hydration, state.Cleanliness, state.Health, state.WeightGrams);

            var result = calculator.Apply(state, Epoch, Epoch - TimeSpan.FromHours(1));

            Assert.That(result.AppliedElapsed, Is.EqualTo(TimeSpan.Zero));
            Assert.That(result.NewGrowthStage, Is.Null);
            Assert.That((state.Hunger, state.Hydration, state.Cleanliness, state.Health, state.WeightGrams), Is.EqualTo(before));
        }

        [Test]
        public void Apply_CapsElapsedTimeAtTwelveHours_RegardlessOfHowMuchMoreTimePassed()
        {
            var thirteenHourState = new PetState();
            calculator.Apply(thirteenHourState, Epoch, Epoch + TimeSpan.FromHours(13));

            var hundredHourState = new PetState();
            var hundredHourResult = calculator.Apply(hundredHourState, Epoch, Epoch + TimeSpan.FromHours(100));

            Assert.That(hundredHourResult.AppliedElapsed, Is.EqualTo(TimeSpan.FromHours(12)));
            Assert.That(hundredHourState.Hunger, Is.EqualTo(thirteenHourState.Hunger).Within(1e-9));
            Assert.That(hundredHourState.Hydration, Is.EqualTo(thirteenHourState.Hydration).Within(1e-9));
            Assert.That(hundredHourState.Cleanliness, Is.EqualTo(thirteenHourState.Cleanliness).Within(1e-9));

            // 12h of decay at the default rates from the PetState defaults (80/80/80), well above zero, so never clamped.
            Assert.That(hundredHourState.Hunger, Is.EqualTo(80d - tuning.HungerDecayPerHour * 12d).Within(1e-9));
            Assert.That(hundredHourState.Hydration, Is.EqualTo(80d - tuning.HydrationDecayPerHour * 12d).Within(1e-9));
            Assert.That(hundredHourState.Cleanliness, Is.EqualTo(80d - tuning.CleanlinessDecayPerHour * 12d).Within(1e-9));
        }

        [Test]
        public void Apply_WhenAllCareStatsStayAtOrAboveHealthyThreshold_RecoversHealth()
        {
            var state = new PetState { Hunger = 90d, Hydration = 90d, Cleanliness = 90d, Health = 50d };

            calculator.Apply(state, Epoch, Epoch + TimeSpan.FromHours(1));

            Assert.That(state.Health, Is.EqualTo(50d + tuning.HealthRecoveryPerHour).Within(1e-9));
        }

        [Test]
        public void Apply_WhenAnyCareStatStaysBelowLowThreshold_DecreasesHealthOnly()
        {
            var state = new PetState { Hunger = 15d, Hydration = 90d, Cleanliness = 90d, Health = 50d };

            calculator.Apply(state, Epoch, Epoch + TimeSpan.FromHours(1));

            Assert.That(state.Health, Is.EqualTo(50d - tuning.HealthDecayPerHour).Within(1e-9));
        }

        [Test]
        public void Apply_WhenCareStatsAreInTheNeitherZone_LeavesHealthUnchanged()
        {
            var state = new PetState { Hunger = 30d, Hydration = 90d, Cleanliness = 90d, Health = 50d };

            calculator.Apply(state, Epoch, Epoch + TimeSpan.FromHours(1));

            Assert.That(state.Health, Is.EqualTo(50d));
        }
    }
}
