using System;
using System.Collections.Generic;
using System.Globalization;
using TerrariumDays.Core;

namespace TerrariumDays.UI
{
    /// <summary>The event tab's words (phase 6 spec §2.6・§3, planner ★9・★12・★15・★16). Money is "28,500円" here only.</summary>
    public static class EventText
    {
        public const string ListedStatus = "出品中";

        public static string Yen(long yen) => yen.ToString("#,0", CultureInfo.InvariantCulture) + "円";

        public static string SignedYen(long yen) => yen > 0 ? "+" + Yen(yen) : yen < 0 ? "-" + Yen(-yen) : Yen(0);

        public static string Clock(int minuteOfDay) => $"{minuteOfDay / 60}:{minuteOfDay % 60:00}";

        public static string DateLabel(int number) => $"{EventSchedule.MonthOf(number)}月{EventSchedule.EventDayOfMonth}日";

        public static string DeadlineNotice(int number, int daysLeft) =>
            daysLeft <= 1
                ? $"{EventSchedule.NameOf(number)}の申し込みは今日までです"
                : $"{EventSchedule.NameOf(number)}（{DateLabel(number)}）の申し込みはあと{daysLeft}日で締め切りです";

        public static string DayStartedNotice(int number) => $"今日は{EventSchedule.NameOf(number)}です。イベントタブで遊べます";

        public static string PendingNotice(int number) => $"{EventSchedule.NameOf(number)}の続きがあります（イベントタブ）";

        public static string WithdrawnNotice(IEnumerable<string> names) => "出品を取り消しました：" + string.Join("、", names);

        /// <summary>The notices of one report: withdrawn, the day began, unfinished, deadline. The caller folds them like phase 5 (first one + 「ほか○件」).</summary>
        public static List<string> NoticesOf(EventReport report)
        {
            var notices = new List<string>();
            if (report.Withdrawn.Count > 0)
            {
                notices.Add(WithdrawnNotice(report.Withdrawn));
            }

            if (report.DayStartedNumber >= 0)
            {
                notices.Add(DayStartedNotice(report.DayStartedNumber));
            }

            if (report.PendingNumber >= 0)
            {
                notices.Add(PendingNotice(report.PendingNumber));
            }

            if (report.DeadlineNoticeNumber >= 0)
            {
                notices.Add(DeadlineNotice(report.DeadlineNoticeNumber, report.DeadlineDaysLeft));
            }

            return notices;
        }

        public static string SaleLine(EventAnimalLine sale) => $"{Clock(sale.MinuteOfDay)} {sale.MorphLabel} 売約（{Yen(sale.Amount)}）";

        public static string NoSaleLine(int minuteOfDay) => $"{Clock(minuteOfDay)} 売約はなかった";

        public static string BoothLine(int minuteOfDay, int offerCount) => $"{Clock(minuteOfDay)} 他のブースで売り物を{offerCount}匹見つけた";

        public static string OfferLine(EventBoothOffer offer) => (offer.Rare ? "掘り出し物！ " : string.Empty) + $"{offer.MorphLabel} {Yen(offer.Price)}";

        /// <summary>A summary row: 「レイプター♀ ハナ 28,500円」.</summary>
        public static string AnimalLine(EventAnimalLine line) => $"{line.MorphLabel} {line.Name} {Yen(line.Amount)}";

        public static string Chance(double chance) => $"売れる見込み {Math.Round(chance * 100d, MidpointRounding.AwayFromZero):0}%";

        public static string Tolerance(int percent) => percent == 0 ? "値引きしない" : $"{percent}%まで値引き";

        public static string ExpectedTotal(double yen) => "期待値の合計 " + Yen((long)Math.Round(yen, MidpointRounding.AwayFromZero));

        public static string ActionsLeft(int left, int count) => $"残りの行動 {left}/{count}";

        public static string Listings(int count, int max) => $"出品 {count}/{max}匹";

        /// <summary>Why a listing is refused on the preparation screen. None and NotFound have no reason to show.</summary>
        public static string ListingReason(ListingProblem problem)
        {
            switch (problem)
            {
                case ListingProblem.InTray:
                    return "トレイ";
                case ListingProblem.Gravid:
                    return "抱卵中";
                case ListingProblem.Pairing:
                    return "ペアリング中";
                case ListingProblem.Weak:
                    return "衰弱中";
                default:
                    return string.Empty;
            }
        }

        public static string ConfirmRegister(int number) =>
            $"{EventSchedule.NameOf(number)}に申し込みますか？（出店料 {Yen(EventSchedule.BoothFee(EventSchedule.KindOf(number)))}・取り消せません）";

        public const string ConfirmServeAll = "残りの行動を全部接客にしますか？まだ買っていない売り物は買えなくなります";

        public static string ConfirmBuy(EventBoothOffer offer) => $"{offer.MorphLabel}を{Yen(offer.Price)}で買いますか？";

        public static string RegisterFailure(EventRegisterResult result)
        {
            switch (result)
            {
                case EventRegisterResult.NotOpen:
                    return "この回はもう受け付けていません";
                case EventRegisterResult.Pending:
                    return "前の回がまだ終わっていません";
                case EventRegisterResult.AlreadyRegistered:
                    return "もう申し込んでいます";
                case EventRegisterResult.TooMany:
                    return "出品できる数を超えています";
                case EventRegisterResult.NotEligible:
                    return "出品できない個体が入っています";
                case EventRegisterResult.Duplicate:
                    return "同じ個体が2回入っています";
                case EventRegisterResult.InvalidPrice:
                    return "価格が低すぎます";
                case EventRegisterResult.InvalidTolerance:
                    return "値引きの幅が正しくありません";
                case EventRegisterResult.NotEnoughMoney:
                    return "出店料が足りません";
                case EventRegisterResult.Frozen:
                    return "開催日になったので出品は変えられません";
                default:
                    return string.Empty;
            }
        }

        public static string BuyFailure(EventBuyResult result)
        {
            switch (result)
            {
                case EventBuyResult.NoEvent:
                    return "イベントは終わりました";
                case EventBuyResult.NotFound:
                    return "売り物が見つかりません";
                case EventBuyResult.AlreadyBought:
                    return "もう買いました";
                case EventBuyResult.NotEnoughMoney:
                    return "所持金が足りません";
                case EventBuyResult.NoEmptyCage:
                    return "ケージが足りません";
                default:
                    return string.Empty;
            }
        }
    }
}
