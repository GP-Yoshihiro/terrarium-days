using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class IncubatorServiceTests
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset Now = Epoch + GameCalendar.RealTimeFor(20);

        private readonly CareTuning care = new CareTuning();
        private IncubatorService service;

        [SetUp]
        public void SetUp()
        {
            service = new IncubatorService(care);
        }

        private static Colony NewColony() => new Colony { CalendarEpochUtc = Epoch };

        private static Egg AddCageEgg(Colony colony, Cage cage, double development = 10d, bool fertile = true)
        {
            var egg = new Egg
            {
                Id = colony.NextEggId++,
                CageId = cage.Id,
                LaidAtUtc = Epoch,
                Place = EggPlace.NestBox,
                Fertile = fertile,
                DevelopmentPercent = development,
                MiddleThirdGameDays = 2d,
                MiddleThirdTemperatureSum = 54d
            };
            colony.Eggs.Add(egg);
            return egg;
        }

        [Test]
        public void MoveAllFromCage_MovesEggsIntoTheFirstIncubator()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Standard);
            var first = AddCageEgg(colony, cage);
            var second = AddCageEgg(colony, cage);

            var result = service.MoveAllFromCage(colony, cage);

            Assert.AreEqual(2, result.Moved);
            Assert.AreEqual(0, result.Left);
            foreach (var egg in new[] { first, second })
            {
                Assert.AreEqual(EggPlace.Incubator, egg.Place);
                Assert.AreEqual(1, egg.IncubatorId);
                Assert.AreEqual(-1, egg.CageId);
            }

            Assert.IsEmpty(colony.EggsIn(cage));
        }

        [Test]
        public void MoveAllFromCage_KeepsDevelopmentAndTheMiddleThirdRecord()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Standard);
            var egg = AddCageEgg(colony, cage, 40d);

            service.MoveAllFromCage(colony, cage);

            Assert.AreEqual(40d, egg.DevelopmentPercent);
            Assert.AreEqual(2d, egg.MiddleThirdGameDays);
            Assert.AreEqual(54d, egg.MiddleThirdTemperatureSum);
        }

        [Test]
        public void MoveAllFromCage_LeavesWhatDoesNotFit_LowestEggIdsGoFirst()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Standard);
            for (var i = 0; i < 8; i++)
            {
                AddCageEgg(colony, cage);
            }

            var result = service.MoveAllFromCage(colony, cage);

            Assert.AreEqual(6, result.Moved);
            Assert.AreEqual(2, result.Left);
            var left = colony.EggsIn(cage);
            Assert.AreEqual(2, left.Count);
            Assert.AreEqual(7, left[0].Id);
            Assert.AreEqual(8, left[1].Id);
        }

        [Test]
        public void MoveAllFromCage_FillsTheNextIncubatorInIdOrder()
        {
            var colony = NewColony();
            colony.AddIncubator(IncubatorModel.Standard);
            var cage = colony.AddCage(CageSize.Standard);
            for (var i = 0; i < 8; i++)
            {
                AddCageEgg(colony, cage);
            }

            var result = service.MoveAllFromCage(colony, cage);

            Assert.AreEqual(8, result.Moved);
            Assert.AreEqual(6, IncubatorService.EggsInIncubator(colony, colony.Incubators[0]).Count);
            Assert.AreEqual(2, IncubatorService.EggsInIncubator(colony, colony.Incubators[1]).Count);
        }

        [Test]
        public void MoveAllFromCage_NoFreeSlot_MovesNothing()
        {
            var colony = NewColony();
            service.AddDebugEggs(colony, Now, 6);
            var cage = colony.AddCage(CageSize.Standard);
            AddCageEgg(colony, cage);

            var result = service.MoveAllFromCage(colony, cage);

            Assert.AreEqual(0, result.Moved);
            Assert.AreEqual(1, result.Left);
        }

        [Test]
        public void MoveAllFromCage_SkipsDeadEggs()
        {
            var colony = NewColony();
            var cage = colony.AddCage(CageSize.Standard);
            var dead = AddCageEgg(colony, cage);
            dead.Failure = EggFailure.Cold;

            var result = service.MoveAllFromCage(colony, cage);

            Assert.AreEqual(0, result.Moved);
            Assert.AreEqual(0, result.Left);
            Assert.AreEqual(EggPlace.NestBox, dead.Place);
        }

        [Test]
        public void AddWater_AddsFifteenPercent_CapsAtHundred_AndWarnsAboveNinetyFive()
        {
            var incubator = new Incubator { Id = 1, HumidityPercent = 70d };

            Assert.IsFalse(service.AddWater(incubator));
            Assert.AreEqual(85d, incubator.HumidityPercent, 1e-9);

            Assert.IsTrue(service.AddWater(incubator));
            Assert.AreEqual(100d, incubator.HumidityPercent, 1e-9);
        }

        [Test]
        public void SetTemperature_ClampsToTheRange()
        {
            var incubator = new Incubator { Id = 1 };

            Assert.AreEqual(34, service.SetTemperature(incubator, 35));
            Assert.AreEqual(34, incubator.SetTemperatureC);
            Assert.AreEqual(24, service.SetTemperature(incubator, 10));
            Assert.AreEqual(29, service.SetTemperature(incubator, 29));
        }

        [Test]
        public void RemoveEgg_OnlyKnownInfertileOrDeadEggs()
        {
            var colony = NewColony();
            service.AddDebugEggs(colony, Now, 3);
            var live = colony.Eggs[0];
            var infertile = colony.Eggs[1];
            infertile.Fertile = false;
            infertile.LaidAtUtc = Now - GameCalendar.RealTimeFor(6.9d);
            var dead = colony.Eggs[2];
            dead.Failure = EggFailure.Mold;

            Assert.IsFalse(service.RemoveEgg(colony, live, Now));
            Assert.IsFalse(service.RemoveEgg(colony, infertile, Now), "infertile but not candled yet");
            Assert.IsTrue(service.RemoveEgg(colony, dead, Now));

            infertile.LaidAtUtc = Now - GameCalendar.RealTimeFor(7d);
            Assert.IsTrue(service.RemoveEgg(colony, infertile, Now));
            Assert.AreEqual(1, colony.Eggs.Count);
        }

        [Test]
        public void AddDebugEggs_AddsThreeFertileEggsAtNinetyPercentWithUnknownParents()
        {
            var colony = NewColony();

            Assert.AreEqual(3, service.AddDebugEggs(colony, Now));

            Assert.AreEqual(3, colony.Eggs.Count);
            foreach (var egg in colony.Eggs)
            {
                Assert.IsTrue(egg.Fertile);
                Assert.AreEqual(90d, egg.DevelopmentPercent);
                Assert.AreEqual(0, egg.MotherId);
                Assert.AreEqual(0, egg.FatherId);
                Assert.AreEqual(EggPlace.Incubator, egg.Place);
                Assert.AreEqual(1, egg.IncubatorId);
                Assert.AreEqual(28d, egg.MiddleThirdTemperatureSum / egg.MiddleThirdGameDays, 1e-9);
                Assert.Less(egg.LaidAtUtc, Now);
            }
        }

        [Test]
        public void AddDebugEggs_OnlyFillsTheFreeSlots()
        {
            var colony = NewColony();
            service.AddDebugEggs(colony, Now, 5);

            Assert.AreEqual(1, service.AddDebugEggs(colony, Now));
            Assert.AreEqual(0, service.AddDebugEggs(colony, Now));
            Assert.AreEqual(6, IncubatorService.EggsInIncubator(colony, colony.Incubators[0]).Count);
            Assert.AreEqual(0, IncubatorService.FreeSlots(colony, colony.Incubators[0]));
        }
    }
}
