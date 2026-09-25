using System;
using System.IO;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class ColonySaveServiceTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        private readonly ColonySaveService service = new ColonySaveService(new CareTuning(), new EconomyTuning());
        private string path;

        [SetUp]
        public void SetUp() => path = Path.Combine(Path.GetTempPath(), $"colony-{Guid.NewGuid():N}.json");

        [TearDown]
        public void TearDown()
        {
            File.Delete(path);
            File.Delete(ColonySaveService.BackupPathFor(path));
        }

        [Test]
        public void WithoutAFile_CreatesAndSavesANewColony()
        {
            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(colony.Animals, Has.Count.EqualTo(1));
            Assert.That(File.Exists(path), Is.True);
            Assert.That(service.LastLoadMigrated, Is.False);
        }

        [Test]
        public void RoundTripsEverything()
        {
            var colony = service.LoadOrCreate(path, Now, new Random(1));
            var pet = colony.Animals[0];
            pet.Name = "ハナ";
            pet.Sex = Sex.Male;
            pet.WeightGrams = 22.5d;
            pet.Stage = GrowthStage.Juvenile;
            pet.StageUpDueAtUtc = Now.AddMinutes(90);
            pet.Hunger = 41d;
            colony.AddCage(CageSize.Large);
            colony.RackCount = 2;
            colony.LastBilledMonthIndex = 3;
            colony.Wallet.Charge(300, LedgerCategory.Electricity, "電気代", Now);

            service.Save(path, colony);
            var loaded = service.LoadOrCreate(path, Now, new Random(2));

            var back = loaded.Animals[0];
            Assert.That((back.Name, back.Sex, back.WeightGrams, back.Stage), Is.EqualTo(("ハナ", Sex.Male, 22.5d, GrowthStage.Juvenile)));
            Assert.That(back.StageUpDueAtUtc, Is.EqualTo(Now.AddMinutes(90)));
            Assert.That(back.Hunger, Is.EqualTo(41d));
            Assert.That(back.HatchedAtUtc, Is.EqualTo(pet.HatchedAtUtc));
            Assert.That(loaded.Cages, Has.Count.EqualTo(2));
            Assert.That(loaded.Cages[1].Size, Is.EqualTo(CageSize.Large));
            Assert.That(loaded.Cages[0].AnimalId, Is.EqualTo(back.Id));
            Assert.That((loaded.RackCount, loaded.LastBilledMonthIndex, loaded.NextAnimalId, loaded.NextCageId), Is.EqualTo((2, 3, 2, 3)));
            Assert.That(loaded.Wallet.Money, Is.EqualTo(49700));
            Assert.That(loaded.Wallet.Ledger[0].Category, Is.EqualTo(LedgerCategory.Electricity));
            Assert.That(loaded.CalendarEpochUtc, Is.EqualTo(Now));
        }

        [Test]
        public void ASchemaTwoSave_IsMigratedIntoCageOneAndBackedUp()
        {
            File.WriteAllText(path,
                "{\"schemaVersion\":2,\"lastSavedAtUtc\":\"2026-09-25T11:00:00.0000000+00:00\",\"hunger\":70,\"hydration\":60," +
                "\"cleanliness\":50,\"health\":90,\"growth\":50,\"growthStage\":\"Juvenile\",\"selectedDecorId\":\"plant_01\"," +
                "\"unlockedDecorIds\":[\"rock_01\",\"plant_01\"],\"lastShedAtUtc\":\"2026-09-20T00:00:00.0000000+00:00\"," +
                "\"nextShedAtUtc\":\"2026-11-19T00:00:00.0000000+00:00\"}");

            var colony = service.LoadOrCreate(path, Now, new Random(3));

            Assert.That(service.LastLoadMigrated, Is.True);
            Assert.That(File.Exists(ColonySaveService.BackupPathFor(path)), Is.True);
            var pet = colony.AnimalIn(colony.Cages[0]);
            Assert.That(pet.Stage, Is.EqualTo(GrowthStage.Juvenile));
            Assert.That(pet.WeightGrams, Is.EqualTo(15d).Within(1e-9));
            Assert.That((pet.Hunger, pet.Hydration, pet.Cleanliness, pet.Health), Is.EqualTo((70d, 60d, 50d, 90d)));
            Assert.That(pet.SelectedDecorId, Is.EqualTo("plant_01"));
            Assert.That(pet.UnlockedDecorIds, Is.EquivalentTo(new[] { "rock_01", "plant_01" }));
            Assert.That(pet.HatchedAtUtc, Is.EqualTo(Now.AddDays(-5)));
            Assert.That(pet.NextShedAtUtc - Now, Is.LessThanOrEqualTo(SheddingModel.IntervalFor(GrowthStage.Juvenile, new CareTuning())),
                "the old 60-real-day shed date is pulled into the new game-time cycle");
            Assert.That(colony.Wallet.Money, Is.EqualTo(50000));
            Assert.That(colony.IncubatorCount, Is.EqualTo(1));
        }

        [TestCase(0d, 3d)]
        [TestCase(25d, 9d)]
        [TestCase(75d, 30d)]
        [TestCase(100d, 45d)]
        public void MigratedWeight_FollowsTheOldGrowthGauge(double growth, double grams)
        {
            Assert.That(ColonySaveService.WeightFromLegacyGrowth(growth), Is.EqualTo(grams).Within(1e-9));
        }

        [Test]
        public void AnUnreadableFile_StartsFreshWithoutOverwritingIt()
        {
            File.WriteAllText(path, "not json");

            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(colony.Animals, Has.Count.EqualTo(1));
            Assert.That(File.ReadAllText(path), Is.EqualTo("not json"));
        }
    }
}
