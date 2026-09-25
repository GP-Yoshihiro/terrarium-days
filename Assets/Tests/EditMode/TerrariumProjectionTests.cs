using NUnit.Framework;
using TerrariumDays.Core;

namespace TerrariumDays.Tests
{
    public sealed class TerrariumProjectionTests
    {
        private readonly TerrariumArtLayout art = new TerrariumArtLayout();

        // A wide-ish view (background cropped top/bottom) and a tall one (cropped left/right).
        private static readonly float[][] ViewSizes = { new[] { 393f, 300f }, new[] { 360f, 560f } };

        [Test]
        public void Cover_FillsTheViewAndKeepsTheBottomEdge([Values(0, 1)] int sizeIndex)
        {
            var size = ViewSizes[sizeIndex];
            var projection = new TerrariumProjection(size[0], size[1], art);

            var left = projection.ToViewX(0f);
            var right = projection.ToViewX(art.BackgroundWidth);
            var top = projection.ToViewY(0f);
            var bottom = projection.ToViewY(art.BackgroundHeight);

            Assert.That(left, Is.LessThanOrEqualTo(0.001f));
            Assert.That(top, Is.LessThanOrEqualTo(0.001f));
            Assert.That(right, Is.GreaterThanOrEqualTo(size[0] - 0.001f));
            Assert.That(bottom, Is.GreaterThanOrEqualTo(size[1] - 0.001f));
            Assert.That(left + right, Is.EqualTo(size[0]).Within(0.01f), "horizontal overflow is cropped evenly");
            Assert.That(bottom, Is.EqualTo(size[1]).Within(0.01f), "the art's bottom edge is never cropped");
        }

        [Test]
        public void BackgroundBox_KeepsTheArtsAspectAndMatchesTheArtMapping([Values(0, 1)] int sizeIndex)
        {
            var size = ViewSizes[sizeIndex];
            var projection = new TerrariumProjection(size[0], size[1], art);

            var box = projection.BackgroundBox();

            Assert.That(box.Width / box.Height, Is.EqualTo(art.BackgroundWidth / art.BackgroundHeight).Within(1e-4f));
            Assert.That(box.Left, Is.EqualTo(projection.ToViewX(0f)).Within(0.01f));
            Assert.That(box.Top + box.Height, Is.EqualTo(projection.ToViewY(art.BackgroundHeight)).Within(0.01f));
        }

        [Test]
        public void WideView_KeepsTheWholeFloorOnScreen()
        {
            // iPhone portrait: the terrarium area is wider than the 240x321 art.
            var projection = new TerrariumProjection(393f, 360f, art);

            projection.FloorLine(0f, out var backY, out _, out _);
            projection.FloorLine(1f, out var frontY, out _, out _);

            Assert.That(backY, Is.GreaterThan(0f));
            Assert.That(frontY, Is.LessThan(360f));
        }

        [Test]
        public void PlaceHanging_WhenTheCeilingIsCroppedOff_HangsFromTheViewTop()
        {
            var projection = new TerrariumProjection(393f, 200f, art);
            var lamp = art.Decor["heat_lamp_01"];

            var box = projection.PlaceHanging(lamp.Sprite, lamp.X, 100f);

            Assert.That(projection.ToViewY(art.CeilingY), Is.LessThan(0f));
            Assert.That(box.Top + lamp.Sprite.BodyTop / lamp.Sprite.ImageHeight * box.Height, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void PlaceOnFloor_PutsThePetsFeetExactlyOnTheFloorLine(
            [Values(0, 1)] int sizeIndex, [Values(0f, 0.5f, 1f)] float depth)
        {
            var size = ViewSizes[sizeIndex];
            var projection = new TerrariumProjection(size[0], size[1], art);
            var pet = art.Pet;

            var box = projection.PlaceOnFloor(pet, 0.5f, depth, 90f);

            projection.FloorLine(depth, out var floorY, out _, out _);
            var feetY = box.Top + pet.BodyBottom / pet.ImageHeight * box.Height;
            Assert.That(feetY, Is.EqualTo(floorY).Within(0.01f));
        }

        [Test]
        public void PlaceOnFloor_KeepsTheVisibleBodyInsideTheFloorEdges([Values(0f, 1f)] float x, [Values(0f, 1f)] float depth)
        {
            var projection = new TerrariumProjection(393f, 300f, art);
            var pet = art.Pet;

            var box = projection.PlaceOnFloor(pet, x, depth, 90f);

            projection.FloorLine(depth, out _, out var floorLeft, out var floorRight);
            var pixelsPerSource = box.Width / pet.ImageWidth;
            var bodyLeft = box.Left + pet.BodyLeft * pixelsPerSource;
            var bodyRight = box.Left + pet.BodyRight * pixelsPerSource;
            Assert.That(bodyLeft, Is.GreaterThanOrEqualTo(floorLeft - 0.01f));
            Assert.That(bodyRight, Is.LessThanOrEqualTo(floorRight + 0.01f));
        }

        [Test]
        public void PlaceOnFloor_KeepsTheSpritesOwnAspectRatio()
        {
            var projection = new TerrariumProjection(393f, 300f, art);
            var rock = art.Decor["rock_01"].Sprite;

            var box = projection.PlaceOnFloor(rock, 0.3f, 0.2f, 100f);

            Assert.That(box.Width / box.Height, Is.EqualTo(rock.ImageWidth / rock.ImageHeight).Within(1e-4f));
        }

        [Test]
        public void PlaceOnFloor_DrawsSpritesAtTheBackSmallerThanAtTheFront()
        {
            var projection = new TerrariumProjection(393f, 300f, art);

            var back = projection.PlaceOnFloor(art.Pet, 0.5f, 0f, 90f);
            var front = projection.PlaceOnFloor(art.Pet, 0.5f, 1f, 90f);

            Assert.That(back.Width, Is.LessThan(front.Width));
        }

        [Test]
        public void PlaceHanging_AttachesTheLampTopToTheCeiling()
        {
            // Tall view: the whole art fits vertically, so the ceiling is on screen.
            var projection = new TerrariumProjection(360f, 560f, art);
            var lamp = art.Decor["heat_lamp_01"];

            var box = projection.PlaceHanging(lamp.Sprite, lamp.X, 100f);

            var bodyTop = box.Top + lamp.Sprite.BodyTop / lamp.Sprite.ImageHeight * box.Height;
            Assert.That(bodyTop, Is.EqualTo(projection.ToViewY(art.CeilingY)).Within(0.01f));
        }
    }
}
