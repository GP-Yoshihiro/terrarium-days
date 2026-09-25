using System;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.Gameplay;

namespace TerrariumDays.Tests
{
    public sealed class AppetiteAndSheddingTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        private readonly CareTuning tuning = new CareTuning();

        private PetState StateWithShedIn(TimeSpan untilShed, double growth = 10d)
        {
            return new PetState
            {
                Growth = growth,
                LastSavedAtUtc = Now,
                NextShedAtUtc = Now + untilShed,
            };
        }

        [Test]
        public void Appetite_IsNormalFarFromAShedAndMidStage()
        {
            var state = StateWithShedIn(TimeSpan.FromDays(30));

            Assert.That(AppetiteModel.Evaluate(state, Now, tuning), Is.EqualTo(AppetiteState.Normal));
        }

        [Test]
        public void Appetite_IsLostInTheDaysBeforeAShed()
        {
            var state = StateWithShedIn(TimeSpan.FromDays(tuning.PreShedDays - 0.5));

            Assert.That(AppetiteModel.Evaluate(state, Now, tuning), Is.EqualTo(AppetiteState.PreShed));
        }

        [Test]
        public void Appetite_IsLostJustBeforeTheGrowthGaugeFills()
        {
            // 48 of 50 in the Baby band = 96% of the stage.
            var state = StateWithShedIn(TimeSpan.FromDays(30), growth: 48d);

            Assert.That(AppetiteModel.Evaluate(state, Now, tuning), Is.EqualTo(AppetiteState.PreGrowth));
        }

        [Test]
        public void Appetite_AnAdultHasNoGrowthRefusal()
        {
            var state = StateWithShedIn(TimeSpan.FromDays(30), growth: 100d);

            Assert.That(AppetiteModel.Evaluate(state, Now, tuning), Is.EqualTo(AppetiteState.Normal));
        }

        [Test]
        public void Feed_WhileRefusingFood_DoesNotRaiseHunger()
        {
            var state = StateWithShedIn(TimeSpan.FromDays(1));
            state.Hunger = 50d;

            var result = new CareService(tuning).Feed(state, Now);

            Assert.That(result, Is.EqualTo(AppetiteState.PreShed));
            Assert.That(state.Hunger, Is.EqualTo(50d));
        }

        [Test]
        public void Feed_WithANormalAppetite_Eats()
        {
            var state = StateWithShedIn(TimeSpan.FromDays(30));
            state.Hunger = 50d;

            var result = new CareService(tuning).Feed(state, Now);

            Assert.That(result, Is.EqualTo(AppetiteState.Normal));
            Assert.That(state.Hunger, Is.EqualTo(50d + tuning.FeedHungerAmount));
        }

        [Test]
        public void Offline_WhileRefusingFood_HungerDropsSlowerAndDoesNotHurtHealth()
        {
            var state = StateWithShedIn(TimeSpan.FromDays(1));
            state.Hunger = 15d; // would normally count as "low care"
            state.Health = 80d;

            new OfflineProgressCalculator(tuning).Apply(state, Now, Now + TimeSpan.FromHours(10));

            var expectedHunger = 15d - tuning.HungerDecayPerHour * tuning.AnorexiaHungerDecayMultiplier * 10d;
            Assert.That(state.Hunger, Is.EqualTo(expectedHunger).Within(1e-6));
            Assert.That(state.Health, Is.GreaterThanOrEqualTo(80d));
        }

        [Test]
        public void Offline_RefusingFoodBeforeAStageUp_DoesNotStallGrowth()
        {
            // Hunger too low to count as healthy, but the pet is in its pre-growth fast.
            var state = StateWithShedIn(TimeSpan.FromDays(30), growth: 48d);
            state.Hunger = 30d;

            new OfflineProgressCalculator(tuning).Apply(state, Now, Now + TimeSpan.FromHours(12));

            Assert.That(state.GrowthStage, Is.EqualTo(GrowthStage.Juvenile));
        }

        [Test]
        public void Offline_PassingTheShedDate_ShedsAndSchedulesTheNextOne()
        {
            var state = StateWithShedIn(TimeSpan.FromHours(3));

            var result = new OfflineProgressCalculator(tuning).Apply(state, Now, Now + TimeSpan.FromHours(6));

            Assert.That(result.ShedCount, Is.EqualTo(1));
            Assert.That(state.LastShedAtUtc, Is.EqualTo(Now + TimeSpan.FromHours(3)));
            Assert.That(state.NextShedAtUtc, Is.EqualTo(Now + TimeSpan.FromHours(3) + TimeSpan.FromDays(tuning.ShedIntervalDays)));
        }

        [Test]
        public void Offline_GrowingIntoTheNextStage_TriggersAShed()
        {
            var state = StateWithShedIn(TimeSpan.FromDays(30), growth: 49.9d);

            var result = new OfflineProgressCalculator(tuning).Apply(state, Now, Now + TimeSpan.FromHours(2));

            Assert.That(result.NewGrowthStage, Is.EqualTo(GrowthStage.Juvenile));
            Assert.That(result.ShedCount, Is.EqualTo(1));
            Assert.That(state.NextShedAtUtc - state.LastShedAtUtc, Is.EqualTo(TimeSpan.FromDays(tuning.ShedIntervalDays)));
        }

        [Test]
        public void Save_RoundTripsTheSheddingSchedule()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"shed-{Guid.NewGuid():N}.json");
            try
            {
                var state = StateWithShedIn(TimeSpan.FromDays(5));
                state.LastShedAtUtc = Now - TimeSpan.FromDays(55);
                var service = new SaveService();

                service.Save(path, state);
                var loaded = service.LoadOrCreateDefault(path, Now);

                Assert.That(loaded.NextShedAtUtc, Is.EqualTo(state.NextShedAtUtc));
                Assert.That(loaded.LastShedAtUtc, Is.EqualTo(state.LastShedAtUtc));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [Test]
        public void Save_FromBeforeShedding_SchedulesTheFirstShedOneIntervalAfterTheLastSave()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"shed-legacy-{Guid.NewGuid():N}.json");
            try
            {
                System.IO.File.WriteAllText(path,
                    "{\"schemaVersion\":1,\"lastSavedAtUtc\":\"2026-09-20T00:00:00.0000000+00:00\",\"hunger\":80,\"hydration\":80,\"cleanliness\":80,\"health\":100,\"growth\":0,\"growthStage\":\"Baby\",\"selectedDecorId\":\"rock_01\",\"unlockedDecorIds\":[\"rock_01\"]}");

                var loaded = new SaveService().LoadOrCreateDefault(path, Now);

                var lastSaved = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
                Assert.That(loaded.NextShedAtUtc, Is.EqualTo(lastSaved + TimeSpan.FromDays(tuning.ShedIntervalDays)));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
