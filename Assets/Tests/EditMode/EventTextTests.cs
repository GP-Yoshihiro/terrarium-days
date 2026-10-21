using System.Collections.Generic;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.UI;

namespace TerrariumDays.Tests
{
    public sealed class EventTextTests
    {
        private const string Spring = "ジャパン・レプタイルズ・フェスタ（春）";
        private const string Marche = "レプタイル・マルシェ";

        private static EventAnimalLine Sale(int minute, long amount, string label = "レイプター♀") =>
            new EventAnimalLine { AnimalId = minute, Name = "ハナ", MorphLabel = label, Amount = amount, MinuteOfDay = minute };

        [Test]
        public void Yen_UsesTheEn()
        {
            Assert.That(EventText.Yen(28500), Is.EqualTo("28,500円"));
            Assert.That(EventText.Yen(0), Is.EqualTo("0円"));
            Assert.That(EventText.Yen(1234567), Is.EqualTo("1,234,567円"));
        }

        [Test]
        public void SignedYen_ShowsThePlusOrMinus()
        {
            Assert.That(EventText.SignedYen(12000), Is.EqualTo("+12,000円"));
            Assert.That(EventText.SignedYen(0), Is.EqualTo("0円"));
            Assert.That(EventText.SignedYen(-3000), Is.EqualTo("-3,000円"));
        }

        [Test]
        public void Clock_IsHoursAndMinutes()
        {
            Assert.That(EventText.Clock(600), Is.EqualTo("10:00"));
            Assert.That(EventText.Clock(684), Is.EqualTo("11:24"));
            Assert.That(EventText.Clock(1019), Is.EqualTo("16:59"));
        }

        [Test]
        public void Notices_MatchThePlannedWording()
        {
            Assert.That(EventText.DeadlineNotice(1, 3), Is.EqualTo(Marche + "（5月15日）の申し込みはあと3日で締め切りです"));
            Assert.That(EventText.DeadlineNotice(0, 1), Is.EqualTo(Spring + "の申し込みは今日までです"));
            Assert.That(EventText.DayStartedNotice(0), Is.EqualTo("今日は" + Spring + "です。イベントタブで遊べます"));
            Assert.That(EventText.PendingNotice(0), Is.EqualTo(Spring + "の続きがあります（イベントタブ）"));
            Assert.That(EventText.WithdrawnNotice(new[] { "ハナ", "タロウ" }), Is.EqualTo("出品を取り消しました：ハナ、タロウ"));
        }

        [Test]
        public void NoticesOf_ListsTheReportInAFixedOrder()
        {
            var report = new EventReport { DeadlineNoticeNumber = 1, DeadlineDaysLeft = 3, DayStartedNumber = 0, PendingNumber = 0 };
            report.Withdrawn.Add("ハナ");

            Assert.That(EventText.NoticesOf(report), Is.EqualTo(new[]
            {
                "出品を取り消しました：ハナ",
                "今日は" + Spring + "です。イベントタブで遊べます",
                Spring + "の続きがあります（イベントタブ）",
                Marche + "（5月15日）の申し込みはあと3日で締め切りです",
            }));
            Assert.That(EventText.NoticesOf(new EventReport()), Is.Empty);
        }

        [Test]
        public void LogLines_MatchTheSpecExample()
        {
            Assert.That(EventText.SaleLine(Sale(684, 28500)), Is.EqualTo("11:24 レイプター♀ 売約（28,500円）"));
            Assert.That(EventText.BoothLine(705, 2), Is.EqualTo("11:45 他のブースで売り物を2匹見つけた"));
            Assert.That(EventText.NoSaleLine(652), Is.EqualTo("10:52 売約はなかった"));
            Assert.That(EventText.OfferLine(new EventBoothOffer { MorphLabel = "ハイポ?", Price = 8000 }), Is.EqualTo("ハイポ? 8,000円"));
            Assert.That(EventText.OfferLine(new EventBoothOffer { MorphLabel = "マックスノー ベル♂", Price = 120000, Rare = true }),
                Is.EqualTo("掘り出し物！ マックスノー ベル♂ 120,000円"));
        }

        [Test]
        public void Labels_ForThePreparationScreen()
        {
            Assert.That(EventText.Chance(0.754d), Is.EqualTo("売れる見込み 75%"));
            Assert.That(EventText.Tolerance(0), Is.EqualTo("値引きしない"));
            Assert.That(EventText.Tolerance(15), Is.EqualTo("15%まで値引き"));
            Assert.That(EventText.ExpectedTotal(12345.6d), Is.EqualTo("期待値の合計 12,346円"));
            Assert.That(EventText.ActionsLeft(3, 8), Is.EqualTo("残りの行動 3/8"));
            Assert.That(EventText.Listings(2, 5), Is.EqualTo("出品 2/5匹"));
        }

        [Test]
        public void ListingReason_NamesEachProblem()
        {
            Assert.That(EventText.ListingReason(ListingProblem.None), Is.EqualTo(""));
            Assert.That(EventText.ListingReason(ListingProblem.NotFound), Is.EqualTo(""));
            Assert.That(EventText.ListingReason(ListingProblem.InTray), Is.EqualTo("トレイ"));
            Assert.That(EventText.ListingReason(ListingProblem.Gravid), Is.EqualTo("抱卵中"));
            Assert.That(EventText.ListingReason(ListingProblem.Pairing), Is.EqualTo("ペアリング中"));
            Assert.That(EventText.ListingReason(ListingProblem.Weak), Is.EqualTo("衰弱中"));
        }

        [Test]
        public void FailureMessages_CoverEveryRefusal()
        {
            foreach (EventRegisterResult result in System.Enum.GetValues(typeof(EventRegisterResult)))
            {
                Assert.That(EventText.RegisterFailure(result), result == EventRegisterResult.Ok ? Is.Empty : Is.Not.Empty, result.ToString());
            }

            foreach (EventBuyResult result in System.Enum.GetValues(typeof(EventBuyResult)))
            {
                Assert.That(EventText.BuyFailure(result), result == EventBuyResult.Ok ? Is.Empty : Is.Not.Empty, result.ToString());
            }

            Assert.That(EventText.BuyFailure(EventBuyResult.NoEmptyCage), Is.EqualTo("ケージが足りません"));
            Assert.That(EventText.BuyFailure(EventBuyResult.NotEnoughMoney), Is.EqualTo("所持金が足りません"));
            Assert.That(EventText.RegisterFailure(EventRegisterResult.NotEnoughMoney), Is.EqualTo("出店料が足りません"));
        }

        [Test]
        public void Confirmations()
        {
            Assert.That(EventText.ConfirmRegister(0), Is.EqualTo(Spring + "に申し込みますか？（出店料 10,000円・取り消せません）"));
            Assert.That(EventText.ConfirmRegister(0, 0), Does.StartWith("出品する動物が0匹です").And.EndsWith("取り消せません）"));
            Assert.That(EventText.ConfirmBuy(new EventBoothOffer { MorphLabel = "ハイポ?", Price = 8000 }), Is.EqualTo("ハイポ?を8,000円で買いますか？"));
        }

        [Test]
        public void Duration_GrowsWithTheEventsUpToFifteenSeconds()
        {
            Assert.That(EventReplay.DurationSeconds(0), Is.EqualTo(10d));
            Assert.That(EventReplay.DurationSeconds(2), Is.EqualTo(13d));
            Assert.That(EventReplay.DurationSeconds(9), Is.EqualTo(15d));
        }

        [Test]
        public void Script_ServeWithSales_ListsThemInTimeOrder()
        {
            var record = new EventActionRecord { Index = 1, Kind = EventActionKind.Serve };
            record.Sales.Add(Sale(700, 9000));
            record.Sales.Add(Sale(720, 28500, "ハイポ♂"));

            var lines = EventReplay.Script(record, new List<EventBoothOffer>(), 8);

            Assert.That(lines.Count, Is.EqualTo(3));
            Assert.That(lines[0].Text, Is.EqualTo("10:52 接客を始めた"));
            Assert.That(lines[0].Highlight, Is.False);
            Assert.That(lines[1].Text, Is.EqualTo("11:40 レイプター♀ 売約（9,000円）"));
            Assert.That(lines[1].Bubble, Is.EqualTo(EventReplay.SaleBubble));
            Assert.That(lines[1].Highlight, Is.True);
            Assert.That(lines[2].MinuteOfDay, Is.EqualTo(720));
            Assert.That(EventReplay.EventCount(record, new List<EventBoothOffer>()), Is.EqualTo(2));
        }

        [Test]
        public void Script_ServeWithoutSales_SaysSo()
        {
            var record = new EventActionRecord { Index = 0, Kind = EventActionKind.Serve };

            var lines = EventReplay.Script(record, new List<EventBoothOffer>(), 8);

            Assert.That(lines.Count, Is.EqualTo(2));
            Assert.That(lines[1].Text, Is.EqualTo("10:51 売約はなかった"));
        }

        [Test]
        public void Script_Browse_ShowsOnlyThisActionsOffers()
        {
            var record = new EventActionRecord { Index = 2, Kind = EventActionKind.Browse, BoothMinuteOfDay = 731 };
            record.Sales.Add(Sale(710, 9000));
            var offers = new List<EventBoothOffer>
            {
                new EventBoothOffer { OfferId = 1, ActionIndex = 0, MorphLabel = "ハイポ?", Price = 8000 },
                new EventBoothOffer { OfferId = 2, ActionIndex = 2, MorphLabel = "ノーマル♀", Price = 6000 },
                new EventBoothOffer { OfferId = 3, ActionIndex = 2, MorphLabel = "マックスノー ベル♂", Price = 120000, Rare = true },
            };

            var lines = EventReplay.Script(record, offers, 8);

            Assert.That(lines.Count, Is.EqualTo(5));
            Assert.That(lines[0].Text, Is.EqualTo("11:45 他のブースを見に行った"));
            Assert.That(lines[1].Text, Is.EqualTo("11:50 レイプター♀ 売約（9,000円）"));
            Assert.That(lines[2].Text, Is.EqualTo("12:11 他のブースで売り物を2匹見つけた"));
            Assert.That(lines[3].Text, Is.EqualTo("ノーマル♀ 6,000円"));
            Assert.That(lines[3].OfferId, Is.EqualTo(2));
            Assert.That(lines[4].Highlight, Is.True);
            Assert.That(lines[4].Bubble, Is.EqualTo(EventReplay.RareBubble));
            Assert.That(EventReplay.EventCount(record, offers), Is.EqualTo(3));
        }

        [Test]
        public void RevealedCount_ShowsTheFirstLineAtOnceAndAllAtTheEnd()
        {
            Assert.That(EventReplay.RevealedCount(4, -1d, 12d), Is.EqualTo(0));
            Assert.That(EventReplay.RevealedCount(4, 0d, 12d), Is.EqualTo(1));
            Assert.That(EventReplay.RevealedCount(4, 2.9d, 12d), Is.EqualTo(1));
            Assert.That(EventReplay.RevealedCount(4, 3d, 12d), Is.EqualTo(2));
            Assert.That(EventReplay.RevealedCount(4, 11.9d, 12d), Is.EqualTo(4));
            Assert.That(EventReplay.RevealedCount(4, 99d, 12d), Is.EqualTo(4));
            Assert.That(EventReplay.RevealedCount(0, 5d, 12d), Is.EqualTo(0));
        }

        [Test]
        public void EachBubbleLineMapsToItsSpeaker_OurGreetingAndBrowsingAreTheClerksOthersTheCustomers()
        {
            Assert.That(EventReplay.SpeakerOf(EventReplay.ServeBubble), Is.EqualTo(ReplaySpeaker.Clerk));
            Assert.That(EventReplay.SpeakerOf(EventReplay.BrowseBubble), Is.EqualTo(ReplaySpeaker.Clerk));
            Assert.That(EventReplay.SpeakerOf(EventReplay.SaleBubble), Is.EqualTo(ReplaySpeaker.Customer));
            Assert.That(EventReplay.SpeakerOf(EventReplay.RareBubble), Is.EqualTo(ReplaySpeaker.Customer));
            Assert.That(new ReplayLine { Bubble = EventReplay.ServeBubble }.Speaker, Is.EqualTo(ReplaySpeaker.Clerk));
        }
    }
}
