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

        private static void MakeGravid(PetState female, PetState male, int planned, double firstClutchDay, int pairingId = 1)
        {
            female.Gravid = new GravidState
            {
                PairingId = pairingId,
                FatherId = male.Id,
                FatherName = male.Name,
                FatherGenotype = male.Genotype.Clone(),
                FatherKnown = male.Known.Clone(),
                Compatibility = Compatibility.Normal,
                SeasonYear = 2026,
                ClutchesPlanned = planned,
                NextClutchAtUtc = At(firstClutchDay),
            };
        }

        [Test]
        public void ASuccessfulPairing_MakesTheFemaleGravidWithAScheduleInRange()
        {
            var colony = Room(out var male, out var female);
            breeding.StartPairing(colony, male.Id, female.Id, Epoch, calendar);
            colony.PairingOf(female).SuccessChance = 1d;

            Assert.That(breeding.Advance(colony, At(2.9d), calendar, 28d).HasEvents, Is.False);
            Assert.That(colony.IsVisiting(female), Is.True);

            var report = breeding.Advance(colony, At(3d), calendar, 28d);

            Assert.That(report.PairingsSucceeded, Is.EqualTo(new[] { (female, male) }));
            Assert.That(colony.Pairings, Is.Empty);
            Assert.That(colony.CageOf(male).VisitorAnimalId, Is.EqualTo(-1));
            var gravid = female.Gravid;
            Assert.That(gravid, Is.Not.Null);
            Assert.That(gravid.FatherId, Is.EqualTo(male.Id));
            Assert.That(gravid.FatherGenotype, Is.Not.SameAs(male.Genotype));
            Assert.That(gravid.Compatibility, Is.EqualTo(Compatibility.Good));
            Assert.That(gravid.SeasonYear, Is.EqualTo(2026));
            Assert.That(gravid.ClutchesPlanned, Is.InRange(5, 9));
            Assert.That(gravid.NextClutchAtUtc, Is.InRange(At(3d + 21d), At(3d + 28d)));
        }

        [Test]
        public void AFailedPairing_LeavesTheFemaleNotGravid()
        {
            var colony = Room(out var male, out var female);
            breeding.StartPairing(colony, male.Id, female.Id, Epoch, calendar);
            colony.PairingOf(female).SuccessChance = 0d;

            var report = breeding.Advance(colony, At(3d), calendar, 28d);

            Assert.That(report.PairingsFailed, Is.EqualTo(new[] { (female, male) }));
            Assert.That(female.Gravid, Is.Null);
            Assert.That(colony.IsVisiting(female), Is.False);
        }

        [Test]
        public void APairingEndingAfterTheSeason_BearsNoFruit()
        {
            var colony = Room(out var male, out var female);
            Assert.That(breeding.StartPairing(colony, male.Id, female.Id, At(178d), calendar), Is.EqualTo(PairingProblem.None)); // 29 September
            colony.PairingOf(female).SuccessChance = 1d;

            var report = breeding.Advance(colony, At(181d), calendar, 28d); // 2 October

            Assert.That(report.PairingsCancelled, Is.EqualTo(new[] { (female, male, BreedingEnd.SeasonOver) }));
            Assert.That(female.Gravid, Is.Null);
        }

        [Test]
        public void AWeakAnimal_EndsThePairing()
        {
            var colony = Room(out var male, out var female);
            breeding.StartPairing(colony, male.Id, female.Id, Epoch, calendar);
            male.Weak = true;

            var report = breeding.Advance(colony, At(1d), calendar, 28d);

            Assert.That(report.PairingsCancelled, Is.EqualTo(new[] { (female, male, BreedingEnd.Weak) }));
            Assert.That(colony.IsVisiting(female), Is.False);
        }

        [Test]
        public void Clutches_ComeOnScheduleAndStopAfterThePlannedNumber()
        {
            var colony = Room(out var male, out var female);
            colony.CageOf(female).HasNestBox = true;
            female.WeightGrams = 60d;
            MakeGravid(female, male, planned: 3, firstClutchDay: 30d);

            Assert.That(breeding.Advance(colony, At(29.9d), calendar, 28d).Clutches, Is.Empty);

            var first = breeding.Advance(colony, At(30d), calendar, 28d);

            Assert.That(first.Clutches, Has.Count.EqualTo(1));
            Assert.That(first.Clutches[0].Female, Is.SameAs(female));
            Assert.That(first.Clutches[0].EggCount, Is.InRange(1, 2));
            Assert.That(first.Clutches[0].InNestBox, Is.True);
            Assert.That(colony.Eggs, Has.Count.EqualTo(first.Clutches[0].EggCount));
            Assert.That(colony.Eggs.TrueForAll(e => e.Place == EggPlace.NestBox && e.LaidAtUtc == At(30d)
                && e.MotherId == female.Id && e.FatherId == male.Id && e.CageId == colony.CageOf(female).Id), Is.True);
            Assert.That(female.WeightGrams, Is.InRange(55d, 57d));
            Assert.That(female.Gravid.ClutchesLaid, Is.EqualTo(1));
            Assert.That(female.Gravid.NextClutchAtUtc, Is.InRange(At(30d + 14d), At(30d + 28d)));

            var rest = breeding.Advance(colony, At(120d), calendar, 28d); // 1 August, still in season

            Assert.That(rest.Clutches, Has.Count.EqualTo(2));
            Assert.That(rest.GravidEnded, Is.EqualTo(new[] { (female, BreedingEnd.AllClutchesLaid) }));
            Assert.That(female.Gravid, Is.Null);
        }

        [Test]
        public void OverManyClutches_IntervalsEggCountsAndTheSingleEggRateFollowTheSpec()
        {
            var singles = 0;
            var clutches = 0;
            for (var pairingId = 1; pairingId <= 60; pairingId++)
            {
                var colony = Room(out var male, out var female);
                colony.CageOf(female).HasNestBox = true;
                female.WeightGrams = 500d; // never too thin in this test
                MakeGravid(female, male, planned: 8, firstClutchDay: 30d, pairingId: pairingId);

                while (female.Gravid != null)
                {
                    var due = female.Gravid.NextClutchAtUtc;
                    var report = breeding.Advance(colony, due, calendar, 28d);
                    foreach (var clutch in report.Clutches)
                    {
                        clutches++;
                        Assert.That(clutch.EggCount, Is.InRange(1, 2));
                        if (clutch.EggCount == 1)
                        {
                            singles++;
                        }
                    }

                    if (female.Gravid != null)
                    {
                        Assert.That(GameCalendar.GameDaysBetween(due, female.Gravid.NextClutchAtUtc), Is.InRange(14d, 28d));
                    }
                }
            }

            Assert.That(clutches, Is.GreaterThan(150));
            Assert.That((double)singles / clutches, Is.InRange(0.03d, 0.2d));
        }

        [Test]
        public void AFemaleBelowFortyGrams_StopsLayingForTheSeason()
        {
            var colony = Room(out var male, out var female);
            female.WeightGrams = 42d;
            MakeGravid(female, male, planned: 6, firstClutchDay: 30d);

            var report = breeding.Advance(colony, At(30d), calendar, 28d);

            Assert.That(report.Clutches, Has.Count.EqualTo(1), "she still lays this clutch");
            Assert.That(female.WeightGrams, Is.LessThan(40d));
            Assert.That(report.GravidEnded, Is.EqualTo(new[] { (female, BreedingEnd.TooThin) }));
            Assert.That(female.Gravid, Is.Null);

            var thinRoom = Room(out var male2, out var female2);
            female2.WeightGrams = 39d;
            MakeGravid(female2, male2, planned: 6, firstClutchDay: 30d);

            var none = breeding.Advance(thinRoom, At(30d), calendar, 28d);

            Assert.That(none.Clutches, Is.Empty);
            Assert.That(none.GravidEnded, Is.EqualTo(new[] { (female2, BreedingEnd.TooThin) }));
        }

        [Test]
        public void TheEndOfTheSeason_EndsTheGravidState()
        {
            var colony = Room(out var male, out var female);
            MakeGravid(female, male, planned: 8, firstClutchDay: 175d); // 26 September

            var report = breeding.Advance(colony, At(185d), calendar, 28d); // 6 October

            Assert.That(report.Clutches, Has.Count.EqualTo(1));
            Assert.That(report.GravidEnded, Is.EqualTo(new[] { (female, BreedingEnd.SeasonOver) }));
            Assert.That(female.Gravid, Is.Null);
        }

        [Test]
        public void AWeakGravidFemale_StopsBeingGravid()
        {
            var colony = Room(out var male, out var female);
            MakeGravid(female, male, planned: 6, firstClutchDay: 30d);
            female.Weak = true;

            var report = breeding.Advance(colony, At(10d), calendar, 28d);

            Assert.That(report.GravidEnded, Is.EqualTo(new[] { (female, BreedingEnd.Weak) }));
            Assert.That(female.Gravid, Is.Null);
        }

        [Test]
        public void WithoutANestBox_EggsDryUpAfterTwoGameDays()
        {
            var colony = Room(out var male, out var female);
            MakeGravid(female, male, planned: 1, firstClutchDay: 30d);

            var laid = breeding.Advance(colony, At(30d), calendar, 28d);
            var count = colony.Eggs.Count;

            Assert.That(laid.Clutches[0].InNestBox, Is.False);
            Assert.That(colony.Eggs.TrueForAll(e => e.Place == EggPlace.Loose), Is.True);
            Assert.That(breeding.Advance(colony, At(31.9d), calendar, 28d).EggsDried, Is.Empty);

            var dried = breeding.Advance(colony, At(32d), calendar, 28d);

            Assert.That(dried.EggsDried, Has.Count.EqualTo(count));
            Assert.That(dried.EggsDried.TrueForAll(e => e.Failure == EggFailure.Dried), Is.True);
            Assert.That(colony.Eggs, Is.Empty);
        }

        [Test]
        public void NestBoxEggs_DevelopAtRoomTemperatureUpToTheOfflineCap()
        {
            var colony = Room(out var male, out var female);
            colony.CageOf(female).HasNestBox = true;
            MakeGravid(female, male, planned: 1, firstClutchDay: 30d);
            breeding.Advance(colony, At(30d), calendar, 28d);
            colony.Eggs.ForEach(e => e.Fertile = true);

            breeding.Advance(colony, At(40d), calendar, 28d); // 10 game days = 8 real hours

            Assert.That(colony.Eggs[0].DevelopmentPercent, Is.EqualTo(100d * 10d / 60d).Within(1e-6));

            breeding.Advance(colony, At(70d), calendar, 28d); // 30 game days away, but at most 12 real hours (15 game days) apply

            Assert.That(colony.Eggs[0].DevelopmentPercent, Is.EqualTo(100d * 25d / 60d).Within(1e-6));
        }

        [Test]
        public void InfertileEggsDoNotDevelopAndColdKillsFertileOnes()
        {
            var colony = Room(out var male, out var female);
            colony.CageOf(female).HasNestBox = true;
            MakeGravid(female, male, planned: 1, firstClutchDay: 30d);
            breeding.Advance(colony, At(30d), calendar, 28d);
            colony.Eggs.ForEach(e => e.Fertile = false);

            breeding.Advance(colony, At(40d), calendar, 28d);
            Assert.That(colony.Eggs[0].DevelopmentPercent, Is.EqualTo(0d));

            colony.Eggs.ForEach(e => e.Fertile = true);
            breeding.Advance(colony, At(42d), calendar, 20d);
            Assert.That(colony.Eggs[0].ColdGameDays, Is.EqualTo(2d).Within(1e-9));
            Assert.That(colony.Eggs[0].Failed, Is.False);

            breeding.Advance(colony, At(43d), calendar, 20d);
            Assert.That(colony.Eggs[0].Failure, Is.EqualTo(EggFailure.Cold));
            Assert.That(colony.Eggs[0].DevelopmentPercent, Is.EqualTo(0d));
        }

        [Test]
        public void EggsCarryTheChildsGenesAndWhatThePlayerKnows()
        {
            var colony = Room(out var male, out var female);
            colony.CageOf(female).HasNestBox = true;
            female.Genotype = Genotype.Normal().Set(GeneId.Eclipse, 2);
            MakeGravid(female, male, planned: 1, firstClutchDay: 30d);

            breeding.Advance(colony, At(30d), calendar, 28d);

            Assert.That(colony.Eggs, Is.Not.Empty);
            foreach (var egg in colony.Eggs)
            {
                Assert.That(egg.ChildGenotype.Copies(GeneId.Eclipse), Is.EqualTo(1));
                Assert.That(egg.ChildKnown.HetProbability(GeneId.Eclipse), Is.EqualTo(1d));
                Assert.That(MorphNamer.FullName(egg.ChildGenotype, egg.ChildKnown), Does.Contain("ヘテロエクリプス"));
            }
        }

        [TestCase(24d, 76d)]
        [TestCase(28d, 60d)]
        [TestCase(29d, 56d)]
        [TestCase(30d, 52d)]
        [TestCase(32d, 45d)]
        public void DaysToHatch_FollowsTheIncubationTable(double celsius, double days)
        {
            Assert.That(EggDevelopment.DaysToHatch(celsius), Is.EqualTo(days).Within(1e-9));
        }

        [Test]
        public void TheMiddleThirdRecordsItsAverageTemperature()
        {
            var egg = new Egg { Fertile = true, DevelopmentPercent = 30d };

            EggDevelopment.Apply(egg, 6.8d, 26d, care); // 26 ℃: 68 days to hatch, so +10 %

            Assert.That(egg.DevelopmentPercent, Is.EqualTo(40d).Within(1e-9));
            Assert.That(egg.MiddleThirdGameDays, Is.EqualTo((40d - 100d / 3d) * 68d / 100d).Within(1e-9));
            Assert.That(egg.MiddleThirdTemperatureSum / egg.MiddleThirdGameDays, Is.EqualTo(26d).Within(1e-9));
        }
    }
}
