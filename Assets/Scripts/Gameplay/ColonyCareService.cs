using System;
using System.Collections.Generic;
using TerrariumDays.Core;

namespace TerrariumDays.Gameplay
{
    public enum FeedOutcome
    {
        Ate,
        RefusedPreShed,
        RefusedPreGrowth,
        NotEnoughMoney,
        Full
    }

    public sealed class BulkCareResult
    {
        public int Fed { get; set; }
        public int Refused { get; set; }
        public int NoMoney { get; set; }
        public int Full { get; set; }

        public string ToMessage()
        {
            var text = $"{Fed}匹が食べました";
            var parts = new List<string>();
            if (Refused > 0)
            {
                parts.Add($"{Refused}匹は拒食中");
            }

            if (NoMoney > 0)
            {
                parts.Add($"{NoMoney}匹はお金が足りず");
            }

            if (Full > 0)
            {
                parts.Add($"{Full}匹は満腹");
            }

            return parts.Count > 0 ? text + "（" + string.Join("、", parts) + "）" : text;
        }
    }

    /// <summary>Care across the colony, with food paid for from the wallet.</summary>
    public sealed class ColonyCareService
    {
        private readonly CareTuning care;
        private readonly EconomyTuning economy;
        private readonly CareService careService;

        public ColonyCareService(CareTuning care, EconomyTuning economy)
        {
            this.care = care;
            this.economy = economy;
            careService = new CareService(care);
        }

        public FeedOutcome Feed(Colony colony, PetState pet, DateTimeOffset nowUtc)
        {
            if (pet.Hunger >= care.FeedFullThreshold)
            {
                return FeedOutcome.Full;
            }

            var appetite = AppetiteModel.Evaluate(pet, nowUtc, care);
            if (appetite == AppetiteState.PreShed)
            {
                return FeedOutcome.RefusedPreShed;
            }

            if (appetite == AppetiteState.PreGrowth)
            {
                return FeedOutcome.RefusedPreGrowth;
            }

            var cost = MaintenanceCosts.FeedCost(pet.Stage, economy);
            if (!colony.Wallet.TrySpend(cost, LedgerCategory.Food, $"餌代（{pet.Name}）", nowUtc))
            {
                return FeedOutcome.NotEnoughMoney;
            }

            careService.Feed(pet);
            return FeedOutcome.Ate;
        }

        public BulkCareResult FeedAll(Colony colony, DateTimeOffset nowUtc)
        {
            var result = new BulkCareResult();
            foreach (var cage in colony.OccupiedCages())
            {
                switch (Feed(colony, colony.AnimalIn(cage), nowUtc))
                {
                    case FeedOutcome.Ate:
                        result.Fed++;
                        break;
                    case FeedOutcome.NotEnoughMoney:
                        result.NoMoney++;
                        break;
                    case FeedOutcome.Full:
                        result.Full++;
                        break;
                    default:
                        result.Refused++;
                        break;
                }
            }

            return result;
        }

        public int RefreshWaterAll(Colony colony)
        {
            var count = 0;
            foreach (var cage in colony.OccupiedCages())
            {
                careService.RefreshWater(colony.AnimalIn(cage));
                count++;
            }

            return count;
        }

        public int CleanAll(Colony colony)
        {
            var count = 0;
            foreach (var cage in colony.OccupiedCages())
            {
                careService.Clean(colony.AnimalIn(cage));
                count++;
            }

            return count;
        }
    }
}
