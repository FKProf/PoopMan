using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PoopManLibrary;
using PoopManLibrary.Scenes;

namespace PoopMan.UI;

/// <summary>
/// Schermata di inserimento nome dopo il Game Over.
/// Mostra punteggio e livello raggiunti, chiede il nome e salva nella leaderboard.
/// </summary>
public sealed class NameEntryScreen : Scene
{
    private readonly int _score;
    private readonly int _level;

    public NameEntryScreen(int score, int level)
    {
        _score = score;
        _level = level;
    }

    // ── Risorse ───────────────────────────────────────────────────────────
    private SpriteBatch _sb;
    private SpriteFont _font;
    private Texture2D _pixel;

    // ── Input nome ────────────────────────────────────────────────────────
    private string _name = "";
    private const int MaxNameLength = 16;
    private float _caretTimer = 0f;
    private bool _caretVisible = true;

    // ── Stato ─────────────────────────────────────────────────────────────
    private KeyboardState _prevKb;
    private bool _confirmed = false;

    // ─────────────────────────────────────────────────────────────────────
    public override void LoadContent()
    {
        base.LoadContent();
        _sb = new SpriteBatch(Core.GraphicsDevice);
        _font = Content.Load<SpriteFont>("font/Score");
        _pixel = new Texture2D(Core.GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _prevKb = Keyboard.GetState();
    }

    // ─────────────────────────────────────────────────────────────────────
    public override void Update(GameTime gameTime)
    {
        if (_confirmed) return;

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Lampeggio caret
        _caretTimer += dt;
        if (_caretTimer >= 0.5f)
        {
            _caretTimer = 0f;
            _caretVisible = !_caretVisible;
        }

        // ── Legge i tasti premuti in questo frame ─────────────────────────
        KeyboardState kb = Keyboard.GetState();
        Keys[] pressed = kb.GetPressedKeys();

        bool shift = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);

        foreach (Keys key in pressed)
        {
            if (WasJustPressed(key, kb))
            {
                // Backspace
                if (key == Keys.Back && _name.Length > 0)
                {
                    _name = _name[..^1];
                    continue;
                }

                // Conferma
                if (key == Keys.Enter)
                {
                    Confirm();
                    break;
                }

                // Salta senza nome
                if (key == Keys.Escape)
                {
                    _name = "???";
                    Confirm();
                    break;
                }

                // Caratteri validi (lettere, cifre, spazio, trattino)
                if (_name.Length < MaxNameLength)
                {
                    char? c = KeyToChar(key, shift);
                    if (c.HasValue)
                        _name += c.Value;
                }
            }
        }

        _prevKb = kb;
    }

    private bool WasJustPressed(Keys key, KeyboardState current) =>
        current.IsKeyDown(key) && !_prevKb.IsKeyDown(key);

    private static char? KeyToChar(Keys key, bool shift)
    {
        // Lettere
        if (key >= Keys.A && key <= Keys.Z)
            return shift ? (char)('A' + (key - Keys.A)) : (char)('a' + (key - Keys.A));

        // Cifre (riga numerica)
        if (key >= Keys.D0 && key <= Keys.D9)
            return (char)('0' + (key - Keys.D0));

        // Numpad
        if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            return (char)('0' + (key - Keys.NumPad0));

        // Spazio
        if (key == Keys.Space) return ' ';

        // Trattino
        if (key == Keys.OemMinus) return shift ? '_' : '-';

        return null;
    }

    private void Confirm()
    {
        _confirmed = true;
        LeaderboardManager.AddEntry(_name, _score, _level);
        Core.ChangeScene(new LeaderboardScreen(LeaderboardManager.LastSavedIndex, fromGameOver: true));
    }

    // ─────────────────────────────────────────────────────────────────────
    public override void Draw(GameTime gameTime)
    {
        int vw = Core.GraphicsDevice.Viewport.Width;
        int vh = Core.GraphicsDevice.Viewport.Height;
        int cx = vw / 2;
        int cy = vh / 2;

        Core.GraphicsDevice.Clear(new Color(12, 10, 28));

        _sb.Begin(samplerState: SamplerState.PointClamp);

        // ── Sfondo scuro totale ───────────────────────────────────────────
        DrawRect(new Rectangle(0, 0, vw, vh), Color.Black * 0.85f);

        // ── Riquadro centrale ─────────────────────────────────────────────
        int boxW = (int)(vw * 0.54f);
        int boxH = (int)(vh * 0.52f);
        int boxX = cx - boxW / 2;
        int boxY = cy - boxH / 2;

        DrawRect(new Rectangle(boxX, boxY, boxW, boxH), new Color(18, 12, 36) * 0.98f);
        DrawRect(new Rectangle(boxX, boxY, boxW, 3), new Color(180, 20, 20));
        DrawRect(new Rectangle(boxX, boxY + boxH - 3, boxW, 3), new Color(180, 20, 20));
        DrawRect(new Rectangle(boxX, boxY, 3, boxH), new Color(180, 20, 20));
        DrawRect(new Rectangle(boxX + boxW - 3, boxY, 3, boxH), new Color(180, 20, 20));

        // ── Game Over & punteggio ─────────────────────────────────────────
        DrawTextCentered("GAME OVER", cx, boxY + 24, Color.Red, 2.2f);
        DrawTextCentered($"Punteggio: {_score}", cx, boxY + 80, Color.Gold, 1.6f);
        DrawTextCentered($"Livello raggiunto: {_level}", cx, boxY + 114, Color.LightGray, 1.1f);

        // ── Separatore ───────────────────────────────────────────────────
        DrawRect(new Rectangle(boxX + 30, boxY + 146, boxW - 60, 2), new Color(80, 50, 160));

        // ── Prompt nome ───────────────────────────────────────────────────
        DrawTextCentered("Inserisci il tuo nome:", cx, boxY + 162, Color.LightGray, 1f);

        // Campo testo
        int fieldW = 340;
        int fieldH = 40;
        int fieldX = cx - fieldW / 2;
        int fieldY = boxY + 190;

        DrawRect(new Rectangle(fieldX, fieldY, fieldW, fieldH), new Color(30, 20, 60));
        DrawRect(new Rectangle(fieldX, fieldY, fieldW, 2), new Color(120, 80, 220));
        DrawRect(new Rectangle(fieldX, fieldY + fieldH - 2, fieldW, 2), new Color(120, 80, 220));

        string displayText = _name + (_caretVisible ? "|" : " ");
        var textSize = _font.MeasureString(displayText);
        float textScale = Math.Min(1f, (fieldW - 20f) / Math.Max(textSize.X, 1f));
        _sb.DrawString(_font, displayText,
            new Vector2(fieldX + 10, fieldY + (fieldH - textSize.Y * textScale) / 2f),
            Color.White, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);

        // Contatore caratteri
        DrawTextCentered($"{_name.Length}/{MaxNameLength}", cx, fieldY + fieldH + 6, Color.Gray * 0.7f, 0.75f);

        // ── Istruzioni ────────────────────────────────────────────────────
        DrawTextCentered("ENTER  per confermare   ·   ESC  per saltare", cx, boxY + boxH - 36, Color.DimGray, 0.85f);

        _sb.End();
    }

    // ── Helpers ──────────────────────────────────────────────────────────
    private void DrawRect(Rectangle r, Color c) =>
        _sb.Draw(_pixel, r, c);

    private void DrawTextCentered(string text, int cx, int y, Color color, float scale)
    {
        var size = _font.MeasureString(text) * scale;
        _sb.DrawString(_font, text, new Vector2(cx - size.X / 2f, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
}
