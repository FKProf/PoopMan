using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PoopMan.Scenes;
using PoopMan.UI;
using PoopManLibrary;
using PoopManLibrary.World;
using System;

namespace PoopMan;

public class Game1 : Core
{
    // Dimensioni base della mappa (pixel naturali)
    private const int MapWidth = TileMap.Cols * TileMap.TileSize; // 39×32 = 1248
    private const int MapHeight = TileMap.Rows * TileMap.TileSize; // 23×32 = 736
    private const int DefaultWidth = MapWidth;
    private const int DefaultHeight = MapHeight + GameHud.Height; // 736+20 = 756

    public Game1() : base("PoopMan Miner", DefaultWidth, DefaultHeight, false)
    {
        Graphics.GraphicsProfile = GraphicsProfile.HiDef;
        Window.AllowUserResizing = true;
        Graphics.SynchronizeWithVerticalRetrace = true;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
    }

    protected override void Initialize()
    {
        base.Initialize();
        LeaderboardManager.Load();
        ChangeScene(new TitleScene());
    }

    protected override void Update(GameTime gameTime)
    {
        if (GameController.ToggleFullScreen())
        {
            Graphics.IsFullScreen = !Graphics.IsFullScreen;
            if (!Graphics.IsFullScreen)
            {
                Graphics.PreferredBackBufferWidth = DefaultWidth;
                Graphics.PreferredBackBufferHeight = DefaultHeight;
            }
            else
            {
                Graphics.PreferredBackBufferWidth = GraphicsDevice.Adapter.CurrentDisplayMode.Width;
                Graphics.PreferredBackBufferHeight = GraphicsDevice.Adapter.CurrentDisplayMode.Height;
            }

            Graphics.ApplyChanges();
        }

        base.Update(gameTime);
    }

    /// <summary>
    ///     Calcola la matrice di scala letterbox che adatta la mappa all'area disponibile
    ///     mantenendo l'aspect ratio. Usata da GameScene per il rendering.
    /// </summary>
    public static Matrix GetMapScaleMatrix(int hudHeight)
    {
        var vw = GraphicsDevice.Viewport.Width;
        var vh = GraphicsDevice.Viewport.Height;
        var availH = vh - hudHeight;

        var scaleX = (float)vw / MapWidth;
        var scaleY = (float)availH / MapHeight;
        var scale = scaleX < scaleY ? scaleX : scaleY;

        // Centra la mappa nell'area disponibile
        var offsetX = (vw - MapWidth * scale) / 2f;
        var offsetY = hudHeight + (availH - MapHeight * scale) / 2f;

        return Matrix.CreateScale(scale, scale, 1f)
               * Matrix.CreateTranslation(offsetX, offsetY, 0f);
    }
}