using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class SpriteCachePolicyTests
    {
        [Test]
        public void UnderCapacity_NothingIsEvicted()
        {
            var policy = new SpriteCachePolicy(3);
            policy.Touch("a");
            policy.Touch("b");

            Assert.IsEmpty(policy.Evict(null));
            Assert.AreEqual(2, policy.Count);
        }

        [Test]
        public void OverCapacity_TheLeastRecentlyUsedGoesFirst()
        {
            var policy = new SpriteCachePolicy(2);
            policy.Touch("a");
            policy.Touch("b");
            policy.Touch("a");
            policy.Touch("c");

            CollectionAssert.AreEqual(new[] { "b" }, policy.Evict(null));
            Assert.IsTrue(policy.Contains("a"));
            Assert.IsTrue(policy.Contains("c"));
            Assert.IsFalse(policy.Contains("b"));
        }

        [Test]
        public void KeysInUse_AreNeverEvicted()
        {
            var policy = new SpriteCachePolicy(1);
            policy.Touch("a");
            policy.Touch("b");
            policy.Touch("c");

            var evicted = policy.Evict(new[] { "a", "c" });

            CollectionAssert.AreEquivalent(new[] { "b" }, evicted);
            Assert.IsTrue(policy.Contains("a"));
            Assert.IsTrue(policy.Contains("c"));
            Assert.AreEqual(2, policy.Count, "stays over capacity rather than dropping a sprite on screen");
        }

        [Test]
        public void EverythingInUse_EvictsNothing()
        {
            var policy = new SpriteCachePolicy(1);
            policy.Touch("a");
            policy.Touch("b");

            Assert.IsEmpty(policy.Evict(new[] { "a", "b" }));
            Assert.AreEqual(2, policy.Count);
        }

        [Test]
        public void Remove_ForgetsAKey()
        {
            var policy = new SpriteCachePolicy(4);
            policy.Touch("a");

            Assert.IsTrue(policy.Remove("a"));
            Assert.IsFalse(policy.Remove("a"));
            Assert.AreEqual(0, policy.Count);
        }

        [Test]
        public void DefaultCapacity_IsThirtyTwo()
        {
            Assert.AreEqual(32, new SpriteCachePolicy().Capacity);
        }
    }
}
