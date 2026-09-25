namespace TerrariumDays.Core
{
    /// <summary>
    /// How lively the pet looks. Drives movement pace and tint only; never gameplay values.
    /// </summary>
    public enum PetMood
    {
        Lively,
        Normal,
        Sluggish
    }

    public static class PetMoodEvaluator
    {
        public static PetMood Evaluate(PetState state, CareTuning careTuning, PetBehaviourTuning behaviourTuning)
        {
            var lowestCare = System.Math.Min(state.Hunger, System.Math.Min(state.Hydration, state.Cleanliness));

            if (lowestCare < careTuning.LowCareThreshold || state.Health < careTuning.HealthyCareThreshold)
            {
                return PetMood.Sluggish;
            }

            if (lowestCare >= careTuning.HealthyCareThreshold && state.Health >= behaviourTuning.LivelyHealthThreshold)
            {
                return PetMood.Lively;
            }

            return PetMood.Normal;
        }
    }
}
