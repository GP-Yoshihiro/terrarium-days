using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.Gameplay;

namespace TerrariumDays.Tests
{
    public sealed class DecorUnlockServiceTests
    {
        [Test]
        public void GrantUnlocksForStage_AtJuvenile_AddsOnlyTheJuvenileTierItems()
        {
            var state = new PetState();

            DecorUnlockService.GrantUnlocksForStage(state, GrowthStage.Juvenile);

            Assert.That(state.UnlockedDecorIds, Is.EquivalentTo(new[] { PetState.DefaultDecorId, "plant_01", "water_dish_01" }));
        }

        [Test]
        public void GrantUnlocksForStage_AtAdult_AddsTheRemainingItemsOnTop()
        {
            var state = new PetState();

            DecorUnlockService.GrantUnlocksForStage(state, GrowthStage.Juvenile);
            DecorUnlockService.GrantUnlocksForStage(state, GrowthStage.Adult);

            Assert.That(state.UnlockedDecorIds, Is.EquivalentTo(new[]
            {
                PetState.DefaultDecorId, "plant_01", "water_dish_01", "heat_lamp_01", "driftwood_01"
            }));
        }

        [Test]
        public void GrantUnlocksForStage_CalledTwiceForTheSameStage_DoesNotDuplicateEntries()
        {
            var state = new PetState();

            DecorUnlockService.GrantUnlocksForStage(state, GrowthStage.Juvenile);
            DecorUnlockService.GrantUnlocksForStage(state, GrowthStage.Juvenile);

            Assert.That(state.UnlockedDecorIds.Count, Is.EqualTo(3));
        }
    }
}
