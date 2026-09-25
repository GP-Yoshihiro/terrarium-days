using TerrariumDays.Core;

namespace TerrariumDays.Gameplay
{
    /// <summary>
    /// Applies the three player care actions to a PetState. No MonoBehaviour dependency.
    /// </summary>
    public sealed class CareService
    {
        private readonly CareTuning tuning;

        public CareService(CareTuning tuning)
        {
            this.tuning = tuning;
        }

        public void Feed(PetState state)
        {
            state.Hunger += tuning.FeedHungerAmount;
        }

        /// <summary>
        /// Offers food. A pet in a food-refusal period (before a shed or a growth spurt)
        /// does not eat; the returned state says why so the view can explain it.
        /// </summary>
        public AppetiteState Feed(PetState state, System.DateTimeOffset nowUtc)
        {
            var appetite = AppetiteModel.Evaluate(state, nowUtc, tuning);
            if (appetite == AppetiteState.Normal)
            {
                Feed(state);
            }

            return appetite;
        }

        public void RefreshWater(PetState state)
        {
            state.Hydration += tuning.RefreshWaterHydrationAmount;
        }

        public void Clean(PetState state)
        {
            state.Cleanliness += tuning.CleanCleanlinessAmount;
        }
    }
}
