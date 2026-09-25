using System;
using System.IO;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.Gameplay;

namespace TerrariumDays.Tests
{
    public sealed class ColonySessionTests
    {
        private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        private readonly CareTuning care = new CareTuning();
        private readonly EconomyTuning economy = new EconomyTuning();
        private DateTimeOffset realNow;
        private string path;

        [SetUp]
        public void SetUp()
        {
            realNow = Start;
            path = Path.Combine(Path.GetTempPath(), $"session-{Guid.NewGuid():N}.json");
        }

        [TearDown]
        public void TearDown() => File.Delete(path);

        private ColonySession NewSession() =>
            new ColonySession(path, new TimeService(() => realNow), care, economy, new Random(4));

        [Test]
        public void Load_AppliesTheTimeAwayToEveryAnimal()
        {
            var first = NewSession();
            first.Load();
            first.Colony.AddAnimal(new PetState { LastSavedAtUtc = realNow, NextShedAtUtc = realNow.AddDays(3) }, first.Colony.AddCage(CageSize.Standard));
            first.Colony.Animals[0].NextShedAtUtc = realNow.AddDays(3);
            first.Save();

            realNow += TimeSpan.FromHours(2);
            var second = NewSession();
            second.Load();

            foreach (var pet in second.Colony.Animals)
            {
                Assert.That(pet.Hunger, Is.EqualTo(80d - care.HungerDecayPerHour * 2d).Within(1e-6));
            }
        }

        [Test]
        public void ANewGameMonth_BillsElectricityOncePerMonth()
        {
            var session = NewSession();
            session.Load();

            realNow += TimeSpan.FromHours(12);
            var sameMonth = session.Resume();
            realNow += TimeSpan.FromHours(13);
            var nextMonth = session.Resume();

            Assert.That(sameMonth.ElectricityCharged, Is.EqualTo(0));
            Assert.That(nextMonth.ElectricityCharged, Is.EqualTo(MaintenanceCosts.MonthlyElectricity(1, 1, economy)));
            Assert.That(session.Colony.Wallet.Money, Is.EqualTo(50000 - 800));
            Assert.That(session.Colony.LastBilledMonthIndex, Is.EqualTo(1));
        }

        [Test]
        public void ACareActionAfterTheDebugClockRanAhead_IsNotUndoneByTheNextTick()
        {
            var session = NewSession();
            session.Load();
            var pet = session.Colony.Animals[0];
            pet.NextShedAtUtc = realNow.AddDays(3);
            var clock = new TimeService(() => realNow) { TimeMultiplier = 600d };
            session.UseClock(clock);

            session.Advance(TimeSpan.FromSeconds(6)); // one game-clock hour ahead
            new CareService(care).Clean(pet);
            session.Save();
            session.Advance(TimeSpan.FromSeconds(0.1)); // one minute

            Assert.That(pet.Cleanliness, Is.EqualTo(100d - care.CleanlinessDecayPerHour / 60d).Within(1e-6));
        }

        [Test]
        public void Resume_AppliesTheBackgroundTimeOnce()
        {
            var session = NewSession();
            session.Load();
            session.Colony.Animals[0].NextShedAtUtc = realNow.AddDays(3);
            session.Save();

            realNow += TimeSpan.FromHours(2);
            session.Resume();

            Assert.That(session.Colony.Animals[0].Hunger, Is.EqualTo(80d - care.HungerDecayPerHour * 2d).Within(1e-6));
        }

        [Test]
        public void StageUpsAreReportedWithThePet()
        {
            var session = NewSession();
            session.Load();
            var pet = session.Colony.Animals[0];
            pet.WeightGrams = 15.5d;
            pet.NextShedAtUtc = realNow.AddDays(3);

            var report = session.SimulateGameTime(GameCalendar.RealTimeFor(care.PreGrowthFastGameDays) + TimeSpan.FromMinutes(2));

            Assert.That(report.StageUps, Has.Count.EqualTo(1));
            Assert.That(report.StageUps[0].Pet, Is.SameAs(pet));
            Assert.That(report.StageUps[0].Stage, Is.EqualTo(GrowthStage.Juvenile));
        }
    }
}
