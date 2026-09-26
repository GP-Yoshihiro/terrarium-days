using System;
using System.Collections.Generic;
using TerrariumDays.Core;

namespace TerrariumDays.UI
{
    /// <summary>Texts on the ledger tab (§6.2): the animal list and money in/out.</summary>
    public static class LedgerText
    {
        public const int MaxEntriesShown = 100;

        public static string CategoryLabel(LedgerCategory category)
        {
            switch (category)
            {
                case LedgerCategory.Food:
                    return "餌代";
                case LedgerCategory.Electricity:
                    return "電気代";
                case LedgerCategory.BoothFee:
                    return "出店料";
                case LedgerCategory.Purchase:
                    return "用品";
                case LedgerCategory.AnimalPurchase:
                    return "生体の購入";
                case LedgerCategory.EventSale:
                    return "イベント売上";
                case LedgerCategory.Wholesale:
                    return "卸売り";
                default:
                    return "その他";
            }
        }

        /// <summary>ASCII minus: the UI font may lack U+2212.</summary>
        public static string SignedYen(long amount) => amount >= 0 ? $"+¥{amount:N0}" : $"-¥{-amount:N0}";

        public static string EntryLine(LedgerEntry entry, GameCalendar calendar)
        {
            var date = calendar.DateAt(entry.AtUtc);
            return $"{date.Month}月{date.Day}日　{CategoryLabel(entry.Category)}　{entry.Note}";
        }

        /// <summary>Income and expense (both positive) for one game month.</summary>
        public static (long Income, long Expense) MonthTotals(IEnumerable<LedgerEntry> ledger, GameCalendar calendar, int monthIndex)
        {
            long income = 0;
            long expense = 0;
            foreach (var entry in ledger)
            {
                if (calendar.MonthIndexAt(entry.AtUtc) != monthIndex)
                {
                    continue;
                }

                if (entry.Amount >= 0)
                {
                    income += entry.Amount;
                }
                else
                {
                    expense -= entry.Amount;
                }
            }

            return (income, expense);
        }

        public static string MonthSummary(long income, long expense) =>
            $"今月　収入 ¥{income:N0}　支出 ¥{expense:N0}　差引 {SignedYen(income - expense)}";

        /// <summary>Newest first, at most <see cref="MaxEntriesShown"/>.</summary>
        public static List<LedgerEntry> Recent(IList<LedgerEntry> ledger)
        {
            var recent = new List<LedgerEntry>();
            for (var i = ledger.Count - 1; i >= 0 && recent.Count < MaxEntriesShown; i--)
            {
                recent.Add(ledger[i]);
            }

            return recent;
        }

        public static string AgeLabel(PetState pet, DateTimeOffset nowUtc) =>
            $"生後{(int)Math.Floor(GrowthModel.AgeMonths(pet, nowUtc))}か月";

        public static string AnimalDetail(PetState pet, DateTimeOffset nowUtc) =>
            $"{CageStatusText.SexLabel(pet)}・{GrowthModel.StageLabel(pet.Stage)}・{pet.WeightGrams:0.0}g・{AgeLabel(pet, nowUtc)}";
    }
}
