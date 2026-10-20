using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>One animal put up at the event (phase 6 spec §2.2). The frozen values are set at 0:00 on the day.</summary>
    public sealed class EventListing
    {
        public int AnimalId { get; set; }
        public long Price { get; set; }
        public int TolerancePercent { get; set; }
        public long FrozenBase { get; set; }
        public double FrozenChance { get; set; }

        /// <summary>The action it sold in, or -1 while unsold.</summary>
        public int SoldInAction { get; set; } = -1;

        public bool Sold => SoldInAction >= 0;
    }

    /// <summary>One animal in a log or the summary: sold, unsold or bought. Name and look are copied when it happens.</summary>
    public sealed class EventAnimalLine
    {
        public int AnimalId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string MorphLabel { get; set; } = string.Empty;
        public long Amount { get; set; }
        public int MinuteOfDay { get; set; }
    }

    /// <summary>An animal another booth showed (spec §2.5). Animal is null once bought.</summary>
    public sealed class EventBoothOffer
    {
        public int OfferId { get; set; }
        public int ActionIndex { get; set; }
        public PetState Animal { get; set; }
        public string MorphLabel { get; set; } = string.Empty;
        public long Price { get; set; }
        public bool Rare { get; set; }
        public bool Bought { get; set; }
    }

    public sealed class EventActionRecord
    {
        public int Index { get; set; }
        public EventActionKind Kind { get; set; }
        public DateTimeOffset PlayedAtUtc { get; set; }

        /// <summary>When the visited booth shows up in the log (browse only).</summary>
        public int BoothMinuteOfDay { get; set; }

        public List<EventAnimalLine> Sales { get; set; } = new List<EventAnimalLine>();
    }

    /// <summary>The one event the player has entered and not finished yet.</summary>
    public sealed class EventEntry
    {
        public int Number { get; set; }
        public long FeePaid { get; set; }
        public bool Frozen { get; set; }

        /// <summary>Fixed at 0:00 from the event number and the listings (spec §2.7).</summary>
        public int ListingSeed { get; set; }

        public int NextOfferId { get; set; } = 1;
        public List<EventListing> Listings { get; set; } = new List<EventListing>();
        public List<EventActionRecord> Actions { get; set; } = new List<EventActionRecord>();
        public List<EventBoothOffer> BoothOffers { get; set; } = new List<EventBoothOffer>();
        public List<EventAnimalLine> Purchases { get; set; } = new List<EventAnimalLine>();

        public EventListing ListingOf(int animalId) => Listings.Find(l => l.AnimalId == animalId);
    }

    /// <summary>The last finished event's result (spec §2.6).</summary>
    public sealed class EventSummary
    {
        public int Number { get; set; }
        public long BoothFee { get; set; }
        public List<EventAnimalLine> Sold { get; set; } = new List<EventAnimalLine>();
        public List<EventAnimalLine> Unsold { get; set; } = new List<EventAnimalLine>();
        public List<EventAnimalLine> Bought { get; set; } = new List<EventAnimalLine>();

        public long TotalSales => Sum(Sold);

        public long PurchaseTotal => Sum(Bought);

        public long Net => TotalSales - PurchaseTotal - BoothFee;

        private static long Sum(List<EventAnimalLine> lines)
        {
            var total = 0L;
            foreach (var line in lines)
            {
                total += line.Amount;
            }

            return total;
        }
    }

    public sealed class EventState
    {
        public EventEntry Entry { get; set; }
        public EventSummary LastSummary { get; set; }

        /// <summary>False from the finish until the player closes the result screen.</summary>
        public bool LastSummarySeen { get; set; } = true;

        /// <summary>The event the deadline notices are counting for, and how far they got (0 none, 1 three days, 2 the day before).</summary>
        public int NoticeNumber { get; set; } = -1;

        public int NoticeStage { get; set; }

        public bool IsListed(int animalId)
        {
            var listing = Entry?.ListingOf(animalId);
            return listing != null && !listing.Sold;
        }
    }
}
