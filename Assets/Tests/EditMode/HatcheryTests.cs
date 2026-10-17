using System;
using System.Collections.Generic;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class HatcheryTests
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset Now = Epoch + GameCalendar.RealTimeFor(60);

        private readonly CareTuning care = new CareTuning();
        private Hatchery hatchery;

        [SetUp]
        public void SetUp()
        {
            hatchery = new Hatchery(care);
        }

        private static Colony NewColony() => new Colony { CalendarEpochUtc = Epoch };

        private static Egg AddEgg(Colony colony, EggPlace place, double development, bool fertile = true, double middleC = 0d, int cageId = -1)
        {
            var egg = new Egg
            {
                Id = colony.NextEggId++,
                MotherId = 0,
                FatherId = 0,
                CageId = cageId,
                LaidAtUtc = Epoch,
                Place = place,
                IncubatorId = place == EggPlace.Incubator ? colony.Incubators[0].Id : 0,
                Fertile = fertile,
                DevelopmentPercent = development
            };
            if (middleC > 0d)
            {
                egg.MiddleThirdGameDays = 20d;
                egg.MiddleThirdTemperatureSum = middleC * 20d;
            }

            colony.Eggs.Add(egg);
            return egg;
        }

        [Test]
        public void ADevelopedEgg_HatchesIntoTheTray()
        {
            var colony = NewColony();
            var egg = AddEgg(colony, EggPlace.Incubator, 100d, middleC: 26d);
            egg.MotherId = 41;
            egg.FatherId = 42;
            var report = new IncubationReport();

            hatchery.Advance(colony, Now, 22d, report);

            Assert.AreEqual(1, report.Hatched.Count);
            var pet = report.Hatched[0];
            CollectionAssert.Contains(colony.Animals, pet);
            CollectionAssert.Contains(colony.TrayAnimals(), pet);
            Assert.IsNull(colony.CageOf(pet));
            Assert.AreEqual($"レオパ{pet.Id}", pet.Name);
            Assert.AreEqual(GrowthStage.Baby, pet.Stage);
            Assert.IsFalse(pet.SexRevealed);
            Assert.IsFalse(pet.PersonalityKnown);
            Assert.AreEqual(Now + GameCalendar.RealTimeFor(care.PersonalityRevealGameDays), pet.PersonalityRevealAtUtc);
            Assert.AreEqual(care.HatchlingWeightGrams, pet.WeightGrams, 1e-9);
            Assert.AreEqual(Now, pet.HatchedAtUtc);
            Assert.AreEqual(Now, pet.LastSavedAtUtc);
            Assert.AreEqual(41, pet.MotherId);
            Assert.AreEqual(42, pet.FatherId);
            Assert.AreEqual(Now + GameCalendar.RealTimeFor(care.HatchlingCareGraceGameDays), pet.CareGraceUntilUtc);
            CollectionAssert.DoesNotContain(colony.Eggs, egg);
        }

        [Test]
        public void UndevelopedInfertileOrDeadEggs_DoNotHatch()
        {
            var colony = NewColony();
            AddEgg(colony, EggPlace.Incubator, 99.9d);
            AddEgg(colony, EggPlace.Incubator, 100d, fertile: false);
            var dead = AddEgg(colony, EggPlace.Incubator, 100d);
            dead.Failure = EggFailure.Heat;
            var report = new IncubationReport();

            hatchery.Advance(colony, Now, 22d, report);

            Assert.IsEmpty(report.Hatched);
            Assert.AreEqual(3, colony.Eggs.Count, "incubator eggs are removed by the player, not automatically");
        }

        [Test]
        public void ParentsThatExist_PassOnTheirPersonality_AboutAFifthEachPlusTheRandomRoll()
        {
            var colony = NewColony();
            var mother = colony.AddAnimal(new PetState { Sex = Sex.Female, Personality = Personality.Shy }, null);
            var father = colony.AddAnimal(new PetState { Sex = Sex.Male, Personality = Personality.Bold }, null);
            for (var i = 0; i < 200; i++)
            {
                var egg = AddEgg(colony, EggPlace.Incubator, 100d);
                egg.MotherId = mother.Id;
                egg.FatherId = father.Id;
            }

            var report = new IncubationReport();
            hatchery.Advance(colony, Now, 22d, report);

            Assert.AreEqual(200, report.Hatched.Count);
            Assert.That(report.Hatched.TrueForAll(p => p.MotherId == mother.Id));
            var shy = report.Hatched.FindAll(p => p.Personality == Personality.Shy).Count;
            var bold = report.Hatched.FindAll(p => p.Personality == Personality.Bold).Count;
            // Expected 72 each (20% inherited + 20% of the 60% random roll); a random-only hatchery gives ~40.
            Assert.GreaterOrEqual(shy, 45, "the mother's personality must be passed on");
            Assert.GreaterOrEqual(bold, 45, "the father's personality must be passed on");
            Assert.GreaterOrEqual(shy + bold, 110, "inheritance must beat a plain random roll (~80)");
        }

        [Test]
        public void CoolMiddleThird_GivesMostlyFemales()
        {
            var colony = NewColony();
            for (var i = 0; i < 200; i++)
            {
                AddEgg(colony, EggPlace.Incubator, 100d, middleC: 26d);
            }

            var report = new IncubationReport();
            hatchery.Advance(colony, Now, 22d, report);

            Assert.AreEqual(200, report.Hatched.Count);
            Assert.GreaterOrEqual(report.Hatched.FindAll(p => p.Sex == Sex.Female).Count, 170);
        }

        [Test]
        public void NoRecord_InAnIncubator_UsesTheSetTemperature()
        {
            var colony = NewColony();
            colony.Incubators[0].SetTemperatureC = 32;
            for (var i = 0; i < 200; i++)
            {
                AddEgg(colony, EggPlace.Incubator, 100d);
            }

            var report = new IncubationReport();
            hatchery.Advance(colony, Now, 26d, report);

            Assert.LessOrEqual(report.Hatched.FindAll(p => p.Sex == Sex.Female).Count, 80);
        }

        [Test]
        public void NoRecord_InANestBox_UsesTheRoomTemperature()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Standard);
            for (var i = 0; i < 200; i++)
            {
                AddEgg(colony, EggPlace.NestBox, 100d, cageId: cage.Id);
            }

            var report = new IncubationReport();
            hatchery.Advance(colony, Now, 32d, report);

            Assert.AreEqual(200, report.Hatched.Count);
            Assert.LessOrEqual(report.Hatched.FindAll(p => p.Sex == Sex.Female).Count, 80);
        }

        [Test]
        public void Sex_IsTheSameForTheSameEgg()
        {
            var first = NewColony();
            var second = NewColony();
            for (var i = 0; i < 20; i++)
            {
                AddEgg(first, EggPlace.Incubator, 100d, middleC: 29.5d);
                AddEgg(second, EggPlace.Incubator, 100d, middleC: 29.5d);
            }

            var firstReport = new IncubationReport();
            var secondReport = new IncubationReport();
            hatchery.Advance(first, Now, 22d, firstReport);
            hatchery.Advance(second, Now, 22d, secondReport);

            var firstSexes = new List<Sex>();
            var secondSexes = new List<Sex>();
            firstReport.Hatched.ForEach(p => firstSexes.Add(p.Sex));
            secondReport.Hatched.ForEach(p => secondSexes.Add(p.Sex));
            CollectionAssert.AreEqual(firstSexes, secondSexes);
        }

        [Test]
        public void NestBox_InfertileEggs_AreClearedAtSevenDays()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Standard);
            var egg = AddEgg(colony, EggPlace.NestBox, 0d, fertile: false, cageId: cage.Id);

            var early = new IncubationReport();
            hatchery.Advance(colony, Epoch + GameCalendar.RealTimeFor(6.9d), 26d, early);
            Assert.IsEmpty(early.NestBoxEggsRemoved);
            CollectionAssert.Contains(colony.Eggs, egg);

            var report = new IncubationReport();
            hatchery.Advance(colony, Epoch + GameCalendar.RealTimeFor(7d), 26d, report);
            CollectionAssert.AreEqual(new[] { egg }, report.NestBoxEggsRemoved);
            CollectionAssert.DoesNotContain(colony.Eggs, egg);
            Assert.AreEqual(cage.Id, egg.CageId, "the report keeps where it was");
        }

        [Test]
        public void NestBox_DeadEggs_AreClearedAtOnce()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Standard);
            var egg = AddEgg(colony, EggPlace.NestBox, 20d, cageId: cage.Id);
            egg.Failure = EggFailure.Cold;
            var report = new IncubationReport();

            hatchery.Advance(colony, Epoch + GameCalendar.RealTimeFor(3d), 26d, report);

            CollectionAssert.AreEqual(new[] { egg }, report.NestBoxEggsRemoved);
            CollectionAssert.DoesNotContain(colony.Eggs, egg);
        }

        [Test]
        public void MoveToCage_UsesTheLowestEmptyCage()
        {
            var colony = NewColony();
            var occupied = colony.AddCage(CageSize.Standard);
            colony.AddAnimal(new PetState(), occupied);
            var empty = colony.AddCage(CageSize.Standard);
            var hatchling = colony.AddAnimal(new PetState(), null);

            Assert.AreEqual(MoveToCageResult.Ok, Hatchery.MoveToCage(colony, hatchling));
            Assert.AreSame(empty, colony.CageOf(hatchling));
            Assert.IsEmpty(colony.TrayAnimals());
            Assert.AreEqual(MoveToCageResult.NotInTray, Hatchery.MoveToCage(colony, hatchling));
        }

        [Test]
        public void MoveToCage_WithoutAnEmptyCage_Refuses()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Standard);
            colony.AddAnimal(new PetState(), cage);
            var hatchling = colony.AddAnimal(new PetState(), null);

            Assert.IsNull(colony.FirstEmptyCage());
            Assert.AreEqual(MoveToCageResult.NoEmptyCage, Hatchery.MoveToCage(colony, hatchling));
            CollectionAssert.Contains(colony.TrayAnimals(), hatchling);
        }
    }
}
