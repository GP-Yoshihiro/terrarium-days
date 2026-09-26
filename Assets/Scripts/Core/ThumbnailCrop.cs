using System;

namespace TerrariumDays.Core
{
    /// <summary>A rectangle in texture pixels, Y measured up from the bottom (Unity's Sprite.Create rect).</summary>
    public readonly struct PixelRect
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Width;
        public readonly float Height;

        public PixelRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    /// <summary>Crops a thumbnail to the sprite's visible body so it is not mostly transparent padding.</summary>
    public static class ThumbnailCrop
    {
        public static PixelRect BodyRect(SpriteFootprint sprite, float padding)
        {
            var left = Math.Max(0f, sprite.BodyLeft - padding);
            var right = Math.Min(sprite.ImageWidth, sprite.BodyRight + padding);
            var top = Math.Max(0f, sprite.BodyTop - padding);
            var bottom = Math.Min(sprite.ImageHeight, sprite.BodyBottom + padding);
            return new PixelRect(left, sprite.ImageHeight - bottom, right - left, bottom - top);
        }
    }
}
