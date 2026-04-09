using System;
using Microsoft.Xna.Framework;

namespace PoopManLibrary;

public readonly struct Collision : IEquatable<Collision>
{
    private static readonly Collision empty = new Collision();

    public readonly int X;
    public readonly int Y;
    public readonly int Radius;

    public readonly Point Location => new Point(X, Y);

    public static Collision Empty => empty;

    public readonly bool IsEmpty => X == 0 && Y == 0 && Radius == 0;

    public readonly int Top    => Y - Radius;
    public readonly int Bottom => Y + Radius;
    public readonly int Left   => X - Radius;
    public readonly int Right  => X + Radius;

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
        var radiiSquared = (this.Radius + other.Radius) * (this.Radius + other.Radius);
        var distanceSquared = Vector2.DistanceSquared(this.Location.ToVector2(), other.Location.ToVector2());
        return distanceSquared <= radiiSquared;
    }

    public override readonly bool Equals(object obj) => obj is Collision other && Equals(other);

    public readonly bool Equals(Collision other) => this.X == other.X &&
                                                    this.Y == other.Y &&
                                                    this.Radius == other.Radius;

    public override readonly int GetHashCode() => HashCode.Combine(X, Y, Radius);

    public static bool operator ==(Collision lhs, Collision rhs) => lhs.Equals(rhs);

    public static bool operator !=(Collision lhs, Collision rhs) => !lhs.Equals(rhs);
}