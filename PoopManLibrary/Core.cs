using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PoopManLibrary.Input;
using PoopManLibrary.Scenes;

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

    private static Scene p_activeScene;

    private static Scene p_nextScene;

    public Core(string Title, int width, int height, bool fullScreen)
    {
        if (p_istance != null)
            throw new InvalidComObjectException("Only a single Core instance can be created.");

        p_istance = this;
        Graphics = new GraphicsDeviceManager(this);

        Graphics.PreferredBackBufferWidth = width;
        Graphics.PreferredBackBufferHeight = height;
        Graphics.IsFullScreen = fullScreen;

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

        if (p_nextScene != null)
        {
            TransitionScene();
        }

        if (p_activeScene != null)
        {
            p_activeScene.Update(gameTime);
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.SetRenderTarget(null);
        if (p_activeScene == null)
            GraphicsDevice.Clear(Color.CornflowerBlue);
        if (p_activeScene != null)
        {
            p_activeScene.Draw(gameTime);
        }
        base.Draw(gameTime);
    }

    public static void ChangeScene(Scene newScene)
    {
        if (p_activeScene != newScene)
            p_nextScene = newScene;
    }

    private static void TransitionScene()
    {
        if (p_activeScene != null)
            p_activeScene.Dispose();

        GC.Collect();

        p_activeScene = p_nextScene;
        p_nextScene = null;

        if (p_activeScene != null)
            p_activeScene.Initialize();
    }
}