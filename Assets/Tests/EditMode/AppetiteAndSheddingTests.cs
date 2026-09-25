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

        private PetState StateWithShedIn(TimeSpan untilShed, double weight = 10d, GrowthStage stage = GrowthStage.Baby, double ageMonths = 2d)
        {
            return new PetState
            {
                WeightGrams = weight,
                Stage = stage,
                HatchedAtUtc = Now.AddDays(-ageMonths),
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
        public void Appetite_IsLostInTheGameDaysBeforeAShed()
        {
            var state = StateWithShedIn(GameCalendar.RealTimeFor(tuning.PreShedGameDays - 0.5));

            Assert.That(AppetiteModel.Evaluate(state, Now, tuning), Is.EqualTo(AppetiteState.PreShed));
        }

        [Test]
        public void Appetite_IsLostWhileAStageUpIsPending()
        {
            var state = StateWithShedIn(TimeSpan.FromDays(3));
            state.StageUpDueAtUtc = Now + TimeSpan.FromMinutes(30);

            Assert.That(AppetiteModel.Evaluate(state, Now, tuning), Is.EqualTo(AppetiteState.PreGrowth));
            Assert.That(AppetiteModel.Evaluate(state, Now + TimeSpan.FromMinutes(31), tuning), Is.EqualTo(AppetiteState.Normal));
        }

        [Test]
        public void Feed_WhileRefusingFood_DoesNotRaiseHunger()
        {
            var state = StateWithShedIn(GameCalendar.RealTimeFor(1d));
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
            // Stays inside the pre-shed window the whole time (the shed itself, 1 game day
            // out, would otherwise reschedule NextShedAtUtc and end the refusal early).
            var elapsed = GameCalendar.RealTimeFor(1d) - TimeSpan.FromMinutes(1);
            var state = StateWithShedIn(GameCalendar.RealTimeFor(1d));
            state.Hunger = 15d; // would normally count as "low care"
            state.Health = 80d;

            new OfflineProgressCalculator(tuning).Apply(state, Now, Now + elapsed);

            var expectedHunger = 15d - tuning.HungerDecayPerHour * tuning.AnorexiaHungerDecayMultiplier * elapsed.TotalHours;
            Assert.That(state.Hunger, Is.EqualTo(expectedHunger).Within(1e-6));
            Assert.That(state.Health, Is.GreaterThanOrEqualTo(80d));
        }

        [Test]
        public void Offline_ReachingTheNextStagesWeight_FastsThreeGameDaysThenGrowsAndSheds()
        {
            var state = StateWithShedIn(TimeSpan.FromDays(3), weight: 15.2d);
            var fast = GameCalendar.RealTimeFor(tuning.PreGrowthFastGameDays);

            var first = new OfflineProgressCalculator(tuning).Apply(state, Now, Now + TimeSpan.FromMinutes(1));
            Assert.That(first.NewGrowthStage, Is.Null);
            Assert.That(state.StageUpDueAtUtc, Is.EqualTo(Now + TimeSpan.FromMinutes(1) + fast));
            Assert.That(AppetiteModel.Evaluate(state, Now + TimeSpan.FromMinutes(2), tuning), Is.EqualTo(AppetiteState.PreGrowth));

            var second = new OfflineProgressCalculator(tuning).Apply(state, state.LastSavedAtUtc + first.AppliedElapsed, Now + TimeSpan.FromMinutes(1) + fast);
            Assert.That(second.NewGrowthStage, Is.EqualTo(GrowthStage.Juvenile));
            Assert.That(second.ShedCount, Is.EqualTo(1));
            Assert.That(state.StageUpDueAtUtc, Is.Null);
        }

        [Test]
        public void Offline_AnUnderAgeHeavyJuvenile_DoesNotBecomeAnAdult()
        {
            var state = StateWithShedIn(TimeSpan.FromDays(3), weight: 50d, stage: GrowthStage.Juvenile, ageMonths: 6d);

            var result = new OfflineProgressCalculator(tuning).Apply(state, Now, Now + TimeSpan.FromHours(12));

            Assert.That(result.NewGrowthStage, Is.Null);
            Assert.That(state.StageUpDueAtUtc, Is.Null);
        }

        [Test]
        public void Offline_FastingLosesATenthOfAGramPerGameDay()
        {
            var state = StateWithShedIn(GameCalendar.RealTimeFor(1d), weight: 20d, stage: GrowthStage.Juvenile);

            new OfflineProgressCalculator(tuning).Apply(state, Now, Now + GameCalendar.RealTimeFor(1d));

            Assert.That(state.WeightGrams, Is.EqualTo(20d - 0.1d).Within(1e-6));
        }

        [TestCase(GrowthStage.Baby, 17.5)]
        [TestCase(GrowthStage.Juvenile, 17.5)]
        [TestCase(GrowthStage.Adult, 45d)]
        public void ShedInterval_DependsOnTheStage(GrowthStage stage, double gameDays)
        {
            Assert.That(SheddingModel.IntervalFor(stage, tuning), Is.EqualTo(GameCalendar.RealTimeFor(gameDays)));
        }

        [Test]
        public void Offline_PassingTheShedDate_ShedsAndSchedulesTheNextOne()
        {
            var state = StateWithShedIn(TimeSpan.FromHours(3));

            var result = new OfflineProgressCalculator(tuning).Apply(state, Now, Now + TimeSpan.FromHours(6));

            Assert.That(result.ShedCount, Is.EqualTo(1));
            Assert.That(state.LastShedAtUtc, Is.EqualTo(Now + TimeSpan.FromHours(3)));
            Assert.That(state.NextShedAtUtc, Is.EqualTo(Now + TimeSpan.FromHours(3) + SheddingModel.IntervalFor(GrowthStage.Baby, tuning)));
        }
    }
}
