using System;

namespace TerrariumDays.Core
{
    /// <summary>An opaque colour without Unity types, so palettes are EditMode tested.</summary>
    public readonly struct Rgb : IEquatable<Rgb>
    {
        public Rgb(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public Rgb(int r, int g, int b) : this((byte)r, (byte)g, (byte)b)
        {
        }

        public byte R { get; }

        public byte G { get; }

        public byte B { get; }

        public static Rgb Lerp(Rgb from, Rgb to, double t) => new Rgb(
            (int)Math.Round(from.R + (to.R - from.R) * t),
            (int)Math.Round(from.G + (to.G - from.G) * t),
            (int)Math.Round(from.B + (to.B - from.B) * t));

        public bool Equals(Rgb other) => R == other.R && G == other.G && B == other.B;

        public override bool Equals(object obj) => obj is Rgb other && Equals(other);

        public override int GetHashCode() => (R << 16) | (G << 8) | B;

        public override string ToString() => $"{R:X2}{G:X2}{B:X2}";
    }
}
