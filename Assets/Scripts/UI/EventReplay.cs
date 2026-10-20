using System;
using System.Collections.Generic;
using System.Linq;
using TerrariumDays.Core;

namespace TerrariumDays.UI
{
    /// <summary>One line of the replay log. Offer lines carry the offer to buy (OfferId &gt; 0).</summary>
    public sealed class ReplayLine
    {
        public int MinuteOfDay { get; set; }
        public string Text { get; set; }

        /// <summary>The visitor's speech bubble, or null.</summary>
        public string Bubble { get; set; }

        public bool Highlight { get; set; }
        public int OfferId { get; set; }
    }

    /// <summary>
    /// The 10–15 s replay of one action (★K・★12). It only shows results that are already decided,
    /// so skipping or closing the app never changes anything.
    /// </summary>
    public static class EventReplay
    {
        public const double BaseSeconds = 10d;
        public const double SecondsPerEvent = 1.5d;
        public const double MaxSeconds = 15d;
        public const string ServeBubble = "いらっしゃいませ！";
        public const string BrowseBubble = "どんな子がいるかな";
        public const string SaleBubble = "この子にします！";
        public const string RareBubble = "掘り出し物！";

        public static double DurationSeconds(int events) => Math.Min(MaxSeconds, BaseSeconds + SecondsPerEvent * Math.Max(0, events));

        /// <summary>Sales plus the offers this action found.</summary>
        public static int EventCount(EventActionRecord record, IList<EventBoothOffer> offers) =>
            record.Sales.Count + OffersOf(record, offers).Count;

        public static List<ReplayLine> Script(EventActionRecord record, IList<EventBoothOffer> offers, int actionCount)
        {
            var start = EventSchedule.ActionWindowStart(record.Index, actionCount);
            var length = EventSchedule.ActionWindowLength(actionCount);
            var browse = record.Kind == EventActionKind.Browse;
            var lines = new List<ReplayLine>
            {
                new ReplayLine
                {
                    MinuteOfDay = start,
                    Text = $"{EventText.Clock(start)} {(browse ? "他のブースを見に行った" : "接客を始めた")}",
                    Bubble = browse ? BrowseBubble : ServeBubble,
                },
            };

            var timed = new List<ReplayLine>();
            foreach (var sale in record.Sales)
            {
                timed.Add(new ReplayLine { MinuteOfDay = sale.MinuteOfDay, Text = EventText.SaleLine(sale), Bubble = SaleBubble, Highlight = true });
            }

            if (browse)
            {
                var found = OffersOf(record, offers);
                timed.Add(new ReplayLine { MinuteOfDay = record.BoothMinuteOfDay, Text = EventText.BoothLine(record.BoothMinuteOfDay, found.Count) });
                foreach (var offer in found)
                {
                    timed.Add(new ReplayLine
                    {
                        MinuteOfDay = record.BoothMinuteOfDay,
                        Text = EventText.OfferLine(offer),
                        Bubble = offer.Rare ? RareBubble : null,
                        Highlight = offer.Rare,
                        OfferId = offer.OfferId,
                    });
                }
            }
            else if (record.Sales.Count == 0)
            {
                timed.Add(new ReplayLine { MinuteOfDay = start + length - 1, Text = EventText.NoSaleLine(start + length - 1) });
            }

            // OrderBy is stable: the booth line stays before its offers.
            lines.AddRange(timed.OrderBy(l => l.MinuteOfDay));
            return lines;
        }

        /// <summary>Line i appears at duration·i/count; everything is shown once the time is up (or on 「飛ばす」).</summary>
        public static int RevealedCount(int lineCount, double elapsedSeconds, double durationSeconds)
        {
            if (lineCount <= 0 || elapsedSeconds < 0d)
            {
                return 0;
            }

            if (elapsedSeconds >= durationSeconds || durationSeconds <= 0d)
            {
                return lineCount;
            }

            return Math.Min(lineCount, (int)Math.Floor(elapsedSeconds * lineCount / durationSeconds) + 1);
        }

        private static List<EventBoothOffer> OffersOf(EventActionRecord record, IList<EventBoothOffer> offers)
        {
            var found = new List<EventBoothOffer>();
            if (record.Kind != EventActionKind.Browse || offers == null)
            {
                return found;
            }

            foreach (var offer in offers)
            {
                if (offer.ActionIndex == record.Index)
                {
                    found.Add(offer);
                }
            }

            return found;
        }
    }
}
