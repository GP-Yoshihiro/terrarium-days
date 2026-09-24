namespace TerrariumDays.Core
{
    /// <summary>
    /// Maps PetState.Growth (0-100 across the whole Baby-Adult journey) to a 0-100
    /// percent WITHIN the current growth stage band, for the top-of-screen growth gauge.
    /// </summary>
    public static class GrowthGaugeCalculator
    {
        private const double StageSpan = 50d;

        public static double PercentWithinStage(double growth)
        {
            var clamped = StatusValue.Clamp(growth);

            if (clamped >= 100d)
            {
                return 100d;
            }

            if (clamped >= StageSpan)
            {
                return (clamped - StageSpan) / StageSpan * 100d;
            }

            return clamped / StageSpan * 100d;
        }
    }
}
