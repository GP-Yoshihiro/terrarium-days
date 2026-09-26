using System.Collections.Generic;

namespace TerrariumDays.Core
{
    /// <summary>
    /// Where things are inside the art, measured in source-image pixels: the floor
    /// trapezoid and ceiling of terrarium_background.png, and where each sprite's visible
    /// body sits inside its (transparent-padded) PNG. Update these when the art changes.
    /// </summary>
    public sealed class TerrariumArtLayout
    {
        public float BackgroundWidth { get; set; } = 240f;
        public float BackgroundHeight { get; set; } = 321f;

        // Walkable top surface of the substrate (not its front face).
        public float FloorBackY { get; set; } = 242f;
        public float FloorBackLeft { get; set; } = 25f;
        public float FloorBackRight { get; set; } = 215f;
        public float FloorFrontY { get; set; } = 297f;
        public float FloorFrontLeft { get; set; } = 11f;
        public float FloorFrontRight { get; set; } = 229f;

        /// <summary>Inner edge of the glass top, where hanging decor attaches.</summary>
        public float CeilingY { get; set; } = 11f;

        /// <summary>Sprites drawn smaller at the back of the floor than at the front.</summary>
        public float BackDepthScale { get; set; } = 0.8f;

        /// <summary>Measured on Resources/Gecko/idle_00.png (tools/sprites/gecko_sprites.py).</summary>
        public SpriteFootprint Pet { get; set; } = new SpriteFootprint(256f, 256f, 22f, 204f, 98f, 168f);

        /// <summary>
        /// How far the body travels over one full walk cycle, in pet-sprite pixels (see
        /// WALK_FRAMES / WALK_STEP in tools/sprites/gecko_sprites.py). Walk frames advance by
        /// distance moved against this stride, so planted feet never slide on the floor.
        /// </summary>
        public float PetWalkStride { get; set; } = 24f;

        public IReadOnlyDictionary<string, DecorPlacement> Decor { get; set; } = new Dictionary<string, DecorPlacement>
        {
            // Floor decor stands inside the sand area (depth 0.35–0.6), not on its back edge.
            ["rock_01"] = DecorPlacement.OnFloor(new SpriteFootprint(200f, 161f, 37f, 178f, 56f, 153f), 0.15f, 0.45f, 0.28f),
            ["plant_01"] = DecorPlacement.OnFloor(new SpriteFootprint(180f, 159f, 63f, 132f, 45f, 140f), 0.08f, 0.35f, 0.18f),
            ["water_dish_01"] = DecorPlacement.OnFloor(new SpriteFootprint(150f, 195f, 19f, 131f, 59f, 145f), 0.85f, 0.6f, 0.26f),
            ["heat_lamp_01"] = DecorPlacement.Hanging(new SpriteFootprint(180f, 164f, 35f, 137f, 0f, 106f), 0.72f, 0.3f),
            ["driftwood_01"] = DecorPlacement.OnFloor(new SpriteFootprint(180f, 164f, 20f, 156f, 17f, 111f), 0.12f, 0.5f, 0.38f),
        };

        /// <summary>
        /// Where a cage's decor slots stand on the floor, by slot index (X, Depth). Floor decor
        /// uses its slot's spot so up to three pieces do not overlap; hanging decor keeps its own X.
        /// </summary>
        public IReadOnlyList<(float X, float Depth)> DecorSlotSpots { get; set; } = new List<(float, float)>
        {
            (0.15f, 0.45f),
            (0.85f, 0.6f),
            (0.5f, 0.38f),
        };

        public DecorPlacement PlacementFor(string decorId, int slot)
        {
            var own = Decor[decorId];
            if (own.IsHanging || slot < 0 || slot >= DecorSlotSpots.Count)
            {
                return own;
            }

            var spot = DecorSlotSpots[slot];
            return DecorPlacement.OnFloor(own.Sprite, spot.X, spot.Depth, own.BodyWidthFraction);
        }
    }

    /// <summary>
    /// A sprite's PNG size and the box its visible pixels occupy (source pixels;
    /// right/bottom are exclusive). Bottom is the ground contact line: the base of the
    /// object's outline, not its drop shadow (a shadow below the base made decor float).
    /// </summary>
    public readonly struct SpriteFootprint
    {
        public readonly float ImageWidth;
        public readonly float ImageHeight;
        public readonly float BodyLeft;
        public readonly float BodyRight;
        public readonly float BodyTop;
        public readonly float BodyBottom;

        public SpriteFootprint(float imageWidth, float imageHeight, float bodyLeft, float bodyRight, float bodyTop, float bodyBottom)
        {
            ImageWidth = imageWidth;
            ImageHeight = imageHeight;
            BodyLeft = bodyLeft;
            BodyRight = bodyRight;
            BodyTop = bodyTop;
            BodyBottom = bodyBottom;
        }
    }

    /// <summary>
    /// Where a decoration stands. Floor decor uses floor coordinates (X 0 = left edge,
    /// 1 = right edge; Depth 0 = back, 1 = front); hanging decor uses X only.
    /// BodyWidthFraction is the visible body width as a fraction of the terrarium view width.
    /// </summary>
    public readonly struct DecorPlacement
    {
        public readonly SpriteFootprint Sprite;
        public readonly bool IsHanging;
        public readonly float X;
        public readonly float Depth;
        public readonly float BodyWidthFraction;

        private DecorPlacement(SpriteFootprint sprite, bool isHanging, float x, float depth, float bodyWidthFraction)
        {
            Sprite = sprite;
            IsHanging = isHanging;
            X = x;
            Depth = depth;
            BodyWidthFraction = bodyWidthFraction;
        }

        public static DecorPlacement OnFloor(SpriteFootprint sprite, float x, float depth, float bodyWidthFraction)
        {
            return new DecorPlacement(sprite, false, x, depth, bodyWidthFraction);
        }

        public static DecorPlacement Hanging(SpriteFootprint sprite, float x, float bodyWidthFraction)
        {
            return new DecorPlacement(sprite, true, x, 0f, bodyWidthFraction);
        }
    }
}
