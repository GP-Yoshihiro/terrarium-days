using System;
using System.IO;
using System.Linq;
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
        public void TearDown()
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
            File.Delete(ColonySaveService.BackupPathFor(path));
            foreach (var backup in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(path) + ".corrupt-*.bak"))
            {
                File.Delete(backup);
            }
        }

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
        public void Load_WithAnUnreadableFile_BacksItUpAndLeavesNoTmpFile()
        {
            File.WriteAllText(path, "not json");
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(".*could not be loaded.*"));

            var session = NewSession();
            session.Load();

            var backupPath = ColonySaveService.CorruptBackupPathFor(path, Start);
            Assert.That(File.Exists(backupPath), Is.True);
            Assert.That(File.ReadAllText(backupPath), Is.EqualTo("not json"));
            Assert.That(File.Exists(path + ".tmp"), Is.False);
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
        public void SeveralGameMonthsAway_BillsEachMonthOnce()
        {
            var session = NewSession();
            session.Load();

            realNow += TimeSpan.FromDays(3) + TimeSpan.FromHours(1);
            var report = session.Resume();

            Assert.That(report.ElectricityCharged, Is.EqualTo(3 * MaintenanceCosts.MonthlyElectricity(1, 1, economy)));
            Assert.That(session.Colony.LastBilledMonthIndex, Is.EqualTo(3));
            Assert.That(session.Colony.Wallet.Ledger.Count(entry => entry.Category == LedgerCategory.Electricity), Is.EqualTo(3));
            Assert.That(session.Colony.Wallet.Money, Is.EqualTo(50000 - 2400));

            var again = session.Resume();

            Assert.That(again.ElectricityCharged, Is.EqualTo(0));
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
        public void Load_WithASchemaTwoSave_MigratesAndAppliesTheOfflineGapFromTheOldLastSaved()
        {
            var lastSaved = Start.AddHours(-2);
            File.WriteAllText(path,
                "{\"schemaVersion\":2,\"lastSavedAtUtc\":\"" + lastSaved.ToString("o", System.Globalization.CultureInfo.InvariantCulture) + "\"," +
                "\"hunger\":80,\"hydration\":60,\"cleanliness\":50,\"health\":90,\"growth\":50,\"growthStage\":\"Juvenile\"," +
                "\"selectedDecorId\":\"plant_01\",\"unlockedDecorIds\":[\"rock_01\",\"plant_01\"]," +
                "\"lastShedAtUtc\":\"2026-09-20T00:00:00.0000000+00:00\",\"nextShedAtUtc\":\"2026-11-19T00:00:00.0000000+00:00\"}");

            var session = NewSession();
            session.Load();

            Assert.That(session.Migrated, Is.True);
            Assert.That(File.Exists(ColonySaveService.BackupPathFor(path)), Is.True);
            Assert.That(session.Colony.Cages, Has.Count.EqualTo(1));
            var pet = session.Colony.AnimalIn(session.Colony.Cages[0]);
            Assert.That(pet, Is.Not.Null);
            Assert.That(pet.Hunger, Is.EqualTo(80d - care.HungerDecayPerHour * 2d).Within(1e-6),
                "the 2-hour gap since the old lastSavedAtUtc must be applied as offline progress");
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
