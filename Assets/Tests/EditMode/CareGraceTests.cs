using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class CareGraceTests
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

        private OfflineProgressCalculator calculator;

        [SetUp]
        public void SetUp()
        {
            calculator = new OfflineProgressCalculator(new CareTuning());
        }

        private static PetState NewPet() => new PetState
        {
            Stage = GrowthStage.Baby,
            HatchedAtUtc = Epoch,
            LastSavedAtUtc = Epoch,
            LastShedAtUtc = Epoch,
            NextShedAtUtc = Epoch + TimeSpan.FromDays(30)
        };

        [Test]
        public void DuringGrace_CareStatsDoNotDrop()
        {
            var pet = NewPet();
            pet.CareGraceUntilUtc = Epoch + TimeSpan.FromHours(10);
            var hunger = pet.Hunger;
            var hydration = pet.Hydration;
            var cleanliness = pet.Cleanliness;
            var health = pet.Health;

            calculator.Apply(pet, Epoch, Epoch + TimeSpan.FromHours(6));

            Assert.AreEqual(hunger, pet.Hunger, 1e-9);
            Assert.AreEqual(hydration, pet.Hydration, 1e-9);
            Assert.AreEqual(cleanliness, pet.Cleanliness, 1e-9);
            Assert.AreEqual(health, pet.Health, 1e-9);
        }

        [Test]
        public void WithoutGrace_CareStatsDrop()
        {
            var pet = NewPet();
            var hunger = pet.Hunger;

            calculator.Apply(pet, Epoch, Epoch + TimeSpan.FromHours(6));

            Assert.Less(pet.Hunger, hunger);
        }

        [Test]
        public void GraceEndingMidway_OnlyTheRestCounts()
        {
            var control = NewPet();
            var partial = NewPet();
            partial.CareGraceUntilUtc = Epoch + TimeSpan.FromHours(1);
            var hunger = partial.Hunger;

            calculator.Apply(control, Epoch, Epoch + TimeSpan.FromHours(6));
            calculator.Apply(partial, Epoch, Epoch + TimeSpan.FromHours(6));

            Assert.Less(partial.Hunger, hunger);
            Assert.Greater(partial.Hunger, control.Hunger);
        }

        [Test]
        public void DuringGrace_SheddingStillHappens()
        {
            var pet = NewPet();
            pet.CareGraceUntilUtc = Epoch + TimeSpan.FromHours(10);
            pet.NextShedAtUtc = Epoch + TimeSpan.FromHours(1);

            calculator.Apply(pet, Epoch, Epoch + TimeSpan.FromHours(6));

            Assert.Greater(pet.LastShedAtUtc, Epoch);
        }
    }
}
