using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class EventDayTests
    {
        private const string Spring = "ジャパン・レプタイルズ・フェスタ（春）";
        private static readonly DateTimeOffset Epoch = EventTestRoom.Epoch;
        private readonly CareTuning care = new CareTuning();
        private EventService service;

        [SetUp]
        public void SetUp() => service = new EventService(care);

        private static DateTimeOffset Day(GameCalendar calendar, double gameDaysLate = 0d) =>
            EventSchedule.StartUtc(calendar, 0) + GameCalendar.RealTimeFor(gameDaysLate) + TimeSpan.FromMinutes(1);

        /// <summary>Registers event 0 (big, 8 actions) on the epoch and freezes it when the player opens the app on the day.</summary>
        private Colony Entered(int animals, Func<Colony, List<ListingRequest>> requests, out GameCalendar calendar, double gameDaysLate = 0d)
        {
            var colony = EventTestRoom.Create(animals, out calendar);
            Assert.That(service.Register(colony, calendar, 0, requests(colony), Epoch), Is.EqualTo(EventRegisterResult.Ok));
            service.Advance(colony, calendar, Day(calendar, gameDaysLate));
            return colony;
        }

        private List<ListingRequest> AtBase(Colony colony) => EventTestRoom.AtBaseValue(service, colony, 0);

        private static List<ListingRequest> Cheap(Colony colony) => EventTestRoom.AtPrice(colony, 1000);

        private static List<ListingRequest> Nothing(Colony colony) => new List<ListingRequest>();

        private string Transcript(EventActionKind[] kinds, double gameDaysLate)
        {
            var colony = Entered(5, AtBase, out var calendar, gameDaysLate);
            var text = new StringBuilder();
            for (var i = 0; i < kinds.Length; i++)
            {
                Assert.That(service.PlayAction(colony, calendar, kinds[i], Day(calendar, gameDaysLate) + TimeSpan.FromMinutes(i), out var record),
                    Is.EqualTo(EventActionResult.Ok));
                text.Append(record.Kind).Append('|').Append(record.BoothMinuteOfDay).Append('|');
                foreach (var sale in record.Sales)
                {
                    text.Append(sale.AnimalId).Append(':').Append(sale.Amount).Append(':').Append(sale.MinuteOfDay).Append(';');
                }

                text.Append('\n');
            }

            foreach (var offer in colony.Events.Entry.BoothOffers)
            {
                text.Append(offer.OfferId).Append(':').Append(offer.MorphLabel).Append(':').Append(offer.Price).Append(':').Append(offer.Rare).Append('\n');
            }

            return text.Append(colony.Wallet.Money).ToString();
        }

        private static readonly EventActionKind[] Mixed =
        {
            EventActionKind.Serve, EventActionKind.Browse, EventActionKind.Serve, EventActionKind.Serve,
            EventActionKind.Browse, EventActionKind.Serve, EventActionKind.Serve, EventActionKind.Serve,
        };

        [Test]
        public void SameChoices_GiveSameResults()
        {
            Assert.That(Transcript(Mixed, 0d), Is.EqualTo(Transcript(Mixed, 0d)));
        }

        [Test]
        public void PlayingLate_GivesSameResults()
        {
            Assert.That(Transcript(Mixed, 5d), Is.EqualTo(Transcript(Mixed, 0d)));
        }

        [Test]
        public void AllServe_SaleRateIsNearEventChance()
        {
            var listed = 0;
            var sold = 0;
            var expected = 0d;
            for (var trial = 0; trial < 300; trial++)
            {
                var colony = Entered(5, AtBase, out var calendar);
                var entry = colony.Events.Entry;
                entry.ListingSeed = 7919 * trial + 13;
                foreach (var listing in entry.Listings)
                {
                    expected += listing.FrozenChance;
                }

                listed += entry.Listings.Count;
                Assert.That(service.ServeAllRemaining(colony, calendar, Day(calendar), out _), Is.EqualTo(EventActionResult.Ok));
                sold += colony.Events.LastSummary.Sold.Count;
            }

            Assert.That(sold / (double)listed, Is.EqualTo(expected / listed).Within(0.04d));
        }

        [Test]
        public void PlayAction_BeforeTheDayOrWithoutAnEntry_IsRefused()
        {
            var empty = EventTestRoom.Create(1, out var emptyCalendar);
            Assert.That(service.PlayAction(empty, emptyCalendar, EventActionKind.Serve, Day(emptyCalendar), out _), Is.EqualTo(EventActionResult.NoEvent));

            var colony = EventTestRoom.Create(2, out var calendar);
            Assert.That(service.Register(colony, calendar, 0, Nothing(colony), Epoch), Is.EqualTo(EventRegisterResult.Ok));
            var before = EventSchedule.StartUtc(calendar, 0) - TimeSpan.FromSeconds(1);
            Assert.That(service.PlayAction(colony, calendar, EventActionKind.Serve, before, out var record), Is.EqualTo(EventActionResult.NotYet));
            Assert.That(record, Is.Null);
            Assert.That(service.ServeAllRemaining(colony, calendar, before, out _), Is.EqualTo(EventActionResult.NotYet));
            Assert.That(service.ActionsLeft(colony), Is.EqualTo(8));
        }

        [Test]
        public void Sales_PayIntoTheLedgerAndKeepTheLineage()
        {
            var colony = Entered(5, Cheap, out var calendar);
            var fullNames = new Dictionary<int, string>();
            foreach (var pet in colony.Animals)
            {
                fullNames[pet.Id] = MorphNamer.FullName(pet.Genotype, pet.Known);
            }

            var money = colony.Wallet.Money;
            var playedAt = Day(calendar) + TimeSpan.FromMinutes(5);

            Assert.That(service.ServeAllRemaining(colony, calendar, playedAt, out _), Is.EqualTo(EventActionResult.Ok));

            var sold = colony.Events.LastSummary.Sold;
            Assert.That(sold, Is.Not.Empty);
            foreach (var line in sold)
            {
                Assert.That(colony.AnimalById(line.AnimalId), Is.Null);
                var lineage = colony.Lineage.Find(r => r.Id == line.AnimalId);
                Assert.That(lineage, Is.Not.Null);
                Assert.That(lineage.Name, Is.EqualTo(line.Name));
                Assert.That(line.Amount, Is.EqualTo(1000));
                var note = $"{Spring}：{line.Name}（{fullNames[line.AnimalId]}）が売れました";
                var entry = colony.Wallet.Ledger.Find(e => e.Category == LedgerCategory.EventSale && e.Note == note);
                Assert.That(entry, Is.Not.Null, note);
                Assert.That(entry.Amount, Is.EqualTo(1000));
                Assert.That(entry.AtUtc, Is.EqualTo(playedAt));
            }

            Assert.That(colony.Wallet.Money, Is.EqualTo(money + 1000L * sold.Count));
        }

        [Test]
        public void SaleTimes_AreInsideTheirActionWindows()
        {
            var colony = Entered(5, Cheap, out var calendar);

            Assert.That(service.ServeAllRemaining(colony, calendar, Day(calendar), out var records), Is.EqualTo(EventActionResult.Ok));

            Assert.That(records.Count, Is.EqualTo(8));
            for (var i = 0; i < records.Count; i++)
            {
                var start = EventSchedule.ActionWindowStart(i, 8);
                Assert.That(records[i].Index, Is.EqualTo(i));
                foreach (var sale in records[i].Sales)
                {
                    Assert.That(sale.MinuteOfDay, Is.InRange(start, start + EventSchedule.ActionWindowLength(8) - 1));
                }
            }
        }

        [Test]
        public void Browse_AddsOneToThreeOffers()
        {
            var colony = Entered(2, Nothing, out var calendar);
            var entry = colony.Events.Entry;

            Assert.That(service.PlayAction(colony, calendar, EventActionKind.Browse, Day(calendar), out var first), Is.EqualTo(EventActionResult.Ok));
            var count = entry.BoothOffers.Count;
            Assert.That(count, Is.InRange(1, 3));
            Assert.That(entry.NextOfferId, Is.EqualTo(1 + count));
            Assert.That(first.BoothMinuteOfDay, Is.InRange(600, 600 + EventSchedule.ActionWindowLength(8) - 1));
            for (var i = 0; i < count; i++)
            {
                Assert.That(entry.BoothOffers[i].OfferId, Is.EqualTo(1 + i));
                Assert.That(entry.BoothOffers[i].ActionIndex, Is.EqualTo(0));
            }

            Assert.That(service.PlayAction(colony, calendar, EventActionKind.Browse, Day(calendar), out _), Is.EqualTo(EventActionResult.Ok));
            Assert.That(entry.BoothOffers[count].OfferId, Is.EqualTo(1 + count));
            Assert.That(entry.BoothOffers[count].ActionIndex, Is.EqualTo(1));
            Assert.That(service.ActionsLeft(colony), Is.EqualTo(6));
        }

        [Test]
        public void Buy_PutsTheAnimalInTheLowestEmptyCageAndRecordsIt()
        {
            var colony = Entered(2, Nothing, out var calendar);
            colony.AddCage(CageSize.Standard);
            colony.AddCage(CageSize.Small);
            var at = Day(calendar) + TimeSpan.FromMinutes(3);
            Assert.That(service.PlayAction(colony, calendar, EventActionKind.Browse, at, out _), Is.EqualTo(EventActionResult.Ok));
            var entry = colony.Events.Entry;
            var offer = entry.BoothOffers[0];
            var pet = offer.Animal;
            var price = offer.Price;
            var fullName = MorphNamer.FullName(pet.Genotype, pet.Known);
            var cage = colony.FirstEmptyCage();
            var newName = $"レオパ{colony.NextAnimalId}";
            var money = colony.Wallet.Money;

            Assert.That(service.CheckBuy(colony, offer.OfferId), Is.EqualTo(EventBuyResult.Ok));
            Assert.That(service.Buy(colony, offer.OfferId, at), Is.EqualTo(EventBuyResult.Ok));

            Assert.That(colony.Wallet.Money, Is.EqualTo(money - price));
            Assert.That(colony.CageOf(pet), Is.SameAs(cage));
            Assert.That(pet.Name, Is.EqualTo(newName));
            Assert.That(pet.PersonalityKnown, Is.False);
            Assert.That(pet.PersonalityRevealAtUtc, Is.EqualTo(at + GameCalendar.RealTimeFor(care.PersonalityRevealGameDays)));
            Assert.That(offer.Bought, Is.True);
            Assert.That(offer.Animal, Is.Null);
            Assert.That(entry.Purchases.Count, Is.EqualTo(1));
            Assert.That(entry.Purchases[0].Name, Is.EqualTo(newName));
            Assert.That(entry.Purchases[0].Amount, Is.EqualTo(price));
            Assert.That(entry.Purchases[0].MorphLabel, Is.EqualTo(offer.MorphLabel));

            var ledger = colony.Wallet.Ledger[colony.Wallet.Ledger.Count - 1];
            Assert.That(ledger.Category, Is.EqualTo(LedgerCategory.AnimalPurchase));
            Assert.That(ledger.Amount, Is.EqualTo(-price));
            Assert.That(ledger.Note, Is.EqualTo($"{Spring}：{newName}（{fullName}）を購入"));
            Assert.That(ledger.AtUtc, Is.EqualTo(at));

            Assert.That(service.Buy(colony, offer.OfferId, at), Is.EqualTo(EventBuyResult.AlreadyBought));
            Assert.That(service.Buy(colony, 999, at), Is.EqualTo(EventBuyResult.NotFound));
        }

        [Test]
        public void Buy_WithNoEmptyCage_IsRefusedBeforeTheMoney()
        {
            var noEntry = EventTestRoom.Create(1, out _);
            Assert.That(service.CheckBuy(noEntry, 1), Is.EqualTo(EventBuyResult.NoEvent));

            var colony = Entered(2, Nothing, out var calendar);
            Assert.That(service.PlayAction(colony, calendar, EventActionKind.Browse, Day(calendar), out _), Is.EqualTo(EventActionResult.Ok));
            for (var cage = colony.FirstEmptyCage(); cage != null; cage = colony.FirstEmptyCage())
            {
                colony.AddAnimal(new PetState { Name = "満室" }, cage);
            }

            colony.Wallet.Money = 0;
            var offer = colony.Events.Entry.BoothOffers[0];

            Assert.That(service.CheckBuy(colony, offer.OfferId), Is.EqualTo(EventBuyResult.NoEmptyCage));
            Assert.That(service.Buy(colony, offer.OfferId, Day(calendar)), Is.EqualTo(EventBuyResult.NoEmptyCage));
            Assert.That(offer.Bought, Is.False);

            colony.AddCage(CageSize.Standard);
            Assert.That(service.CheckBuy(colony, offer.OfferId), Is.EqualTo(EventBuyResult.NotEnoughMoney));
        }

        [Test]
        public void LastServe_FinishesWhenNothingIsLeftToBuy()
        {
            var colony = Entered(3, AtBase, out var calendar);
            for (var i = 0; i < 8; i++)
            {
                Assert.That(service.PlayAction(colony, calendar, EventActionKind.Serve, Day(calendar), out _), Is.EqualTo(EventActionResult.Ok));
            }

            var summary = colony.Events.LastSummary;
            Assert.That(colony.Events.Entry, Is.Null);
            Assert.That(summary.Number, Is.EqualTo(0));
            Assert.That(summary.BoothFee, Is.EqualTo(10000));
            Assert.That(summary.Sold.Count + summary.Unsold.Count, Is.EqualTo(3));
            Assert.That(colony.Events.LastSummarySeen, Is.False);
            foreach (var line in summary.Unsold)
            {
                Assert.That(colony.AnimalById(line.AnimalId), Is.Not.Null);
                Assert.That(colony.Events.IsListed(line.AnimalId), Is.False);
            }

            Assert.That(service.PlayAction(colony, calendar, EventActionKind.Serve, Day(calendar), out _), Is.EqualTo(EventActionResult.NoEvent));
            Assert.That(service.OpenNumber(colony, calendar, Day(calendar)), Is.EqualTo(1));

            service.DismissSummary(colony);
            Assert.That(colony.Events.LastSummarySeen, Is.True);
        }

        [Test]
        public void LastAction_WithUnboughtOffers_WaitsForFinish()
        {
            var colony = Entered(3, AtBase, out var calendar);
            Assert.That(service.Finish(colony), Is.False);
            for (var i = 0; i < 7; i++)
            {
                Assert.That(service.PlayAction(colony, calendar, EventActionKind.Serve, Day(calendar), out _), Is.EqualTo(EventActionResult.Ok));
            }

            Assert.That(service.PlayAction(colony, calendar, EventActionKind.Browse, Day(calendar), out _), Is.EqualTo(EventActionResult.Ok));

            Assert.That(colony.Events.Entry, Is.Not.Null);
            Assert.That(service.ActionsLeft(colony), Is.EqualTo(0));
            Assert.That(service.PlayAction(colony, calendar, EventActionKind.Serve, Day(calendar), out _), Is.EqualTo(EventActionResult.NoActionsLeft));
            Assert.That(service.Finish(colony), Is.True);
            Assert.That(colony.Events.Entry, Is.Null);
            Assert.That(colony.Events.LastSummary.Bought, Is.Empty);
        }

        [Test]
        public void BuyingTheLastOffer_AfterTheActions_Finishes()
        {
            var colony = Entered(1, Nothing, out var calendar);
            colony.AddCage(CageSize.Standard);
            colony.AddCage(CageSize.Standard);
            colony.AddCage(CageSize.Standard);
            for (var i = 0; i < 7; i++)
            {
                Assert.That(service.PlayAction(colony, calendar, EventActionKind.Serve, Day(calendar), out _), Is.EqualTo(EventActionResult.Ok));
            }

            Assert.That(service.PlayAction(colony, calendar, EventActionKind.Browse, Day(calendar), out _), Is.EqualTo(EventActionResult.Ok));
            var offers = new List<EventBoothOffer>(colony.Events.Entry.BoothOffers);
            foreach (var offer in offers)
            {
                Assert.That(service.Buy(colony, offer.OfferId, Day(calendar)), Is.EqualTo(EventBuyResult.Ok));
            }

            Assert.That(colony.Events.Entry, Is.Null);
            Assert.That(colony.Events.LastSummary.Bought.Count, Is.EqualTo(offers.Count));
        }

        [Test]
        public void ServeAllRemaining_PlaysTheRestAsServesAndFinishes()
        {
            var colony = Entered(3, AtBase, out var calendar);
            Assert.That(service.PlayAction(colony, calendar, EventActionKind.Browse, Day(calendar), out _), Is.EqualTo(EventActionResult.Ok));

            Assert.That(service.ServeAllRemaining(colony, calendar, Day(calendar), out var records), Is.EqualTo(EventActionResult.Ok));

            Assert.That(records.Count, Is.EqualTo(7));
            foreach (var record in records)
            {
                Assert.That(record.Kind, Is.EqualTo(EventActionKind.Serve));
            }

            Assert.That(colony.Events.Entry, Is.Null);
            Assert.That(colony.Events.LastSummary, Is.Not.Null);
        }
    }
}
