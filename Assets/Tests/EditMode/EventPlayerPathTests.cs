using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    /// <summary>The player's real path through an event (session, save/load at each boundary, debug skip), not the service in isolation.</summary>
    public sealed class EventPlayerPathTests
    {
        private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);
        private readonly CareTuning care = new CareTuning();
        private readonly EconomyTuning economy = new EconomyTuning();
        private DateTimeOffset realNow;
        private string path;

        [SetUp]
        public void SetUp()
        {
            realNow = Start;
            path = Path.Combine(Path.GetTempPath(), $"eventpath-{Guid.NewGuid():N}.json");
        }

        [TearDown]
        public void TearDown()
        {
            File.Delete(path);
            File.Delete(path + ".tmp");
            File.Delete(ColonySaveService.BackupPathFor(path));
        }

        private ColonySession NewSession(int seed = 4)
        {
            var session = new ColonySession(path, new TimeService(() => realNow), care, economy, new Random(seed));
            session.Load();
            return session;
        }

        private ColonySession Reload(ColonySession session, int seed = 4)
        {
            session.Save();
            return NewSession(seed);
        }

        private void Fill(ColonySession session, int count)
        {
            var colony = session.Colony;
            colony.Wallet.Money = 1000000;
            colony.RackCount = 3;
            for (var i = colony.Animals.Count; i < count; i++)
            {
                var pet = ShopStockGenerator.Animal(new Random(100 + i), session.GameNowUtc, care);
                pet.Name = "テスト" + i;
                colony.AddAnimal(pet, colony.FirstEmptyCage() ?? colony.AddCage(CageSize.Standard));
            }
        }

        private static List<ListingRequest> AtMarket(ColonySession session, int number, double factor, int tolerance = 0)
        {
            return session.Colony.Animals.Select(p => new ListingRequest
            {
                AnimalId = p.Id,
                Price = Math.Max(EventService.MinPrice, (long)(session.Events.BaseValueOf(session.Colony, p, number) * factor)),
                TolerancePercent = tolerance,
            }).ToList();
        }

        private void Check(ColonySession s, int expected, string stage)
        {
            Assert.That(s.Colony.Events.Entry, Is.Not.Null, stage);
            Assert.That(s.Colony.Events.Entry.Listings.Count, Is.EqualTo(expected), stage);
        }

        [Test]
        public void Listings_SurviveEveryBoundary_AndTheSummaryListsEveryAnimal()
        {
            var session = NewSession();
            Fill(session, 4);
            var number = session.Events.OpenNumber(session.Colony, session.Calendar, session.GameNowUtc);
            Assert.That(session.Events.Register(session.Colony, session.Calendar, number, AtMarket(session, number, 1d), session.GameNowUtc),
                Is.EqualTo(EventRegisterResult.Ok));
            Check(session, 4, "registered");

            session = Reload(session);
            Check(session, 4, "reloaded after register");

            var report = session.SkipToNextEventDay();
            Assert.That(report.Events.Withdrawn, Is.Empty, "withdrawn: " + string.Join(",", report.Events.Withdrawn));
            Check(session, 4, "after skip");
            Assert.That(session.Colony.Events.Entry.Frozen, Is.True);
            Assert.That(session.Colony.Events.Entry.Listings.All(l => l.FrozenBase > 0 && l.FrozenChance > 0d), Is.True);

            session = Reload(session);
            Check(session, 4, "reloaded after freeze");

            var played = 0;
            while (session.Colony.Events.Entry != null && session.Events.ActionsLeft(session.Colony) > 0)
            {
                session.Events.PlayAction(session.Colony, session.Calendar, EventActionKind.Serve, session.GameNowUtc, out _);
                played++;
                if (played == 3)
                {
                    session = Reload(session);
                    Assert.That(session.Colony.Events.Entry.Actions.Count, Is.EqualTo(3), "reloaded mid-day");
                }
            }

            if (session.Colony.Events.Entry != null)
            {
                session = Reload(session);
                Assert.That(session.Events.Finish(session.Colony), Is.True);
            }

            var summary = session.Colony.Events.LastSummary;
            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.Number, Is.EqualTo(number));
            Assert.That(summary.Sold.Count + summary.Unsold.Count, Is.EqualTo(4), "every listing is sold or unsold");

            session = Reload(session);
            var loaded = session.Colony.Events.LastSummary;
            Assert.That(loaded.Sold.Count + loaded.Unsold.Count, Is.EqualTo(4), "summary after reload");
        }

        [Test]
        public void SellRate_AtMarketPrice_OverManySeeds()
        {
            var sold = 0;
            var total = 0;
            for (var seed = 1; seed <= 30; seed++)
            {
                path = Path.Combine(Path.GetTempPath(), $"eventpath-{Guid.NewGuid():N}.json");
                var session = NewSession(seed);
                Fill(session, 4);
                var number = session.Events.OpenNumber(session.Colony, session.Calendar, session.GameNowUtc);
                session.Colony.Wallet.Money = 1000000;
                session.Events.Register(session.Colony, session.Calendar, number, AtMarket(session, number, 1d), session.GameNowUtc);
                session.SkipToNextEventDay();
                session.Events.ServeAllRemaining(session.Colony, session.Calendar, session.GameNowUtc, out _);
                var summary = session.Colony.Events.LastSummary;
                Assert.That(summary, Is.Not.Null, "seed " + seed);
                sold += summary.Sold.Count;
                total += summary.Sold.Count + summary.Unsold.Count;
                File.Delete(path);
            }

            Assert.That(total, Is.EqualTo(120));
            Assert.That(sold, Is.GreaterThan(total * 0.55), $"sold {sold}/{total}");
        }

        [Test]
        public void RealisticDecay_ElevenHourSkip_KeepsHealthyListings()
        {
            var session = NewSession();
            Fill(session, 4);
            var number = session.Events.OpenNumber(session.Colony, session.Calendar, session.GameNowUtc);
            session.Events.Register(session.Colony, session.Calendar, number, AtMarket(session, number, 1d), session.GameNowUtc);
            realNow += TimeSpan.FromHours(11);
            session = Reload(session);
            var report = session.SkipToNextEventDay();
            Assert.That(report.Events.Withdrawn, Is.Empty, string.Join(",", report.Events.Withdrawn));
            Check(session, 4, "after an 11 h skip with no feeding");
        }

        [Test]
        public void WeakListing_WithdrawnAtFreeze_ShowsAsUnsoldInTheSummary_AndSurvivesReload()
        {
            var session = NewSession();
            Fill(session, 4);
            var number = session.Events.OpenNumber(session.Colony, session.Calendar, session.GameNowUtc);
            session.Events.Register(session.Colony, session.Calendar, number, AtMarket(session, number, 1d), session.GameNowUtc);
            var weak = session.Colony.Animals[0];
            weak.Weak = true;
            weak.Health = 0d;
            weak.Hunger = 0d;
            weak.Hydration = 0d;
            session = Reload(session);
            var report = session.SkipToNextEventDay();
            Assert.That(report.Events.Withdrawn.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(session.Colony.Events.Entry.Withdrawn.Count, Is.EqualTo(report.Events.Withdrawn.Count));
            var kept = session.Colony.Events.Entry.Listings.Count;
            session = Reload(session);
            Assert.That(session.Colony.Events.Entry.Withdrawn.Count, Is.EqualTo(report.Events.Withdrawn.Count), "withdrawn round-trips");
            session.Events.ServeAllRemaining(session.Colony, session.Calendar, session.GameNowUtc, out _);
            var summary = session.Colony.Events.LastSummary;
            Assert.That(summary.Sold.Count + summary.Unsold.Count, Is.EqualTo(4), "withdrawn listings are not lost");
            Assert.That(summary.Unsold.Any(l => l.AnimalId == weak.Id), Is.True);
            Assert.That(kept, Is.EqualTo(4 - report.Events.Withdrawn.Count));
        }

        [Test]
        public void DeviceSave_RegisterEveryAnimal_SkipToEventDay_ReportsOutcome()
        {
            var source = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".superpowers", "sdd", "2026-10-21-phase6-device-fixes", "device-save.json");
            var found = new[] { source, Path.Combine(Directory.GetCurrentDirectory(), ".superpowers/sdd/2026-10-21-phase6-device-fixes/device-save.json") }.FirstOrDefault(File.Exists);
            if (found == null)
            {
                Assert.Ignore("device-save.json not present (git-ignored)");
            }

            File.Copy(found, path, true);
            realNow = new DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);
            var session = NewSession();
            session.Colony.Wallet.Money = 1000000;
            var number = session.Events.OpenNumber(session.Colony, session.Calendar, session.GameNowUtc);
            var requests = new List<ListingRequest>();
            foreach (var pet in session.Colony.Animals)
            {
                var problem = EventRules.Check(session.Colony, pet);
                TestContext.WriteLine($"animal {pet.Id} {pet.Name}: problem before = {problem}");
                if (problem == ListingProblem.None)
                {
                    requests.Add(new ListingRequest { AnimalId = pet.Id, Price = Math.Max(EventService.MinPrice, session.Events.BaseValueOf(session.Colony, pet, number)) });
                }
            }

            Assert.That(session.Events.Register(session.Colony, session.Calendar, number, requests, session.GameNowUtc), Is.EqualTo(EventRegisterResult.Ok));
            var report = session.SkipToNextEventDay();
            foreach (var pet in session.Colony.Animals)
            {
                TestContext.WriteLine($"animal {pet.Id}: H={pet.Hunger:0} W={pet.Hydration:0} C={pet.Cleanliness:0} HP={pet.Health:0} weak={pet.Weak} problem after = {EventRules.Check(session.Colony, pet)}");
            }

            TestContext.WriteLine($"registered {requests.Count}, kept {session.Colony.Events.Entry.Listings.Count}, withdrawn [{string.Join(",", report.Events.Withdrawn)}]");
            Assert.That(session.Colony.Events.Entry.Listings.Count + session.Colony.Events.Entry.Withdrawn.Count, Is.EqualTo(requests.Count));
        }

        [Test]
        public void Listings_SurviveRealTimeTicksAndReloadsAcrossSeveralEvents()
        {
            var session = NewSession();
            Fill(session, 4);
            for (var n = 0; n < 4; n++)
            {
                var number = session.Events.OpenNumber(session.Colony, session.Calendar, session.GameNowUtc);
                Assert.That(number, Is.EqualTo(n));
                session.Colony.Wallet.Money = 1000000;
                var tolerance = EventOdds.TolerancePercents[n % EventOdds.TolerancePercents.Length];
                var registered = session.Events.Register(session.Colony, session.Calendar, number, AtMarket(session, number, 0.9d, tolerance), session.GameNowUtc);
                Assert.That(registered, Is.EqualTo(EventRegisterResult.Ok), $"event {number}, animals {session.Colony.Animals.Count}, problems "
                    + string.Join(",", session.Colony.Animals.Select(p => EventRules.Check(session.Colony, p))));
                var expected = session.Colony.Animals.Count;
                while (!EventSchedule.HasStarted(session.Calendar, number, session.GameNowUtc))
                {
                    realNow += TimeSpan.FromMinutes(30);
                    // The player feeds, waters and cleans on every visit; health is never touched.
                    foreach (var pet in session.Colony.Animals)
                    {
                        pet.Hunger = 100d;
                        pet.Hydration = 100d;
                        pet.Cleanliness = 100d;
                    }

                    var report = session.Advance(TimeSpan.FromMinutes(30));
                    Assert.That(report.Events.Withdrawn, Is.Empty, $"event {number}: " + string.Join(",", report.Events.Withdrawn));
                    session.Save();
                    session = NewSession();
                    Check(session, expected, $"event {number} before the day");
                }

                Assert.That(session.Colony.Events.Entry.Frozen, Is.True);
                Check(session, expected, $"event {number} on the day");
                session.Events.ServeAllRemaining(session.Colony, session.Calendar, session.GameNowUtc, out _);
                var summary = session.Colony.Events.LastSummary;
                Assert.That(summary.Number, Is.EqualTo(number));
                Assert.That(summary.Sold.Count + summary.Unsold.Count, Is.EqualTo(expected), $"event {number}");
                session.Events.DismissSummary(session.Colony);
                session = Reload(session);
                if (session.Colony.Animals.Count < 4)
                {
                    Fill(session, 4);
                }
            }
        }
    }
}
