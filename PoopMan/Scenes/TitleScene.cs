using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PoopMan.UI;
using PoopManLibrary;
using PoopManLibrary.Input;
using PoopManLibrary.Scenes;
using System;

namespace PoopMan.Scenes;

public class TitleScene : Scene
{
    private const float Cloud1Speed = 55f;
    private const float Cloud2Speed = 28f;
    private const int BtnW = 260;
    private const int BtnH = 36;
    private const int BtnGap = 14;

    private static readonly string[] MenuItems = { "GIOCA", "CLASSIFICA", "ISTRUZIONI", "AUDIO", "COMANDI" };

    // ── Audio Panel ───────────────────────────────────────────────────
    private AudioSettingsPanel _audioPanel;
    private ControlsSettingsPanel _controlsPanel;
    private Texture2D _bgFixed;

    // ── Nuvole ────────────────────────────────────────────────────────
    private float _c1X;
    private float _c2X;
    private Texture2D _cloud1;
    private Texture2D _cloud2;

    // ── Animazione cursore ────────────────────────────────────────────
    private float _cursorPulse;
    private SpriteFont _font;

    private Texture2D _pixel;
    private Texture2D _glow;

    // ── Braci che salgono dal fondo (atmosfera) ───────────────────────
    private readonly System.Collections.Generic.List<(Vector2 pos, Vector2 vel, float life, float max, float size)> _embers = new();
    private readonly Random _rng = new();
    private float _emberTimer;

    // ── Grafica ────────────────────────────────────────────────────────
    private SpriteBatch _sb;
    private MenuScreen _screen = MenuScreen.Main;
    private int _selectedItem;

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

        _glow = PoopMan.Scenes.FxTextures.CreateRadial(Core.GraphicsDevice, 128, 1.8f);

        _audioPanel = new AudioSettingsPanel(_font, _pixel);
        _controlsPanel = new ControlsSettingsPanel(_font, _pixel);

        AudioManager.Load(Content);
        AudioManager.StartTitleAudio();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        _c1X -= Cloud1Speed * dt;
        _c2X -= Cloud2Speed * dt;
        _cursorPulse += dt * 3.5f;
        UpdateEmbers(dt);

        var kb = Core.Input.Keyboard;
        var mouse = Core.Input.Mouse;

        // ── Pannello audio ────────────────────────────────────────────
        if (_screen == MenuScreen.Audio)
        {
            _audioPanel.Update(gameTime);
            if (kb.WasKeyJustPressed(Keys.Back) || GameController.Pause() || GameController.MenuBack())
                _screen = MenuScreen.Main;
            return;
        }

        // ── Pannello comandi ──────────────────────────────────────────
        if (_screen == MenuScreen.Comandi)
        {
            _controlsPanel.Update(gameTime);
            if (!_controlsPanel.BlocksBack && (GameController.Pause() || GameController.MenuBack()))
                _screen = MenuScreen.Main;
            return;
        }

        // ── Pannello istruzioni ───────────────────────────────────────
        if (_screen == MenuScreen.Istruzioni)
        {
            if (kb.WasKeyJustPressed(Keys.Back) || GameController.Pause() || GameController.MenuBack() ||
                GameController.Confirm() ||
                mouse.WasButtonJustPressed(MouseButton.Left))
                _screen = MenuScreen.Main;
            return;
        }

        // ── Menu principale ───────────────────────────────────────────
        if (GameController.MenuUp())
            _selectedItem = (_selectedItem - 1 + MenuItems.Length) % MenuItems.Length;
        if (GameController.MenuDown())
            _selectedItem = (_selectedItem + 1) % MenuItems.Length;

        // Mouse: hover selects, click confirms (geometria identica a DrawMainMenu)
        {
            var W = Core.GraphicsDevice.Viewport.Width;
            var H = Core.GraphicsDevice.Viewport.Height;
            var titleY = H / 4;
            var startY = titleY + 128 + 20; // sepY + 20
            var cx = W / 2;
            var mp = mouse.Position;
            for (var i = 0; i < MenuItems.Length; i++)
            {
                var btnY = startY + i * (BtnH + BtnGap);
                var btnRect = new Rectangle(cx - BtnW / 2, btnY, BtnW, BtnH);
                if (btnRect.Contains(mp))
                {
                    // L'hover seleziona solo se il mouse si muove: un cursore fermo
                    // sopra un pulsante non deve bloccare la navigazione da tastiera
                    if (mouse.WasMoved || mouse.WasButtonJustPressed(MouseButton.Left))
                        _selectedItem = i;
                    if (mouse.WasButtonJustPressed(MouseButton.Left))
                        switch (i)
                        {
                            case 0:
                                AudioManager.StopTitleAudio();
                                Core.ChangeScene(new GameScene());
                                break;
                            case 1: Core.ChangeScene(new LeaderboardScreen(fromGameOver: false)); break;
                            case 2: _screen = MenuScreen.Istruzioni; break;
                            case 3: _screen = MenuScreen.Audio; break;
                            case 4: OpenControls(); break;
                        }
                }
            }
        }

        if (GameController.Confirm())
            switch (_selectedItem)
            {
                case 0: // GIOCA
                    AudioManager.StopTitleAudio();
                    Core.ChangeScene(new GameScene());
                    break;
                case 1: // CLASSIFICA
                    Core.ChangeScene(new LeaderboardScreen(fromGameOver: false));
                    break;
                case 2: // ISTRUZIONI
                    _screen = MenuScreen.Istruzioni;
                    break;
                case 3: // AUDIO
                    _screen = MenuScreen.Audio;
                    break;
                case 4: // COMANDI
                    OpenControls();
                    break;
            }
    }

    private void OpenControls()
    {
        _screen = MenuScreen.Comandi;
        _controlsPanel.Open();
    }

    public override void Draw(GameTime gameTime)
    {
        var W = Core.GraphicsDevice.Viewport.Width;
        var H = Core.GraphicsDevice.Viewport.Height;

        Core.GraphicsDevice.Clear(Color.Black);

        // Sfondo
        _sb.Begin(samplerState: SamplerState.LinearWrap);
        _sb.Draw(_bgFixed, new Rectangle(0, 0, W, H), Color.White);
        DrawScrollingCloud(_cloud1, _c1X, W, H, 0.55f);
        DrawScrollingCloud(_cloud2, _c2X, W, H, 0.45f);
        _sb.Draw(_pixel, new Rectangle(0, 0, W, H), Color.Black * 0.45f);
        // Sfumatura scura verso il basso: stacca il menu dallo sfondo
        for (var y = H / 2; y < H; y += 4)
            _sb.Draw(_pixel, new Rectangle(0, y, W, 4), Color.Black * (0.35f * (y - H / 2f) / (H / 2f)));
        _sb.End();

        var titleY = H / 4;
        var bob = MathF.Sin(_cursorPulse * 0.55f) * 4f;

        // Braci + alone caldo dietro al logo (additivi)
        _sb.Begin(samplerState: SamplerState.LinearClamp, blendState: BlendState.Additive);
        var glowPulse = 0.85f + 0.15f * MathF.Sin(_cursorPulse * 0.8f);
        _sb.Draw(_glow, new Rectangle(W / 2 - 330, (int)(titleY - 110 + bob), 660, 260),
            new Color(255, 170, 60) * (0.28f * glowPulse));
        foreach (var e in _embers)
        {
            var k = e.life / e.max;
            var a = MathF.Sin(k * MathF.PI);
            _sb.Draw(_glow, new Rectangle((int)(e.pos.X - e.size * 3), (int)(e.pos.Y - e.size * 3),
                (int)(e.size * 6), (int)(e.size * 6)), new Color(255, 140, 50) * (0.35f * a));
            _sb.Draw(_pixel, new Rectangle((int)e.pos.X, (int)e.pos.Y, (int)MathF.Max(1f, e.size * 0.6f),
                (int)MathF.Max(1f, e.size * 0.6f)), new Color(255, 220, 140) * a);
        }

        _sb.End();

        _sb.Begin(samplerState: SamplerState.PointClamp);

        // Titolo (galleggia leggermente)
        DrawTextCentered("POOPMAN", W / 2, (int)(titleY + bob), Color.Yellow, 3.0f);
        DrawTextCentered("MINER", W / 2, (int)(titleY + 78 + bob * 0.6f), new Color(255, 160, 40), 2.0f);

        // Linea separatrice sfumata ai lati
        var sepY = titleY + 128;
        for (var i = 0; i < 36; i++)
        {
            var a = 1f - MathF.Abs(i - 17.5f) / 18f;
            _sb.Draw(_pixel, new Rectangle(W / 2 - 180 + i * 10, sepY, 10, 2), new Color(140, 100, 255) * a);
        }

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
            case MenuScreen.Comandi:
                _sb.Draw(_pixel, new Rectangle(0, 0, W, H), Color.Black * 0.55f);
                _controlsPanel.Draw(_sb, W / 2, H / 2);
                break;
        }

        _sb.DrawString(_font, "PoopMan v1.3.0", new Vector2(8, H - 18), Color.DarkGray * 0.7f);
        _sb.End();
    }

    // ─────────────────────────────────────────────────────────────────
    private void DrawMainMenu(int W, int H, int startY)
    {
        var cx = W / 2;

        for (var i = 0; i < MenuItems.Length; i++)
        {
            var btnY = startY + i * (BtnH + BtnGap);
            var sel = i == _selectedItem;

            // Sfondo pulsante
            var pulse = sel ? 0.85f + 0.15f * (float)Math.Sin(_cursorPulse) : 1f;

            UiDraw.Button(_sb, _pixel, new Rectangle(cx - BtnW / 2, btnY, BtnW, BtnH), sel, false,
                new Color(120, 80, 220), (float)Math.Sin(_cursorPulse));

            var textColor = sel ? Color.White : Color.LightGray;
            var scale = sel ? 1.05f : 1.0f;
            DrawTextCentered(MenuItems[i], cx, btnY + BtnH / 2, textColor, scale);

            // Cursore freccia
            if (sel)
            {
                var arrow = ">";
                var arSz = _font.MeasureString(arrow);
                var arX = cx - BtnW / 2 - arSz.X - 10;
                _sb.DrawString(_font, arrow,
                    new Vector2(arX, btnY + BtnH / 2 - arSz.Y / 2),
                    Color.Yellow * pulse);
            }
        }

        DrawTextCentered("^v / D-pad: seleziona    ENTER / (A): conferma", cx,
            startY + MenuItems.Length * (BtnH + BtnGap) + 18,
            Color.Gray, 0.75f);
        DrawTextCentered("F11: schermo intero", cx,
            startY + MenuItems.Length * (BtnH + BtnGap) + 36,
            Color.DarkGray, 0.70f);
    }

    private void UpdateEmbers(float dt)
    {
        var vp = Core.GraphicsDevice.Viewport;
        _emberTimer += dt;
        while (_emberTimer >= 0.08f && _embers.Count < 70)
        {
            _emberTimer -= 0.08f;
            var life = 3f + (float)_rng.NextDouble() * 3f;
            _embers.Add((new Vector2(_rng.Next(0, vp.Width), vp.Height + 10),
                new Vector2((float)(_rng.NextDouble() - 0.5) * 20f, -30f - (float)_rng.NextDouble() * 50f),
                life, life, 2f + (float)_rng.NextDouble() * 2.5f));
        }

        for (var i = _embers.Count - 1; i >= 0; i--)
        {
            var e = _embers[i];
            e.life -= dt;
            if (e.life <= 0f)
            {
                _embers.RemoveAt(i);
                continue;
            }

            e.pos += e.vel * dt;
            e.pos.X += MathF.Sin(e.life * 2f + i) * 12f * dt; // oscillazione laterale
            _embers[i] = e;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    private void DrawAudioOverlay(int W, int H)
    {
        const int rowSpacing = 52;
        const int headerH = 58; // titolo + separatore
        const int footerH = 62; // hint controls + hint ESC + padding
        const int rowsH = 3 * rowSpacing;
        var boxW = 520;
        var boxH = headerH + rowsH + footerH;
        var cx = W / 2;
        var boxX = cx - boxW / 2;
        var boxY = H / 2 - boxH / 2;

        _sb.Draw(_pixel, new Rectangle(0, 0, W, H), Color.Black * 0.55f);
        UiDraw.Panel(_sb, _pixel, new Rectangle(boxX, boxY, boxW, boxH),
            new Color(30, 38, 84, 248), new Color(12, 14, 34, 248), Color.CornflowerBlue, 12, true, 2);

        DrawTextCentered("IMPOSTAZIONI AUDIO", cx, boxY + 26, Color.CornflowerBlue, 1.3f);
        _sb.Draw(_pixel, new Rectangle(boxX + 16, boxY + 46, boxW - 32, 2), new Color(40, 80, 160));

        var firstRowY = boxY + headerH + rowSpacing / 2;
        _audioPanel.Draw(_sb, cx, firstRowY, false);

        var hintControlY = boxY + headerH + rowsH + 18;
        var hintEscY = boxY + headerH + rowsH + 42;
        DrawTextCentered(AudioSettingsPanel.AudioHint,
            cx, hintControlY, new Color(100, 100, 130), 0.78f);
        DrawTextCentered(AudioSettingsPanel.BackHint, cx, hintEscY, Color.DarkGray, 0.95f);
    }

    // ─────────────────────────────────────────────────────────────────
    // Comandi mostrati nelle istruzioni: azione, tastiera, gamepad
    // Generati ogni volta dalle assegnazioni correnti (personalizzabili in COMANDI)
    private static (string action, string keys, string pad)[] GameControls =>
        new[]
        {
            Row("Su", GameAction.Up),
            Row("Giu", GameAction.Down),
            Row("Sinistra", GameAction.Left),
            Row("Destra", GameAction.Right),
            Row("Bomba piccola", GameAction.MiniBomb),
            Row("Bomba grande", GameAction.BigBomb),
            Row("Detonatore", GameAction.Detonate),
            Row("Pausa / menu", GameAction.Pause),
            ("Schermo intero", "F11", "-")
        };

    private static (string action, string keys, string pad) Row(string label, GameAction action)
    {
        return (label, InputBindings.KeysLabel(action),
            InputBindings.ButtonName(InputBindings.GetButton(action)));
    }

    private static readonly (string action, string keys, string pad)[] MenuControls =
    {
        ("Seleziona", "FRECCE + TASTI MOVIMENTO", "D-PAD / LEVETTA SX"),
        ("Conferma", "ENTER", "A"),
        ("Indietro / chiudi", "ESC", "B"),
        ("Sezioni enciclopedia", "TAB / Q / E", "LB / RB"),
        ("Volume / mute", "< >  /  M", "D-PAD < >  /  X")
    };

    private void DrawIstruzioniOverlay(int W, int H)
    {
        const int lineH = 21;
        const int sectionH = 26;
        var rows = GameControls.Length + MenuControls.Length;
        var boxW = Math.Min(W - 32, 640);
        var boxH = Math.Min(H - 16, 70 + sectionH * 2 + rows * lineH + 2 * lineH + 40);
        var boxX = W / 2 - boxW / 2;
        var boxY = H / 2 - boxH / 2;
        var cx = W / 2;

        // Colonne: azione | tastiera | gamepad
        var colAction = boxX + boxW * 18 / 100;
        var colKeys = boxX + boxW * 50 / 100;
        var colPad = boxX + boxW * 80 / 100;

        _sb.Draw(_pixel, new Rectangle(0, 0, W, H), Color.Black * 0.55f);
        UiDraw.Panel(_sb, _pixel, new Rectangle(boxX, boxY, boxW, boxH),
            new Color(44, 34, 84, 248), new Color(14, 12, 34, 248), new Color(255, 200, 60), 12, true, 2);

        DrawTextCentered("ISTRUZIONI", cx, boxY + 22, Color.Yellow, 1.1f);

        var ly = boxY + 50;
        DrawTextCentered("TASTIERA", colKeys, ly, new Color(255, 220, 120), 0.85f);
        DrawTextCentered("GAMEPAD", colPad, ly, new Color(150, 200, 255), 0.85f);
        ly += lineH;

        void Section(string title)
        {
            _sb.Draw(_pixel, new Rectangle(boxX + 16, ly - 2, boxW - 32, 1), new Color(110, 90, 170));
            DrawTextCentered(title, colAction, ly + 9, new Color(200, 170, 255), 0.8f);
            ly += sectionH;
        }

        void Rows((string action, string keys, string pad)[] list)
        {
            foreach (var (action, keys, pad) in list)
            {
                DrawTextCentered(action, colAction, ly, Color.LightGray, 0.8f);
                DrawTextCentered(keys, colKeys, ly, Color.White, 0.8f);
                DrawTextCentered(pad, colPad, ly, new Color(170, 215, 255), 0.8f);
                ly += lineH;
            }
        }

        Section("IN GIOCO");
        Rows(GameControls);
        Section("MENU");
        Rows(MenuControls);

        ly += 4;
        DrawTextCentered("Dal livello 5 serve la chiave per aprire la porta!", cx, ly, new Color(180, 255, 160), 0.8f);
        ly += lineH;
        DrawTextCentered("Le bombe esplodono a catena. Vita extra ogni 1000 pt.", cx, ly, new Color(255, 220, 80), 0.8f);

        DrawTextCentered("ESC / ENTER  (B / A): chiudi", cx, boxY + boxH - 16, Color.DarkGray, 0.75f);
    }

    // ─────────────────────────────────────────────────────────────────
    private void DrawScrollingCloud(Texture2D tex, float offsetX, int W, int H, float alpha)
    {
        var x = offsetX % W;
        if (x > 0) x -= W;
        _sb.Draw(tex, new Rectangle((int)x, 0, W, H), Color.White * alpha);
        _sb.Draw(tex, new Rectangle((int)x + W, 0, W, H), Color.White * alpha);
    }

    private void DrawRect(Rectangle r, Color c)
    {
        _sb.Draw(_pixel, r, c);
    }

    private void DrawBorder(int x, int y, int w, int h, Color c)
    {
        _sb.Draw(_pixel, new Rectangle(x, y, w, 2), c);
        _sb.Draw(_pixel, new Rectangle(x, y + h - 2, w, 2), c);
        _sb.Draw(_pixel, new Rectangle(x, y, 2, h), c);
        _sb.Draw(_pixel, new Rectangle(x + w - 2, y, 2, h), c);
    }

    private void DrawTextCentered(string text, int cx, int cy, Color color, float scale)
    {
        var origin = _font.MeasureString(text) * 0.5f;
        var pos = new Vector2(cx, cy);
        var outline = Color.Black * 0.92f;
        var d = Math.Max(1f, scale * 1.5f);
        _sb.DrawString(_font, text, pos + new Vector2(-d, -d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        _sb.DrawString(_font, text, pos + new Vector2(d, -d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        _sb.DrawString(_font, text, pos + new Vector2(-d, d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        _sb.DrawString(_font, text, pos + new Vector2(d, d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        _sb.DrawString(_font, text, pos + new Vector2(d + 1f, d + 1f), Color.Black * 0.5f, 0f, origin, scale,
            SpriteEffects.None, 0f);
        _sb.DrawString(_font, text, pos, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    // ── Menu ──────────────────────────────────────────────────────────
    private enum MenuScreen
    {
        Main,
        Audio,
        Istruzioni,
        Comandi
    }
}