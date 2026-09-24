namespace TerrariumDays.Core
{
    /// <summary>
    /// Maps a care status value to its display color tier, reusing CareTuning's own
    /// healthy/low thresholds so the visual cue and the gameplay rule never drift apart.
    /// </summary>
    public static class CareStatusPresentation
    {
        public static StatusBarLevel LevelFor(double value, CareTuning tuning)
        {
            if (value >= tuning.HealthyCareThreshold)
            {
                return StatusBarLevel.Good;
            }

            if (value >= tuning.LowCareThreshold)
            {
                return StatusBarLevel.Warning;
            }

            return StatusBarLevel.Critical;
        }
    }
}
