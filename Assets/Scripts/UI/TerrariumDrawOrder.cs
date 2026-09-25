using System;
using System.Collections.Generic;
using TerrariumDays.Core;
using UnityEngine.UIElements;

namespace TerrariumDays.UI
{
    /// <summary>
    /// Keeps the terrarium's children in DrawOrder: every registered element supplies its
    /// sort key each frame, and the sibling order is rewritten only when it actually changes.
    /// Unregistered children are left alone.
    /// </summary>
    public sealed class TerrariumDrawOrder
    {
        private readonly VisualElement container;
        private readonly List<VisualElement> elements = new List<VisualElement>();
        private readonly List<Func<DrawSortKey>> keySources = new List<Func<DrawSortKey>>();
        private readonly List<DrawSortKey> keys = new List<DrawSortKey>();

        public TerrariumDrawOrder(VisualElement container)
        {
            this.container = container;
        }

        public void Register(VisualElement element, Func<DrawSortKey> key)
        {
            if (element == null || element.parent != container)
            {
                return;
            }

            elements.Add(element);
            keySources.Add(key);
        }

        /// <summary>Re-sorts the registered children. Returns true if the order changed.</summary>
        public bool Apply()
        {
            keys.Clear();
            foreach (var source in keySources)
            {
                keys.Add(source());
            }

            var order = DrawOrder.BackToFront(keys);
            if (IsAlreadyInOrder(order))
            {
                return false;
            }

            // BringToFront moves an element to the end, so walking back-to-front leaves the
            // registered elements in order (after any unregistered ones).
            foreach (var index in order)
            {
                elements[index].BringToFront();
            }

            return true;
        }

        private bool IsAlreadyInOrder(int[] order)
        {
            var previous = -1;
            foreach (var index in order)
            {
                var position = container.IndexOf(elements[index]);
                if (position < previous)
                {
                    return false;
                }

                previous = position;
            }

            return true;
        }
    }
}
