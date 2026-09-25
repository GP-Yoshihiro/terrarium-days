using System;
using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>Strict back-to-front bands; nothing in a later band is ever drawn behind an earlier one.</summary>
    public enum DrawLayer
    {
        Background = 0,
        World = 1,
        /// <summary>Time-of-day light over the terrarium (night darkness, dusk glow).</summary>
        Lighting = 2,
        Effects = 3
    }

    /// <summary>
    /// Sort key for one drawable: layer first, then floor depth (0 = back … 1 = front; the
    /// further forward, the later it is drawn), then a fixed tie-break so equal depths never
    /// flicker between frames.
    /// </summary>
    public readonly struct DrawSortKey : IComparable<DrawSortKey>
    {
        /// <summary>Depth for things that are not standing on the floor (e.g. hanging decor).</summary>
        public const float BehindFloor = -1f;

        public readonly DrawLayer Layer;
        public readonly float Depth;
        public readonly int TieBreak;

        public DrawSortKey(DrawLayer layer, float depth, int tieBreak)
        {
            Layer = layer;
            Depth = depth;
            TieBreak = tieBreak;
        }

        public int CompareTo(DrawSortKey other)
        {
            var byLayer = Layer.CompareTo(other.Layer);
            if (byLayer != 0)
            {
                return byLayer;
            }

            var byDepth = Depth.CompareTo(other.Depth);
            return byDepth != 0 ? byDepth : TieBreak.CompareTo(other.TieBreak);
        }
    }

    /// <summary>Tie-breaks for things at the same depth: higher is drawn in front.</summary>
    public static class DrawTieBreak
    {
        public const int Decor = 0;
        public const int Pet = 1;
    }

    public static class DrawOrder
    {
        /// <summary>Indices of <paramref name="keys"/> in back-to-front drawing order (stable).</summary>
        public static int[] BackToFront(IReadOnlyList<DrawSortKey> keys)
        {
            var order = new int[keys.Count];
            for (var i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            // Insertion sort: stable, and the list is tiny and almost always already sorted.
            for (var i = 1; i < order.Length; i++)
            {
                var current = order[i];
                var j = i - 1;
                while (j >= 0 && keys[order[j]].CompareTo(keys[current]) > 0)
                {
                    order[j + 1] = order[j];
                    j--;
                }

                order[j + 1] = current;
            }

            return order;
        }
    }
}
