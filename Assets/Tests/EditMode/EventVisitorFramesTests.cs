using NUnit.Framework;
using TerrariumDays.UI;

namespace TerrariumDays.Tests
{
    public class EventVisitorFramesTests
    {
        [TestCase('a', 0)]
        [TestCase('a', 1)]
        [TestCase('b', 0)]
        [TestCase('b', 1)]
        [TestCase('c', 0)]
        [TestCase('c', 1)]
        [TestCase('a', 'f')]
        [TestCase('b', 'f')]
        [TestCase('c', 'f')]
        public void VisitorFrameLoadsThroughTheViewPath(char kind, object frame)
        {
            var frames = EventView.LoadVisitorFrames();

            Assert.IsTrue(frames.TryGetValue($"visitor_{kind}_{(frame is char ? "front" : frame.ToString())}", out var texture), "visitor frame missing");
            Assert.IsNotNull(texture);
            Assert.AreEqual(48, texture.width);
            Assert.AreEqual(80, texture.height);
        }
    }
}
