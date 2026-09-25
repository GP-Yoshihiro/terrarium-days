using System;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.Gameplay;

namespace TerrariumDays.Tests
{
    public sealed class GrowthModelTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        private readonly CareTuning tuning = new CareTuning();

        private PetState Pet(double weight, GrowthStage stage, double ageMonths, Sex sex = Sex.Female) => new PetState
        {
            WeightGrams = weight,
            Stage = stage,
            HatchedAtUtc = Now.AddDays(-ageMonths),
            Sex = sex,
        };

        [Test]
        public void ANewPetIsAThreeGramBabyWhoseSexIsNotKnownYet()
        {
            var pet = new PetState();

            Assert.That(pet.WeightGrams, Is.EqualTo(3d));
            Assert.That(pet.Stage, Is.EqualTo(GrowthStage.Baby));
            Assert.That(pet.SexKnown, Is.False);
        }

        [Test]
        public void SexBecomesKnownFromTheJuvenileStage()
        {
            Assert.That(Pet(20d, GrowthStage.Juvenile, 4).SexKnown, Is.True);
        }

        [TestCase(14.9, GrowthStage.Baby, 1, false)]
        [TestCase(15.0, GrowthStage.Baby, 1, true)]
        [TestCase(45.0, GrowthStage.Juvenile, 9.9, false)]
        [TestCase(39.9, GrowthStage.Juvenile, 12, false)]
        [TestCase(40.0, GrowthStage.Juvenile, 10, true)]
        [TestCase(70.0, GrowthStage.Adult, 30, false)]
        public void MeetsNextStage_NeedsWeightAndForAdultsAge(double weight, GrowthStage stage, double ageMonths, bool expected)
        {
            Assert.That(GrowthModel.MeetsNextStage(Pet(weight, stage, ageMonths), Now, tuning), Is.EqualTo(expected));
        }

        [Test]
        public void FeedGain_ShrinksAsThePetNearsItsCap()
        {
            var light = GrowthModel.FeedGain(Pet(3d, GrowthStage.Baby, 0), tuning);
            var heavy = GrowthModel.FeedGain(Pet(50d, GrowthStage.Adult, 12), tuning);

            Assert.That(light, Is.EqualTo(2.2d * (1d - 3d / 65d)).Within(1e-9), "sex unknown → 65 g cap");
            Assert.That(heavy, Is.EqualTo(2.2d * (1d - 50d / 60d)).Within(1e-9), "adult female → 60 g cap");
            Assert.That(GrowthModel.FeedGain(Pet(80d, GrowthStage.Adult, 12, Sex.Male), tuning), Is.EqualTo(0d));
        }

        [Test]
        public void TwoAndAHalfMealsADay_ReachAdultWeightInAboutTenDays()
        {
            var pet = Pet(3d, GrowthStage.Baby, 0, Sex.Male);
            var meals = 0;
            while (pet.WeightGrams < tuning.AdultMinWeightGrams && meals < 200)
            {
                if (pet.WeightGrams >= tuning.JuvenileMinWeightGrams)
                {
                    pet.Stage = GrowthStage.Juvenile;
                }

                pet.WeightGrams += GrowthModel.FeedGain(pet, tuning);
                meals++;
            }

            Assert.That(meals / 2.5, Is.InRange(9d, 12d));
        }

        [Test]
        public void ProgressToNextStage_UsesTheSlowerOfWeightAndAgeForAdults()
        {
            // 40 g reached but only 5 of 10 months old → half way.
            Assert.That(GrowthModel.ProgressToNextStage(Pet(40d, GrowthStage.Juvenile, 5), Now, tuning), Is.EqualTo(0.5).Within(1e-9));
            // Baby at 9 g: (9-3)/(15-3).
            Assert.That(GrowthModel.ProgressToNextStage(Pet(9d, GrowthStage.Baby, 1), Now, tuning), Is.EqualTo(0.5).Within(1e-9));
            Assert.That(GrowthModel.ProgressToNextStage(Pet(60d, GrowthStage.Adult, 20), Now, tuning), Is.EqualTo(1d));
        }

        [TestCase(GrowthStage.Baby, "ベビー")]
        [TestCase(GrowthStage.Juvenile, "ヤング")]
        [TestCase(GrowthStage.Adult, "アダルト")]
        public void StageLabelsAreJapanese(GrowthStage stage, string label)
        {
            Assert.That(GrowthModel.StageLabel(stage), Is.EqualTo(label));
        }

        [Test]
        public void Feeding_AddsWeight()
        {
            var pet = Pet(10d, GrowthStage.Baby, 1);

            new CareService(tuning).Feed(pet);

            Assert.That(pet.WeightGrams, Is.EqualTo(10d + 2.2d * (1d - 10d / 65d)).Within(1e-9));
        }
    }
}
