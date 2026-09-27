using System;
using System.Linq;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class BreedingRulesTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);
        private readonly CareTuning care = new CareTuning();

        private static PetState Adult(Sex sex, double months = 12d, double grams = 55d, Personality personality = Personality.Calm) =>
            new PetState
            {
                Sex = sex,
                SexRevealed = true,
                Stage = GrowthStage.Adult,
                WeightGrams = grams,
                HatchedAtUtc = Now.AddDays(-months),
                Personality = personality,
                PersonalityKnown = true,
            };

        [TestCase(2, false)]
        [TestCase(3, true)]
        [TestCase(6, true)]
        [TestCase(9, true)]
        [TestCase(10, false)]
        [TestCase(12, false)]
        public void TheBreedingSeasonIsMarchToSeptember(int month, bool inSeason)
        {
            var date = new GameDate(2027, month, 15);

            Assert.That(BreedingRules.IsBreedingSeason(date, care), Is.EqualTo(inSeason));
            Assert.That(BreedingRules.SeasonOf(date, care), Is.EqualTo(inSeason ? 2027 : -1));
        }

        [Test]
        public void AFemaleNeedsTenMonthsAndFortyFiveGrams()
        {
            Assert.That(BreedingRules.CheckAnimal(Adult(Sex.Female, 10d, 45d), Sex.Female, Now, care), Is.EqualTo(PairingProblem.None));
            Assert.That(BreedingRules.CheckAnimal(Adult(Sex.Female, 9.9d, 50d), Sex.Female, Now, care), Is.EqualTo(PairingProblem.TooYoung));
            Assert.That(BreedingRules.CheckAnimal(Adult(Sex.Female, 12d, 44.9d), Sex.Female, Now, care), Is.EqualTo(PairingProblem.TooLight));
        }

        [Test]
        public void AMaleNeedsEightMonthsAndFortyGrams()
        {
            Assert.That(BreedingRules.CheckAnimal(Adult(Sex.Male, 8d, 40d), Sex.Male, Now, care), Is.EqualTo(PairingProblem.None));
            Assert.That(BreedingRules.CheckAnimal(Adult(Sex.Male, 7.9d, 50d), Sex.Male, Now, care), Is.EqualTo(PairingProblem.TooYoung));
            Assert.That(BreedingRules.CheckAnimal(Adult(Sex.Male, 12d, 39.9d), Sex.Male, Now, care), Is.EqualTo(PairingProblem.TooLight));
        }

        [Test]
        public void UnknownSexTheWrongSexAndWeakness_BlockBreeding()
        {
            var unknown = Adult(Sex.Female);
            unknown.SexRevealed = false;
            var weak = Adult(Sex.Female);
            weak.Weak = true;

            Assert.That(BreedingRules.CheckAnimal(unknown, Sex.Female, Now, care), Is.EqualTo(PairingProblem.SexUnknown));
            Assert.That(BreedingRules.CheckAnimal(Adult(Sex.Male), Sex.Female, Now, care), Is.EqualTo(PairingProblem.NotMaleAndFemale));
            Assert.That(BreedingRules.CheckAnimal(weak, Sex.Female, Now, care), Is.EqualTo(PairingProblem.Weak));
            Assert.That(BreedingRules.CheckAnimal(null, Sex.Female, Now, care), Is.EqualTo(PairingProblem.NotFound));
        }

        [Test]
        public void MatingSuccess_IsSeventyPercentTimesCompatibilityHealthAndWeight()
        {
            var male = Adult(Sex.Male);
            var female = Adult(Sex.Female, grams: 55d);

            Assert.That(BreedingRules.MatingSuccess(Compatibility.Good, male, female, care), Is.EqualTo(0.84d).Within(1e-9));
            Assert.That(BreedingRules.MatingSuccess(Compatibility.Normal, male, female, care), Is.EqualTo(0.7d).Within(1e-9));
            Assert.That(BreedingRules.MatingSuccess(Compatibility.Bad, male, female, care), Is.EqualTo(0.42d).Within(1e-9));

            female.WeightGrams = 45d;
            Assert.That(BreedingRules.MatingSuccess(Compatibility.Normal, male, female, care), Is.EqualTo(0.595d).Within(1e-9));

            female.WeightGrams = 55d;
            male.Health = 50d;
            Assert.That(BreedingRules.MatingSuccess(Compatibility.Normal, male, female, care), Is.EqualTo(0.525d).Within(1e-9));
        }

        [Test]
        public void ClutchRangeAndFertility_FollowCompatibility()
        {
            Assert.That(BreedingRules.ClutchRange(Compatibility.Good, care), Is.EqualTo((5, 9)));
            Assert.That(BreedingRules.ClutchRange(Compatibility.Normal, care), Is.EqualTo((4, 8)));
            Assert.That(BreedingRules.ClutchRange(Compatibility.Bad, care), Is.EqualTo((3, 7)));
            Assert.That(BreedingRules.FertilityFor(Compatibility.Good, care), Is.EqualTo(0.9d));
            Assert.That(BreedingRules.FertilityFor(Compatibility.Normal, care), Is.EqualTo(0.9d));
            Assert.That(BreedingRules.FertilityFor(Compatibility.Bad, care), Is.EqualTo(0.75d));
        }

        [Test]
        public void Forecast_UsesWhatThePlayerKnows()
        {
            var male = Adult(Sex.Male, personality: Personality.Calm);
            male.Known = new KnownGenetics().SetHet(GeneId.Eclipse, 1d);
            var female = Adult(Sex.Female, personality: Personality.Shy);
            female.Known = new KnownGenetics().SetHet(GeneId.Eclipse, 1d);

            var forecast = BreedingForecast.For(male, female, care);

            Assert.That(forecast.CompatibilityKnown, Is.True);
            Assert.That(forecast.Compatibility, Is.EqualTo(Compatibility.Good));
            Assert.That(forecast.SuccessChance, Is.EqualTo(0.84d).Within(1e-9));
            Assert.That((forecast.MinClutches, forecast.MaxClutches), Is.EqualTo((5, 9)));
            Assert.That(forecast.Offspring.Select(o => o.Name), Is.EqualTo(new[] { "ノーマル", "エクリプス" }));
            Assert.That(forecast.Offspring.Select(o => o.Probability), Is.EqualTo(new[] { 0.75d, 0.25d }).Within(1e-9));
        }

        [Test]
        public void Forecast_WithAnUnknownPersonality_AssumesANormalMatch()
        {
            var male = Adult(Sex.Male, personality: Personality.Bold);
            var female = Adult(Sex.Female, personality: Personality.Shy); // Bold x Shy is bad in truth
            female.PersonalityKnown = false;

            var forecast = BreedingForecast.For(male, female, care);

            Assert.That(forecast.CompatibilityKnown, Is.False);
            Assert.That(forecast.Compatibility, Is.EqualTo(Compatibility.Normal));
            Assert.That(forecast.SuccessChance, Is.EqualTo(0.7d).Within(1e-9));
            Assert.That((forecast.MinClutches, forecast.MaxClutches), Is.EqualTo((4, 8)));
        }

        [Test]
        public void BreedingRandom_IsReproducibleAndDiffersByIndex()
        {
            var a = BreedingRandom.For(123, BreedingRandom.ClutchStream, 7, 0).NextDouble();
            var b = BreedingRandom.For(123, BreedingRandom.ClutchStream, 7, 0).NextDouble();
            var c = BreedingRandom.For(123, BreedingRandom.ClutchStream, 7, 1).NextDouble();
            var d = BreedingRandom.For(123, BreedingRandom.PairingStream, 7, 0).NextDouble();

            Assert.That(a, Is.EqualTo(b));
            Assert.That(c, Is.Not.EqualTo(a));
            Assert.That(d, Is.Not.EqualTo(a));
            Assert.That(BreedingRandom.SeedOf(Now), Is.EqualTo(BreedingRandom.SeedOf(Now)));
            Assert.That(BreedingRandom.Uniform(new Random(1), 3d, 5d), Is.InRange(3d, 5d));
        }
    }
}
