using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace PoopManLibrary.World
{
    public class TileAtlas
    {
        public Texture2D Texture;
        public Dictionary<string, Rectangle> Tiles;

        public TileAtlas(Texture2D texture)
        {
            Texture = texture;
            Tiles = new Dictionary<string, Rectangle>();
        }

        public void AddTile(string name, int x, int y, int w, int h)
        {
            Tiles[name] = new Rectangle(x, y, w, h);
        }

        public Rectangle GetTile(string name)
        {
            return Tiles[name];
        }
    }
}