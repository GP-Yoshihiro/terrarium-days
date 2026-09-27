using System;
using System.Linq;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class BreedingServiceTests
    {
        // Calendar day 0 is 1 April 2026 (in season).
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);
        private readonly CareTuning care = new CareTuning();
        private readonly GameCalendar calendar = new GameCalendar(Epoch);
        private BreedingService breeding;

        [SetUp]
        public void SetUp() => breeding = new BreedingService(care);

        private static DateTimeOffset At(double gameDays) => Epoch + GameCalendar.RealTimeFor(gameDays);

        private static PetState Adult(Sex sex, Personality personality = Personality.Calm, double grams = 55d) =>
            new PetState
            {
                Name = sex == Sex.Male ? "タロウ" : "ハナ",
                Sex = sex,
                SexRevealed = true,
                Stage = GrowthStage.Adult,
                WeightGrams = grams,
                HatchedAtUtc = Epoch.AddDays(-12),
                Personality = personality,
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

        [Test]
        public void StartPairing_MovesTheFemaleIntoTheMalesCageForThreeGameDays()
        {
            var colony = Room(out var male, out var female);
            var maleCage = colony.CageOf(male);
            var femaleCage = colony.CageOf(female);

            Assert.That(breeding.StartPairing(colony, male.Id, female.Id, Epoch, calendar), Is.EqualTo(PairingProblem.None));

            var pairing = colony.PairingOf(female);
            Assert.That(pairing, Is.SameAs(colony.PairingOf(male)));
            Assert.That(pairing.EndsAtUtc, Is.EqualTo(At(3d)));
            Assert.That(pairing.SuccessChance, Is.EqualTo(0.84d).Within(1e-9));
            Assert.That(maleCage.VisitorAnimalId, Is.EqualTo(female.Id));
            Assert.That(colony.VisitorIn(maleCage), Is.SameAs(female));
            Assert.That(colony.IsVisiting(female), Is.True);
            Assert.That(colony.IsVisiting(male), Is.False);
            Assert.That(colony.CageShowing(female), Is.SameAs(maleCage));
            Assert.That(colony.CageShowing(male), Is.SameAs(maleCage));
            Assert.That(colony.ResidentShown(femaleCage), Is.Null, "her own cage looks empty while she is away");
            Assert.That(femaleCage.IsEmpty, Is.False, "but it stays hers: the shop cannot put a new animal in it");
            Assert.That(colony.ShownCages(), Is.EqualTo(new[] { maleCage }));
        }

        [Test]
        public void StartPairing_RefusesOutOfSeasonBusyAndWrongPairs()
        {
            var colony = Room(out var male, out var female);
            var october = At(6 * GameCalendar.DaysPerMonth);

            Assert.That(breeding.StartPairing(colony, male.Id, female.Id, october, calendar), Is.EqualTo(PairingProblem.OutOfSeason));
            Assert.That(breeding.StartPairing(colony, female.Id, male.Id, Epoch, calendar), Is.EqualTo(PairingProblem.NotMaleAndFemale));
            Assert.That(breeding.StartPairing(colony, male.Id, 99, Epoch, calendar), Is.EqualTo(PairingProblem.NotFound));
            Assert.That(colony.Pairings, Is.Empty);

            Assert.That(breeding.StartPairing(colony, male.Id, female.Id, Epoch, calendar), Is.EqualTo(PairingProblem.None));
            Assert.That(breeding.StartPairing(colony, male.Id, female.Id, Epoch, calendar), Is.EqualTo(PairingProblem.AlreadyPairing));
            Assert.That(breeding.CheckCandidate(colony, male, Epoch, calendar), Is.EqualTo(PairingProblem.AlreadyPairing));
        }

        [Test]
        public void AGravidFemale_CannotPairAgain()
        {
            var colony = Room(out var male, out var female);
            female.Gravid = new GravidState { SeasonYear = 2026 };

            Assert.That(breeding.StartPairing(colony, male.Id, female.Id, Epoch, calendar), Is.EqualTo(PairingProblem.Gravid));
            Assert.That(breeding.CheckCandidate(colony, female, Epoch, calendar), Is.EqualTo(PairingProblem.Gravid));
            Assert.That(breeding.CheckCandidate(colony, male, Epoch, calendar), Is.EqualTo(PairingProblem.None));
            Assert.That(breeding.CheckCandidate(colony, male, At(200d), calendar), Is.EqualTo(PairingProblem.OutOfSeason));
        }

        [Test]
        public void CancelPairing_SendsTheFemaleHome()
        {
            var colony = Room(out var male, out var female);
            breeding.StartPairing(colony, male.Id, female.Id, Epoch, calendar);

            Assert.That(breeding.CancelPairing(colony, male), Is.True);

            Assert.That(colony.Pairings, Is.Empty);
            Assert.That(colony.CageOf(male).VisitorAnimalId, Is.EqualTo(-1));
            Assert.That(colony.ResidentShown(colony.CageOf(female)), Is.SameAs(female));
            Assert.That(breeding.CancelPairing(colony, male), Is.False);
        }

        [Test]
        public void RemovingAPairedAnimal_EndsThePairingAndClearsTheVisitor()
        {
            var colony = Room(out var male, out var female);
            breeding.StartPairing(colony, male.Id, female.Id, Epoch, calendar);

            colony.RemoveAnimal(male);

            Assert.That(colony.Pairings, Is.Empty);
            Assert.That(colony.Cages.TrueForAll(c => c.VisitorAnimalId == -1), Is.True);
            Assert.That(colony.ResidentShown(colony.CageOf(female)), Is.SameAs(female));
        }

        [Test]
        public void NestBox_ComesFromTheInventoryAndGoesBack()
        {
            var colony = Room(out _, out var female);
            var cage = colony.CageOf(female);

            Assert.That(breeding.PlaceNestBox(colony, cage), Is.EqualTo(NestBoxResult.NoneInInventory));
            colony.Inventory.Add(ShopCatalog.NestBoxId, 1);
            Assert.That(breeding.PlaceNestBox(colony, cage), Is.EqualTo(NestBoxResult.Ok));
            Assert.That(cage.HasNestBox, Is.True);
            Assert.That(colony.Inventory.Count(ShopCatalog.NestBoxId), Is.EqualTo(0));
            Assert.That(breeding.PlaceNestBox(colony, cage), Is.EqualTo(NestBoxResult.AlreadyPlaced));

            colony.Eggs.Add(new Egg { Id = 1, CageId = cage.Id, Place = EggPlace.NestBox });
            Assert.That(breeding.RemoveNestBox(colony, cage), Is.EqualTo(NestBoxResult.EggsInside));
            Assert.That(colony.EggsIn(cage), Has.Count.EqualTo(1));

            colony.Eggs.Clear();
            Assert.That(breeding.RemoveNestBox(colony, cage), Is.EqualTo(NestBoxResult.Ok));
            Assert.That(cage.HasNestBox, Is.False);
            Assert.That(colony.Inventory.Count(ShopCatalog.NestBoxId), Is.EqualTo(1));
            Assert.That(breeding.RemoveNestBox(colony, cage), Is.EqualTo(NestBoxResult.NotPlaced));
            Assert.That(breeding.PlaceNestBox(colony, null), Is.EqualTo(NestBoxResult.NoCage));
        }
    }
}
