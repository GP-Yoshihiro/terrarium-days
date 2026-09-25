using System;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.Gameplay;

namespace TerrariumDays.Tests
{
    public sealed class ColonyTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        private readonly CareTuning care = new CareTuning();
        private readonly EconomyTuning economy = new EconomyTuning();

        private Colony NewColony() => Colony.CreateNew(Now, economy, care, new Random(1));

        [Test]
        public void ANewColonyHasOneBabyInOneStandardCageAndStartingMoney()
        {
            var colony = NewColony();

            Assert.That(colony.Wallet.Money, Is.EqualTo(50000));
            Assert.That(colony.RackCount, Is.EqualTo(1));
            Assert.That(colony.IncubatorCount, Is.EqualTo(1));
            Assert.That(colony.Cages, Has.Count.EqualTo(1));
            Assert.That(colony.Cages[0].Size, Is.EqualTo(CageSize.Standard));
            var pet = colony.AnimalIn(colony.Cages[0]);
            Assert.That(pet.Id, Is.EqualTo(1));
            Assert.That(pet.Stage, Is.EqualTo(GrowthStage.Baby));
            Assert.That(colony.CageOf(pet), Is.SameAs(colony.Cages[0]));
        }

        [Test]
        public void ARackHoldsFourCages()
        {
            var colony = NewColony();

            for (var i = 0; i < 3; i++)
            {
                Assert.That(colony.AddCage(CageSize.Small), Is.Not.Null);
            }

            Assert.That(colony.CanAddCage, Is.False);
            Assert.That(colony.AddCage(CageSize.Small), Is.Null);

            colony.RackCount = 2;
            Assert.That(colony.AddCage(CageSize.Large).Id, Is.EqualTo(5));
        }

        [Test]
        public void AddAnimal_GivesItTheNextIdAndPutsItInTheCage()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Standard);

            var pet = colony.AddAnimal(new PetState(), cage);

            Assert.That(pet.Id, Is.EqualTo(2));
            Assert.That(cage.AnimalId, Is.EqualTo(2));
            Assert.That(colony.OccupiedCages(), Has.Count.EqualTo(2));
        }

        [Test]
        public void Feed_ChargesTheFeedCostAndAddsWeight()
        {
            var colony = NewColony();
            var pet = colony.Animals[0];
            pet.NextShedAtUtc = Now.AddDays(3);
            var weight = pet.WeightGrams;

            var outcome = new ColonyCareService(care, economy).Feed(colony, pet, Now);

            Assert.That(outcome, Is.EqualTo(FeedOutcome.Ate));
            Assert.That(colony.Wallet.Money, Is.EqualTo(50000 - 30));
            Assert.That(pet.WeightGrams, Is.GreaterThan(weight));
        }

        [Test]
        public void Feed_WithoutMoney_DoesNotFeed()
        {
            var colony = NewColony();
            colony.Wallet.Money = 10;
            var pet = colony.Animals[0];
            pet.NextShedAtUtc = Now.AddDays(3);
            var hunger = pet.Hunger;

            Assert.That(new ColonyCareService(care, economy).Feed(colony, pet, Now), Is.EqualTo(FeedOutcome.NotEnoughMoney));
            Assert.That(pet.Hunger, Is.EqualTo(hunger));
        }

        [Test]
        public void Feed_WhileFasting_IsFreeAndRefused()
        {
            var colony = NewColony();
            var pet = colony.Animals[0];
            pet.NextShedAtUtc = Now.AddMinutes(10);

            Assert.That(new ColonyCareService(care, economy).Feed(colony, pet, Now), Is.EqualTo(FeedOutcome.RefusedPreShed));
            Assert.That(colony.Wallet.Money, Is.EqualTo(50000));
        }

        [Test]
        public void FeedAll_ReportsWhoAteAndWhoRefused()
        {
            var colony = NewColony();
            colony.Animals[0].NextShedAtUtc = Now.AddDays(3);
            var second = colony.AddAnimal(new PetState { NextShedAtUtc = Now.AddMinutes(10) }, colony.AddCage(CageSize.Standard));

            var result = new ColonyCareService(care, economy).FeedAll(colony, Now);

            Assert.That((result.Fed, result.Refused, result.NoMoney), Is.EqualTo((1, 1, 0)));
            Assert.That(result.ToMessage(), Is.EqualTo("1匹が食べました（1匹は拒食中）"));
            Assert.That(second.Hunger, Is.EqualTo(80d));
        }

        [Test]
        public void WaterAndCleanAll_CareForEveryPet()
        {
            var colony = NewColony();
            colony.AddAnimal(new PetState { Hydration = 10d, Cleanliness = 10d }, colony.AddCage(CageSize.Standard));
            var service = new ColonyCareService(care, economy);

            Assert.That(service.RefreshWaterAll(colony), Is.EqualTo(2));
            Assert.That(service.CleanAll(colony), Is.EqualTo(2));
            Assert.That(colony.Animals[1].Hydration, Is.EqualTo(10d + care.RefreshWaterHydrationAmount));
        }
    }
}
