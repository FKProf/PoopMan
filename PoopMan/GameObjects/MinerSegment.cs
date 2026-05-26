using Microsoft.Xna.Framework;

namespace PoopMan.GameObjects;

public struct MinerSegment
{
    public Vector2 At;

    public Vector2 To;

    public Vector2 Direction;

    public Vector2 ReverseDirection => new(-Direction.X, -Direction.Y);
}