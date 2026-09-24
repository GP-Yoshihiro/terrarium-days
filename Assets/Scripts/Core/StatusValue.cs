using UnityEngine;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Shared boundary rule for all player-facing care status values.
    /// </summary>
    public static class StatusValue
    {
        public const float Minimum = 0f;
        public const float Maximum = 100f;

        public static float Clamp(float value)
        {
            return Mathf.Clamp(value, Minimum, Maximum);
        }

        public static double Clamp(double value)
        {
            return System.Math.Clamp(value, Minimum, Maximum);
        }
    }
}
