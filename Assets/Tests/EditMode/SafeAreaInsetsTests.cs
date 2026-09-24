using NUnit.Framework;
using TerrariumDays.UI;
using UnityEngine;

namespace TerrariumDays.Tests
{
    public sealed class SafeAreaInsetsTests
    {
        [Test]
        public void Compute_ConvertsIPhoneSafeAreaIntoPanelPadding()
        {
            // iPhone 15 Pro portrait: 1179x2556 px, 59pt top / 34pt bottom inset at 3x.
            var screen = new Vector2(1179f, 2556f);
            var safeArea = new Rect(0f, 102f, 1179f, 2556f - 177f - 102f);
            var panel = new Vector2(393f, 852f);

            var insets = SafeAreaInsets.Compute(safeArea, screen, panel);

            Assert.That(insets.Left, Is.EqualTo(0f).Within(0.01f));
            Assert.That(insets.Right, Is.EqualTo(0f).Within(0.01f));
            Assert.That(insets.Top, Is.EqualTo(59f).Within(0.01f));
            Assert.That(insets.Bottom, Is.EqualTo(34f).Within(0.01f));
        }

        [Test]
        public void Compute_ReturnsZeroWhenSafeAreaIsFullScreen()
        {
            var screen = new Vector2(1080f, 1920f);
            var insets = SafeAreaInsets.Compute(new Rect(0f, 0f, 1080f, 1920f), screen, new Vector2(390f, 693f));

            Assert.That(insets.Top, Is.EqualTo(0f));
            Assert.That(insets.Bottom, Is.EqualTo(0f));
            Assert.That(insets.Left, Is.EqualTo(0f));
            Assert.That(insets.Right, Is.EqualTo(0f));
        }

        [Test]
        public void Compute_ReturnsZeroForDegenerateScreenSize()
        {
            var insets = SafeAreaInsets.Compute(new Rect(0f, 10f, 100f, 100f), Vector2.zero, new Vector2(390f, 844f));

            Assert.That(insets.Top, Is.EqualTo(0f));
            Assert.That(insets.Bottom, Is.EqualTo(0f));
        }
    }
}
