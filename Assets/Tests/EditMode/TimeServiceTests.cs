using System;
using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class TimeServiceTests
    {
        [Test]
        public void UtcNow_ReturnsWhateverTheInjectedProviderReturns()
        {
            var fixedNow = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
            var timeService = new TimeService(() => fixedNow);

            Assert.That(timeService.UtcNow(), Is.EqualTo(fixedNow));
        }

        [Test]
        public void ScaleElapsed_AtDefaultMultiplier_ReturnsTheSameDuration()
        {
            var timeService = new TimeService(() => DateTimeOffset.UtcNow);

            var result = timeService.ScaleElapsed(TimeSpan.FromSeconds(10));

            Assert.That(result, Is.EqualTo(TimeSpan.FromSeconds(10)));
        }

        [Test]
        public void ScaleElapsed_AtNonDefaultMultiplier_ScalesTheDuration()
        {
            var timeService = new TimeService(() => DateTimeOffset.UtcNow) { TimeMultiplier = 60d };

            var result = timeService.ScaleElapsed(TimeSpan.FromSeconds(2));

            Assert.That(result, Is.EqualTo(TimeSpan.FromSeconds(120)));
        }
    }
}
