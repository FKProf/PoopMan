using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PoopManLibrary.Input;

namespace PoopManLibrary;

public class Core : Game
{
    internal static Core p_istance;

    public static Core Instance => p_istance;

    public static GraphicsDeviceManager Graphics { get; private set; }

    public static new GraphicsDevice GraphicsDevice { get; private set; }

    public static SpriteBatch SpriteBatch { get; private set; }

    public static ContentManager ContentManager { get; private set; }

    public static InputManager Input { get; private set; }

    public Core(string Title, int width, int height, bool fullScreen)
    {
        if (p_istance != null)
            throw new InvalidComObjectException("Only a single Core instance can be created.");

        p_istance = this;
        Graphics = new GraphicsDeviceManager(this);

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
        base.Initialize();
        SpriteBatch = new SpriteBatch(GraphicsDevice);
        Input = new InputManager();
    }

    protected override void Update(GameTime gameTime)
    {
        Input.Update(); // ✅ aggiorna keyboard/mouse ogni frame
        base.Update(gameTime);
    }
}