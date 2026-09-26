using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class ColonySaveServiceTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        private readonly CareTuning care = new CareTuning();
        private readonly EconomyTuning economy = new EconomyTuning();
        private readonly ColonySaveService service = new ColonySaveService(new CareTuning(), new EconomyTuning());
        private string path;

        [SetUp]
        public void SetUp() => path = Path.Combine(Path.GetTempPath(), $"colony-{Guid.NewGuid():N}.json");

        [TearDown]
        public void TearDown()
        {
            File.Delete(path);
            File.Delete(ColonySaveService.BackupPathFor(path));
            File.Delete(ColonySaveService.CorruptBackupPathFor(path, Now));
            File.Delete(path + ".tmp");
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

        private void WriteLegacySave(string growthStage)
        {
            File.WriteAllText(path,
                "{\"schemaVersion\":2,\"lastSavedAtUtc\":\"2026-09-25T11:00:00.0000000+00:00\",\"hunger\":70,\"hydration\":60," +
                "\"cleanliness\":50,\"health\":90,\"growth\":50,\"growthStage\":\"" + growthStage + "\",\"selectedDecorId\":\"plant_01\"," +
                "\"unlockedDecorIds\":[\"rock_01\",\"plant_01\"],\"lastShedAtUtc\":\"2026-09-20T00:00:00.0000000+00:00\"," +
                "\"nextShedAtUtc\":\"2026-11-19T00:00:00.0000000+00:00\"}");
        }

        [Test]
        public void ASchemaTwoSave_IsMigratedIntoCageOneAndBackedUp()
        {
            WriteLegacySave("Juvenile");

            var colony = service.LoadOrCreate(path, Now, new Random(3));

            Assert.That(service.LastLoadMigrated, Is.True);
            Assert.That(File.Exists(ColonySaveService.BackupPathFor(path)), Is.True);
            var pet = colony.AnimalIn(colony.Cages[0]);
            Assert.That(pet.Stage, Is.EqualTo(GrowthStage.Juvenile));
            Assert.That(pet.WeightGrams, Is.EqualTo(15d).Within(1e-9));
            Assert.That((pet.Hunger, pet.Hydration, pet.Cleanliness, pet.Health), Is.EqualTo((70d, 60d, 50d, 90d)));
            Assert.That(pet.HatchedAtUtc, Is.EqualTo(Now.AddDays(-5)));
            Assert.That(pet.NextShedAtUtc - Now, Is.LessThanOrEqualTo(SheddingModel.IntervalFor(GrowthStage.Juvenile, new CareTuning())),
                "the old 60-real-day shed date is pulled into the new game-time cycle");
            Assert.That(colony.Wallet.Money, Is.EqualTo(50000));
            Assert.That(colony.IncubatorCount, Is.EqualTo(1));
            Assert.That(service.LastLoadMovedDecor, Is.True);
            Assert.That(colony.Inventory.Count("rock_01"), Is.EqualTo(1));
            Assert.That(colony.Inventory.Count("plant_01"), Is.EqualTo(1));
            Assert.That(colony.Cages[0].DecorIds, Is.Empty);
        }

        [Test]
        public void ASchemaOneSave_WithNoSchemaVersionOrShedFields_MigratesWithoutThrowingAndGetsANextShed()
        {
            File.WriteAllText(path,
                "{\"lastSavedAtUtc\":\"2026-09-25T10:00:00.0000000+00:00\",\"hunger\":70,\"hydration\":60," +
                "\"cleanliness\":50,\"health\":90,\"growth\":50,\"growthStage\":\"Juvenile\",\"selectedDecorId\":\"plant_01\"," +
                "\"unlockedDecorIds\":[\"rock_01\",\"plant_01\"]}");

            Colony colony = null;
            Assert.DoesNotThrow(() => colony = service.LoadOrCreate(path, Now, new Random(3)));

            Assert.That(service.LastLoadMigrated, Is.True);
            var pet = colony.AnimalIn(colony.Cages[0]);
            Assert.That(pet.NextShedAtUtc - Now, Is.LessThanOrEqualTo(SheddingModel.IntervalFor(GrowthStage.Juvenile, new CareTuning())));
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
            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(".*could not be loaded.*"));

            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(colony.Animals, Has.Count.EqualTo(1));
            Assert.That(File.ReadAllText(path), Is.EqualTo("not json"));
        }

        [Test]
        public void AnUnreadableFile_IsBackedUpAndFlagsLastLoadFailed()
        {
            File.WriteAllText(path, "not json");
            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(".*could not be loaded.*"));

            service.LoadOrCreate(path, Now, new Random(1));

            var backupPath = ColonySaveService.CorruptBackupPathFor(path, Now);
            Assert.That(File.Exists(backupPath), Is.True);
            Assert.That(File.ReadAllText(backupPath), Is.EqualTo("not json"));
            Assert.That(service.LastLoadFailed, Is.True);
        }

        [Test]
        public void ASchemaThreeSaveWithMissingTimestamps_FallsBackToTheSuppliedNow()
        {
            File.WriteAllText(path,
                "{\"schemaVersion\":3,\"calendarEpochUtc\":\"\",\"money\":50000," +
                "\"ledger\":[],\"animals\":[{\"id\":1,\"name\":\"Test\",\"sex\":\"Female\"," +
                "\"weightGrams\":15,\"hatchedAtUtc\":\"\",\"stage\":\"Baby\"," +
                "\"stageUpDueAtUtc\":\"\",\"hunger\":50,\"hydration\":50,\"cleanliness\":50," +
                "\"health\":100,\"selectedDecorId\":\"rock_01\",\"unlockedDecorIds\":[\"rock_01\"]," +
                "\"lastSavedAtUtc\":\"\",\"lastShedAtUtc\":\"\",\"nextShedAtUtc\":\"\"}]," +
                "\"cages\":[{\"id\":1,\"size\":\"Standard\",\"animalId\":1}]," +
                "\"rackCount\":0,\"incubatorCount\":1,\"nextAnimalId\":2,\"nextCageId\":2," +
                "\"lastBilledMonthIndex\":0}");

            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(colony.CalendarEpochUtc, Is.EqualTo(Now));
            var pet = colony.Animals[0];
            Assert.That(pet.HatchedAtUtc, Is.EqualTo(Now));
            Assert.That(pet.LastSavedAtUtc, Is.EqualTo(Now));
            Assert.That(pet.StageUpDueAtUtc, Is.Null);
        }

        [Test]
        public void RoundTrip_KeepsGeneticsPersonalityAndSexReveal()
        {
            var colony = Colony.CreateNew(Now, economy, care, new Random(1));
            var pet = colony.Animals[0];
            pet.Genotype = Genotype.Normal(hypo: 72d, tangerine: 15d).Set(GeneId.MackSnow, 2).Set(GeneId.Eclipse, 1);
            pet.Known = new KnownGenetics().SetHet(GeneId.Eclipse, 2d / 3d);
            pet.Personality = Personality.Glutton;
            pet.PersonalityKnown = false;
            pet.SexRevealed = true;

            service.Save(path, colony);
            var loaded = service.LoadOrCreate(path, Now, new Random(2)).Animals[0];

            Assert.That(loaded.Genotype.Copies(GeneId.MackSnow), Is.EqualTo(2));
            Assert.That(loaded.Genotype.Copies(GeneId.Eclipse), Is.EqualTo(1));
            Assert.That(loaded.Genotype.Hypo, Is.EqualTo(72d));
            Assert.That(loaded.Known.HetProbability(GeneId.Eclipse), Is.EqualTo(2d / 3d).Within(1e-9));
            Assert.That(loaded.Known.HetsUnknown, Is.False);
            Assert.That(loaded.Personality, Is.EqualTo(Personality.Glutton));
            Assert.That(loaded.PersonalityKnown, Is.False);
            Assert.That(loaded.SexRevealed, Is.True);
        }

        [Test]
        public void Load_Phase1SchemaThreeFile_GetsStarterGeneticsOnceAndKeepsThem()
        {
            // A schema-3 file written by phase 1: no genome fields at all.
            File.WriteAllText(path, "{\"schemaVersion\":3,\"calendarEpochUtc\":\"2026-09-20T00:00:00.0000000+00:00\",\"money\":50000," +
                "\"animals\":[{\"id\":1,\"name\":\"レオパ1\",\"sex\":\"Male\",\"weightGrams\":45.0,\"stage\":\"Adult\"," +
                "\"hatchedAtUtc\":\"2026-09-08T00:00:00.0000000+00:00\",\"hunger\":80,\"hydration\":80,\"cleanliness\":80,\"health\":100}]," +
                "\"cages\":[{\"id\":1,\"size\":\"Standard\",\"animalId\":1}],\"rackCount\":1,\"incubatorCount\":1,\"nextAnimalId\":2,\"nextCageId\":2}");

            var first = service.LoadOrCreate(path, Now, new Random(3)).Animals[0];

            Assert.That(first.Known.HetsUnknown, Is.True);
            Assert.That(MorphNamer.VisualName(first.Genotype), Is.EqualTo("ノーマル"));
            Assert.That(first.SexRevealed, Is.True, "phase-1 players already saw the sex of a non-Baby animal");

            var colony = service.LoadOrCreate(path, Now, new Random(3));
            service.Save(path, colony);
            var again = service.LoadOrCreate(path, Now, new Random(999)).Animals[0];

            Assert.That(again.Personality, Is.EqualTo(colony.Animals[0].Personality));
            Assert.That(again.Genotype.Hypo, Is.EqualTo(colony.Animals[0].Genotype.Hypo));
        }

        [Test]
        public void Load_Phase1SchemaThreeFile_BabyStaysWithUnknownSex()
        {
            // Same phase-1 schema-3 shape, but the animal is still a Baby: sex was never
            // shown in phase 1 for a Baby, so migration must not reveal it either.
            File.WriteAllText(path, "{\"schemaVersion\":3,\"calendarEpochUtc\":\"2026-09-20T00:00:00.0000000+00:00\",\"money\":50000," +
                "\"animals\":[{\"id\":1,\"name\":\"レオパ1\",\"sex\":\"Male\",\"weightGrams\":3.0,\"stage\":\"Baby\"," +
                "\"hatchedAtUtc\":\"2026-09-08T00:00:00.0000000+00:00\",\"hunger\":80,\"hydration\":80,\"cleanliness\":80,\"health\":100}]," +
                "\"cages\":[{\"id\":1,\"size\":\"Standard\",\"animalId\":1}],\"rackCount\":1,\"incubatorCount\":1,\"nextAnimalId\":2,\"nextCageId\":2}");

            var first = service.LoadOrCreate(path, Now, new Random(3)).Animals[0];

            Assert.That(first.SexRevealed, Is.False, "a migrated Baby has not shed yet, so its sex stays unknown");
        }

        [Test]
        public void Migrate_LegacyPet_IsNormalHetsUnknownWithUnknownSex()
        {
            // Reuse the existing schema-2 fixture helper of this test class for an adult pet.
            WriteLegacySave(growthStage: "Adult");

            var pet = service.LoadOrCreate(path, Now, new Random(4)).Animals[0];

            Assert.That(MorphNamer.FullName(pet.Genotype, pet.Known), Is.EqualTo("ノーマル（ヘテロ不明）"));
            Assert.That(pet.SexRevealed, Is.False);
            Assert.That(pet.PersonalityKnown, Is.True);
        }

        [Test]
        public void CreateNew_StartingAnimalHasStarterGenetics()
        {
            var pet = Colony.CreateNew(Now, economy, care, new Random(5)).Animals[0];

            Assert.That(pet.Known.HetsUnknown, Is.True);
            Assert.That(pet.Genotype.Hypo, Is.InRange(10d, 40d));
            Assert.That(pet.Genotype.Tangerine, Is.InRange(10d, 40d));
        }

        [Test]
        public void RoundTrip_KeepsShopInventoryDecorIncubatorsAndRevealTime()
        {
            var colony = service.LoadOrCreate(path, Now, new Random(1));
            colony.Shop.Seed = 42;
            colony.Shop.EnsureStocked(3, Now, care);
            colony.Inventory.Add("plant_01", 2);
            colony.Inventory.Add(ShopCatalog.NestBoxId, 1);
            var cage = colony.AddCage(CageSize.Large);
            cage.DecorIds.Add("driftwood_01");
            colony.Incubators.Add(IncubatorModel.Luxury);
            var pet = colony.Animals[0];
            pet.PersonalityKnown = false;
            pet.PersonalityRevealAtUtc = Now.AddHours(3);

            service.Save(path, colony);
            var loaded = service.LoadOrCreate(path, Now, new Random(2));

            Assert.That(service.LastLoadMovedDecor, Is.False);
            Assert.That(loaded.Shop.Seed, Is.EqualTo(42));
            Assert.That(loaded.Shop.StockMonthIndex, Is.EqualTo(3));
            Assert.That(loaded.Shop.Offers.Select(o => o.OfferId), Is.EqualTo(colony.Shop.Offers.Select(o => o.OfferId)));
            Assert.That(loaded.Shop.Offers.Select(o => MorphNamer.FullName(o.Animal.Genotype, o.Animal.Known)),
                Is.EqualTo(colony.Shop.Offers.Select(o => MorphNamer.FullName(o.Animal.Genotype, o.Animal.Known))));
            Assert.That(loaded.Shop.Offers.All(o => !o.Animal.PersonalityKnown && o.Animal.PersonalityRevealAtUtc == null), Is.True);
            Assert.That(loaded.Inventory.Count("plant_01"), Is.EqualTo(2));
            Assert.That(loaded.Inventory.Count(ShopCatalog.NestBoxId), Is.EqualTo(1));
            Assert.That(loaded.Cages[0].DecorIds, Is.EqualTo(new[] { "rock_01" }));
            Assert.That(loaded.Cages[1].DecorIds, Is.EqualTo(new[] { "driftwood_01" }));
            Assert.That(loaded.Incubators, Is.EqualTo(new[] { IncubatorModel.Simple, IncubatorModel.Luxury }));
            Assert.That(loaded.Animals[0].PersonalityRevealAtUtc, Is.EqualTo(Now.AddHours(3)));
        }

        [Test]
        public void AColonyWithNoAnimals_SavesAndLoads()
        {
            var colony = service.LoadOrCreate(path, Now, new Random(1));
            colony.RemoveAnimal(colony.Animals[0]);

            service.Save(path, colony);
            var loaded = service.LoadOrCreate(path, Now, new Random(2));

            Assert.That(service.LastLoadFailed, Is.False);
            Assert.That(loaded.Animals, Is.Empty);
            Assert.That(loaded.Cages, Has.Count.EqualTo(1));
            Assert.That(loaded.Cages[0].IsEmpty, Is.True);
            Assert.That(loaded.NextAnimalId, Is.EqualTo(2));
        }

        [Test]
        public void ANeverStockedShop_StaysNeverStockedAfterAReload()
        {
            var colony = service.LoadOrCreate(path, Now, new Random(1));
            service.Save(path, colony);

            Assert.That(service.LoadOrCreate(path, Now, new Random(1)).Shop.StockMonthIndex, Is.EqualTo(ShopStock.NeverStocked));
        }

        [Test]
        public void Load_Phase2File_MovesEachAnimalsUnlockedDecorIntoTheInventoryOnce()
        {
            File.WriteAllText(path, "{\"schemaVersion\":3,\"calendarEpochUtc\":\"2026-09-20T00:00:00.0000000+00:00\",\"money\":50000," +
                "\"animals\":[" +
                "{\"id\":1,\"name\":\"レオパ1\",\"sex\":\"Male\",\"weightGrams\":45.0,\"stage\":\"Adult\",\"hatchedAtUtc\":\"2026-09-08T00:00:00.0000000+00:00\"," +
                "\"hunger\":80,\"hydration\":80,\"cleanliness\":80,\"health\":100,\"selectedDecorId\":\"plant_01\"," +
                "\"unlockedDecorIds\":[\"rock_01\",\"plant_01\",\"water_dish_01\"],\"genomeVersion\":1,\"personality\":\"Calm\",\"personalityKnown\":true}," +
                "{\"id\":2,\"name\":\"レオパ2\",\"sex\":\"Female\",\"weightGrams\":5.0,\"stage\":\"Baby\",\"hatchedAtUtc\":\"2026-09-24T00:00:00.0000000+00:00\"," +
                "\"hunger\":80,\"hydration\":80,\"cleanliness\":80,\"health\":100,\"selectedDecorId\":\"rock_01\"," +
                "\"unlockedDecorIds\":[\"rock_01\"],\"genomeVersion\":1,\"personality\":\"Shy\",\"personalityKnown\":true}]," +
                "\"cages\":[{\"id\":1,\"size\":\"Standard\",\"animalId\":1},{\"id\":2,\"size\":\"Standard\",\"animalId\":2}]," +
                "\"rackCount\":1,\"incubatorCount\":1,\"nextAnimalId\":3,\"nextCageId\":3}");

            var colony = service.LoadOrCreate(path, Now, new Random(3));

            Assert.That(service.LastLoadMovedDecor, Is.True);
            Assert.That(colony.Inventory.Count("rock_01"), Is.EqualTo(2));
            Assert.That(colony.Inventory.Count("plant_01"), Is.EqualTo(1));
            Assert.That(colony.Inventory.Count("water_dish_01"), Is.EqualTo(1));
            Assert.That(colony.Cages.TrueForAll(c => c.DecorIds.Count == 0), Is.True);
            Assert.That(colony.Incubators, Is.EqualTo(new[] { IncubatorModel.Simple }));
            Assert.That(colony.Shop.StockMonthIndex, Is.EqualTo(ShopStock.NeverStocked));

            service.Save(path, colony);
            var again = service.LoadOrCreate(path, Now, new Random(3));

            Assert.That(service.LastLoadMovedDecor, Is.False);
            Assert.That(again.Inventory.Count("rock_01"), Is.EqualTo(2));
        }

        [Test]
        public void AFileWithoutASchemaThatIsNotAnOldPetSave_IsTreatedAsUnreadable()
        {
            File.WriteAllText(path, "{\"foo\":1}");
            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(".*could not be loaded.*"));

            service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(service.LastLoadFailed, Is.True);
            Assert.That(service.LastLoadMigrated, Is.False);
            Assert.That(File.Exists(ColonySaveService.BackupPathFor(path)), Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo("{\"foo\":1}"));
        }

        [Test]
        public void ACorruptBackupThatCannotBeWritten_IsReported()
        {
            File.WriteAllText(path, "not json");
            var backupPath = ColonySaveService.CorruptBackupPathFor(path, Now);
            Directory.CreateDirectory(backupPath);
            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(".*Could not back up.*"));
            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex(".*could not be loaded.*"));
            try
            {
                service.LoadOrCreate(path, Now, new Random(1));

                Assert.That(service.LastLoadBackupFailed, Is.True);
            }
            finally
            {
                Directory.Delete(backupPath);
            }
        }
    }
}
