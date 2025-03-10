using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenSage.Mathematics;

[StructLayout(LayoutKind.Sequential)]
public readonly struct ColorRgbaF : IEquatable<ColorRgbaF>
{
    public static readonly ColorRgbaF Transparent = new();
    public static readonly ColorRgbaF White = new(1.0f, 1.0f, 1.0f, 1.0f);
    public static readonly ColorRgbaF Black = new(0.0f, 0.0f, 0.0f, 1.0f);
    public static readonly ColorRgbaF Red = new(1.0f, 0.0f, 0.0f, 1.0f);
    public static readonly ColorRgbaF Green = new(0.0f, 1.0f, 0.0f, 1.0f);
    public static readonly ColorRgbaF Blue = new(0.0f, 0.0f, 1.0f, 1.0f);

    public readonly float R;
    public readonly float G;
    public readonly float B;
    public readonly float A;

    public ColorRgbaF(float r, float g, float b, float a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public static ColorRgbaF operator *(in ColorRgbaF value1, in ColorRgbaF value2)
    {
        return new ColorRgbaF(
            value1.R * value2.R,
            value1.G * value2.G,
            value1.B * value2.B,
            value1.A * value2.A);
    }

    public static ColorRgbaF operator +(in ColorRgbaF value1, in ColorRgbaF value2)
    {
        return new ColorRgbaF(
            value1.R + value2.R,
            value1.G + value2.G,
            value1.B + value2.B,
            value1.A + value2.A);
    }

    public static bool operator ==(in ColorRgbaF f1, in ColorRgbaF f2)
    {
        return f1.Equals(f2);
    }

    public static bool operator !=(in ColorRgbaF f1, in ColorRgbaF f2)
    {
        return !(f1 == f2);
    }

    public ColorRgbaF WithA(float a)
    {
        return new ColorRgbaF(R, G, B, a);
    }

    public ColorRgbaF WithRGB(float r, float g, float b)
    {
        return new ColorRgbaF(r, g, b, A);
    }

    public override bool Equals(object? obj)
    {
        return obj is ColorRgbaF && Equals((ColorRgbaF)obj);
    }

    public bool Equals(ColorRgbaF other)
    {
        return
            Math.Abs(R - other.R) < float.Epsilon &&
            Math.Abs(G - other.G) < float.Epsilon &&
            Math.Abs(B - other.B) < float.Epsilon &&
            Math.Abs(A - other.A) < float.Epsilon;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(R, G, B, A);
    }

    public Vector4 ToVector4()
    {
        return new Vector4(R, G, B, A);
    }

    public ColorRgba ToColorRgba()
    {
        return new ColorRgba(Transform(R), Transform(G), Transform(B), Transform(A));

        static byte Transform(float f) => (byte)(f * 255.0f);
    }
}
