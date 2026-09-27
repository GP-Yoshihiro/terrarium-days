using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    public sealed class ColonyTickReport
    {
        public List<(PetState Pet, GrowthStage Stage)> StageUps { get; } = new List<(PetState, GrowthStage)>();
        public List<PetState> Sheds { get; } = new List<PetState>();
        public List<PetState> SexReveals { get; } = new List<PetState>();
        public List<PetState> PersonalityReveals { get; } = new List<PetState>();
        public long ElectricityCharged { get; set; }
        public TimeSpan AppliedElapsed { get; set; }
        public bool Restocked { get; set; }

        public bool HasEvents => StageUps.Count > 0 || Sheds.Count > 0 || SexReveals.Count > 0 || ElectricityCharged > 0
            || PersonalityReveals.Count > 0 || Restocked;
    }

    /// <summary>
    /// Owns the colony while the app runs: loading/migrating, applying elapsed time to every
    /// animal (offline, resume, live with the debug multiplier), monthly electricity, and
    /// saving. Keeps each animal's applied-until time and the shared game clock on one
    /// timeline (see the simulated-clock-bookkeeping skill).
    /// </summary>
    public sealed class ColonySession
    {
        private readonly string savePath;
        private readonly CareTuning care;
        private readonly EconomyTuning economy;
        private readonly System.Random random;
        private readonly ColonySaveService saveService;
        private readonly OfflineProgressCalculator calculator;
        private TimeService clock;

        public ColonySession(string savePath, TimeService clock, CareTuning care, EconomyTuning economy, System.Random random)
        {
            this.savePath = savePath;
            this.clock = clock;
            this.care = care;
            this.economy = economy;
            this.random = random;
            saveService = new ColonySaveService(care, economy);
            calculator = new OfflineProgressCalculator(care);
        }

        public Colony Colony { get; private set; }

        public GameCalendar Calendar { get; private set; }

        /// <summary>The game clock: real time, or ahead of it while a debug multiplier runs.</summary>
        public DateTimeOffset GameNowUtc { get; private set; }

        public bool Migrated { get; private set; }

        public bool SaveBlocked { get; private set; }

        public bool DecorMovedToInventory { get; private set; }

        /// <summary>The room temperature for nest-box eggs (§7.7); the UI updates it from the weather.</summary>
        public RoomClimate Room { get; } = new RoomClimate();

        public TimeService Clock => clock;

        public string SavePath => savePath;

        public void UseClock(TimeService newClock)
        {
            clock = newClock;
        }

        public ColonyTickReport Load()
        {
            var realNow = clock.UtcNow();
            Colony = saveService.LoadOrCreate(savePath, realNow, random);
            Migrated = saveService.LastLoadMigrated;
            DecorMovedToInventory = saveService.LastLoadMovedDecor;
            SaveBlocked = saveService.LastLoadBackupFailed;
            Calendar = new GameCalendar(Colony.CalendarEpochUtc);
            GameNowUtc = realNow + Colony.GameClockOffset;
            var report = ApplyUntil(GameNowUtc);
            Resync(realNow);
            if (!SaveBlocked)
            {
                saveService.Save(savePath, Colony);
            }

            return report;
        }

        public ColonyTickReport Advance(TimeSpan realDelta)
        {
            var scaled = clock.ScaleElapsed(realDelta);
            if (scaled <= TimeSpan.Zero)
            {
                return new ColonyTickReport();
            }

            if (scaled > realDelta)
            {
                // Only the part beyond real time is fast-forward; it is kept across saves.
                Colony.GameClockOffset += scaled - realDelta;
            }

            GameNowUtc += scaled;
            return ApplyUntil(GameNowUtc);
        }

        public ColonyTickReport Resume()
        {
            GameNowUtc = clock.UtcNow() + Colony.GameClockOffset;
            var report = ApplyUntil(GameNowUtc);
            Save();
            return report;
        }

        public ColonyTickReport SimulateGameTime(TimeSpan span)
        {
            if (span <= TimeSpan.Zero)
            {
                return new ColonyTickReport();
            }

            Colony.GameClockOffset += span;
            GameNowUtc += span;
            return ApplyUntil(GameNowUtc);
        }

        public void Save()
        {
            Resync(clock.UtcNow());
            if (SaveBlocked)
            {
                return;
            }

            saveService.Save(savePath, Colony);
        }

        private ColonyTickReport ApplyUntil(DateTimeOffset targetUtc)
        {
            var report = new ColonyTickReport();
            foreach (var pet in Colony.Animals)
            {
                var stageBefore = pet.Stage;
                var result = calculator.Apply(pet, pet.LastSavedAtUtc, targetUtc);
                pet.LastSavedAtUtc += result.AppliedElapsed;
                if (result.AppliedElapsed > report.AppliedElapsed)
                {
                    report.AppliedElapsed = result.AppliedElapsed;
                }

                if (pet.Stage != stageBefore)
                {
                    report.StageUps.Add((pet, pet.Stage));
                }

                if (result.ShedCount > 0)
                {
                    report.Sheds.Add(pet);
                }

                if (result.SexRevealed)
                {
                    report.SexReveals.Add(pet);
                }

                if (PersonalityReveal.ApplyIfDue(pet, targetUtc))
                {
                    report.PersonalityReveals.Add(pet);
                }
            }

            report.ElectricityCharged = BillElectricity(targetUtc);
            report.Restocked = Colony.Shop.EnsureStocked(Calendar.MonthIndexAt(targetUtc), targetUtc, care);
            return report;
        }

        private long BillElectricity(DateTimeOffset atUtc)
        {
            var month = Calendar.MonthIndexAt(atUtc);
            long charged = 0;
            while (Colony.LastBilledMonthIndex < month)
            {
                Colony.LastBilledMonthIndex++;
                var bill = MaintenanceCosts.MonthlyElectricity(Colony.Cages.Count, Colony.IncubatorCount, economy);
                Colony.Wallet.Charge(bill, LedgerCategory.Electricity, "電気代", atUtc);
                charged += bill;
            }

            return charged;
        }

        /// <summary>
        /// <summary>
        /// Re-anchors every animal and the game clock on real time plus the saved offset,
        /// keeping each animal's sub-step remainder so a care action is never replayed away.
        /// </summary>
        private void Resync(DateTimeOffset realNowUtc)
        {
            var gameNow = realNowUtc + Colony.GameClockOffset;
            var step = TimeSpan.FromMinutes(care.OfflineProgressStepMinutes);
            foreach (var pet in Colony.Animals)
            {
                var pending = GameNowUtc - pet.LastSavedAtUtc;
                if (pending < TimeSpan.Zero || pending >= step)
                {
                    pending = TimeSpan.Zero;
                }

                pet.LastSavedAtUtc = gameNow - pending;
            }

            GameNowUtc = gameNow;
        }
    }
}
