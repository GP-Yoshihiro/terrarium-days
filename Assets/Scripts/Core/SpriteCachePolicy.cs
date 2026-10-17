using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Least-recently-used bookkeeping for the recoloured sprite caches (phase 5 spec §6, decision 29).
    /// Holds keys only; the caller destroys the textures for the keys <see cref="Evict"/> returns.
    /// </summary>
    public sealed class SpriteCachePolicy
    {
        public const int DefaultCapacity = 32;

        private readonly LinkedList<string> order = new LinkedList<string>();
        private readonly Dictionary<string, LinkedListNode<string>> nodes = new Dictionary<string, LinkedListNode<string>>();

        public SpriteCachePolicy(int capacity = DefaultCapacity)
        {
            Capacity = capacity;
        }

        public int Capacity { get; }

        public int Count => nodes.Count;

        public bool Contains(string key) => key != null && nodes.ContainsKey(key);

        /// <summary>Marks the key as just used (adds it when new).</summary>
        public void Touch(string key)
        {
            if (key == null)
            {
                return;
            }

            if (nodes.TryGetValue(key, out var node))
            {
                order.Remove(node);
                order.AddFirst(node);
                return;
            }

            nodes[key] = order.AddFirst(key);
        }

        public bool Remove(string key)
        {
            if (key == null || !nodes.TryGetValue(key, out var node))
            {
                return false;
            }

            order.Remove(node);
            nodes.Remove(key);
            return true;
        }

        /// <summary>
        /// While over capacity, forgets the oldest keys that are not in <paramref name="inUse"/>
        /// and returns them (oldest first). Keys in use are kept even if that leaves it over capacity.
        /// </summary>
        public List<string> Evict(ICollection<string> inUse)
        {
            var evicted = new List<string>();
            var node = order.Last;
            while (nodes.Count > Capacity && node != null)
            {
                var previous = node.Previous;
                if (inUse == null || !inUse.Contains(node.Value))
                {
                    evicted.Add(node.Value);
                    nodes.Remove(node.Value);
                    order.Remove(node);
                }

                node = previous;
            }

            return evicted;
        }
    }
}
