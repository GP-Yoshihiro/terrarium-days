using TerrariumDays.Core;

namespace TerrariumDays.Gameplay
{
    /// <summary>
    /// One selectable terrarium decoration and the growth stage that unlocks it.
    /// </summary>
    public sealed class DecorDefinition
    {
        public DecorDefinition(string id, string displayName, GrowthStage unlockStage)
        {
            Id = id;
            DisplayName = displayName;
            UnlockStage = unlockStage;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public GrowthStage UnlockStage { get; }
    }
}
