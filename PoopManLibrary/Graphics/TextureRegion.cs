using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PoopManLibrary.Graphics;

public class TextureRegion
{
    public string Name { get; set; }
    public Rectangle Bounds { get; set; }

    public TextureRegion(string name, int x, int y, int width, int height)
    {
        Name = name;
        Bounds = new Rectangle(x, y, width, height);
    }

    public TextureRegion(string name, Rectangle bounds)
    {
        Name = name;
        Bounds = bounds;
    }
}