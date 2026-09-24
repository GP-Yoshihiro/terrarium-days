using TerrariumDays.Core;

namespace TerrariumDays.Gameplay
{
    /// <summary>
    /// Grants decor unlocks for a newly reached growth stage. Idempotent — safe to call
    /// again for the same or an earlier stage without duplicating entries.
    /// </summary>
    public static class DecorUnlockService
    {
        public static void GrantUnlocksForStage(PetState state, GrowthStage reachedStage)
        {
            foreach (var decor in DecorCatalog.All)
            {
                if (decor.UnlockStage <= reachedStage && !state.UnlockedDecorIds.Contains(decor.Id))
                {
                    state.UnlockedDecorIds.Add(decor.Id);
                }
            }
        }
    }
}
