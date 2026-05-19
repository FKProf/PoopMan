using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PoopMan.UI;
using PoopManLibrary;
using PoopManLibrary.Scenes;

namespace PoopMan.Scenes;

public class TitleScene : Scene
{
    // ── Grafica ────────────────────────────────────────────────────────
    private SpriteBatch _sb;
    private SpriteFont _font;
    private Texture2D _pixel;
    private Texture2D _bgFixed;
    private Texture2D _cloud1;
    private Texture2D _cloud2;

    // ── Nuvole ────────────────────────────────────────────────────────
    private float _c1X = 0f;
    private float _c2X = 0f;
    private const float Cloud1Speed = 55f;
    private const float Cloud2Speed = 28f;

    // ── Menu ──────────────────────────────────────────────────────────
    private enum MenuScreen { Main, Audio, Istruzioni }
    private MenuScreen _screen = MenuScreen.Main;
    private int _selectedItem = 0;

    private static readonly string[] MenuItems = { "GIOCA", "CLASSIFICA", "ISTRUZIONI", "AUDIO" };
    private const int BtnW = 260;
    private const int BtnH = 36;
    private const int BtnGap = 14;

    // ── Audio Panel ───────────────────────────────────────────────────
    private AudioSettingsPanel _audioPanel;

    // ── Animazione cursore ────────────────────────────────────────────
    private float _cursorPulse = 0f;

    public override void LoadContent()
    {
        base.LoadContent();
        _sb = new SpriteBatch(Core.GraphicsDevice);
        _font = Content.Load<SpriteFont>("font/Score");

        _bgFixed = Content.Load<Texture2D>("image/backgound/1");
        _cloud1 = Content.Load<Texture2D>("image/backgound/2");
        _cloud2 = Content.Load<Texture2D>("image/backgound/3");

        _pixel = new Texture2D(Core.GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _audioPanel = new AudioSettingsPanel(_font, _pixel);

        AudioManager.Load(Content);
        AudioManager.StartTitleAudio();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        _c1X -= Cloud1Speed * dt;
        _c2X -= Cloud2Speed * dt;
        _cursorPulse += dt * 3.5f;

        var kb = Core.Input.Keyboard;
        var mouse = Core.Input.Mouse;

        // ── Pannello audio ────────────────────────────────────────────
        if (_screen == MenuScreen.Audio)
        {
            _audioPanel.Update(gameTime);
            if (kb.WasKeyJustPressed(Keys.Escape) || kb.WasKeyJustPressed(Keys.Back))
                _screen = MenuScreen.Main;
            return;
        }

        // ── Pannello istruzioni ───────────────────────────────────────
        if (_screen == MenuScreen.Istruzioni)
        {
            if (kb.WasKeyJustPressed(Keys.Escape) || kb.WasKeyJustPressed(Keys.Back) ||
                kb.WasKeyJustPressed(Keys.Enter) ||
                mouse.WasButtonJustPressed(PoopManLibrary.Input.MouseButton.Left))
                _screen = MenuScreen.Main;
            return;
        }

        // ── Menu principale ───────────────────────────────────────────
        if (kb.WasKeyJustPressed(Keys.Up))
            _selectedItem = (_selectedItem - 1 + MenuItems.Length) % MenuItems.Length;
        if (kb.WasKeyJustPressed(Keys.Down))
            _selectedItem = (_selectedItem + 1) % MenuItems.Length;

        // Mouse: hover selects, click confirms (geometria identica a DrawMainMenu)
        {
            int W = Core.GraphicsDevice.Viewport.Width;
            int H = Core.GraphicsDevice.Viewport.Height;
            int titleY = H / 4;
            int startY = titleY + 128 + 20;   // sepY + 20
            int cx = W / 2;
            Point mp = mouse.Position;
            for (int i = 0; i < MenuItems.Length; i++)
            {
                int btnY = startY + i * (BtnH + BtnGap);
                var btnRect = new Rectangle(cx - BtnW / 2, btnY, BtnW, BtnH);
                if (btnRect.Contains(mp))
                {
                    _selectedItem = i;
                    if (mouse.WasButtonJustPressed(PoopManLibrary.Input.MouseButton.Left))
                    {
                        switch (i)
                        {
                            case 0:
                                AudioManager.StopTitleAudio();
                                Core.ChangeScene(new GameScene());
                                break;
                            case 1: Core.ChangeScene(new UI.LeaderboardScreen(fromGameOver: false)); break;
                            case 2: _screen = MenuScreen.Istruzioni; break;
                            case 3: _screen = MenuScreen.Audio; break;
                        }
                    }
                }
            }
        }

        if (kb.WasKeyJustPressed(Keys.Enter))
        {
            switch (_selectedItem)
            {
                case 0: // GIOCA
                    AudioManager.StopTitleAudio();
                    Core.ChangeScene(new GameScene());
                    break;
                case 1: // CLASSIFICA
                    Core.ChangeScene(new UI.LeaderboardScreen(fromGameOver: false));
                    break;
                case 2: // ISTRUZIONI
                    _screen = MenuScreen.Istruzioni;
                    break;
                case 3: // AUDIO
                    _screen = MenuScreen.Audio;
                    break;
            }
        }
    }

    public override void Draw(GameTime gameTime)
    {
        int W = Core.GraphicsDevice.Viewport.Width;
        int H = Core.GraphicsDevice.Viewport.Height;

        Core.GraphicsDevice.Clear(Color.Black);

        // Sfondo
        _sb.Begin(samplerState: SamplerState.LinearWrap);
        _sb.Draw(_bgFixed, new Rectangle(0, 0, W, H), Color.White);
        DrawScrollingCloud(_cloud1, _c1X, W, H, 0.55f);
        DrawScrollingCloud(_cloud2, _c2X, W, H, 0.45f);
        _sb.Draw(_pixel, new Rectangle(0, 0, W, H), Color.Black * 0.45f);
        _sb.End();

        _sb.Begin(samplerState: SamplerState.PointClamp);

        // Titolo
        int titleY = H / 4;
        DrawTextCentered("POOPMAN", W / 2, titleY, Color.Yellow, 3.0f);
        DrawTextCentered("MINER", W / 2, titleY + 78, new Color(255, 160, 40), 2.0f);

        // Linea separatrice
        int sepY = titleY + 128;
        _sb.Draw(_pixel, new Rectangle(W / 2 - 180, sepY, 360, 2), new Color(70, 50, 140));

        switch (_screen)
        {
            case MenuScreen.Main:
                DrawMainMenu(W, H, sepY + 20);
                break;
            case MenuScreen.Audio:
                DrawAudioOverlay(W, H);
                break;
            case MenuScreen.Istruzioni:
                DrawIstruzioniOverlay(W, H);
                break;
        }

        _sb.DrawString(_font, "PoopMan_Rank v0.1.0-alpha", new Vector2(8, H - 18), Color.DarkGray * 0.7f);
        _sb.End();
    }

    // ─────────────────────────────────────────────────────────────────
    private void DrawMainMenu(int W, int H, int startY)
    {
        int cx = W / 2;

        for (int i = 0; i < MenuItems.Length; i++)
        {
            int btnY = startY + i * (BtnH + BtnGap);
            bool sel = i == _selectedItem;

            // Sfondo pulsante
            Color bgColor = sel ? new Color(60, 40, 140, 230) : new Color(20, 20, 50, 180);
            Color border = sel ? Color.Yellow : new Color(80, 80, 120);
            float pulse = sel ? (0.85f + 0.15f * (float)Math.Sin(_cursorPulse)) : 1f;

            DrawRect(new Rectangle(cx - BtnW / 2 - 1, btnY - 1, BtnW + 2, BtnH + 2), border);
            DrawRect(new Rectangle(cx - BtnW / 2, btnY, BtnW, BtnH), bgColor);

            Color textColor = sel ? Color.Yellow * pulse : Color.LightGray;
            float scale = sel ? 1.05f : 1.0f;
            DrawTextCentered(MenuItems[i], cx, btnY + BtnH / 2, textColor, scale);

            // Cursore freccia
            if (sel)
            {
                string arrow = ">";
                Vector2 arSz = _font.MeasureString(arrow);
                float arX = cx - BtnW / 2 - arSz.X - 10;
                _sb.DrawString(_font, arrow,
                    new Vector2(arX, btnY + BtnH / 2 - arSz.Y / 2),
                    Color.Yellow * pulse);
            }
        }

        DrawTextCentered("^v: seleziona    ENTER: conferma", cx,
            startY + MenuItems.Length * (BtnH + BtnGap) + 18,
            Color.Gray, 0.75f);
        DrawTextCentered("F11: schermo intero", cx,
            startY + MenuItems.Length * (BtnH + BtnGap) + 36,
            Color.DarkGray, 0.70f);
    }

    // ─────────────────────────────────────────────────────────────────
    private void DrawAudioOverlay(int W, int H)
    {
        const int rowSpacing = 52;
        const int headerH = 58;   // titolo + separatore
        const int footerH = 62;   // hint controls + hint ESC + padding
        const int rowsH = 3 * rowSpacing;
        int boxW = 520;
        int boxH = headerH + rowsH + footerH;
        int cx = W / 2;
        int boxX = cx - boxW / 2;
        int boxY = H / 2 - boxH / 2;

        _sb.Draw(_pixel, new Rectangle(0, 0, W, H), Color.Black * 0.55f);
        _sb.Draw(_pixel, new Rectangle(boxX, boxY, boxW, boxH), new Color(18, 18, 38, 240));
        DrawBorder(boxX, boxY, boxW, boxH, Color.CornflowerBlue);

        DrawTextCentered("IMPOSTAZIONI AUDIO", cx, boxY + 26, Color.CornflowerBlue, 1.3f);
        _sb.Draw(_pixel, new Rectangle(boxX + 16, boxY + 46, boxW - 32, 2), new Color(40, 80, 160));

        int firstRowY = boxY + headerH + rowSpacing / 2;
        _audioPanel.Draw(_sb, cx, firstRowY, showHint: false);

        int hintControlY = boxY + headerH + rowsH + 18;
        int hintEscY     = boxY + headerH + rowsH + 42;
        DrawTextCentered("< > volume    ^ v seleziona    M = mute    scroll/click barra",
            cx, hintControlY, new Color(100, 100, 130), 0.90f);
        DrawTextCentered("ESC: indietro", cx, hintEscY, Color.DarkGray, 0.95f);
    }

    // ─────────────────────────────────────────────────────────────────
    private void DrawIstruzioniOverlay(int W, int H)
    {
        int boxW = 520;
        int boxH = 260;
        int boxX = W / 2 - boxW / 2;
        int boxY = H / 2 - boxH / 2;
        int cx = W / 2;

        _sb.Draw(_pixel, new Rectangle(0, 0, W, H), Color.Black * 0.55f);
        _sb.Draw(_pixel, new Rectangle(boxX, boxY, boxW, boxH), new Color(18, 18, 38, 240));
        DrawBorder(boxX, boxY, boxW, boxH, new Color(255, 200, 60));

        DrawTextCentered("ISTRUZIONI", cx, boxY + 22, Color.Yellow, 1.1f);

        int ly = boxY + 55;
        const int lineH = 22;
        void Line(string t, Color c, float sc = 0.9f)
        {
            DrawTextCentered(t, cx, ly, c, sc);
            ly += lineH;
        }

        Line("WASD / FRECCE  :  muovi il minatore", Color.LightGray);
        Line("SPAZIO         :  piazza bomba piccola", Color.LightGray);
        Line("X              :  piazza bomba grande", Color.LightGray);
        Line("ESC            :  pausa / menu", Color.LightGray);
        Line("F11            :  schermo intero", Color.LightGray);
        Line("Raccogli chiave, apri porta, avanza!", new Color(180, 255, 160), 0.85f);
        Line("Guadagna vite extra ogni 500 punti.", new Color(255, 220, 80), 0.80f);

        DrawTextCentered("ESC / ENTER: chiudi", cx, boxY + boxH - 18, Color.DarkGray, 0.75f);
    }

    // ─────────────────────────────────────────────────────────────────
    private void DrawScrollingCloud(Texture2D tex, float offsetX, int W, int H, float alpha)
    {
        float x = offsetX % W;
        if (x > 0) x -= W;
        _sb.Draw(tex, new Rectangle((int)x, 0, W, H), Color.White * alpha);
        _sb.Draw(tex, new Rectangle((int)x + W, 0, W, H), Color.White * alpha);
    }

    private void DrawRect(Rectangle r, Color c)
        => _sb.Draw(_pixel, r, c);

    private void DrawBorder(int x, int y, int w, int h, Color c)
    {
        _sb.Draw(_pixel, new Rectangle(x, y, w, 2), c);
        _sb.Draw(_pixel, new Rectangle(x, y + h - 2, w, 2), c);
        _sb.Draw(_pixel, new Rectangle(x, y, 2, h), c);
        _sb.Draw(_pixel, new Rectangle(x + w - 2, y, 2, h), c);
    }

    private void DrawTextCentered(string text, int cx, int cy, Color color, float scale)
    {
        Vector2 origin = _font.MeasureString(text) * 0.5f;
        Vector2 pos = new Vector2(cx, cy);
        Color outline = Color.Black * 0.92f;
        float d = Math.Max(1f, scale * 1.5f);
        _sb.DrawString(_font, text, pos + new Vector2(-d, -d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        _sb.DrawString(_font, text, pos + new Vector2(d, -d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        _sb.DrawString(_font, text, pos + new Vector2(-d, d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        _sb.DrawString(_font, text, pos + new Vector2(d, d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        _sb.DrawString(_font, text, pos + new Vector2(d + 1f, d + 1f), Color.Black * 0.5f, 0f, origin, scale, SpriteEffects.None, 0f);
        _sb.DrawString(_font, text, pos, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }
}