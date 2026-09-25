using System;

namespace TerrariumDays.Core
{
    /// <summary>A placed element box in terrarium-view pixels (origin top-left).</summary>
    public readonly struct ElementBox
    {
        public readonly float Left;
        public readonly float Top;
        public readonly float Width;
        public readonly float Height;

        public ElementBox(float left, float top, float width, float height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    /// Maps art coordinates onto the terrarium view, reproducing the background's
    /// "background-size: cover" anchored center-bottom (uniform scale to cover; horizontal
    /// overflow cropped evenly, vertical overflow cropped from the top), and places
    /// sprites so their visible feet sit exactly on the floor. Pure so it is EditMode tested.
    /// </summary>
    public sealed class TerrariumProjection
    {
        private readonly TerrariumArtLayout art;
        private readonly float scale;
        private readonly float offsetX;
        private readonly float offsetY;

        public TerrariumProjection(float viewWidth, float viewHeight, TerrariumArtLayout art)
        {
            this.art = art;
            ViewWidth = viewWidth;
            ViewHeight = viewHeight;
            scale = Math.Max(viewWidth / art.BackgroundWidth, viewHeight / art.BackgroundHeight);
            offsetX = (viewWidth - art.BackgroundWidth * scale) / 2f;
            // Bottom-anchored: any vertical overflow comes off the ceiling, never the floor.
            offsetY = viewHeight - art.BackgroundHeight * scale;
        }

        public float ViewWidth { get; }

        public float ViewHeight { get; }

        public bool IsValid => ViewWidth > 0f && ViewHeight > 0f;

        public float ToViewX(float artX) => offsetX + artX * scale;

        /// <summary>Where the whole background image is drawn (it may overflow the view).</summary>
        public ElementBox BackgroundBox()
        {
            return new ElementBox(offsetX, offsetY, art.BackgroundWidth * scale, art.BackgroundHeight * scale);
        }

        public float ToViewY(float artY) => offsetY + artY * scale;

        /// <summary>Floor line at a depth (0 = back edge, 1 = front edge), in view pixels.</summary>
        public void FloorLine(float depth, out float y, out float left, out float right)
        {
            depth = Clamp01(depth);
            y = ToViewY(Lerp(art.FloorBackY, art.FloorFrontY, depth));
            left = ToViewX(Lerp(art.FloorBackLeft, art.FloorFrontLeft, depth));
            right = ToViewX(Lerp(art.FloorBackRight, art.FloorFrontRight, depth));
        }

        public float DepthScale(float depth) => Lerp(art.BackDepthScale, 1f, Clamp01(depth));

        /// <summary>
        /// Places a sprite standing on the floor. x (0–1) slides its visible body from the
        /// floor's left edge to its right edge; bodyWidth is in view pixels before depth scaling.
        /// </summary>
        public ElementBox PlaceOnFloor(SpriteFootprint sprite, float x, float depth, float bodyWidth)
        {
            FloorLine(depth, out var floorY, out var floorLeft, out var floorRight);
            var size = SizeFor(sprite, bodyWidth * DepthScale(depth), out var pixelsPerSourcePixel);

            var body = (sprite.BodyRight - sprite.BodyLeft) * pixelsPerSourcePixel;
            var travel = Math.Max(0f, floorRight - floorLeft - body);
            var bodyLeft = floorLeft + Clamp01(x) * travel;

            return new ElementBox(
                bodyLeft - sprite.BodyLeft * pixelsPerSourcePixel,
                floorY - sprite.BodyBottom * pixelsPerSourcePixel,
                size.Width,
                size.Height);
        }

        /// <summary>
        /// Inverse of PlaceOnFloor for X: the floor X (0–1) that centres the sprite's visible
        /// body on <paramref name="bodyCenterX"/> (view pixels) at that depth.
        /// </summary>
        public float FloorXForBodyCenter(SpriteFootprint sprite, float depth, float bodyWidth, float bodyCenterX)
        {
            FloorLine(depth, out _, out var floorLeft, out var floorRight);
            var body = bodyWidth * DepthScale(depth);
            var travel = floorRight - floorLeft - body;
            if (travel <= 0f)
            {
                return 0.5f;
            }

            return Clamp01((bodyCenterX - body / 2f - floorLeft) / travel);
        }

        /// <summary>
        /// Places a sprite hanging from the glass top (or from the view's top edge when the
        /// ceiling is cropped off), centered at x (0–1 across the view).
        /// </summary>
        public ElementBox PlaceHanging(SpriteFootprint sprite, float x, float bodyWidth)
        {
            var size = SizeFor(sprite, bodyWidth, out var pixelsPerSourcePixel);
            var bodyCenter = (sprite.BodyLeft + sprite.BodyRight) / 2f * pixelsPerSourcePixel;

            return new ElementBox(
                Clamp01(x) * ViewWidth - bodyCenter,
                Math.Max(0f, ToViewY(art.CeilingY)) - sprite.BodyTop * pixelsPerSourcePixel,
                size.Width,
                size.Height);
        }

        private static ElementBox SizeFor(SpriteFootprint sprite, float bodyWidth, out float pixelsPerSourcePixel)
        {
            pixelsPerSourcePixel = bodyWidth / (sprite.BodyRight - sprite.BodyLeft);
            // Keep the element at the PNG's own aspect so scale-to-fit adds no letterboxing
            // and source pixel offsets map straight onto the element.
            return new ElementBox(0f, 0f, sprite.ImageWidth * pixelsPerSourcePixel, sprite.ImageHeight * pixelsPerSourcePixel);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
