using System;
// Add using for MonoGame Vector2
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

namespace BeeSwarmGame;

public struct Vector2
{
    public double X { get; set; }
    public double Y { get; set; }

    public Vector2(double x, double y)
    {
        X = x;
        Y = y;
    }

    public static Vector2 Zero => new Vector2(0, 0);

    public double Length() => Math.Sqrt(X * X + Y * Y);

    public double LengthSquared() => X * X + Y * Y;

    public void Normalize()
    {
        double length = Length();
        if (length > 1e-9) // Avoid division by zero
        {
            X /= length;
            Y /= length;
        }
    }

    public static Vector2 Normalize(Vector2 value)
    {
        Vector2 result = value;
        result.Normalize();
        return result;
    }

    public static double Distance(Vector2 value1, Vector2 value2)
    {
        double dx = value1.X - value2.X;
        double dy = value1.Y - value2.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

     public static double DistanceSquared(Vector2 value1, Vector2 value2)
    {
        double dx = value1.X - value2.X;
        double dy = value1.Y - value2.Y;
        return dx * dx + dy * dy;
    }

    public static Vector2 operator +(Vector2 value1, Vector2 value2)
    {
        return new Vector2(value1.X + value2.X, value1.Y + value2.Y);
    }

    public static Vector2 operator -(Vector2 value1, Vector2 value2)
    {
        return new Vector2(value1.X - value2.X, value1.Y - value2.Y);
    }

    public static Vector2 operator *(Vector2 value, double scalar)
    {
        return new Vector2(value.X * scalar, value.Y * scalar);
    }

     public static Vector2 operator *(double scalar, Vector2 value)
    {
        return new Vector2(value.X * scalar, value.Y * scalar);
    }

    public static Vector2 operator /(Vector2 value, double scalar)
    {
        if (Math.Abs(scalar) < 1e-9)
            throw new DivideByZeroException("Cannot divide Vector2 by zero.");
        return new Vector2(value.X / scalar, value.Y / scalar);
    }

    public override string ToString() => $"({X:F1}, {Y:F1})";

    public override bool Equals(object? obj) => obj is Vector2 other && this.Equals(other);

    public bool Equals(Vector2 other) => X == other.X && Y == other.Y;

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public static bool operator ==(Vector2 left, Vector2 right) => left.Equals(right);

    public static bool operator !=(Vector2 left, Vector2 right) => !(left == right);

    // --- Conversion Helpers ---
    public XnaVector2 ToXnaVector2()
    {
        return new XnaVector2((float)X, (float)Y);
    }

    public static Vector2 FromXnaVector2(XnaVector2 xnaVector)
    {
        return new Vector2(xnaVector.X, xnaVector.Y);
    }

    // Optional: Implicit/Explicit operators (use with caution)
    // public static implicit operator XnaVector2(Vector2 v) => new XnaVector2((float)v.X, (float)v.Y);
    // public static explicit operator Vector2(XnaVector2 v) => new Vector2(v.X, v.Y);
}
