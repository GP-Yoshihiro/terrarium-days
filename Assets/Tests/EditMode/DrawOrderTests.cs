using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.UI;
using UnityEngine.UIElements;

namespace TerrariumDays.Tests
{
    public sealed class DrawOrderTests
    {
        [Test]
        public void Layers_AlwaysWinOverDepth()
        {
            var keys = new[]
            {
                new DrawSortKey(DrawLayer.Effects, -5f, 0),
                new DrawSortKey(DrawLayer.World, 1f, DrawTieBreak.Pet),
                new DrawSortKey(DrawLayer.Background, 9f, 0),
            };

            Assert.That(DrawOrder.BackToFront(keys), Is.EqualTo(new[] { 2, 1, 0 }));
        }

        [Test]
        public void WithinTheWorld_WhateverStandsFurtherForwardIsDrawnLater()
        {
            var keys = new[]
            {
                new DrawSortKey(DrawLayer.World, 0.8f, DrawTieBreak.Pet),
                new DrawSortKey(DrawLayer.World, 0.45f, DrawTieBreak.Decor),
                new DrawSortKey(DrawLayer.World, DrawSortKey.BehindFloor, DrawTieBreak.Decor),
            };

            Assert.That(DrawOrder.BackToFront(keys), Is.EqualTo(new[] { 2, 1, 0 }));
        }

        [Test]
        public void AtTheSameDepth_ThePetIsDrawnInFrontOfDecor()
        {
            var keys = new[]
            {
                new DrawSortKey(DrawLayer.World, 0.5f, DrawTieBreak.Pet),
                new DrawSortKey(DrawLayer.World, 0.5f, DrawTieBreak.Decor),
            };

            Assert.That(DrawOrder.BackToFront(keys), Is.EqualTo(new[] { 1, 0 }));
        }

        [Test]
        public void TerrariumDrawOrder_ReordersChildrenAsThePetWalksAroundTheDecor()
        {
            var container = new VisualElement();
            var background = new VisualElement();
            var decor = new VisualElement();
            var pet = new VisualElement();
            var effects = new VisualElement();
            // Deliberately scrambled starting order.
            container.Add(effects);
            container.Add(pet);
            container.Add(decor);
            container.Add(background);

            var petDepth = 0.2f;
            var order = new TerrariumDrawOrder(container);
            order.Register(background, () => new DrawSortKey(DrawLayer.Background, 0f, 0));
            order.Register(decor, () => new DrawSortKey(DrawLayer.World, 0.45f, DrawTieBreak.Decor));
            order.Register(pet, () => new DrawSortKey(DrawLayer.World, petDepth, DrawTieBreak.Pet));
            order.Register(effects, () => new DrawSortKey(DrawLayer.Effects, 0f, 0));

            Assert.That(order.Apply(), Is.True);
            Assert.That(ChildOrder(container), Is.EqualTo(new[] { background, pet, decor, effects }), "pet behind the rock");

            Assert.That(order.Apply(), Is.False, "nothing moved, so nothing is rewritten");

            petDepth = 0.9f;
            Assert.That(order.Apply(), Is.True);
            Assert.That(ChildOrder(container), Is.EqualTo(new[] { background, decor, pet, effects }), "pet in front of the rock");
        }

        private static VisualElement[] ChildOrder(VisualElement container)
        {
            var children = new VisualElement[container.childCount];
            for (var i = 0; i < children.Length; i++)
            {
                children[i] = container[i];
            }

            return children;
        }
    }
}
