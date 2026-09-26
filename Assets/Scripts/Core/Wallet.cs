using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    public enum LedgerCategory
    {
        Food,
        Electricity,
        BoothFee,
        Purchase,
        EventSale,
        Wholesale,
        Other,
        AnimalPurchase
    }

    /// <summary>One money movement: positive = income, negative = expense.</summary>
    public sealed class LedgerEntry
    {
        public DateTimeOffset AtUtc { get; set; }
        public LedgerCategory Category { get; set; }
        public long Amount { get; set; }
        public string Note { get; set; }
    }

    /// <summary>Money on hand plus every movement, for the ledger screen.</summary>
    public sealed class Wallet
    {
        public long Money { get; set; }

        public List<LedgerEntry> Ledger { get; set; } = new List<LedgerEntry>();

        /// <summary>Pays only if affordable (e.g. food, shop). Returns false and records nothing otherwise.</summary>
        public bool TrySpend(long amount, LedgerCategory category, string note, DateTimeOffset atUtc)
        {
            RequireNonNegative(amount);
            if (amount > Money)
            {
                return false;
            }

            Charge(amount, category, note, atUtc);
            return true;
        }

        /// <summary>A bill that must be paid even into the red (e.g. electricity).</summary>
        public void Charge(long amount, LedgerCategory category, string note, DateTimeOffset atUtc)
        {
            RequireNonNegative(amount);
            Money -= amount;
            Ledger.Add(new LedgerEntry { AtUtc = atUtc, Category = category, Amount = -amount, Note = note });
        }

        public void Earn(long amount, LedgerCategory category, string note, DateTimeOffset atUtc)
        {
            RequireNonNegative(amount);
            Money += amount;
            Ledger.Add(new LedgerEntry { AtUtc = atUtc, Category = category, Amount = amount, Note = note });
        }

        private static void RequireNonNegative(long amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount,
                    "Amounts are never negative: use Charge/TrySpend for expenses and Earn for income.");
            }
        }
    }
}
