using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.Gameplay;
using UnityEngine.TestTools;

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

        [Test]
        public void SexRevealsAreReportedWithThePetAndCountAsAnEvent()
        {
            var session = NewSession();
            session.Load();
            var pet = session.Colony.Animals[0];
            pet.Stage = GrowthStage.Juvenile;
            pet.NextShedAtUtc = realNow + TimeSpan.FromMinutes(3);

            var report = session.SimulateGameTime(TimeSpan.FromMinutes(10));

            Assert.That(report.SexReveals, Has.Count.EqualTo(1));
            Assert.That(report.SexReveals[0], Is.SameAs(pet));
            Assert.That(report.HasEvents, Is.True);
            Assert.That(pet.SexKnown, Is.True);
        }

        [Test]
        public void Load_StocksTheShopAndANewGameMonthRestocksIt()
        {
            var session = NewSession();
            var first = session.Load();

            Assert.That(first.Restocked, Is.True);
            Assert.That(session.Colony.Shop.Offers.Count, Is.InRange(ShopStockGenerator.MinOffers, ShopStockGenerator.MaxOffers));
            var month = session.Colony.Shop.StockMonthIndex;

            Assert.That(session.SimulateGameTime(GameCalendar.RealTimeFor(1d)).Restocked, Is.False);
            var next = session.SimulateGameTime(GameCalendar.RealTimeFor(30d));

            Assert.That(next.Restocked, Is.True);
            Assert.That(next.HasEvents, Is.True);
            Assert.That(session.Colony.Shop.StockMonthIndex, Is.EqualTo(month + 1));
        }

        [Test]
        public void ABoughtAnimal_RevealsItsPersonalityAfterSevenGameDays()
        {
            var session = NewSession();
            session.Load();
            session.Colony.AddCage(CageSize.Standard);
            session.Colony.Wallet.Money = 1_000_000;
            var offer = session.Colony.Shop.Offers[0];

            Assert.That(new ShopService(economy, care).BuyAnimal(session.Colony, offer.OfferId, session.GameNowUtc), Is.EqualTo(ShopResult.Ok));

            Assert.That(session.SimulateGameTime(GameCalendar.RealTimeFor(6.9d)).PersonalityReveals, Is.Empty);
            var later = session.SimulateGameTime(GameCalendar.RealTimeFor(0.2d));

            Assert.That(later.PersonalityReveals, Is.EquivalentTo(new[] { offer.Animal }));
            Assert.That(offer.Animal.PersonalityKnown, Is.True);
            Assert.That(later.HasEvents, Is.True);
        }

        [Test]
        public void Load_WhenTheCorruptBackupCannotBeWritten_BlocksSavingSoTheFileSurvives()
        {
            File.WriteAllText(path, "not json");
            var backupPath = ColonySaveService.CorruptBackupPathFor(path, Start);
            Directory.CreateDirectory(backupPath);
            LogAssert.Expect(UnityEngine.LogType.Error, new Regex(".*Could not back up.*"));
            LogAssert.Expect(UnityEngine.LogType.Error, new Regex(".*could not be loaded.*"));
            try
            {
                var session = NewSession();
                session.Load();
                session.Save();

                Assert.That(session.SaveBlocked, Is.True);
                Assert.That(File.ReadAllText(path), Is.EqualTo("not json"));
            }
            finally
            {
                Directory.Delete(backupPath);
            }
        }

        [Test]
        public void Load_ReportsWhenDecorWasMovedToTheInventory()
        {
            File.WriteAllText(path,
                "{\"schemaVersion\":2,\"lastSavedAtUtc\":\"2026-09-25T11:00:00.0000000+00:00\",\"hunger\":70,\"hydration\":60," +
                "\"cleanliness\":50,\"health\":90,\"growth\":50,\"growthStage\":\"Juvenile\",\"selectedDecorId\":\"plant_01\"," +
                "\"unlockedDecorIds\":[\"rock_01\",\"plant_01\"]}");

            var session = NewSession();
            session.Load();

            Assert.That(session.DecorMovedToInventory, Is.True);
            Assert.That(session.SaveBlocked, Is.False);
        }

        [Test]
        public void DebugFastForward_IsKeptWhenSavingAndReloading()
        {
            var session = NewSession();
            session.Load();
            session.Colony.Animals[0].NextShedAtUtc = realNow.AddDays(30);
            session.UseClock(new TimeService(() => realNow) { TimeMultiplier = 600d });

            realNow += TimeSpan.FromSeconds(60);
            session.Advance(TimeSpan.FromSeconds(60)); // 10 game-clock hours
            var gameNow = session.GameNowUtc;
            session.Save();

            Assert.That(gameNow, Is.EqualTo(Start + TimeSpan.FromHours(10)));
            Assert.That(session.GameNowUtc, Is.EqualTo(gameNow), "saving must not rewind the game clock");
            Assert.That(session.Colony.GameClockOffset, Is.EqualTo(TimeSpan.FromHours(10) - TimeSpan.FromSeconds(60)));

            var reloaded = NewSession();
            reloaded.Load();

            Assert.That(reloaded.GameNowUtc, Is.EqualTo(gameNow));
            Assert.That(reloaded.Calendar.DateAt(reloaded.GameNowUtc).ToDisplayText(),
                Is.EqualTo(session.Calendar.DateAt(gameNow).ToDisplayText()));
        }

        [Test]
        public void AFastForwardedMonth_IsNotRewoundOrBilledAgainAfterSaving()
        {
            var session = NewSession();
            session.Load();
            session.Colony.Animals[0].NextShedAtUtc = realNow.AddDays(30);

            var first = session.SimulateGameTime(TimeSpan.FromHours(25)); // into game month 1
            session.Save();
            var again = session.Resume();

            Assert.That(first.ElectricityCharged, Is.EqualTo(MaintenanceCosts.MonthlyElectricity(1, 1, economy)));
            Assert.That(again.ElectricityCharged, Is.EqualTo(0));
            Assert.That(session.GameNowUtc, Is.EqualTo(Start + TimeSpan.FromHours(25)));
            Assert.That(session.Calendar.MonthIndexAt(session.GameNowUtc), Is.EqualTo(1));
        }

        [Test]
        public void ABreedingPair_PairsLaysAndSurvivesReloads()
        {
            var session = NewSession();
            session.Load();
            var male = session.Colony.Animals[0];
            male.Sex = Sex.Male;
            male.SexRevealed = true;
            male.Stage = GrowthStage.Adult;
            male.WeightGrams = 55d;
            male.HatchedAtUtc = realNow.AddDays(-365);
            male.PersonalityKnown = true;
            male.NextShedAtUtc = realNow.AddDays(30);
            male.LastSavedAtUtc = realNow;
            var female = session.Colony.AddAnimal(new PetState
            {
                Sex = Sex.Female,
                SexRevealed = true,
                Stage = GrowthStage.Adult,
                WeightGrams = 55d,
                HatchedAtUtc = realNow.AddDays(-365),
                PersonalityKnown = true,
                NextShedAtUtc = realNow.AddDays(30),
                LastSavedAtUtc = realNow,
            }, session.Colony.AddCage(CageSize.Standard));
            session.Colony.CageOf(female).HasNestBox = true;

            void ResetCare()
            {
                foreach (var pet in session.Colony.Animals)
                {
                    pet.Hunger = 100d;
                    pet.Hydration = 100d;
                    pet.Cleanliness = 100d;
                }
            }

            Assert.That(session.Breeding.StartPairing(session.Colony, male.Id, female.Id, session.GameNowUtc, session.Calendar), Is.EqualTo(PairingProblem.None));
            var pairing = session.Colony.PairingOf(female);
            Assert.That(pairing, Is.Not.Null);
            pairing.SuccessChance = 1d; // pin the mating roll to always succeed

            ResetCare();
            var report = session.SimulateGameTime(GameCalendar.RealTimeFor(care.PairingGameDays)); // past the pairing window

            Assert.That(report.Breeding.PairingsSucceeded, Has.Exactly(1)
                .Matches<(PetState Female, PetState Male)>(p => p.Female == female && p.Male == male));
            Assert.That(report.Breeding.HasEvents, Is.True);
            Assert.That(session.Colony.Pairings, Is.Empty);
            Assert.That(female.Gravid, Is.Not.Null);

            session.Save();
            var reloaded = NewSession();
            reloaded.Load();

            var reloadedFemale = reloaded.Colony.AnimalById(female.Id);
            Assert.That(reloadedFemale.Gravid, Is.Not.Null);
            Assert.That(reloaded.Colony.CageOf(reloadedFemale).HasNestBox, Is.True);

            var nextClutchAtUtc = female.Gravid.NextClutchAtUtc;
            ResetCare();
            var clutchReport = session.SimulateGameTime(nextClutchAtUtc - session.GameNowUtc);

            Assert.That(clutchReport.Breeding.Clutches, Has.Count.EqualTo(1));
            Assert.That(session.Colony.Eggs, Is.Not.Empty);

            session.Save();
            var reloadedAgain = NewSession();
            reloadedAgain.Load();
            Assert.That(reloadedAgain.Colony.Eggs, Is.Not.Empty);
        }

        [Test]
        public void HealthReachingZero_IsReportedAsWeak()
        {
            var session = NewSession();
            session.Load();
            var pet = session.Colony.Animals[0];
            pet.Health = 2d;
            pet.Hunger = 0d;
            pet.Hydration = 0d;
            pet.Cleanliness = 0d;
            pet.LastSavedAtUtc = realNow;

            var report = session.SimulateGameTime(TimeSpan.FromHours(1));

            Assert.That(report.WeakStarted, Is.EquivalentTo(new[] { pet }));
            Assert.That(pet.Weak, Is.True);
            Assert.That(report.HasEvents, Is.True);
        }

        [Test]
        public void Resume_AfterAFastForward_AppliesOnlyTheRealTimeAway()
        {
            var session = NewSession();
            session.Load();
            var pet = session.Colony.Animals[0];
            pet.NextShedAtUtc = realNow.AddDays(30);
            session.SimulateGameTime(TimeSpan.FromHours(2));
            session.Save();

            realNow += TimeSpan.FromHours(1);
            session.Resume();

            Assert.That(session.GameNowUtc, Is.EqualTo(realNow + TimeSpan.FromHours(2)));
            Assert.That(pet.Hunger, Is.EqualTo(80d - care.HungerDecayPerHour * 3d).Within(1e-6));
        }
    }
}
