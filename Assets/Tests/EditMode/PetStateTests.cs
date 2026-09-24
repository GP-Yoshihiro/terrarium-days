using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class PetStateTests
    {
        [Test]
        public void Constructor_SetsDocumentedDefaultValues()
        {
            var state = new PetState();

            Assert.That(state.Hunger, Is.EqualTo(80d));
            Assert.That(state.Hydration, Is.EqualTo(80d));
            Assert.That(state.Cleanliness, Is.EqualTo(80d));
            Assert.That(state.Health, Is.EqualTo(100d));
            Assert.That(state.Growth, Is.EqualTo(0d));
            Assert.That(state.SelectedDecorId, Is.EqualTo("rock_01"));
            Assert.That(state.UnlockedDecorIds, Is.EqualTo(new[] { "rock_01" }));
        }

        [Test]
        public void CareStatusSetters_ClampAssignedValuesToZeroToOneHundred()
        {
            var state = new PetState
            {
                Hunger = 150d,
                Hydration = -20d,
                Cleanliness = 64d,
                Health = 500d,
                Growth = -5d
            };

            Assert.That(state.Hunger, Is.EqualTo(100d));
            Assert.That(state.Hydration, Is.EqualTo(0d));
            Assert.That(state.Cleanliness, Is.EqualTo(64d));
            Assert.That(state.Health, Is.EqualTo(100d));
            Assert.That(state.Growth, Is.EqualTo(0d));
        }

        [TestCase(0d, GrowthStage.Baby)]
        [TestCase(49.999d, GrowthStage.Baby)]
        [TestCase(50d, GrowthStage.Juvenile)]
        [TestCase(99.999d, GrowthStage.Juvenile)]
        [TestCase(100d, GrowthStage.Adult)]
        public void GrowthStage_IsDerivedFromGrowthAtDocumentedBoundaries(double growth, GrowthStage expectedStage)
        {
            var state = new PetState { Growth = growth };

            Assert.That(state.GrowthStage, Is.EqualTo(expectedStage));
        }
    }
}
