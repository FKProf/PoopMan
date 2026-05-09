using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PoopManLibrary;
using PoopManLibrary.Scenes;

namespace PoopMan.Scenes;

public class TitleScene : Scene
{
    private SpriteBatch _sb;
    private SpriteFont  _font;
    private Texture2D   _pixel;

    private Texture2D _bgFixed;     // 1.png — sfondo fisso
    private Texture2D _cloud1;      // 2.png — nuvole veloci
    private Texture2D _cloud2;      // 3.png — nuvole lente

    // Posizioni X di scroll per ciascun layer nuvola (due copie per seamless loop)
    private float _c1X = 0f;
    private float _c2X = 0f;

    private const float Cloud1Speed =  55f;   // px/s (più veloci)
    private const float Cloud2Speed =  28f;   // px/s (più lente)

    private float _blinkTimer   = 0f;
    private bool  _blinkVisible = true;

    public override void LoadContent()
    {
        base.LoadContent();
        _sb   = new SpriteBatch(Core.GraphicsDevice);
        _font = Content.Load<SpriteFont>("font/Score");

        _bgFixed = Content.Load<Texture2D>("image/backgound/1");
        _cloud1  = Content.Load<Texture2D>("image/backgound/2");
        _cloud2  = Content.Load<Texture2D>("image/backgound/3");

        // Pixel 1x1 bianco per rettangoli
        _pixel = new Texture2D(Core.GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Scorrimento nuvole: si spostano a sinistra e wrappano
        _c1X -= Cloud1Speed * dt;
        _c2X -= Cloud2Speed * dt;

        // Blink
        _blinkTimer += dt;
        if (_blinkTimer >= 0.55f) { _blinkTimer = 0f; _blinkVisible = !_blinkVisible; }

        if (Core.Input.Keyboard.WasKeyJustPressed(Keys.Enter))
            Core.ChangeScene(new GameScene());
    }

    public override void Draw(GameTime gameTime)
    {
        int W = Core.GraphicsDevice.Viewport.Width;
        int H = Core.GraphicsDevice.Viewport.Height;

        Core.GraphicsDevice.Clear(Color.Black);

        _sb.Begin(samplerState: SamplerState.LinearWrap);

        // ── Layer 1: sfondo fisso (1.png), copre tutta la finestra ───────
        _sb.Draw(_bgFixed, new Rectangle(0, 0, W, H), Color.White);

        // ── Layer 2: nuvole veloci (2.png) in scroll seamless ─────────────
        DrawScrollingCloud(_cloud1, _c1X, W, H, 0.55f);

        // ── Layer 3: nuvole lente (3.png) in scroll seamless ─────────────
        DrawScrollingCloud(_cloud2, _c2X, W, H, 0.45f);

        // ── Overlay scuro per leggibilità testo ───────────────────────────
        _sb.Draw(_pixel, new Rectangle(0, 0, W, H), Color.Black * 0.42f);

        _sb.End();

        _sb.Begin(samplerState: SamplerState.PointClamp);

        // ── Titolo ────────────────────────────────────────────────────────
        DrawTextCentered("POOPMAN",   W / 2, H / 3,       Color.Yellow,            3f);
        DrawTextCentered("MINER",     W / 2, H / 3 + 75,  new Color(255, 160, 40), 2f);
        _sb.Draw(_pixel, new Rectangle(W / 2 - 200, H / 2 - 15, 400, 2), new Color(70, 50, 140));

        if (_blinkVisible)
            DrawTextCentered("PREMI  ENTER  PER  INIZIARE", W / 2, H / 2 + 32, Color.White, 1f);

        // ── Tasti ─────────────────────────────────────────────────────────
        DrawTextCentered("WASD / FRECCE : muovi",                     W / 2, H * 3 / 4,       Color.LightGray, 1f);
        DrawTextCentered("SPAZIO : bomba piccola    X : bomba grande", W / 2, H * 3 / 4 + 22, Color.LightGray, 1f);
        DrawTextCentered("F11 : fullscreen    ESC : pausa",            W / 2, H * 3 / 4 + 44, Color.Gray,      1f);

        _sb.DrawString(_font, "v0.2", new Vector2(8, H - 18), Color.DarkGray * 0.7f);

        _sb.End();
    }

    /// <summary>Disegna una texture in scroll orizzontale seamless (due copie affiancate).</summary>
    private void DrawScrollingCloud(Texture2D tex, float offsetX, int W, int H, float alpha)
    {
        // Normalizza l'offset in [0, W) per il loop
        float x = offsetX % W;
        if (x > 0) x -= W;   // mantieni sempre a sinistra di zero

        // Due copie affiancate per coprire tutta la larghezza senza buco
        _sb.Draw(tex, new Rectangle((int)x,     0, W, H), Color.White * alpha);
        _sb.Draw(tex, new Rectangle((int)x + W, 0, W, H), Color.White * alpha);
    }

    private void DrawTextCentered(string text, int cx, int cy, Color color, float scale)
    {
        Vector2 origin = _font.MeasureString(text) * 0.5f;
        Vector2 pos    = new Vector2(cx, cy);
        _sb.DrawString(_font, text, pos + new Vector2(2, 2) * scale, Color.Black * 0.55f, 0f, origin, scale, SpriteEffects.None, 0f);
        _sb.DrawString(_font, text, pos,                              color,               0f, origin, scale, SpriteEffects.None, 0f);
    }
}
