using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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

        [TestCase(0)]
        [TestCase(1)]
        public void Load_ATooLowNextAnimalId_IsClampedPastEveryUsedId(int savedNextAnimalId)
        {
            File.WriteAllText(path, "{\"schemaVersion\":3,\"calendarEpochUtc\":\"2026-09-20T00:00:00.0000000+00:00\",\"money\":50000," +
                "\"animals\":[{\"id\":1,\"name\":\"レオパ1\",\"sex\":\"Male\",\"weightGrams\":45.0,\"stage\":\"Adult\"," +
                "\"hatchedAtUtc\":\"2026-09-08T00:00:00.0000000+00:00\",\"hunger\":80,\"hydration\":80,\"cleanliness\":80,\"health\":100}]," +
                "\"lineage\":[{\"id\":5,\"name\":\"売却済み\",\"sex\":\"Female\",\"sexKnown\":true}]," +
                "\"cages\":[{\"id\":1,\"size\":\"Standard\",\"animalId\":1}],\"rackCount\":1,\"incubatorCount\":1,\"nextAnimalId\":" + savedNextAnimalId + ",\"nextCageId\":2}");

            var colony = service.LoadOrCreate(path, Now, new Random(1));
            var added = colony.AddAnimal(new PetState { Name = "新入り" }, colony.AddCage(CageSize.Standard));

            Assert.That(service.LastLoadFailed, Is.False);
            Assert.That(added.Id, Is.EqualTo(6), "must not reuse the live animal's id 1 or the sold animal's id 5");
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
            colony.AddIncubator(IncubatorModel.Luxury);
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
            Assert.That(loaded.Incubators.Select(i => i.Model), Is.EqualTo(new[] { IncubatorModel.Simple, IncubatorModel.Luxury }));
            Assert.That(loaded.Animals[0].PersonalityRevealAtUtc, Is.EqualTo(Now.AddHours(3)));
        }

        [Test]
        public void RoundTrip_KeepsEachIncubatorsStateAndStillWritesTheLegacyFields()
        {
            var colony = service.LoadOrCreate(path, Now, new Random(1));
            var second = colony.AddIncubator(IncubatorModel.Standard);
            second.SetTemperatureC = 31;
            second.HumidityPercent = 55.5;
            second.AppliedUntilUtc = Now.AddDays(2);

            service.Save(path, colony);
            var loaded = service.LoadOrCreate(path, Now, new Random(2));

            Assert.That(loaded.Incubators, Has.Count.EqualTo(2));
            Assert.That(loaded.Incubators[0].Id, Is.EqualTo(1));
            Assert.That(loaded.Incubators[0].Model, Is.EqualTo(IncubatorModel.Simple));
            Assert.That(loaded.Incubators[0].SetTemperatureC, Is.EqualTo(28));
            Assert.That(loaded.Incubators[0].HumidityPercent, Is.EqualTo(80d));
            Assert.That(loaded.Incubators[0].AppliedUntilUtc, Is.EqualTo(default(DateTimeOffset)));
            Assert.That(loaded.Incubators[1].Id, Is.EqualTo(2));
            Assert.That(loaded.Incubators[1].Model, Is.EqualTo(IncubatorModel.Standard));
            Assert.That(loaded.Incubators[1].SetTemperatureC, Is.EqualTo(31));
            Assert.That(loaded.Incubators[1].HumidityPercent, Is.EqualTo(55.5));
            Assert.That(loaded.Incubators[1].AppliedUntilUtc, Is.EqualTo(Now.AddDays(2)));

            var json = File.ReadAllText(path);
            Assert.That(json, Does.Contain("\"incubators\":[\"Simple\",\"Standard\"]"));
            Assert.That(json, Does.Contain("\"incubatorCount\":2"));
        }

        [Test]
        public void Load_LegacyIncubatorNames_BecomeDefaultStatesWithSequentialIds()
        {
            File.WriteAllText(path, LegacyIncubatorFile("\"incubators\":[\"Simple\",\"Luxury\"]"));

            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(colony.Incubators.Select(i => i.Id), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(colony.Incubators.Select(i => i.Model), Is.EqualTo(new[] { IncubatorModel.Simple, IncubatorModel.Luxury }));
            Assert.That(colony.Incubators.All(i => i.SetTemperatureC == 28 && i.HumidityPercent == 80d
                && i.AppliedUntilUtc == default(DateTimeOffset)), Is.True);
        }

        [Test]
        public void Load_OnlyAnIncubatorCount_BecomesThatManySimpleIncubators()
        {
            File.WriteAllText(path, LegacyIncubatorFile("\"incubatorCount\":2"));

            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(colony.Incubators.Select(i => i.Id), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(colony.Incubators.Select(i => i.Model), Is.EqualTo(new[] { IncubatorModel.Simple, IncubatorModel.Simple }));
        }

        private static string LegacyIncubatorFile(string incubatorFields) =>
            "{\"schemaVersion\":3,\"calendarEpochUtc\":\"2026-09-20T00:00:00.0000000+00:00\",\"money\":50000," +
            "\"animals\":[{\"id\":1,\"name\":\"レオパ1\",\"sex\":\"Male\",\"weightGrams\":45.0,\"stage\":\"Adult\"," +
            "\"hatchedAtUtc\":\"2026-09-08T00:00:00.0000000+00:00\",\"hunger\":80,\"hydration\":80,\"cleanliness\":80,\"health\":100," +
            "\"genomeVersion\":1,\"personality\":\"Calm\",\"personalityKnown\":true}]," +
            "\"cages\":[{\"id\":1,\"size\":\"Standard\",\"animalId\":1}],\"rackCount\":1," + incubatorFields +
            ",\"nextAnimalId\":2,\"nextCageId\":2}";

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
            Assert.That(colony.Incubators.Select(i => i.Model), Is.EqualTo(new[] { IncubatorModel.Simple }));
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

        [Test]
        public void RoundTrip_KeepsPairingsGravidEggsNestBoxAndWeakness()
        {
            var colony = service.LoadOrCreate(path, Now, new Random(1));
            var male = colony.Animals[0];
            male.Sex = Sex.Male;
            var maleCage = colony.Cages[0];
            var femaleCage = colony.AddCage(CageSize.Standard);
            var female = new PetState { Sex = Sex.Female, Weak = true };
            colony.AddAnimal(female, femaleCage);
            maleCage.HasNestBox = true;

            colony.Pairings.Add(new Pairing
            {
                Id = 1,
                MaleId = male.Id,
                FemaleId = female.Id,
                StartedAtUtc = Now,
                EndsAtUtc = Now.AddHours(6),
                SuccessChance = 0.5d,
            });
            maleCage.VisitorAnimalId = female.Id;
            colony.NextPairingId = 2;

            var gravidFemale = new PetState { Sex = Sex.Female };
            colony.AddAnimal(gravidFemale, colony.AddCage(CageSize.Standard));
            gravidFemale.Gravid = new GravidState
            {
                PairingId = 1,
                FatherId = male.Id,
                FatherName = male.Name,
                FatherGenotype = Genotype.Normal(hypo: 40d, tangerine: 0d).Set(GeneId.MackSnow, 1),
                FatherKnown = new KnownGenetics().SetHet(GeneId.Eclipse, 0.5d),
                Compatibility = Compatibility.Good,
                SeasonYear = 2026,
                ClutchesPlanned = 3,
                ClutchesLaid = 1,
                NextClutchAtUtc = Now.AddDays(10),
            };

            colony.Eggs.Add(new Egg
            {
                Id = 1,
                MotherId = gravidFemale.Id,
                FatherId = male.Id,
                CageId = femaleCage.Id,
                LaidAtUtc = Now,
                Place = EggPlace.NestBox,
                Fertile = true,
                ChildGenotype = Genotype.Normal(hypo: 20d, tangerine: 10d).Set(GeneId.Eclipse, 2),
                ChildKnown = new KnownGenetics { HetsUnknown = true },
                DevelopmentPercent = 12.5d,
                MiddleThirdTemperatureSum = 300d,
                MiddleThirdGameDays = 5d,
                ColdGameDays = 1d,
                Failure = EggFailure.None,
                AppliedUntilUtc = Now,
            });
            colony.NextEggId = 2;

            service.Save(path, colony);
            var loaded = service.LoadOrCreate(path, Now, new Random(2));

            Assert.That(loaded.Pairings, Has.Count.EqualTo(1));
            var loadedPairing = loaded.Pairings[0];
            Assert.That((loadedPairing.MaleId, loadedPairing.FemaleId, loadedPairing.SuccessChance), Is.EqualTo((male.Id, female.Id, 0.5d)));
            Assert.That(loadedPairing.EndsAtUtc, Is.EqualTo(Now.AddHours(6)));
            Assert.That(loaded.CageOf(loaded.AnimalById(male.Id)).VisitorAnimalId, Is.EqualTo(female.Id));
            Assert.That(loaded.CageOf(loaded.AnimalById(male.Id)).HasNestBox, Is.True);
            Assert.That(loaded.AnimalById(female.Id).Weak, Is.True);

            var loadedGravid = loaded.AnimalById(gravidFemale.Id).Gravid;
            Assert.That(loadedGravid, Is.Not.Null);
            Assert.That((loadedGravid.FatherId, loadedGravid.FatherName, loadedGravid.Compatibility), Is.EqualTo((male.Id, male.Name, Compatibility.Good)));
            Assert.That(loadedGravid.FatherGenotype.Copies(GeneId.MackSnow), Is.EqualTo(1));
            Assert.That(loadedGravid.FatherKnown.HetProbability(GeneId.Eclipse), Is.EqualTo(0.5d).Within(1e-9));
            Assert.That((loadedGravid.SeasonYear, loadedGravid.ClutchesPlanned, loadedGravid.ClutchesLaid), Is.EqualTo((2026, 3, 1)));

            Assert.That(loaded.Eggs, Has.Count.EqualTo(1));
            var loadedEgg = loaded.Eggs[0];
            Assert.That((loadedEgg.MotherId, loadedEgg.FatherId, loadedEgg.Fertile, loadedEgg.Place), Is.EqualTo((gravidFemale.Id, male.Id, true, EggPlace.NestBox)));
            Assert.That(loadedEgg.ChildGenotype.Copies(GeneId.Eclipse), Is.EqualTo(2));
            Assert.That(loadedEgg.ChildKnown.HetsUnknown, Is.True);
            Assert.That(loadedEgg.DevelopmentPercent, Is.EqualTo(12.5d));

            Assert.That(loaded.NextPairingId, Is.EqualTo(2));
            Assert.That(loaded.NextEggId, Is.EqualTo(2));
        }

        [Test]
        public void APhaseThreeSave_LoadsWithNoBreeding()
        {
            WriteSchemaThreeWithAnimal("{" + AnimalBase + "}");

            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(service.LastLoadFailed, Is.False);
            Assert.That(colony.Pairings, Is.Empty);
            Assert.That(colony.Eggs, Is.Empty);
            Assert.That(colony.Animals.TrueForAll(a => a.Gravid == null), Is.True);
            Assert.That((colony.NextPairingId, colony.NextEggId), Is.EqualTo((1, 1)));
        }

        [Test]
        public void APairingWithAMissingAnimal_IsDropped()
        {
            var colony = service.LoadOrCreate(path, Now, new Random(1));
            var male = colony.Animals[0];
            male.Sex = Sex.Male;

            colony.Pairings.Add(new Pairing
            {
                Id = 1,
                MaleId = male.Id,
                FemaleId = 999,
                StartedAtUtc = Now,
                EndsAtUtc = Now.AddHours(6),
                SuccessChance = 0.5d,
            });

            service.Save(path, colony);
            var loaded = service.LoadOrCreate(path, Now, new Random(2));

            Assert.That(loaded.Pairings, Is.Empty);
        }

        [Test]
        public void RoundTrip_KeepsTheGameClockOffset()
        {
            var colony = service.LoadOrCreate(path, Now, new Random(1));
            colony.GameClockOffset = TimeSpan.FromHours(30);

            service.Save(path, colony);

            Assert.That(service.LoadOrCreate(path, Now, new Random(1)).GameClockOffset, Is.EqualTo(TimeSpan.FromHours(30)));
        }

        [Test]
        public void RoundTrip_KeepsNewRackIndices()
        {
            var colony = service.LoadOrCreate(path, Now, new Random(1));
            colony.RackCount = 3;
            colony.NewRackIndices.Add(1);
            colony.NewRackIndices.Add(2);

            service.Save(path, colony);

            Assert.That(service.LoadOrCreate(path, Now, new Random(1)).NewRackIndices, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void ASaveWithoutNewRackIndices_LoadsWithAnEmptyList()
        {
            WriteSchemaThreeWithAnimal("{" + AnimalBase + "}");

            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(colony.NewRackIndices, Is.Empty);
        }

        private const string AnimalBase = "\"id\":1,\"name\":\"レオパ1\",\"sex\":\"Female\",\"weightGrams\":45.0,\"stage\":\"Adult\"," +
            "\"hatchedAtUtc\":\"2026-09-08T00:00:00.0000000+00:00\",\"hunger\":80,\"hydration\":80,\"cleanliness\":80,\"health\":100";

        private void WriteSchemaThreeWithAnimal(string animalJson)
        {
            File.WriteAllText(path, "{\"schemaVersion\":3,\"calendarEpochUtc\":\"2026-09-20T00:00:00.0000000+00:00\",\"money\":50000," +
                "\"animals\":[" + animalJson + "]," +
                "\"cages\":[{\"id\":1,\"size\":\"Standard\",\"animalId\":1}],\"rackCount\":1,\"incubatorCount\":1,\"nextAnimalId\":2,\"nextCageId\":2}");
        }

        [Test]
        public void GenomeVersionOne_WithHetsUnknown_KeepsHetsUnknown()
        {
            WriteSchemaThreeWithAnimal("{" + AnimalBase + ",\"genomeVersion\":1,\"genes\":[],\"hets\":[],\"hetsUnknown\":true," +
                "\"personality\":\"Calm\",\"personalityKnown\":true}");

            var pet = service.LoadOrCreate(path, Now, new Random(1)).Animals[0];

            Assert.That(service.LastLoadFailed, Is.False);
            Assert.That(pet.Known.HetsUnknown, Is.True);
            Assert.That(MorphNamer.FullName(pet.Genotype, pet.Known), Is.EqualTo("ノーマル（ヘテロ不明）"));
        }

        [Test]
        public void UnknownAndNumericGeneNames_AreIgnored()
        {
            WriteSchemaThreeWithAnimal("{" + AnimalBase + ",\"genomeVersion\":1," +
                "\"genes\":[{\"gene\":\"Eclipse\",\"copies\":2},{\"gene\":\"Lemonfrost\",\"copies\":2},{\"gene\":\"99\",\"copies\":2}]," +
                "\"hets\":[{\"gene\":\"Blizzard\",\"probability\":1.0},{\"gene\":\"Lemonfrost\",\"probability\":1.0},{\"gene\":\"42\",\"probability\":0.5}]," +
                "\"personality\":\"Calm\",\"personalityKnown\":true}");

            var pet = service.LoadOrCreate(path, Now, new Random(1)).Animals[0];

            Assert.That(service.LastLoadFailed, Is.False);
            Assert.That(MorphNamer.FullName(pet.Genotype, pet.Known), Is.EqualTo("エクリプス ヘテロブリザード"));
        }

        [Test]
        public void MissingGeneAndHetLists_LoadAsNormalWithNoHets()
        {
            WriteSchemaThreeWithAnimal("{" + AnimalBase + ",\"genomeVersion\":1,\"hypo\":30,\"tangerine\":20," +
                "\"personality\":\"Shy\",\"personalityKnown\":true}");

            var pet = service.LoadOrCreate(path, Now, new Random(1)).Animals[0];

            Assert.That(service.LastLoadFailed, Is.False);
            Assert.That(MorphNamer.FullName(pet.Genotype, pet.Known), Is.EqualTo("ノーマル"));
            Assert.That(pet.Personality, Is.EqualTo(Personality.Shy));
        }

        [TestCase("Grumpy")]
        [TestCase("42")]
        [TestCase("")]
        public void AnUnreadablePersonality_IsRolledFromTheDefinedOnes(string personality)
        {
            WriteSchemaThreeWithAnimal("{" + AnimalBase + ",\"genomeVersion\":1,\"personality\":\"" + personality + "\",\"personalityKnown\":true}");

            var pet = service.LoadOrCreate(path, Now, new Random(1)).Animals[0];

            Assert.That(service.LastLoadFailed, Is.False);
            Assert.That(Enum.IsDefined(typeof(Personality), pet.Personality), Is.True);
        }

        [Test]
        public void NumericOrUnknownEnumNames_FallBackToDefaults()
        {
            File.WriteAllText(path, "{\"schemaVersion\":3,\"calendarEpochUtc\":\"2026-09-20T00:00:00.0000000+00:00\",\"money\":50000," +
                "\"ledger\":[{\"atUtc\":\"2026-09-20T00:00:00.0000000+00:00\",\"category\":\"77\",\"amount\":-30,\"note\":\"x\"}]," +
                "\"animals\":[{\"id\":1,\"name\":\"レオパ1\",\"sex\":\"5\",\"weightGrams\":10.0,\"stage\":\"8\"," +
                "\"hatchedAtUtc\":\"2026-09-08T00:00:00.0000000+00:00\",\"hunger\":80,\"hydration\":80,\"cleanliness\":80,\"health\":100," +
                "\"genomeVersion\":1,\"personality\":\"Calm\",\"personalityKnown\":true}]," +
                "\"cages\":[{\"id\":1,\"size\":\"12\",\"animalId\":1}],\"rackCount\":1,\"incubators\":[\"9\"],\"nextAnimalId\":2,\"nextCageId\":2}");

            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(service.LastLoadFailed, Is.False);
            Assert.That(colony.Animals[0].Sex, Is.EqualTo(Sex.Female));
            Assert.That(colony.Animals[0].Stage, Is.EqualTo(GrowthStage.Baby));
            Assert.That(colony.Cages[0].Size, Is.EqualTo(CageSize.Standard));
            Assert.That(colony.Incubators.Select(i => i.Model), Is.EqualTo(new[] { IncubatorModel.Simple }));
            Assert.That(colony.Wallet.Ledger[0].Category, Is.EqualTo(LedgerCategory.Other));
        }

        private const string StateTime = "\"2026-09-25T12:00:00.0000000+00:00\"";

        private void WriteIncubatorFile(string incubatorStates, string eggs = "", string animalExtra = "", string extra = "")
        {
            File.WriteAllText(path, "{\"schemaVersion\":3,\"calendarEpochUtc\":\"2026-09-20T00:00:00.0000000+00:00\",\"money\":50000," +
                "\"animals\":[{\"id\":1,\"name\":\"A\",\"sex\":\"Male\",\"weightGrams\":45.0,\"stage\":\"Adult\"," +
                "\"hatchedAtUtc\":\"2026-09-08T00:00:00.0000000+00:00\",\"hunger\":80,\"hydration\":80,\"cleanliness\":80,\"health\":100," +
                "\"genomeVersion\":1,\"personality\":\"Calm\",\"personalityKnown\":true" + animalExtra + "}]," +
                "\"cages\":[{\"id\":1,\"size\":\"Standard\",\"animalId\":1}],\"rackCount\":1," +
                "\"incubatorStates\":[" + incubatorStates + "],\"eggs\":[" + eggs + "]" + extra + ",\"nextAnimalId\":2,\"nextCageId\":2}");
        }

        private static string IncState(int id, string model = "Simple", int temp = 28, int humidity = 80, string applied = null) =>
            "{\"id\":" + id + ",\"model\":\"" + model + "\",\"setTemperatureC\":" + temp + ",\"humidityPercent\":" + humidity +
            ",\"appliedUntilUtc\":" + (applied ?? StateTime) + "}";

        private static string IncEgg(int id, int incubatorId, string place = "Incubator") =>
            "{\"id\":" + id + ",\"cageId\":-1,\"laidAtUtc\":" + StateTime + ",\"place\":\"" + place + "\",\"fertile\":true," +
            "\"incubatorId\":" + incubatorId + ",\"appliedUntilUtc\":" + StateTime + "}";

        [Test]
        public void Load_IncubatorModelOutOfRange_ReadsAsSimple()
        {
            WriteIncubatorFile(IncState(1, "99"));

            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(service.LastLoadFailed, Is.False);
            Assert.That(colony.Incubators[0].Model, Is.EqualTo(IncubatorModel.Simple));
        }

        [TestCase(10, -5, 24, 0d)]
        [TestCase(50, 140, 34, 100d)]
        public void Load_IncubatorSettings_AreClamped(int temp, int humidity, int expectedTemp, double expectedHumidity)
        {
            WriteIncubatorFile(IncState(1, "Standard", temp, humidity));

            var incubator = service.LoadOrCreate(path, Now, new Random(1)).Incubators[0];

            Assert.That(incubator.SetTemperatureC, Is.EqualTo(expectedTemp));
            Assert.That(incubator.HumidityPercent, Is.EqualTo(expectedHumidity));
        }

        [Test]
        public void Load_BadOrDuplicateIncubatorIds_AreRenumberedAndEggsFollow()
        {
            WriteIncubatorFile(IncState(0) + "," + IncState(5, "Standard") + "," + IncState(5, "Luxury") + "," + IncState(-2),
                IncEgg(1, 5));

            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(colony.Incubators.Select(i => i.Id), Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(colony.Eggs[0].IncubatorId, Is.EqualTo(2));
        }

        [Test]
        public void Load_EggInAMissingIncubator_GoesToTheFirstOneWithRoom()
        {
            // Incubator 1 (Simple, capacity 6) is full, so the stray egg goes to incubator 2.
            var eggs = string.Join(",", Enumerable.Range(1, 6).Select(i => IncEgg(i, 1))) + "," + IncEgg(7, 9);
            WriteIncubatorFile(IncState(1) + "," + IncState(2, "Standard"), eggs);

            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(colony.Eggs.Find(e => e.Id == 7).IncubatorId, Is.EqualTo(2));
        }

        [Test]
        public void Load_EggInAMissingIncubator_WithNoRoomAnywhere_GoesToTheFirstOver()
        {
            var eggs = string.Join(",", Enumerable.Range(1, 6).Select(i => IncEgg(i, 1))) + "," + IncEgg(7, 0);
            WriteIncubatorFile(IncState(1), eggs);

            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(colony.Eggs.Find(e => e.Id == 7).IncubatorId, Is.EqualTo(1));
        }

        [Test]
        public void Load_BrokenGraceAndAppliedTimes_AreDroppedWithoutBreakingTheSave()
        {
            WriteIncubatorFile(IncState(1, applied: "\"garbage\""), IncEgg(1, 1), ",\"careGraceUntilUtc\":\"not a time\"");

            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(service.LastLoadFailed, Is.False);
            Assert.That(colony.Animals[0].CareGraceUntilUtc, Is.Null);
            Assert.That(colony.Incubators[0].AppliedUntilUtc, Is.EqualTo(default(DateTimeOffset)));
            Assert.That(colony.Animals, Has.Count.EqualTo(1));
            Assert.That(colony.Eggs, Has.Count.EqualTo(1));
        }

        [Test]
        public void Load_MissingOrNullLineage_IsAnEmptyList()
        {
            WriteIncubatorFile(IncState(1));
            Assert.That(service.LoadOrCreate(path, Now, new Random(1)).Lineage, Is.Empty);

            WriteIncubatorFile(IncState(1), extra: ",\"lineage\":null");
            var colony = service.LoadOrCreate(path, Now, new Random(1));
            Assert.That(service.LastLoadFailed, Is.False);
            Assert.That(colony.Lineage, Is.Empty);
        }

        [Test]
        public void RoundTrip_KeepsTrayAnimalParentsGraceLineageAndIncubatorEggs()
        {
            var colony = service.LoadOrCreate(path, Now, new Random(1));
            var tray = colony.AddAnimal(new PetState
            {
                Name = "Tray",
                MotherId = 7,
                FatherId = 8,
                CareGraceUntilUtc = Now.AddDays(2),
                LastSavedAtUtc = Now,
                NextShedAtUtc = Now.AddDays(3),
            }, null);
            colony.AddCage(CageSize.Standard);
            colony.Lineage.Add(new LineageRecord { Id = 7, Name = "Mum", MorphName = "ノーマル", Sex = Sex.Female, SexKnown = true, MotherId = 3, FatherId = 4 });
            colony.Eggs.Add(new Egg { Id = 1, CageId = -1, LaidAtUtc = Now, Place = EggPlace.Incubator, IncubatorId = colony.Incubators[0].Id, Fertile = true, AppliedUntilUtc = Now });
            colony.NextEggId = 2;
            var cageCount = colony.Cages.Count;

            service.Save(path, colony);
            var loaded = service.LoadOrCreate(path, Now, new Random(2));

            var loadedTray = loaded.AnimalById(tray.Id);
            Assert.That(loadedTray, Is.Not.Null);
            Assert.That(loaded.CageOf(loadedTray), Is.Null);
            Assert.That(loaded.TrayAnimals().Select(a => a.Id), Is.EqualTo(new[] { tray.Id }));
            Assert.That(loaded.Cages, Has.Count.EqualTo(cageCount));
            Assert.That(loadedTray.MotherId, Is.EqualTo(7));
            Assert.That(loadedTray.FatherId, Is.EqualTo(8));
            Assert.That(loadedTray.CareGraceUntilUtc, Is.EqualTo(Now.AddDays(2)));
            Assert.That(loaded.Lineage, Has.Count.EqualTo(1));
            var record = loaded.Lineage[0];
            Assert.That((record.Id, record.Name, record.MorphName, record.Sex, record.SexKnown, record.MotherId, record.FatherId),
                Is.EqualTo((7, "Mum", "ノーマル", Sex.Female, true, 3, 4)));
            Assert.That(loaded.Eggs[0].Place, Is.EqualTo(EggPlace.Incubator));
            Assert.That(loaded.Eggs[0].IncubatorId, Is.EqualTo(loaded.Incubators[0].Id));
        }

        [Test]
        public void Load_PhaseFourFile_HasNoParentsGraceLineageOrIncubatorIds()
        {
            File.WriteAllText(path, "{\"schemaVersion\":3,\"calendarEpochUtc\":\"2026-09-20T00:00:00.0000000+00:00\",\"money\":50000," +
                "\"animals\":[{\"id\":1,\"name\":\"A\",\"sex\":\"Male\",\"weightGrams\":45.0,\"stage\":\"Adult\"," +
                "\"hatchedAtUtc\":\"2026-09-08T00:00:00.0000000+00:00\",\"hunger\":80,\"hydration\":80,\"cleanliness\":80,\"health\":100," +
                "\"genomeVersion\":1,\"personality\":\"Calm\",\"personalityKnown\":true}]," +
                "\"cages\":[{\"id\":1,\"size\":\"Standard\",\"animalId\":1}],\"rackCount\":1,\"incubatorCount\":1," +
                "\"eggs\":[{\"id\":1,\"cageId\":1,\"laidAtUtc\":" + StateTime + ",\"place\":\"NestBox\",\"fertile\":true,\"appliedUntilUtc\":" + StateTime + "}]," +
                "\"nextAnimalId\":2,\"nextCageId\":2}");

            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(colony.Animals[0].MotherId, Is.EqualTo(0));
            Assert.That(colony.Animals[0].FatherId, Is.EqualTo(0));
            Assert.That(colony.Animals[0].CareGraceUntilUtc, Is.Null);
            Assert.That(colony.Lineage, Is.Empty);
            Assert.That(colony.Eggs[0].IncubatorId, Is.EqualTo(0));
            Assert.That(colony.Eggs[0].Place, Is.EqualTo(EggPlace.NestBox));
        }

        [Test]
        public void Load_IncubatorEggWithNoIncubatorList_GetsTheDefaultIncubator()
        {
            WriteIncubatorFile(string.Empty, IncEgg(1, 3));

            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(service.LastLoadFailed, Is.False);
            Assert.That(colony.Incubators.Select(i => i.Id), Is.EqualTo(new[] { 1 }));
            Assert.That(colony.Eggs[0].IncubatorId, Is.EqualTo(1));
        }

        [Test]
        public void Load_StaleIncubatorId_OnANestBoxEgg_IsNotRemapped()
        {
            WriteIncubatorFile(IncState(0) + "," + IncState(5), IncEgg(1, 5, "NestBox"));

            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(colony.Eggs[0].Place, Is.EqualTo(EggPlace.NestBox));
            Assert.That(colony.Eggs[0].IncubatorId, Is.EqualTo(0));
        }

        [Test]
        public void Load_NullEntriesInLineageAndIncubatorStates_AreSkippedOrDefaulted()
        {
            WriteIncubatorFile("null," + IncState(2), IncEgg(1, 2), extra: ",\"lineage\":[null]");

            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(service.LastLoadFailed, Is.False);
            Assert.That(colony.Lineage, Is.Empty);
            Assert.That(colony.Incubators, Has.Count.EqualTo(2));
            Assert.That(colony.Animals, Has.Count.EqualTo(1));
            Assert.That(colony.Eggs, Has.Count.EqualTo(1));
        }

        // ---- schema 4: events (phase 6, task 9) ----

        private static readonly DateTimeOffset EventNow = EventTestRoom.Epoch;
        private readonly EventService events = new EventService(new CareTuning());

        private string EventsJson(Colony colony)
        {
            service.Save(path, colony);
            return UnityEngine.JsonUtility.ToJson(UnityEngine.JsonUtility.FromJson<ColonySaveData>(File.ReadAllText(path)).events);
        }

        private Colony Registered(int animals, bool frozen)
        {
            var colony = EventTestRoom.Create(animals, out var calendar);
            var requests = EventTestRoom.AtBaseValue(events, colony, 0);
            requests.RemoveAt(requests.Count - 1);
            Assert.That(events.Register(colony, calendar, 0, requests, EventNow), Is.EqualTo(EventRegisterResult.Ok));
            if (frozen)
            {
                events.Advance(colony, calendar, EventSchedule.StartUtc(calendar, 0) + TimeSpan.FromMinutes(1));
            }

            return colony;
        }

        private Colony LoadAfterEdit(Colony colony, Action<ColonySaveData> edit)
        {
            service.Save(path, colony);
            var data = UnityEngine.JsonUtility.FromJson<ColonySaveData>(File.ReadAllText(path));
            edit(data);
            File.WriteAllText(path, UnityEngine.JsonUtility.ToJson(data));
            return service.LoadOrCreate(path, EventNow, new Random(2));
        }

        [Test]
        public void Schema3Save_LoadsWithNoEvent()
        {
            File.WriteAllText(path,
                "{\"schemaVersion\":3,\"calendarEpochUtc\":\"2026-09-20T00:00:00.0000000+00:00\",\"money\":12345," +
                "\"ledger\":[],\"animals\":[{\"id\":1,\"name\":\"Keeper\",\"sex\":\"Female\"," +
                "\"weightGrams\":15,\"hatchedAtUtc\":\"\",\"stage\":\"Baby\"," +
                "\"stageUpDueAtUtc\":\"\",\"hunger\":50,\"hydration\":50,\"cleanliness\":50," +
                "\"health\":100,\"selectedDecorId\":\"rock_01\",\"unlockedDecorIds\":[\"rock_01\"]," +
                "\"lastSavedAtUtc\":\"\",\"lastShedAtUtc\":\"\",\"nextShedAtUtc\":\"\"}]," +
                "\"cages\":[{\"id\":1,\"size\":\"Standard\",\"animalId\":1}]," +
                "\"rackCount\":0,\"incubatorCount\":1,\"nextAnimalId\":2,\"nextCageId\":2," +
                "\"lastBilledMonthIndex\":0}");

            var colony = service.LoadOrCreate(path, Now, new Random(1));

            Assert.That(service.LastLoadMigrated, Is.False);
            Assert.That(File.Exists(ColonySaveService.BackupPathFor(path)), Is.False);
            Assert.That(colony.Animals.Select(a => a.Name), Is.EqualTo(new[] { "Keeper" }));
            Assert.That(colony.Wallet.Money, Is.EqualTo(12345));
            Assert.That(colony.Events.Entry, Is.Null);
            Assert.That(colony.Events.LastSummary, Is.Null);
            Assert.That(colony.Events.LastSummarySeen, Is.True);
            Assert.That(colony.Events.NoticeNumber, Is.EqualTo(-1));
        }

        [Test]
        public void EventBeforeTheDay_RoundTrips()
        {
            var colony = Registered(3, frozen: false);
            var before = EventsJson(colony);

            var loaded = service.LoadOrCreate(path, EventNow, new Random(2));

            Assert.That(loaded.Events.Entry.Listings, Has.Count.EqualTo(2));
            Assert.That(loaded.Events.Entry.Frozen, Is.False);
            Assert.That(EventsJson(loaded), Is.EqualTo(before));
        }

        [Test]
        public void EventMidDay_RoundTrips()
        {
            var colony = Registered(3, frozen: true);
            var calendar = new GameCalendar(colony.CalendarEpochUtc);
            var day = EventSchedule.StartUtc(calendar, 0) + TimeSpan.FromMinutes(1);
            Assert.That(events.PlayAction(colony, calendar, EventActionKind.Serve, day, out _), Is.EqualTo(EventActionResult.Ok));
            for (var i = 0; i < 4 && colony.Events.Entry.BoothOffers.Count == 0; i++)
            {
                events.PlayAction(colony, calendar, EventActionKind.Browse, day + TimeSpan.FromMinutes(i + 1), out _);
            }

            colony.AddCage(CageSize.Standard);
            var offer = colony.Events.Entry.BoothOffers.Find(o => !o.Bought);
            Assert.That(offer, Is.Not.Null, "browsing should show a booth animal");
            Assert.That(events.Buy(colony, offer.OfferId, day + TimeSpan.FromMinutes(9)), Is.EqualTo(EventBuyResult.Ok));
            colony.Events.NoticeNumber = 0;
            colony.Events.NoticeStage = 2;
            var before = EventsJson(colony);

            var loaded = service.LoadOrCreate(path, EventNow, new Random(2));

            Assert.That(loaded.Events.Entry.Frozen, Is.True);
            Assert.That(loaded.Events.Entry.Actions.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(loaded.Events.Entry.Purchases, Has.Count.EqualTo(1));
            Assert.That(loaded.Events.NoticeNumber, Is.EqualTo(0));
            Assert.That(loaded.Events.NoticeStage, Is.EqualTo(2));
            Assert.That(EventsJson(loaded), Is.EqualTo(before));
        }

        [Test]
        public void EventSummaryOnly_RoundTripsWithItsUnreadFlag()
        {
            var colony = Colony.CreateNew(Now, economy, care, new Random(1));
            colony.Events.LastSummary = new EventSummary
            {
                Number = 3,
                BoothFee = 10000,
                Sold = { new EventAnimalLine { AnimalId = 4, Name = "A", MorphLabel = "ノーマル", Amount = 8000, MinuteOfDay = 650 } },
                Unsold = { new EventAnimalLine { AnimalId = 5, Name = "B", MorphLabel = "ハイポ", Amount = 0 } },
                Bought = { new EventAnimalLine { AnimalId = 9, Name = "C", MorphLabel = "ノーマル", Amount = 3000, MinuteOfDay = 700 } },
            };
            colony.Events.LastSummarySeen = false;
            var before = EventsJson(colony);

            var loaded = service.LoadOrCreate(path, Now, new Random(2));

            Assert.That(loaded.Events.Entry, Is.Null);
            Assert.That(loaded.Events.LastSummarySeen, Is.False);
            Assert.That(loaded.Events.LastSummary.Net, Is.EqualTo(8000 - 3000 - 10000));
            Assert.That(EventsJson(loaded), Is.EqualTo(before));
        }

        [Test]
        public void EventEntry_NegativeNumberIsDropped()
        {
            var loaded = LoadAfterEdit(Registered(3, false), d => d.events.entry.number = -1);

            Assert.That(loaded.Events.Entry, Is.Null);
        }

        [Test]
        public void EventListing_MissingAnimalIsDropped()
        {
            var loaded = LoadAfterEdit(Registered(3, false), d => d.events.entry.listings[0].animalId = 9999);

            Assert.That(loaded.Events.Entry.Listings, Has.Count.EqualTo(1));
            Assert.That(loaded.Events.Entry.Listings[0].AnimalId, Is.Not.EqualTo(9999));
        }

        [Test]
        public void EventListing_BadPriceToleranceAndDuplicateAreDropped()
        {
            var loaded = LoadAfterEdit(Registered(4, false), d =>
            {
                d.events.entry.listings[0].price = 0;
                d.events.entry.listings[1].tolerancePercent = 3;
                d.events.entry.listings.Add(UnityEngine.JsonUtility.FromJson<EventListingSaveData>(UnityEngine.JsonUtility.ToJson(d.events.entry.listings[2])));
            });

            Assert.That(loaded.Events.Entry.Listings, Has.Count.EqualTo(1));
        }

        [Test]
        public void EventListing_MoreThanTheMaximumIsCut()
        {
            var loaded = LoadAfterEdit(Registered(8, false), d =>
            {
                d.events.entry.number = 1; // a small event: at most 5
                d.events.entry.listings.Clear();
                foreach (var animal in d.animals.Take(7))
                {
                    d.events.entry.listings.Add(new EventListingSaveData { animalId = animal.id, price = 5000 });
                }
            });

            Assert.That(loaded.Events.Entry.Listings.Count, Is.EqualTo(EventSchedule.MaxListings(EventKind.Small)));
        }

        [Test]
        public void EventListing_FrozenSoldInActionIsClamped()
        {
            var loaded = LoadAfterEdit(Registered(3, true), d =>
            {
                d.events.entry.listings[0].soldInActionPlusOne = -7;
                d.events.entry.listings[1].soldInActionPlusOne = 99;
            });

            var listings = loaded.Events.Entry.Listings;
            Assert.That(listings[0].SoldInAction, Is.EqualTo(-1));
            Assert.That(listings[1].SoldInAction, Is.EqualTo(EventSchedule.ActionCount(EventKind.Big) - 1));
        }

        [Test]
        public void EventListing_FrozenBadChanceIsRecomputed()
        {
            var loaded = LoadAfterEdit(Registered(3, true), d =>
            {
                d.events.entry.listings[0].frozenChance = double.NaN;
                d.events.entry.listings[1].frozenChance = 7d;
            });

            foreach (var listing in loaded.Events.Entry.Listings)
            {
                Assert.That(listing.FrozenChance, Is.InRange(0d, 1d));
            }
        }

        [Test]
        public void EventListing_FrozenBadChanceWithoutBaseIsDropped()
        {
            var loaded = LoadAfterEdit(Registered(3, true), d =>
            {
                d.events.entry.listings[0].frozenChance = double.NaN;
                d.events.entry.listings[0].frozenBase = 0;
            });

            Assert.That(loaded.Events.Entry.Listings, Has.Count.EqualTo(1));
        }

        [Test]
        public void EventListing_FrozenDuplicateAndMaximumAreCut()
        {
            var duplicate = LoadAfterEdit(Registered(3, true), d =>
                d.events.entry.listings.Add(UnityEngine.JsonUtility.FromJson<EventListingSaveData>(UnityEngine.JsonUtility.ToJson(d.events.entry.listings[0]))));
            Assert.That(duplicate.Events.Entry.Listings, Has.Count.EqualTo(2));

            var capped = LoadAfterEdit(Registered(8, true), d => d.events.entry.number = 1);
            Assert.That(capped.Events.Entry.Listings.Count, Is.EqualTo(EventSchedule.MaxListings(EventKind.Small)));
        }

        [Test]
        public void EventEntry_LoadedWithEveryActionPlayedCanFinish()
        {
            var colony = Registered(3, true);
            var loaded = LoadAfterEdit(colony, d =>
            {
                for (var i = 0; i < 10; i++)
                {
                    d.events.entry.actions.Add(new EventActionSaveData { index = i, kind = "Serve", playedAtUtc = "" });
                }
            });
            Assert.That(loaded.Events.Entry, Is.Not.Null);

            var calendar = new GameCalendar(loaded.CalendarEpochUtc);
            events.Advance(loaded, calendar, EventSchedule.StartUtc(calendar, 0) + TimeSpan.FromMinutes(1));

            Assert.That(loaded.Events.Entry, Is.Null);
            Assert.That(loaded.Events.LastSummary, Is.Not.Null);
        }

        [Test]
        public void EventActions_ExtraAreCutAndUnknownKindBecomesServe()
        {
            var colony = Registered(3, true);
            var loaded = LoadAfterEdit(colony, d =>
            {
                for (var i = 0; i < 10; i++)
                {
                    d.events.entry.actions.Add(new EventActionSaveData { index = 99, kind = i == 0 ? "???" : "Browse", playedAtUtc = "" });
                }
            });

            var actions = loaded.Events.Entry.Actions;
            Assert.That(actions, Has.Count.EqualTo(EventSchedule.ActionCount(EventKind.Big)));
            Assert.That(actions[0].Kind, Is.EqualTo(EventActionKind.Serve));
            Assert.That(actions.Select(a => a.Index), Is.EqualTo(Enumerable.Range(0, actions.Count)));
        }

        [Test]
        public void EventBoothOffer_WithoutAnAnimalIsDroppedUnlessBought()
        {
            var loaded = LoadAfterEdit(Registered(3, true), d =>
            {
                d.events.entry.nextOfferId = 1;
                d.events.entry.boothOffers.Add(new EventBoothOfferSaveData { offerId = 4, hasAnimal = false, price = 1000 });
                d.events.entry.boothOffers.Add(new EventBoothOfferSaveData { offerId = 7, hasAnimal = false, price = 1000, bought = true });
            });

            var offers = loaded.Events.Entry.BoothOffers;
            Assert.That(offers.Select(o => o.OfferId), Is.EqualTo(new[] { 7 }));
            Assert.That(loaded.Events.Entry.NextOfferId, Is.EqualTo(8));
        }

        [Test]
        public void EventNoticeStage_IsClamped()
        {
            var loaded = LoadAfterEdit(Registered(3, false), d => d.events.noticeStage = 9);

            Assert.That(loaded.Events.NoticeStage, Is.EqualTo(2));
        }
    }
}
