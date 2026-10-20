using System;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.UI;

namespace TerrariumDays.Tests
{
    public sealed class EventLockTests
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);
        private readonly CareTuning care = new CareTuning();
        private readonly GameCalendar calendar = new GameCalendar(Epoch);

        private static PetState Adult(Sex sex) =>
            new PetState
            {
                Name = sex == Sex.Male ? "タロウ" : "ハナ",
                Sex = sex,
                SexRevealed = true,
                Stage = GrowthStage.Adult,
                WeightGrams = 55d,
                HatchedAtUtc = Epoch.AddDays(-12),
                Personality = Personality.Calm,
                PersonalityKnown = true,
                LastSavedAtUtc = Epoch,
            };

        private static Colony Room(out PetState male, out PetState female)
        {
            var colony = new Colony { CalendarEpochUtc = Epoch };
            male = colony.AddAnimal(Adult(Sex.Male), colony.AddCage(CageSize.Standard));
            female = colony.AddAnimal(Adult(Sex.Female), colony.AddCage(CageSize.Standard));
            return colony;
        }

        private static void List(Colony colony, PetState pet)
        {
            colony.Events.Entry = new EventEntry { Number = 0, FeePaid = 10000 };
            colony.Events.Entry.Listings.Add(new EventListing { AnimalId = pet.Id, Price = 5000 });
        }

        [Test]
        public void ListedAnimals_CannotPair()
        {
            var breeding = new BreedingService(care);
            var colony = Room(out var male, out var female);
            Assert.That(breeding.CheckPair(colony, male, female, Epoch, calendar), Is.EqualTo(PairingProblem.None));

            List(colony, male);
            Assert.That(breeding.CheckPair(colony, male, female, Epoch, calendar), Is.EqualTo(PairingProblem.Listed));
            Assert.That(breeding.CheckCandidate(colony, male, Epoch, calendar), Is.EqualTo(PairingProblem.Listed));
            Assert.That(breeding.CheckCandidate(colony, female, Epoch, calendar), Is.EqualTo(PairingProblem.None));
            Assert.That(breeding.StartPairing(colony, male.Id, female.Id, Epoch, calendar), Is.EqualTo(PairingProblem.Listed));
            Assert.That(colony.Pairings, Is.Empty);

            List(colony, female);
            Assert.That(breeding.CheckPair(colony, male, female, Epoch, calendar), Is.EqualTo(PairingProblem.Listed));
        }

        [Test]
        public void ListedAnimals_CannotBeSoldToTheShop()
        {
            var shop = new ShopService(new EconomyTuning(), care);
            var colony = Room(out var male, out var female);
            List(colony, male);

            Assert.That(shop.Wholesale(colony, male.Id, Epoch), Is.EqualTo(ShopResult.Listed));
            Assert.That(colony.AnimalById(male.Id), Is.SameAs(male));
            Assert.That(shop.Wholesale(colony, female.Id, Epoch), Is.EqualTo(ShopResult.Ok));
        }

        [Test]
        public void TheLock_EndsWithTheEvent()
        {
            var breeding = new BreedingService(care);
            var colony = Room(out var male, out var female);
            List(colony, male);

            colony.Events.Entry = null;

            Assert.That(breeding.CheckPair(colony, male, female, Epoch, calendar), Is.EqualTo(PairingProblem.None));
        }

        [Test]
        public void Texts_ForTheLock()
        {
            Assert.That(BreedingText.ProblemLabel(PairingProblem.Listed), Is.EqualTo("出品中です"));
            Assert.That(ShopText.FailureMessage(ShopResult.Listed), Is.EqualTo("出品中の個体は売れません"));
        }
    }
}
