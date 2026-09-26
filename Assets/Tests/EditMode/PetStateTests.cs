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
            };

            Assert.That(state.Hunger, Is.EqualTo(100d));
            Assert.That(state.Hydration, Is.EqualTo(0d));
            Assert.That(state.Cleanliness, Is.EqualTo(64d));
            Assert.That(state.Health, Is.EqualTo(100d));
        }
    }
}
