using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class CareFeedbackMessageTests
    {
        [Test]
        public void For_WhenValueWasAlreadyAtMaximum_ReturnsTheAlreadyFullMessage()
        {
            Assert.That(CareFeedbackMessage.For(100d, "ごはんを食べた！"), Is.EqualTo(CareFeedbackMessage.AlreadyFullMessage));
        }

        [Test]
        public void For_WhenValueWasBelowMaximum_ReturnsTheSuccessMessageUnchanged()
        {
            Assert.That(CareFeedbackMessage.For(70d, "ごはんを食べた！"), Is.EqualTo("ごはんを食べた！"));
        }
    }
}
