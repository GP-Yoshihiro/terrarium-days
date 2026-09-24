using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class StatusValueTests
    {
        [Test]
        public void Clamp_ConstrainsValueToTheAllowedCareRange()
        {
            Assert.That(StatusValue.Clamp(-10f), Is.EqualTo(StatusValue.Minimum));
            Assert.That(StatusValue.Clamp(120f), Is.EqualTo(StatusValue.Maximum));
            Assert.That(StatusValue.Clamp(64f), Is.EqualTo(64f));
        }
    }
}
