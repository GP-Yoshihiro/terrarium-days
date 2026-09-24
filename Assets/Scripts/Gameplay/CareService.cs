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
