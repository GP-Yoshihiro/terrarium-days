using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>Owned items not in use (decor, nest boxes), counted by item id.</summary>
    public sealed class Inventory
    {
        private readonly SortedDictionary<string, int> counts = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int Count(string itemId) => counts.TryGetValue(itemId, out var n) ? n : 0;

        public void Add(string itemId, int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Use TryTake to remove items.");
            }

            if (amount == 0)
            {
                return;
            }

            counts[itemId] = Count(itemId) + amount;
        }

        public bool TryTake(string itemId)
        {
            var n = Count(itemId);
            if (n <= 0)
            {
                return false;
            }

            if (n == 1)
            {
                counts.Remove(itemId);
            }
            else
            {
                counts[itemId] = n - 1;
            }

            return true;
        }

        /// <summary>Items in id order (for the save file and lists).</summary>
        public IEnumerable<KeyValuePair<string, int>> Items => counts;
    }
}
