using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PoopManLibrary.World;

public class TileAtlas
{
    public TileAtlas(Texture2D texture)
    {
        Texture = texture;
    }

    public Texture2D Texture { get; }
    public Dictionary<string, Rectangle> Tiles { get; } = new();

    public void AddTile(string name, int x, int y, int w, int h)
    {
        Tiles[name] = new Rectangle(x, y, w, h);
    }

    public Rectangle GetTile(string name)
    {
        return Tiles[name];
    }
}