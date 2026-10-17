using System;
using System.Collections.Generic;

namespace TerrariumDays.Tests
{
    /// <summary>Random whose NextDouble returns queued values (then 0.999); Next(n) returns 0.</summary>
    public sealed class QueuedRandom : Random
    {
        private readonly Queue<double> values;

        public QueuedRandom(params double[] values)
        {
            this.values = new Queue<double>(values);
        }

        public override double NextDouble() => values.Count > 0 ? values.Dequeue() : 0.999d;

        public override int Next(int maxValue) => 0;
    }
}
