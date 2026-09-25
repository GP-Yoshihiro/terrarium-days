using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class PersonalityTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        private readonly CareTuning care = new CareTuning();

        [TestCase(Personality.Calm, "おっとり")]
        [TestCase(Personality.Shy, "臆病")]
        [TestCase(Personality.Curious, "好奇心旺盛")]
        [TestCase(Personality.Bold, "気が強い")]
        [TestCase(Personality.Glutton, "食いしん坊")]
        public void Labels(Personality personality, string label)
        {
            Assert.That(PersonalityTraits.Label(personality), Is.EqualTo(label));
        }

        [Test]
        public void Multipliers_MatchTheSpec()
        {
            Assert.That(PersonalityTraits.ThreatMultiplier(Personality.Calm), Is.EqualTo(0.5d));
            Assert.That(PersonalityTraits.ThreatMultiplier(Personality.Shy), Is.EqualTo(1.5d));
            Assert.That(PersonalityTraits.ThreatMultiplier(Personality.Bold), Is.EqualTo(1.5d));
            Assert.That(PersonalityTraits.WalkFrequencyMultiplier(Personality.Curious), Is.EqualTo(1.3d));
            Assert.That(PersonalityTraits.FastMultiplier(Personality.Shy), Is.EqualTo(1.5d));
            Assert.That(PersonalityTraits.FastMultiplier(Personality.Glutton), Is.EqualTo(0.5d));
            Assert.That(PersonalityTraits.WeightGainMultiplier(Personality.Bold), Is.EqualTo(1.1d));
            Assert.That(PersonalityTraits.WeightGainMultiplier(Personality.Glutton), Is.EqualTo(1.2d));
            Assert.That(PersonalityTraits.PriceMultiplier(Personality.Calm), Is.EqualTo(1.1d));
            Assert.That(PersonalityTraits.PriceMultiplier(Personality.Shy), Is.EqualTo(0.9d));
            Assert.That(PersonalityTraits.PriceMultiplier(Personality.Curious), Is.EqualTo(1.05d));
            Assert.That(PersonalityTraits.PriceMultiplier(Personality.Glutton), Is.EqualTo(1d));
        }

        [TestCase(Personality.Calm, Personality.Bold, Compatibility.Good)]
        [TestCase(Personality.Shy, Personality.Calm, Compatibility.Good)]
        [TestCase(Personality.Shy, Personality.Bold, Compatibility.Bad)]
        [TestCase(Personality.Bold, Personality.Shy, Compatibility.Bad)]
        [TestCase(Personality.Bold, Personality.Bold, Compatibility.Bad)]
        [TestCase(Personality.Curious, Personality.Glutton, Compatibility.Good)]
        [TestCase(Personality.Glutton, Personality.Glutton, Compatibility.Normal)]
        [TestCase(Personality.Shy, Personality.Shy, Compatibility.Normal)]
        public void Compatibility_TableRowsAreTheMale(Personality male, Personality female, Compatibility expected)
        {
            Assert.That(PersonalityTraits.CompatibilityOf(male, female), Is.EqualTo(expected));
        }

        [Test]
        public void Compatibility_Effects()
        {
            Assert.That(PersonalityTraits.MatingSuccessMultiplier(Compatibility.Good), Is.EqualTo(1.2d));
            Assert.That(PersonalityTraits.MatingSuccessMultiplier(Compatibility.Bad), Is.EqualTo(0.6d));
            Assert.That(PersonalityTraits.ClutchDelta(Compatibility.Good), Is.EqualTo(1));
            Assert.That(PersonalityTraits.ClutchDelta(Compatibility.Bad), Is.EqualTo(-1));
            Assert.That(PersonalityTraits.ClutchDelta(Compatibility.Normal), Is.EqualTo(0));
        }

        [Test]
        public void Inherit_TakesEachParentAboutAFifthOfTheTimePlusRandomShare()
        {
            var random = new Random(11);
            var fromMother = 0;
            const int count = 5000;
            for (var i = 0; i < count; i++)
            {
                if (PersonalityTraits.Inherit(Personality.Shy, Personality.Bold, random) == Personality.Shy)
                {
                    fromMother++;
                }
            }

            // 20% inherited + 60% random × 1/5.
            Assert.That(fromMother / (double)count, Is.EqualTo(0.32d).Within(0.025d));
        }

        [Test]
        public void BehaviourTuning_ScalesThreatWalkAndHiding()
        {
            var baseline = new PetBehaviourTuning();

            var calm = PersonalityTraits.BehaviourTuningFor(baseline, Personality.Calm);
            var shy = PersonalityTraits.BehaviourTuningFor(baseline, Personality.Shy);
            var curious = PersonalityTraits.BehaviourTuningFor(baseline, Personality.Curious);
            var glutton = PersonalityTraits.BehaviourTuningFor(baseline, Personality.Glutton);

            Assert.That(calm.WakeStartleChance, Is.EqualTo(baseline.WakeStartleChance * 0.5d).Within(1e-9));
            Assert.That(calm.ThreatTapCount, Is.EqualTo(8));
            Assert.That(shy.ThreatTapCount, Is.EqualTo(3));
            Assert.That(shy.DaySleepMaxSeconds, Is.EqualTo(baseline.DaySleepMaxSeconds * 1.5f).Within(1e-4));
            Assert.That(curious.NormalIdleMaxSeconds, Is.EqualTo(baseline.NormalIdleMaxSeconds / 1.3f).Within(1e-4));
            Assert.That(glutton.PreStrikeTailWagSeconds, Is.EqualTo(baseline.PreStrikeTailWagSeconds * 0.5f).Within(1e-4));
            Assert.That(baseline.ThreatTapCount, Is.EqualTo(4), "the shared tuning must not change");
        }

        [Test]
        public void FeedGain_UsesTheWeightGainMultiplier()
        {
            var calm = new PetState { WeightGrams = 10d, Personality = Personality.Calm };
            var glutton = new PetState { WeightGrams = 10d, Personality = Personality.Glutton };

            Assert.That(GrowthModel.FeedGain(glutton, care), Is.EqualTo(GrowthModel.FeedGain(calm, care) * 1.2d).Within(1e-9));
        }

        [Test]
        public void PreShedFast_IsLongerForShyAndShorterForGlutton()
        {
            // 2.5 game days before the shed: inside a shy (3 days) window, outside calm (2).
            var shedIn = GameCalendar.RealTimeFor(2.5d);
            var shy = new PetState { Personality = Personality.Shy, NextShedAtUtc = Now + shedIn };
            var calm = new PetState { Personality = Personality.Calm, NextShedAtUtc = Now + shedIn };
            // 1.5 game days before: inside calm (2), outside glutton (1).
            var glutton = new PetState { Personality = Personality.Glutton, NextShedAtUtc = Now + GameCalendar.RealTimeFor(1.5d) };

            Assert.That(AppetiteModel.Evaluate(shy, Now, care), Is.EqualTo(AppetiteState.PreShed));
            Assert.That(AppetiteModel.Evaluate(calm, Now, care), Is.EqualTo(AppetiteState.Normal));
            Assert.That(AppetiteModel.Evaluate(glutton, Now, care), Is.EqualTo(AppetiteState.Normal));
        }

        [Test]
        public void PreGrowthFast_IsScaledByPersonality()
        {
            var shy = new PetState
            {
                Personality = Personality.Shy,
                WeightGrams = 15.2d,
                HatchedAtUtc = Now.AddDays(-2d),
                LastSavedAtUtc = Now,
                NextShedAtUtc = Now + TimeSpan.FromDays(3),
            };

            new OfflineProgressCalculator(care).Apply(shy, Now, Now + TimeSpan.FromMinutes(1));

            Assert.That(shy.StageUpDueAtUtc, Is.EqualTo(Now + TimeSpan.FromMinutes(1) + GameCalendar.RealTimeFor(care.PreGrowthFastGameDays * 1.5d)));
        }
    }
}
