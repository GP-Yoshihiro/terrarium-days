using System;
using System.Collections.Generic;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class EventRulesTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        private readonly EconomyTuning economy = new EconomyTuning();
        private readonly CareTuning care = new CareTuning();

        private Colony NewColony(out PetState pet)
        {
            var colony = Colony.CreateNew(Now, economy, care, new Random(1));
            pet = colony.Animals[0];
            return colony;
        }

        [Test]
        public void NewColony_HasNoEntry()
        {
            var colony = NewColony(out _);

            Assert.That(colony.Events, Is.Not.Null);
            Assert.That(colony.Events.Entry, Is.Null);
            Assert.That(colony.Events.LastSummary, Is.Null);
            Assert.That(colony.Events.LastSummarySeen, Is.True);
            Assert.That(colony.Events.NoticeNumber, Is.EqualTo(-1));
        }

        [Test]
        public void Check_AHealthyCagedAnimal_CanBeListed()
        {
            var colony = NewColony(out var pet);

            Assert.That(EventRules.Check(colony, pet), Is.EqualTo(ListingProblem.None));
        }

        [Test]
        public void Check_TrayGravidPairingWeakAndMissing_CannotBeListed()
        {
            var colony = NewColony(out var pet);
            var tray = colony.AddAnimal(new PetState { Name = "トレイ" }, null);
            Assert.That(EventRules.Check(colony, tray), Is.EqualTo(ListingProblem.InTray));

            pet.Gravid = new GravidState();
            Assert.That(EventRules.Check(colony, pet), Is.EqualTo(ListingProblem.Gravid));
            pet.Gravid = null;

            pet.Weak = true;
            Assert.That(EventRules.Check(colony, pet), Is.EqualTo(ListingProblem.Weak));
            pet.Weak = false;

            colony.Pairings.Add(new Pairing { Id = 1, MaleId = pet.Id, FemaleId = 99 });
            Assert.That(EventRules.Check(colony, pet), Is.EqualTo(ListingProblem.Pairing));
            colony.Pairings.Clear();

            colony.RemoveAnimal(pet);
            Assert.That(EventRules.Check(colony, pet), Is.EqualTo(ListingProblem.NotFound));
            Assert.That(EventRules.Check(colony, null), Is.EqualTo(ListingProblem.NotFound));
        }

        [Test]
        public void BaseValue_IsTheMarketPriceWithTheEventDemand()
        {
            var colony = NewColony(out var pet);
            pet.Weak = true;

            Assert.That(EventRules.BaseValue(pet, 42, EventKind.Big),
                Is.EqualTo(MarketPrice.For(pet, EventDemand.For(42, pet.Genotype, true), true)));
            Assert.That(EventRules.BaseValue(pet, 42, EventKind.Small),
                Is.EqualTo(MarketPrice.For(pet, EventDemand.For(42, pet.Genotype, false), true)));
        }

        [Test]
        public void MorphLabel_AddsTheSexMark()
        {
            var pet = new PetState { Genotype = Genotype.Normal(), Sex = Sex.Female, SexRevealed = true };
            var look = MorphNamer.VisualName(pet.Genotype);

            Assert.That(EventRules.MorphLabel(pet), Is.EqualTo(look + "♀"));
            pet.Sex = Sex.Male;
            Assert.That(EventRules.MorphLabel(pet), Is.EqualTo(look + "♂"));
            pet.SexRevealed = false;
            Assert.That(EventRules.MorphLabel(pet), Is.EqualTo(look + "?"));
        }

        [Test]
        public void DemandSeed_DependsOnTheColonyAndTheNumber()
        {
            Assert.That(EventRandom.DemandSeed(123, 0), Is.EqualTo(EventRandom.DemandSeed(123, 0)));
            Assert.That(EventRandom.DemandSeed(123, 0), Is.Not.EqualTo(EventRandom.DemandSeed(123, 1)));
            Assert.That(EventRandom.DemandSeed(123, 0), Is.Not.EqualTo(EventRandom.DemandSeed(456, 0)));
            Assert.That(EventRandom.DemandSeed(123, 0), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void ListingSeed_IgnoresTheOrderButNotThePricesOrTolerances()
        {
            var a = new EventListing { AnimalId = 3, Price = 20000, TolerancePercent = 5 };
            var b = new EventListing { AnimalId = 8, Price = 9000, TolerancePercent = 0 };
            var seed = EventRandom.ListingSeed(1, 4, new List<EventListing> { a, b });

            Assert.That(EventRandom.ListingSeed(1, 4, new List<EventListing> { b, a }), Is.EqualTo(seed));
            Assert.That(EventRandom.ListingSeed(1, 5, new List<EventListing> { a, b }), Is.Not.EqualTo(seed));
            Assert.That(EventRandom.ListingSeed(1, 4, new List<EventListing> { a, new EventListing { AnimalId = 8, Price = 10000 } }), Is.Not.EqualTo(seed));
            Assert.That(EventRandom.ListingSeed(1, 4, new List<EventListing> { a, new EventListing { AnimalId = 8, Price = 9000, TolerancePercent = 10 } }), Is.Not.EqualTo(seed));
            Assert.That(EventRandom.ListingSeed(1, 4, new List<EventListing>()), Is.Not.EqualTo(seed));
        }

        [Test]
        public void ForActionAndForBooth_AreFixedByTheirArguments()
        {
            Assert.That(EventRandom.ForAction(77, 2, EventActionKind.Serve).NextDouble(),
                Is.EqualTo(EventRandom.ForAction(77, 2, EventActionKind.Serve).NextDouble()));
            Assert.That(EventRandom.ForAction(77, 2, EventActionKind.Serve).NextDouble(),
                Is.Not.EqualTo(EventRandom.ForAction(77, 2, EventActionKind.Browse).NextDouble()));
            Assert.That(EventRandom.ForAction(77, 2, EventActionKind.Serve).NextDouble(),
                Is.Not.EqualTo(EventRandom.ForAction(77, 3, EventActionKind.Serve).NextDouble()));
            Assert.That(EventRandom.ForBooth(77, 2).NextDouble(), Is.EqualTo(EventRandom.ForBooth(77, 2).NextDouble()));
        }

        [Test]
        public void IsListed_OnlyWhileUnsoldInTheEntry()
        {
            var state = new EventState();
            Assert.That(state.IsListed(5), Is.False);

            state.Entry = new EventEntry { Number = 1 };
            state.Entry.Listings.Add(new EventListing { AnimalId = 5, Price = 1000 });
            Assert.That(state.IsListed(5), Is.True);
            Assert.That(state.IsListed(6), Is.False);

            state.Entry.Listings[0].SoldInAction = 2;
            Assert.That(state.IsListed(5), Is.False);
        }

        [Test]
        public void Summary_TotalsAndNet()
        {
            var summary = new EventSummary { Number = 1, BoothFee = 3000 };
            summary.Sold.Add(new EventAnimalLine { Amount = 20000 });
            summary.Sold.Add(new EventAnimalLine { Amount = 8500 });
            summary.Bought.Add(new EventAnimalLine { Amount = 12000 });

            Assert.That(summary.TotalSales, Is.EqualTo(28500));
            Assert.That(summary.PurchaseTotal, Is.EqualTo(12000));
            Assert.That(summary.Net, Is.EqualTo(13500));
        }
    }
}
