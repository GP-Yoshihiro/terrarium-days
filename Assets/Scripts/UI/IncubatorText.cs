using System;
using System.Collections.Generic;
using System.Globalization;
using TerrariumDays.Core;

namespace TerrariumDays.UI
{
    /// <summary>Japanese text for the incubator tab, hatchling tray, family tree and notices (phase 5 §4).</summary>
    public static class IncubatorText
    {
        public const string NoEmptyCage = "空いているケージがありません";
        public const string TrayLocation = "孵化トレイ";
        public const string NeedsCare = "世話が必要です。ケージへ移してください";

        public static string ModelLabel(IncubatorModel model)
        {
            switch (model)
            {
                case IncubatorModel.Standard:
                    return "標準";
                case IncubatorModel.Luxury:
                    return "高級";
                default:
                    return "簡易";
            }
        }

        public static string IncubatorName(Incubator incubator) => $"孵卵器{incubator.Id}（{ModelLabel(incubator.Model)}）";

        public static string Occupancy(int count, int capacity) => $"{count}／{capacity}個";

        public static string SetTemperature(int celsius) => $"設定 {celsius}℃";

        public static string RealTemperature(double celsius) =>
            $"実温 {celsius.ToString("0.0", CultureInfo.InvariantCulture)}℃";

        public static string Humidity(double percent) =>
            $"湿度 {(int)Math.Round(percent, MidpointRounding.AwayFromZero)}%";

        public static bool IsHumidityWarning(double percent, CareTuning care) =>
            percent < care.HumidityDryBelowPercent || percent > care.HumidityMoldAbovePercent;

        public static string CandlingLabel(CandlingStage stage)
        {
            switch (stage)
            {
                case CandlingStage.Infertile:
                    return "無精卵";
                case CandlingStage.Early:
                    return "初期";
                case CandlingStage.Middle:
                    return "中期";
                case CandlingStage.Late:
                    return "後期";
                case CandlingStage.AboutToHatch:
                    return "孵化直前";
                case CandlingStage.Dead:
                    return "駄目になった卵";
                default:
                    return "まだ分からない";
            }
        }

        public static string FailureLabel(EggFailure failure)
        {
            switch (failure)
            {
                case EggFailure.Dried:
                    return "乾燥";
                case EggFailure.Mold:
                    return "カビ";
                case EggFailure.Heat:
                    return "高温";
                case EggFailure.Cold:
                    return "低温";
                default:
                    return string.Empty;
            }
        }

        /// <summary>The line under the card when an egg is tapped (★19).</summary>
        public static string EggDetail(Egg egg, CandlingStage stage, DateTimeOffset gameNowUtc)
        {
            var days = Math.Max(0, (int)Math.Floor(GameCalendar.GameDaysBetween(egg.LaidAtUtc, gameNowUtc)));
            string state;
            switch (stage)
            {
                case CandlingStage.Early:
                case CandlingStage.Middle:
                case CandlingStage.Late:
                case CandlingStage.AboutToHatch:
                    var percent = (int)Math.Floor(Math.Min(100d, egg.DevelopmentPercent));
                    state = $"{CandlingLabel(stage)}（発生{percent}%）";
                    break;
                case CandlingStage.Dead:
                    state = $"{CandlingLabel(stage)}（{FailureLabel(egg.Failure)}）";
                    break;
                default:
                    state = CandlingLabel(stage);
                    break;
            }

            return $"卵{egg.Id}：{state}・産卵から{days}日";
        }

        /// <summary>Result of "孵卵器へ移す（全部）" (★7).</summary>
        public static string MoveResult(int moved, int left)
        {
            if (moved == 0)
            {
                return left > 0 ? "孵卵器に空きがありません" : "移す卵がありません";
            }

            return left == 0
                ? $"孵卵器へ{moved}個移しました"
                : $"孵卵器へ{moved}個移しました（{left}個は空きがなく残りました）";
        }

        /// <summary>The home screen's incubator button (★14).</summary>
        public static string HomeButton(int eggsInIncubators, int trayAnimals) =>
            eggsInIncubators == 0 && trayAnimals == 0 ? "孵卵器" : $"孵卵器（卵{eggsInIncubators}・トレイ{trayAnimals}）";

        /// <summary>The ledger row's parents (★15).</summary>
        public static string ParentsLine(FamilyMember mother, FamilyMember father)
        {
            if (mother.Unknown && father.Unknown)
            {
                return "両親：不明";
            }

            return $"両親：{ShortName(mother)}×{ShortName(father)}";
        }

        /// <summary>One family-tree box: name (gone marker), morph, sex mark (★16).</summary>
        public static string TreeBox(FamilyMember member)
        {
            if (member.Unknown)
            {
                return "不明";
            }

            var name = member.Gone ? $"{member.Name}（手放した）" : member.Name;
            return $"{name}\n{member.MorphName}\n{SexMark(member.Sex, member.SexKnown)}";
        }

        public static string SexMark(Sex sex, bool known) => !known ? "?" : sex == Sex.Female ? "♀" : "♂";

        /// <summary>Notices for one advance (★18), in a fixed order.</summary>
        public static List<string> Messages(IncubationReport report, CareTuning care)
        {
            var messages = new List<string>();
            foreach (var pet in report.Hatched)
            {
                messages.Add($"{pet.Name}が孵化しました（{TrayLocation}）");
            }

            foreach (var egg in report.InfertileRevealed)
            {
                messages.Add($"孵卵器{egg.IncubatorId}の卵が無精卵と分かりました");
            }

            foreach (var egg in report.EggsDied)
            {
                messages.Add($"孵卵器{egg.IncubatorId}の卵が駄目になりました（{FailureLabel(egg.Failure)}）");
            }

            foreach (var id in report.HumidityLowIncubatorIds)
            {
                messages.Add($"孵卵器{id}の湿度が{care.HumidityDryBelowPercent.ToString("0", CultureInfo.InvariantCulture)}%を下回りました");
            }

            foreach (var egg in report.NestBoxEggsRemoved)
            {
                var reason = egg.Failed ? FailureLabel(egg.Failure) : "無精卵";
                messages.Add($"ケージ{egg.CageId}の産卵床の卵を1個取り除きました（{reason}）");
            }

            return messages;
        }

        /// <summary>A tray row's title.</summary>
        public static string TrayRow(PetState pet)
        {
            var sex = !pet.SexKnown ? "性別不明" : pet.Sex == Sex.Female ? "メス" : "オス";
            return $"{pet.Name}（{GrowthModel.StageLabel(pet.Stage)}・{sex}）";
        }

        /// <summary>Grace remaining (days rounded up), or the needs-care warning (★11).</summary>
        public static string GraceLine(PetState pet, DateTimeOffset gameNowUtc)
        {
            if (pet.CareGraceUntilUtc.HasValue && gameNowUtc < pet.CareGraceUntilUtc.Value)
            {
                var days = (int)Math.Ceiling(GameCalendar.GameDaysBetween(gameNowUtc, pet.CareGraceUntilUtc.Value));
                return $"お世話不要（あと{days}日）";
            }

            return NeedsCare;
        }

        /// <summary>Shown right after "水を足す" pushes humidity above the mould line (★6).</summary>
        public static string MoldWarning(CareTuning care) =>
            $"湿度が{care.HumidityMoldAbovePercent.ToString("0", CultureInfo.InvariantCulture)}%を超えました。カビに注意してください";

        private static string ShortName(FamilyMember member) => member.Unknown ? "不明" : member.Name;
    }
}
