using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PoopManLibrary.Graphics;

public class TextureRegion
{
    public string Name { get; set; }

    public Texture2D Texture { get; set; }

    public int Width => Bounds.Width;

    public int Height => Bounds.Height;

    public Rectangle Bounds { get; set; }

    public TextureRegion() { }

    public TextureRegion (Texture2D texture, int x, int y, int width, int height)
    {
        Texture = texture;
        Bounds = new Rectangle(x, y, width, height);
    }

    public void Draw(SpriteBatch spriteBatch, Vector2 position, Color color)
    {
        Draw(spriteBatch, position, color, 0.0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0.0f);
    }

    public void Draw(SpriteBatch spritBatch, Vector2 position, Color color,
        float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)
    {
        Draw(
            spritBatch, 
            position,
            color, 
            rotation, 
            origin, 
            new Vector2(scale, scale), 
            effects, 
            layerDepth);
    }

    
    public void Draw (SpriteBatch spriteBatch, Vector2 position, Color color,
        float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)
    {
        spriteBatch.Draw(
            Texture,
            position,
            Bounds,
            color,
            rotation,
            origin,
            scale,
            effects,
            layerDepth
            );
    }

    public float TopTextureCoordinate => Bounds.Top / (float)Texture.Height;

    public float BottomTextureCoordinate => Bounds.Bottom / (float)Texture.Height;

    public float LeftTextureCoordinate => Bounds.Left / (float)Texture.Width;

    public float RightTextureCoordinate => Bounds.Right / (float)Texture.Width;

}