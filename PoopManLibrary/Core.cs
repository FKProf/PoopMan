using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace PoopManLibrary;

public class Core : Game
{
    internal static Core p_istance;

    // Gets a reference to the singleton instance of the Core class.
    public static Core Instance => p_istance;

    // Gets the graphics device manager to control the presentation of graphics.
    public static GraphicsDeviceManager Graphics { get; private set; }

    // Gets the content manager to load game assets.
    public static new GraphicsDevice GraphicsDevice { get; private set; }

    // Gets the sprite batch for efficient rendering of 2D sprites.
    public static SpriteBatch SpriteBatch { get; private set; }

    // Gets the content manager to load game assets.
    public static ContentManager ContentManager { get; private set; }

    public Core (string Title, int width, int height, bool fullScreen)
    {
        if (p_istance != null)
            throw new InvalidComObjectException("Only a single Core instance can be created.");

        p_istance = this; // restore access to the singleton instance
        Graphics = new GraphicsDeviceManager(this);

        // Set graphics properties
        Graphics.PreferredBackBufferWidth = width;
        Graphics.PreferredBackBufferHeight = height;
        Graphics.IsFullScreen = fullScreen;

        Graphics.ApplyChanges();
        ContentManager = base.Content;
        IsMouseVisible = true;
        Window.Title = Title;
        Content = base.Content;
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

    }

    protected override void Initialize()
    {
        GraphicsDevice = base.GraphicsDevice;
        SpriteBatch = new SpriteBatch(GraphicsDevice);
        base.Initialize();
    }
}
