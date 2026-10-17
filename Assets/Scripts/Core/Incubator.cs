using System;

namespace TerrariumDays.Core
{
    /// <summary>
    /// One incubator the player owns (phase 5 spec §2.1): model, set temperature, humidity
    /// and how far its eggs have been advanced. Ids start at 1 in purchase order.
    /// </summary>
    public sealed class Incubator
    {
        public int Id { get; set; }

        public IncubatorModel Model { get; set; } = IncubatorModel.Simple;

        /// <summary>Set temperature in whole degrees (24〜34℃, §2.1).</summary>
        public int SetTemperatureC { get; set; } = 28;

        /// <summary>0〜100%. Drops 2% per game day (§3).</summary>
        public double HumidityPercent { get; set; } = 80d;

        /// <summary>Game time its eggs and humidity were advanced to; default = never (first advance only syncs).</summary>
        public DateTimeOffset AppliedUntilUtc { get; set; }
    }
}
