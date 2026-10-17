using System;

namespace TerrariumDays.Core
{
    public sealed partial class IncubatorService
    {
        /// <summary>
        /// Advances every incubator to <paramref name="targetUtc"/> (game time): humidity falls
        /// continuously, eggs develop at the day's real temperature, and at each game-day boundary
        /// every live fertile egg gets one failure check (★4). At most 12 hours are applied.
        /// An incubator that was never advanced only syncs its clock (decision 22).
        /// </summary>
        public IncubationReport Advance(Colony colony, DateTimeOffset targetUtc, double roomC)
        {
            var report = new IncubationReport();
            var maxSpan = TimeSpan.FromHours(care.MaxOfflineProgressHours);
            var epoch = colony.CalendarEpochUtc;
            var seed = colony.BreedingSeed;

            foreach (var incubator in colony.Incubators)
            {
                if (incubator.AppliedUntilUtc == default)
                {
                    incubator.AppliedUntilUtc = targetUtc;
                    continue;
                }

                if (targetUtc <= incubator.AppliedUntilUtc)
                {
                    continue;
                }

                var previousUtc = incubator.AppliedUntilUtc;
                var cursor = targetUtc - previousUtc > maxSpan ? targetUtc - maxSpan : previousUtc;
                incubator.AppliedUntilUtc = targetUtc;

                var eggs = EggsInIncubator(colony, incubator);
                foreach (var egg in eggs)
                {
                    if (egg.Fertile || egg.Failed)
                    {
                        continue;
                    }

                    var revealUtc = egg.LaidAtUtc + GameCalendar.RealTimeFor(care.CandlingRevealGameDays);
                    if (revealUtc > previousUtc && revealUtc <= targetUtc)
                    {
                        report.InfertileRevealed.Add(egg);
                    }
                }

                var wasHumidEnough = incubator.HumidityPercent >= care.HumidityDryBelowPercent;

                while (cursor < targetUtc)
                {
                    var dayIndex = DayIndexAt(epoch, cursor);
                    var boundary = epoch + GameCalendar.RealTimeFor(dayIndex + 1);
                    var segmentEnd = boundary < targetUtc ? boundary : targetUtc;
                    var gameDays = GameCalendar.GameDaysBetween(cursor, segmentEnd);

                    var temperatureRandom = BreedingRandom.For(seed, BreedingRandom.IncubatorTemperatureStream, incubator.Id, dayIndex);
                    var realC = IncubatorRules.RealTemperature(incubator.Model, incubator.SetTemperatureC, roomC, temperatureRandom, care);

                    incubator.HumidityPercent = Math.Max(0d, incubator.HumidityPercent - care.IncubatorHumidityLossPerGameDay * gameDays);

                    foreach (var egg in eggs)
                    {
                        if (egg.Failed)
                        {
                            continue;
                        }

                        EggDevelopment.Apply(egg, gameDays, realC, care);
                        if (egg.Failed)
                        {
                            report.EggsDied.Add(egg);
                        }
                    }

                    if (boundary <= targetUtc)
                    {
                        foreach (var egg in eggs)
                        {
                            if (!egg.Fertile || egg.Failed)
                            {
                                continue;
                            }

                            var random = BreedingRandom.For(seed, BreedingRandom.IncubationStream, egg.Id, dayIndex);
                            var failure = IncubatorRules.DailyFailure(incubator.HumidityPercent, realC, random, care);
                            if (failure != EggFailure.None)
                            {
                                egg.Failure = failure;
                                report.EggsDied.Add(egg);
                            }
                        }
                    }

                    cursor = segmentEnd;
                }

                if (wasHumidEnough && incubator.HumidityPercent < care.HumidityDryBelowPercent && eggs.Count > 0)
                {
                    report.HumidityLowIncubatorIds.Add(incubator.Id);
                }
            }

            return report;
        }

        /// <summary>Game-day number containing <paramref name="utc"/> (day 0 starts at the epoch), robust to rounding at boundaries.</summary>
        private static int DayIndexAt(DateTimeOffset epochUtc, DateTimeOffset utc)
        {
            var index = (int)Math.Floor(GameCalendar.GameDaysBetween(epochUtc, utc));
            while (epochUtc + GameCalendar.RealTimeFor(index + 1) <= utc)
            {
                index++;
            }

            while (epochUtc + GameCalendar.RealTimeFor(index) > utc)
            {
                index--;
            }

            return index;
        }
    }
}
