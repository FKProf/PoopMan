using Microsoft.Xna.Framework;
using System;

namespace PoopManLibrary;

public readonly struct Collision : IEquatable<Collision>
{
    public readonly int X;
    public readonly int Y;
    public readonly int Radius;

    public readonly Point Location => new(X, Y);

    public static Collision Empty { get; } = new();

    public readonly bool IsEmpty => X == 0 && Y == 0 && Radius == 0;

    public readonly int Top => Y - Radius;
    public readonly int Bottom => Y + Radius;
    public readonly int Left => X - Radius;
    public readonly int Right => X + Radius;

    public Collision(int x, int y, int radius)
    {
        X = x;
        Y = y;
        Radius = radius;
    }

    public Collision(Point location, int radius)
    {
        X = location.X;
        Y = location.Y;
        Radius = radius;
    }

    public bool Intersects(Collision other)
    {
        var radiiSquared = (Radius + other.Radius) * (Radius + other.Radius);
        var distanceSquared = Vector2.DistanceSquared(Location.ToVector2(), other.Location.ToVector2());
        return distanceSquared <= radiiSquared;
    }

    public readonly override bool Equals(object obj)
    {
        return obj is Collision other && Equals(other);
    }

    public readonly bool Equals(Collision other)
    {
        return X == other.X &&
               Y == other.Y &&
               Radius == other.Radius;
    }

    public readonly override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Radius);
    }

    public static bool operator ==(Collision lhs, Collision rhs)
    {
        return lhs.Equals(rhs);
    }

    public static bool operator !=(Collision lhs, Collision rhs)
    {
        return !lhs.Equals(rhs);
    }
}