using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class IncubationAdvanceTests
    {
        private static readonly DateTimeOffset Epoch = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

        private readonly CareTuning care = new CareTuning();
        private IncubatorService service;

        [SetUp]
        public void SetUp()
        {
            service = new IncubatorService(care);
        }

        private static Colony NewColony(IncubatorModel model = IncubatorModel.Luxury, int setC = 28)
        {
            var colony = new Colony { CalendarEpochUtc = Epoch };
            colony.Incubators[0].Model = model;
            colony.Incubators[0].SetTemperatureC = setC;
            colony.Incubators[0].AppliedUntilUtc = Epoch;
            return colony;
        }

        private static Egg AddEgg(Colony colony, bool fertile = true)
        {
            var egg = new Egg
            {
                Id = colony.NextEggId++,
                CageId = -1,
                LaidAtUtc = Epoch,
                Place = EggPlace.Incubator,
                IncubatorId = colony.Incubators[0].Id,
                Fertile = fertile
            };
            colony.Eggs.Add(egg);
            return egg;
        }

        private static DateTimeOffset AtDay(double gameDays) => Epoch + GameCalendar.RealTimeFor(gameDays);

        [Test]
        public void FirstAdvance_OnlySyncsTheClock()
        {
            var colony = NewColony();
            colony.Incubators[0].AppliedUntilUtc = default;
            var target = AtDay(5);

            var report = service.Advance(colony, target, 22d);

            Assert.AreEqual(target, colony.Incubators[0].AppliedUntilUtc);
            Assert.AreEqual(80d, colony.Incubators[0].HumidityPercent);
            Assert.IsFalse(report.HasEvents);
        }

        [Test]
        public void Advance_AtOrBeforeTheLastTime_DoesNothing()
        {
            var colony = NewColony();
            colony.Incubators[0].AppliedUntilUtc = AtDay(3);

            service.Advance(colony, AtDay(2), 22d);

            Assert.AreEqual(AtDay(3), colony.Incubators[0].AppliedUntilUtc);
            Assert.AreEqual(80d, colony.Incubators[0].HumidityPercent);
        }

        [Test]
        public void Humidity_DropsTwoPercentPerGameDay()
        {
            var colony = NewColony();

            service.Advance(colony, AtDay(5), 22d);

            Assert.AreEqual(70d, colony.Incubators[0].HumidityPercent, 1e-6);
        }

        [Test]
        public void Humidity_OnlyTwelveHoursAreApplied()
        {
            var colony = NewColony();
            var target = Epoch + TimeSpan.FromHours(24);

            service.Advance(colony, target, 22d);

            Assert.AreEqual(80d - 2d * 15d, colony.Incubators[0].HumidityPercent, 1e-6);
            Assert.AreEqual(target, colony.Incubators[0].AppliedUntilUtc);
        }

        [Test]
        public void Humidity_StopsAtZero()
        {
            var colony = NewColony();
            colony.Incubators[0].HumidityPercent = 5d;

            service.Advance(colony, AtDay(5), 22d);

            Assert.AreEqual(0d, colony.Incubators[0].HumidityPercent, 1e-9);
        }

        [Test]
        public void Eggs_DevelopAtTheLuxurySetting()
        {
            var colony = NewColony(IncubatorModel.Luxury, 30);
            var egg = AddEgg(colony);
            var twin = new Egg { Fertile = true, LaidAtUtc = Epoch };
            for (var day = 0; day < 5; day++)
            {
                EggDevelopment.Apply(twin, 1d, 30d, care);
            }

            service.Advance(colony, AtDay(5), 22d);

            Assert.AreEqual(twin.DevelopmentPercent, egg.DevelopmentPercent, 1e-6);
            Assert.Greater(egg.DevelopmentPercent, 0d);
        }

        [Test]
        public void Eggs_InASimpleIncubator_FeelTheRoom()
        {
            var colony = NewColony(IncubatorModel.Simple, 28);
            var egg = AddEgg(colony);
            var twin = new Egg { Fertile = true, LaidAtUtc = Epoch };
            for (var day = 0; day < 5; day++)
            {
                EggDevelopment.Apply(twin, 1d, 27d, care);
            }

            service.Advance(colony, AtDay(5), 18d);

            Assert.AreEqual(twin.DevelopmentPercent, egg.DevelopmentPercent, 1e-6);
        }

        [Test]
        public void DryIncubator_KillsEggsAndReportsThem()
        {
            var colony = NewColony();
            colony.Incubators[0].HumidityPercent = 0d;
            for (var i = 0; i < 6; i++)
            {
                AddEgg(colony);
            }

            var report = service.Advance(colony, Epoch + TimeSpan.FromHours(12), 22d);

            var dried = colony.Eggs.FindAll(e => e.Failure == EggFailure.Dried);
            Assert.IsNotEmpty(dried);
            Assert.AreEqual(dried.Count, report.EggsDied.Count);
            Assert.IsEmpty(report.HumidityLowIncubatorIds, "it was already dry, so no new warning");
        }

        [Test]
        public void InfertileEggs_NeverDie_AndAreRevealedAtSevenDays()
        {
            var colony = NewColony();
            colony.Incubators[0].HumidityPercent = 0d;
            colony.Incubators[0].AppliedUntilUtc = AtDay(6);
            var egg = AddEgg(colony, fertile: false);

            var report = service.Advance(colony, AtDay(8), 22d);

            Assert.AreEqual(EggFailure.None, egg.Failure);
            Assert.AreEqual(0d, egg.DevelopmentPercent);
            CollectionAssert.Contains(report.InfertileRevealed, egg);

            var later = service.Advance(colony, AtDay(9), 22d);
            Assert.IsEmpty(later.InfertileRevealed);
        }

        [Test]
        public void HumidityLow_IsReportedOnceWhenItCrossesSixty()
        {
            var colony = NewColony();
            colony.Incubators[0].HumidityPercent = 61d;
            AddEgg(colony);

            var first = service.Advance(colony, AtDay(1), 22d);
            var second = service.Advance(colony, AtDay(2), 22d);

            CollectionAssert.AreEqual(new[] { 1 }, first.HumidityLowIncubatorIds);
            Assert.IsEmpty(second.HumidityLowIncubatorIds);
        }

        [Test]
        public void HumidityLow_IsNotReportedForAnEmptyIncubator()
        {
            var colony = NewColony();
            colony.Incubators[0].HumidityPercent = 61d;

            var report = service.Advance(colony, AtDay(1), 22d);

            Assert.IsEmpty(report.HumidityLowIncubatorIds);
        }

        [Test]
        public void Results_AreTheSameInOneStepOrTwo()
        {
            var oneStep = NewColony(IncubatorModel.Standard, 29);
            var twoSteps = NewColony(IncubatorModel.Standard, 29);
            oneStep.Incubators[0].HumidityPercent = 0d;
            twoSteps.Incubators[0].HumidityPercent = 0d;
            for (var i = 0; i < 6; i++)
            {
                AddEgg(oneStep);
                AddEgg(twoSteps);
            }

            service.Advance(oneStep, Epoch + TimeSpan.FromHours(10), 22d);
            service.Advance(twoSteps, Epoch + TimeSpan.FromHours(3), 22d);
            service.Advance(twoSteps, Epoch + TimeSpan.FromHours(10), 22d);

            for (var i = 0; i < 6; i++)
            {
                Assert.AreEqual(oneStep.Eggs[i].Failure, twoSteps.Eggs[i].Failure, $"egg {i}");
                Assert.AreEqual(oneStep.Eggs[i].DevelopmentPercent, twoSteps.Eggs[i].DevelopmentPercent, 1e-6, $"egg {i}");
            }
        }

        [Test]
        public void ReadyEgg_SurvivesALongStepThroughADryIncubator()
        {
            var colony = NewColony();
            var egg = AddEgg(colony);
            egg.DevelopmentPercent = 99.5d;

            service.Advance(colony, AtDay(15), 28d);

            Assert.Less(colony.Incubators[0].HumidityPercent, care.HumidityDryBelowPercent);
            Assert.IsFalse(egg.Failed, egg.Failure.ToString());
            Assert.AreEqual(100d, egg.DevelopmentPercent, 1e-9);
        }

        [Test]
        public void ReadyEgg_DoesNotAccumulateColdDaysInAColdIncubator()
        {
            var colony = NewColony(IncubatorModel.Luxury, 20);
            var egg = AddEgg(colony);
            egg.DevelopmentPercent = 100d;

            service.Advance(colony, AtDay(15), 20d);

            Assert.IsFalse(egg.Failed, egg.Failure.ToString());
            Assert.AreEqual(0d, egg.ColdGameDays, 1e-9);
        }
    }
}
