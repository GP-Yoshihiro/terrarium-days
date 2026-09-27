using System;
using System.Collections.Generic;
using System.Globalization;
using TerrariumDays.Core;

namespace TerrariumDays.UI
{
    /// <summary>Every breeding and weakness text on screen (§7, §5.5). Pure, so it is EditMode tested.</summary>
    public static class BreedingText
    {
        public const string WeakStatus = "衰弱中（繁殖できません）";
        public const string NestBoxPlacedMessage = "産卵床を置きました";
        public const string NestBoxRemovedMessage = "産卵床を外して所持品に戻しました";
        public const string HatchComesLaterLine = "孵化は段階5（孵卵器）で追加されます";

        public static string ProblemLabel(PairingProblem problem)
        {
            switch (problem)
            {
                case PairingProblem.NotFound:
                    return "個体が見つかりません";
                case PairingProblem.OutOfSeason:
                    return "繁殖期ではありません";
                case PairingProblem.NotMaleAndFemale:
                    return "オスとメスを1匹ずつ選んでください";
                case PairingProblem.SexUnknown:
                    return "性別がまだ分かりません";
                case PairingProblem.Weak:
                    return "衰弱中です";
                case PairingProblem.TooYoung:
                    return "まだ若すぎます";
                case PairingProblem.TooLight:
                    return "体重が足りません";
                case PairingProblem.Gravid:
                    return "抱卵中です";
                case PairingProblem.AlreadyPairing:
                    return "ペアリング中です";
                default:
                    return string.Empty;
            }
        }

        public static string Requirement(Sex role, CareTuning care) => role == Sex.Female
            ? $"メス：生後{Whole(care.FemaleBreedingMinAgeMonths)}か月以上・{Whole(care.FemaleBreedingMinWeightGrams)}g以上"
            : $"オス：生後{Whole(care.MaleBreedingMinAgeMonths)}か月以上・{Whole(care.MaleBreedingMinWeightGrams)}g以上";

        public static string SeasonLine(GameDate today, CareTuning care) => BreedingRules.IsBreedingSeason(today, care)
            ? $"いまは繁殖期です（{care.BreedingSeasonFirstMonth}〜{care.BreedingSeasonLastMonth}月）"
            : $"繁殖期は{care.BreedingSeasonFirstMonth}〜{care.BreedingSeasonLastMonth}月です（いまは{today.Month}月）";

        public static string CompatibilityLabel(BreedingForecast forecast)
        {
            if (!forecast.CompatibilityKnown)
            {
                return "不明（性格がまだ分かりません）";
            }

            switch (forecast.Compatibility)
            {
                case Compatibility.Good:
                    return "良い";
                case Compatibility.Bad:
                    return "悪い";
                default:
                    return "普通";
            }
        }

        /// <summary>A probability as a whole percent; tiny non-zero chances read "1%未満".</summary>
        public static string Percent(double probability)
        {
            if (probability > 0d && probability < 0.005d)
            {
                return "1%未満";
            }

            return Whole(probability * 100d) + "%";
        }

        public static List<string> ForecastLines(BreedingForecast forecast)
        {
            var lines = new List<string>
            {
                $"相性：{CompatibilityLabel(forecast)}",
                $"交尾の成功率：約{Percent(forecast.SuccessChance)}",
                $"産卵：{forecast.MinClutches}〜{forecast.MaxClutches}回（1回に2個、ときどき1個）",
                "子の見込み（分かっている遺伝から）：",
            };
            foreach (var odds in forecast.Offspring)
            {
                lines.Add($"・{odds.Name} {Percent(odds.Probability)}");
            }

            return lines;
        }

        public static string ConfirmPairing(PetState male, PetState female, CareTuning care) =>
            $"{female.Name}を{male.Name}のケージに入れて、ゲーム内{Whole(care.PairingGameDays)}日間ペアリングしますか？";

        public static string CandidateLine(PetState pet, DateTimeOffset nowUtc) =>
            $"{pet.Name}　{pet.WeightGrams.ToString("0.0", CultureInfo.InvariantCulture)}g・生後{(int)Math.Floor(GrowthModel.AgeMonths(pet, nowUtc))}か月";

        /// <summary>Whole game days left, rounded up ("あと3日"); never negative.</summary>
        public static int DaysLeft(DateTimeOffset untilUtc, DateTimeOffset nowUtc) =>
            Math.Max(0, (int)Math.Ceiling(GameCalendar.GameDaysBetween(nowUtc, untilUtc) - 1e-9));

        public static string PairingLine(Pairing pairing, PetState male, PetState female, DateTimeOffset nowUtc) =>
            $"{female.Name}が{male.Name}のケージを訪問中（あと{DaysLeft(pairing.EndsAtUtc, nowUtc)}日）";

        public static string VisitorLabel(PetState visitor, Pairing pairing, DateTimeOffset nowUtc) =>
            $"訪問中：{visitor.Name}（あと{DaysLeft(pairing.EndsAtUtc, nowUtc)}日）";

        public static string GravidStatus(GravidState gravid, GameCalendar calendar)
        {
            var date = calendar.DateAt(gravid.NextClutchAtUtc);
            return $"抱卵中（{gravid.ClutchesLaid + 1}回目の産卵は{date.Month}月{date.Day}日ごろ）";
        }

        public static string GravidLine(PetState female, GameCalendar calendar) =>
            $"{female.Name}：{GravidStatus(female.Gravid, calendar)}";

        /// <summary>The egg lines for one cage (simple status only; candling is phase 5).</summary>
        public static List<string> EggLines(IList<Egg> eggsInCage, RoomClimate room, DateTimeOffset nowUtc, CareTuning care)
        {
            var lines = new List<string>();
            var nest = 0;
            var hatchReady = false;
            var loose = 0;
            var firstLooseLaid = DateTimeOffset.MaxValue;
            foreach (var egg in eggsInCage)
            {
                if (egg.Place == EggPlace.NestBox)
                {
                    nest++;
                    hatchReady |= egg.DevelopmentPercent >= 100d;
                }
                else
                {
                    loose++;
                    if (egg.LaidAtUtc < firstLooseLaid)
                    {
                        firstLooseLaid = egg.LaidAtUtc;
                    }
                }
            }

            if (nest > 0)
            {
                var temperature = RoomTemperature.Label(room.TemperatureC, room.Measured);
                lines.Add(room.TemperatureC < EggDevelopment.MinDevelopingC
                    ? $"産卵床に卵が{nest}個あります。{temperature}では寒くて発生が止まっています"
                    : $"産卵床に卵が{nest}個あります。{temperature}で発生中");
                if (hatchReady)
                {
                    lines.Add(HatchComesLaterLine);
                }
            }

            if (loose > 0)
            {
                var dryAt = firstLooseLaid + GameCalendar.RealTimeFor(care.LooseEggDryGameDays);
                var minutes = Math.Max(0, (int)Math.Ceiling((dryAt - nowUtc).TotalMinutes - 1e-9));
                lines.Add($"産卵床の外に卵が{loose}個（あと約{minutes}分で乾いてしまいます）");
            }

            return lines;
        }

        /// <summary>Breeding alerts for a home/list thumbnail, most important first.</summary>
        public static List<string> Alerts(Colony colony, Cage cage, PetState shown)
        {
            var alerts = new List<string>();
            if (shown != null && shown.Weak)
            {
                alerts.Add("衰弱");
            }

            if (colony.VisitorIn(cage) != null)
            {
                alerts.Add("ペアリング中");
            }

            if (shown != null && shown.Gravid != null)
            {
                alerts.Add("抱卵中");
                if (!cage.HasNestBox)
                {
                    alerts.Add("産卵床なし");
                }
            }

            if (colony.EggsIn(cage).Count > 0)
            {
                alerts.Add("卵あり");
            }

            return alerts;
        }

        /// <summary>The breeding lines on the cage detail screen: weakness, visitor, gravid, eggs.</summary>
        public static List<string> CageDetailLines(Colony colony, Cage cage, PetState shown, GameCalendar calendar, RoomClimate room,
            DateTimeOffset nowUtc, CareTuning care)
        {
            var lines = new List<string>();
            if (shown != null && shown.Weak)
            {
                lines.Add(WeakStatus);
            }

            var visitor = colony.VisitorIn(cage);
            var pairing = colony.PairingOf(visitor);
            if (visitor != null && pairing != null)
            {
                lines.Add(VisitorLabel(visitor, pairing, nowUtc));
            }

            if (shown != null && shown.Gravid != null)
            {
                lines.Add(GravidStatus(shown.Gravid, calendar) + (cage.HasNestBox ? string.Empty : "・産卵床がありません"));
            }

            lines.AddRange(EggLines(colony.EggsIn(cage), room, nowUtc, care));
            return lines;
        }

        public static List<string> Messages(BreedingReport report, CareTuning care)
        {
            var messages = new List<string>();
            foreach (var (female, _) in report.PairingsSucceeded)
            {
                messages.Add($"ペアリング成功！{female.Name}が抱卵しました（産卵はゲーム内3〜4週間後から）");
            }

            foreach (var (female, male) in report.PairingsFailed)
            {
                messages.Add($"{female.Name}と{male.Name}のペアリングはうまくいきませんでした");
            }

            foreach (var (female, male, reason) in report.PairingsCancelled)
            {
                messages.Add(reason == BreedingEnd.Weak
                    ? $"衰弱したため、{female.Name}と{male.Name}のペアリングを中止しました"
                    : $"繁殖期が終わったため、{female.Name}と{male.Name}のペアリングは実りませんでした");
            }

            foreach (var clutch in report.Clutches)
            {
                messages.Add(clutch.InNestBox
                    ? $"{clutch.Female.Name}が産卵床に卵を{clutch.EggCount}個産みました"
                    : $"{clutch.Female.Name}が卵を{clutch.EggCount}個産みました。産卵床がないので、ゲーム内{Whole(care.LooseEggDryGameDays)}日で乾いてしまいます");
            }

            foreach (var (female, reason) in report.GravidEnded)
            {
                messages.Add(GravidEndMessage(female, reason, care));
            }

            if (report.EggsDried.Count > 0)
            {
                messages.Add($"産卵床がなかったため、卵{report.EggsDried.Count}個が乾いてだめになりました");
            }

            return messages;
        }

        public static string GravidEndMessage(PetState female, BreedingEnd reason, CareTuning care)
        {
            switch (reason)
            {
                case BreedingEnd.AllClutchesLaid:
                    return $"{female.Name}の今シーズンの産卵が終わりました";
                case BreedingEnd.TooThin:
                    return $"{female.Name}は体重が{Whole(care.LayingStopWeightGrams)}gを下回ったので、今シーズンの産卵を終えました";
                case BreedingEnd.Weak:
                    return $"{female.Name}は衰弱したので、抱卵が終わりました";
                default:
                    return $"繁殖期が終わり、{female.Name}の抱卵は終わりました";
            }
        }

        public static string WeakStartedMessage(PetState pet, CareTuning care) =>
            $"{pet.Name}が衰弱しました（健康が{Whole(care.WeakRecoveryHealth)}に戻るまで繁殖できません）";

        public static string WeakRecoveredMessage(PetState pet) => $"{pet.Name}の衰弱が治りました";

        /// <summary>One feedback line: the first message, plus how many more there were.</summary>
        public static string Summary(IList<string> messages) =>
            messages.Count == 0 ? string.Empty
            : messages.Count == 1 ? messages[0]
            : $"{messages[0]}（ほか{messages.Count - 1}件）";

        public static string NestBoxButtonLabel(Cage cage, Inventory inventory) =>
            cage.HasNestBox ? "産卵床を外す" : $"産卵床を置く（所持{inventory.Count(ShopCatalog.NestBoxId)}）";

        public static string NestBoxMessage(NestBoxResult result)
        {
            switch (result)
            {
                case NestBoxResult.Ok:
                    return string.Empty;
                case NestBoxResult.NoneInInventory:
                    return "産卵床を持っていません（ショップの「用品」で買えます）";
                case NestBoxResult.EggsInside:
                    return "卵が入っているので外せません";
                case NestBoxResult.AlreadyPlaced:
                    return "産卵床はもう置いてあります";
                case NestBoxResult.NotPlaced:
                    return "産卵床は置いていません";
                default:
                    return "ケージが見つかりません";
            }
        }

        private static string Whole(double value) =>
            Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
    }
}
