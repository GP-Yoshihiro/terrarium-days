using System;
using System.Collections.Generic;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class EventServiceTests
    {
        private const string Spring = "ジャパン・レプタイルズ・フェスタ（春）";
        private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);
        private static readonly DateTimeOffset Epoch = EventTestRoom.Epoch;
        private readonly EventService service = new EventService(new CareTuning());

        private static List<ListingRequest> None() => new List<ListingRequest>();

        [Test]
        public void OpenNumber_IsTheNextEventUntilEntered()
        {
            var colony = EventTestRoom.Create(2, out var calendar);

            Assert.That(service.OpenNumber(colony, calendar, Epoch), Is.EqualTo(0));
            Assert.That(service.Register(colony, calendar, 0, None(), Epoch), Is.EqualTo(EventRegisterResult.Ok));
            Assert.That(service.OpenNumber(colony, calendar, Epoch), Is.EqualTo(-1));
        }

        [Test]
        public void Register_PaysTheFeeAndKeepsTheListingsInIdOrder()
        {
            var colony = EventTestRoom.Create(3, out var calendar);
            var money = colony.Wallet.Money;
            var requests = new List<ListingRequest>
            {
                new ListingRequest { AnimalId = colony.Animals[2].Id, Price = 20000, TolerancePercent = 5 },
                new ListingRequest { AnimalId = colony.Animals[0].Id, Price = 9000 },
            };

            Assert.That(service.Register(colony, calendar, 0, requests, Epoch), Is.EqualTo(EventRegisterResult.Ok));

            Assert.That(colony.Wallet.Money, Is.EqualTo(money - 10000));
            var fee = colony.Wallet.Ledger[colony.Wallet.Ledger.Count - 1];
            Assert.That(fee.Category, Is.EqualTo(LedgerCategory.BoothFee));
            Assert.That(fee.Amount, Is.EqualTo(-10000));
            Assert.That(fee.Note, Is.EqualTo(Spring + "の出店料"));
            Assert.That(fee.AtUtc, Is.EqualTo(Epoch));

            var entry = colony.Events.Entry;
            Assert.That(entry.Number, Is.EqualTo(0));
            Assert.That(entry.FeePaid, Is.EqualTo(10000));
            Assert.That(entry.Frozen, Is.False);
            Assert.That(entry.Listings.Count, Is.EqualTo(2));
            Assert.That(entry.Listings[0].AnimalId, Is.EqualTo(colony.Animals[0].Id));
            Assert.That(entry.Listings[1].Price, Is.EqualTo(20000));
            Assert.That(entry.Listings[1].TolerancePercent, Is.EqualTo(5));
            Assert.That(colony.Events.IsListed(colony.Animals[2].Id), Is.True);
            Assert.That(colony.Events.IsListed(colony.Animals[1].Id), Is.False);
        }

        [Test]
        public void Register_WithNoListings_IsAllowed()
        {
            var colony = EventTestRoom.Create(1, out var calendar);

            Assert.That(service.Register(colony, calendar, 0, None(), Epoch), Is.EqualTo(EventRegisterResult.Ok));
            Assert.That(colony.Events.Entry.Listings, Is.Empty);
        }

        [Test]
        public void Register_RejectsTheWholeRequest()
        {
            var colony = EventTestRoom.Create(11, out var calendar);
            var id = colony.Animals[1].Id;
            var ledger = colony.Wallet.Ledger.Count;

            Assert.That(service.Register(colony, calendar, 1, EventTestRoom.One(id, 1000), Epoch), Is.EqualTo(EventRegisterResult.NotOpen));
            Assert.That(service.Register(colony, calendar, 0, EventTestRoom.AtPrice(colony, 1000), Epoch), Is.EqualTo(EventRegisterResult.TooMany));

            var twice = EventTestRoom.One(id, 1000);
            twice.Add(new ListingRequest { AnimalId = id, Price = 2000 });
            Assert.That(service.Register(colony, calendar, 0, twice, Epoch), Is.EqualTo(EventRegisterResult.Duplicate));

            colony.Animals[1].Gravid = new GravidState();
            Assert.That(service.Register(colony, calendar, 0, EventTestRoom.One(id, 1000), Epoch), Is.EqualTo(EventRegisterResult.NotEligible));
            colony.Animals[1].Gravid = null;
            Assert.That(service.Register(colony, calendar, 0, EventTestRoom.One(999, 1000), Epoch), Is.EqualTo(EventRegisterResult.NotEligible));

            Assert.That(service.Register(colony, calendar, 0, EventTestRoom.One(id, 50), Epoch), Is.EqualTo(EventRegisterResult.InvalidPrice));
            Assert.That(service.Register(colony, calendar, 0, EventTestRoom.One(id, 1000, 3), Epoch), Is.EqualTo(EventRegisterResult.InvalidTolerance));

            colony.Wallet.Money = 9999;
            Assert.That(service.Register(colony, calendar, 0, EventTestRoom.One(id, 1000), Epoch), Is.EqualTo(EventRegisterResult.NotEnoughMoney));
            Assert.That(colony.Wallet.Money, Is.EqualTo(9999));

            Assert.That(colony.Wallet.Ledger.Count, Is.EqualTo(ledger));
            Assert.That(colony.Events.Entry, Is.Null);
        }

        [Test]
        public void Register_Twice_IsAlreadyRegisteredOrPending()
        {
            var colony = EventTestRoom.Create(1, out var calendar);
            Assert.That(service.Register(colony, calendar, 0, None(), Epoch), Is.EqualTo(EventRegisterResult.Ok));

            Assert.That(service.Register(colony, calendar, 0, None(), Epoch), Is.EqualTo(EventRegisterResult.AlreadyRegistered));
            var afterTheDay = EventSchedule.StartUtc(calendar, 0) + Second;
            Assert.That(EventSchedule.NextOpenNumber(calendar, afterTheDay), Is.EqualTo(1));
            Assert.That(service.Register(colony, calendar, 1, None(), afterTheDay), Is.EqualTo(EventRegisterResult.Pending));
        }

        [Test]
        public void UpdateListings_BeforeTheDay_ReplacesThemWithoutARefund()
        {
            var colony = EventTestRoom.Create(3, out var calendar);
            Assert.That(service.UpdateListings(colony, calendar, None(), Epoch), Is.EqualTo(EventRegisterResult.NotOpen));
            Assert.That(service.Register(colony, calendar, 0, EventTestRoom.One(colony.Animals[0].Id, 1000), Epoch), Is.EqualTo(EventRegisterResult.Ok));
            var money = colony.Wallet.Money;

            Assert.That(service.UpdateListings(colony, calendar, EventTestRoom.AtPrice(colony, 3000), Epoch), Is.EqualTo(EventRegisterResult.Ok));
            Assert.That(colony.Events.Entry.Listings.Count, Is.EqualTo(3));
            Assert.That(service.UpdateListings(colony, calendar, EventTestRoom.One(colony.Animals[0].Id, 50), Epoch), Is.EqualTo(EventRegisterResult.InvalidPrice));
            Assert.That(colony.Events.Entry.Listings.Count, Is.EqualTo(3));

            Assert.That(service.UpdateListings(colony, calendar, None(), Epoch), Is.EqualTo(EventRegisterResult.Ok));
            Assert.That(colony.Events.Entry.Listings, Is.Empty);
            Assert.That(colony.Wallet.Money, Is.EqualTo(money));

            Assert.That(service.UpdateListings(colony, calendar, None(), EventSchedule.StartUtc(calendar, 0)), Is.EqualTo(EventRegisterResult.Frozen));
        }

        [Test]
        public void Advance_AtMidnight_FreezesTheValuesOnce()
        {
            var colony = EventTestRoom.Create(3, out var calendar);
            var requests = EventTestRoom.AtPrice(colony, 5000);
            requests[2].TolerancePercent = 10;
            Assert.That(service.Register(colony, calendar, 0, requests, Epoch), Is.EqualTo(EventRegisterResult.Ok));
            colony.Animals[1].Gravid = new GravidState();
            var start = EventSchedule.StartUtc(calendar, 0);

            var before = service.Advance(colony, calendar, start - Second);
            Assert.That(colony.Events.Entry.Frozen, Is.False);
            Assert.That(before.HasEvents, Is.False);

            var report = service.Advance(colony, calendar, start);
            var entry = colony.Events.Entry;
            Assert.That(report.Withdrawn, Is.EqualTo(new[] { "テスト1" }));
            Assert.That(report.DayStartedNumber, Is.EqualTo(0));
            Assert.That(report.HasEvents, Is.True);
            Assert.That(entry.Frozen, Is.True);
            Assert.That(entry.Listings.Count, Is.EqualTo(2));
            foreach (var listing in entry.Listings)
            {
                var pet = colony.AnimalById(listing.AnimalId);
                Assert.That(listing.FrozenBase, Is.EqualTo(service.BaseValueOf(colony, pet, 0)));
                Assert.That(listing.FrozenChance, Is.EqualTo(EventOdds.SaleChance(listing.Price, listing.TolerancePercent, listing.FrozenBase, EventKind.Big)));
                Assert.That(listing.FrozenChance, Is.EqualTo(service.ChanceOf(colony, pet, 0, listing.Price, listing.TolerancePercent)));
            }

            Assert.That(entry.ListingSeed, Is.EqualTo(EventRandom.ListingSeed(colony.BreedingSeed, 0, entry.Listings)));

            var again = service.Advance(colony, calendar, start + TimeSpan.FromMinutes(1));
            Assert.That(again.DayStartedNumber, Is.EqualTo(-1));
            Assert.That(again.Withdrawn, Is.Empty);
        }

        [Test]
        public void Advance_OpenedAfterTheDay_FreezesWithoutTheDayNotice()
        {
            var colony = EventTestRoom.Create(2, out var calendar);
            Assert.That(service.Register(colony, calendar, 0, EventTestRoom.AtPrice(colony, 5000), Epoch), Is.EqualTo(EventRegisterResult.Ok));
            var end = EventSchedule.EndUtc(calendar, 0);

            Assert.That(service.PendingNumber(colony, calendar, end - Second), Is.EqualTo(-1));
            Assert.That(service.PendingNumber(colony, calendar, end), Is.EqualTo(0));

            var report = service.Advance(colony, calendar, end + Second);
            Assert.That(colony.Events.Entry.Frozen, Is.True);
            Assert.That(report.DayStartedNumber, Is.EqualTo(-1));
        }

        [Test]
        public void Advance_DropsListingsWhoseAnimalIsGone()
        {
            var colony = EventTestRoom.Create(2, out var calendar);
            Assert.That(service.Register(colony, calendar, 0, EventTestRoom.AtPrice(colony, 5000), Epoch), Is.EqualTo(EventRegisterResult.Ok));
            var gone = colony.Animals[1];
            colony.RemoveAnimal(gone);

            var report = service.Advance(colony, calendar, Epoch + TimeSpan.FromMinutes(1));

            Assert.That(report.Withdrawn, Is.EqualTo(new[] { "テスト1" }));
            Assert.That(colony.Events.Entry.Listings.Count, Is.EqualTo(1));
            Assert.That(colony.Events.IsListed(gone.Id), Is.False);
        }

        [Test]
        public void Advance_DeadlineNotices_ThreeDaysAndTheDayBefore()
        {
            var colony = EventTestRoom.Create(1, out var calendar);
            var start = EventSchedule.StartUtc(calendar, 0);

            Assert.That(service.Advance(colony, calendar, start - GameCalendar.RealTimeFor(3.1d)).DeadlineNoticeNumber, Is.EqualTo(-1));

            var three = service.Advance(colony, calendar, start - GameCalendar.RealTimeFor(2.9d));
            Assert.That(three.DeadlineNoticeNumber, Is.EqualTo(0));
            Assert.That(three.DeadlineDaysLeft, Is.EqualTo(3));
            Assert.That(service.Advance(colony, calendar, start - GameCalendar.RealTimeFor(2d)).DeadlineNoticeNumber, Is.EqualTo(-1));

            var last = service.Advance(colony, calendar, start - GameCalendar.RealTimeFor(0.9d));
            Assert.That(last.DeadlineNoticeNumber, Is.EqualTo(0));
            Assert.That(last.DeadlineDaysLeft, Is.EqualTo(1));
            Assert.That(service.Advance(colony, calendar, start - Second).DeadlineNoticeNumber, Is.EqualTo(-1));

            var next = service.Advance(colony, calendar, start + GameCalendar.RealTimeFor(27.1d));
            Assert.That(next.DeadlineNoticeNumber, Is.EqualTo(1));
            Assert.That(next.DeadlineDaysLeft, Is.EqualTo(3));
        }

        [Test]
        public void Advance_JumpingToTheDayBefore_ShowsOnlyTheNewerNotice()
        {
            var colony = EventTestRoom.Create(1, out var calendar);
            var start = EventSchedule.StartUtc(calendar, 0);

            var report = service.Advance(colony, calendar, start - GameCalendar.RealTimeFor(0.5d));
            Assert.That(report.DeadlineNoticeNumber, Is.EqualTo(0));
            Assert.That(report.DeadlineDaysLeft, Is.EqualTo(1));
            Assert.That(service.Advance(colony, calendar, start - GameCalendar.RealTimeFor(0.4d)).DeadlineNoticeNumber, Is.EqualTo(-1));
        }

        [Test]
        public void Advance_NoDeadlineNoticesWhileEntered()
        {
            var colony = EventTestRoom.Create(1, out var calendar);
            Assert.That(service.Register(colony, calendar, 0, None(), Epoch), Is.EqualTo(EventRegisterResult.Ok));

            var report = service.Advance(colony, calendar, EventSchedule.StartUtc(calendar, 0) - GameCalendar.RealTimeFor(0.9d));

            Assert.That(report.DeadlineNoticeNumber, Is.EqualTo(-1));
        }
    }
}
