using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PoopManLibrary;
using PoopManLibrary.Input;

namespace PoopMan.UI;

/// <summary>
/// Pannello grafico per regolare Music Volume e SFX Volume.
/// Supporta tastiera (Up/Down/Left/Right) e mouse (hover, click/drag sulla barra, scroll).
/// </summary>
public class AudioSettingsPanel
{
    // ── Stato ────────────────────────────────────────────────────────────
    private int   _selectedRow   = 0;          // 0 = Music, 1 = SFX
    private float _repeatTimer   = 0f;
    private const float RepeatDelay    = 0.35f;
    private const float RepeatInterval = 0.09f;
    private bool  _repeating          = false;
    private float _step               = 0.05f;

    // ── Posizione ultima draw (per hit-test mouse) ────────────────────────
    private int _lastCx;
    private int _lastCy;
    private bool _dragging = false;

    // ── Risorse ──────────────────────────────────────────────────────────
    private readonly SpriteFont _font;
    private readonly Texture2D  _pixel;

    // ── Dimensioni barra ─────────────────────────────────────────────────
    private const int BarWidth  = 200;
    private const int BarHeight = 12;

    public AudioSettingsPanel(SpriteFont font, Texture2D pixel)
    {
        _font  = font;
        _pixel = pixel;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers geometrici (devono coincidere con DrawRow)
    // ─────────────────────────────────────────────────────────────────────
    private int BarX(int cx) => cx - (90 + 8 + BarWidth + 8 + 40) / 2 + 90 + 8;
    private int BarY(int cy) => cy - BarHeight / 2;
    private Rectangle RowHitRect(int cx, int cy) =>
        new Rectangle(cx - (90 + 8 + BarWidth + 8 + 40) / 2, cy - 14,
                      90 + 8 + BarWidth + 8 + 40, 28);
    private Rectangle BarRect(int cx, int cy) =>
        new Rectangle(BarX(cx), BarY(cy), BarWidth, BarHeight);

    // ─────────────────────────────────────────────────────────────────────
    // Update — tastiera + mouse
    // ─────────────────────────────────────────────────────────────────────
    public void Update(GameTime gameTime)
    {
        var kb    = Core.Input.Keyboard;
        var mouse = Core.Input.Mouse;
        float dt  = (float)gameTime.ElapsedGameTime.TotalSeconds;
        Point mp  = mouse.Position;

        // ── Mouse: hover seleziona riga ───────────────────────────────────
        for (int row = 0; row < 2; row++)
        {
            int rowCy = _lastCy + row * 38;
            if (RowHitRect(_lastCx, rowCy).Contains(mp))
                _selectedRow = row;
        }

        // ── Mouse: click/drag sulla barra ────────────────────────────────
        bool leftDown    = mouse.IsButtonDown(MouseButton.Left);
        bool leftPressed = mouse.WasButtonJustPressed(MouseButton.Left);

        if (leftPressed)
        {
            for (int row = 0; row < 2; row++)
            {
                int rowCy = _lastCy + row * 38;
                if (BarRect(_lastCx, rowCy).Contains(mp))
                {
                    _dragging    = true;
                    _selectedRow = row;
                }
            }
        }
        if (!leftDown) _dragging = false;

        if (_dragging && leftDown)
        {
            int rowCy = _lastCy + _selectedRow * 38;
            int bx    = BarX(_lastCx);
            float vol = Math.Clamp((float)(mp.X - bx) / BarWidth, 0f, 1f);
            if (_selectedRow == 0) AudioManager.BgmVolume = vol;
            else                   AudioManager.SfxVolume = vol;
        }

        // ── Mouse: scroll wheel regola la riga selezionata ────────────────
        int scroll = mouse.ScrollWheelDelta;
        if (scroll != 0)
        {
            float delta = Math.Sign(scroll) * _step;
            if (_selectedRow == 0) AudioManager.BgmVolume = AudioManager.BgmVolume + delta;
            else                   AudioManager.SfxVolume = AudioManager.SfxVolume + delta;
        }

        // ── Tastiera: navigazione riga ────────────────────────────────────
        if (kb.WasKeyJustPressed(Keys.Up))   _selectedRow = (_selectedRow - 1 + 2) % 2;
        if (kb.WasKeyJustPressed(Keys.Down))  _selectedRow = (_selectedRow + 1)     % 2;

        // ── Tastiera: modifica volume con auto-repeat ─────────────────────
        bool leftHeld  = kb.IsKeyDown(Keys.Left);
        bool rightHeld = kb.IsKeyDown(Keys.Right);

        bool doStep = false;
        if (leftHeld || rightHeld)
        {
            if (!_repeating)
            {
                doStep = true;
                _repeatTimer = 0f;
                _repeating   = true;
            }
            else
            {
                _repeatTimer += dt;
                float threshold = (_repeatTimer < RepeatDelay) ? RepeatDelay : RepeatInterval;
                if (_repeatTimer >= threshold)
                {
                    doStep = true;
                    _repeatTimer = (_repeatTimer >= RepeatDelay) ? 0f : _repeatTimer;
                }
            }
        }
        else
        {
            _repeating   = false;
            _repeatTimer = 0f;
        }

        if (doStep)
        {
            float delta = rightHeld ? _step : -_step;
            if (_selectedRow == 0) AudioManager.BgmVolume = AudioManager.BgmVolume + delta;
            else                   AudioManager.SfxVolume = AudioManager.SfxVolume + delta;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Draw — disegna il pannello centrato all'ascissa cx, partendo da cy
    // ─────────────────────────────────────────────────────────────────────
    public void Draw(SpriteBatch sb, int cx, int cy, bool showHint = true)
    {
        _lastCx = cx;
        _lastCy = cy;

        DrawRow(sb, cx, cy,      "MUSICA", AudioManager.BgmVolume, _selectedRow == 0);
        DrawRow(sb, cx, cy + 38, "SFX",   AudioManager.SfxVolume, _selectedRow == 1);

        if (showHint)
            DrawTextCentered(sb, "< > volume    ^ v seleziona    scroll/click barra", cx, cy + 76, Color.DarkGray, 0.75f);
    }

    // ─────────────────────────────────────────────────────────────────────
    private void DrawRow(SpriteBatch sb, int cx, int cy, string label, float volume, bool selected)
    {
        Color labelColor = selected ? Color.Yellow : Color.LightGray;
        Color barBg      = new Color(40, 40, 60);
        Color barFill    = selected ? new Color(80, 200, 255) : new Color(60, 130, 180);
        Color border     = selected ? Color.Yellow : new Color(80, 80, 100);

        int totalW = 90 + 8 + BarWidth + 8 + 40;   // label + gap + bar + gap + percent
        int startX = cx - totalW / 2;

        // Label
        string labelText = label + ":";
        Vector2 labelSz  = _font.MeasureString(labelText);
        sb.DrawString(_font, labelText,
            new Vector2(startX + 90 - (int)labelSz.X, cy - (int)(labelSz.Y * 0.5f)),
            labelColor);

        // Barra sfondo
        int barX = startX + 90 + 8;
        int barY = cy - BarHeight / 2;
        DrawRect(sb, new Rectangle(barX - 1, barY - 1, BarWidth + 2, BarHeight + 2), border);
        DrawRect(sb, new Rectangle(barX,     barY,     BarWidth,     BarHeight),     barBg);

        // Barra riempita
        int fillW = (int)(volume * BarWidth);
        if (fillW > 0)
            DrawRect(sb, new Rectangle(barX, barY, fillW, BarHeight), barFill);

        // Percentuale
        string pct = $"{(int)(volume * 100)}%";
        sb.DrawString(_font, pct,
            new Vector2(barX + BarWidth + 8, cy - (int)(_font.MeasureString(pct).Y * 0.5f)),
            labelColor);
    }

    private void DrawRect(SpriteBatch sb, Rectangle r, Color c)
        => sb.Draw(_pixel, r, c);

    private void DrawTextCentered(SpriteBatch sb, string text, int cx, int cy, Color color, float scale)
    {
        Vector2 origin = _font.MeasureString(text) * 0.5f;
        sb.DrawString(_font, text, new Vector2(cx, cy), color, 0f, origin, scale, SpriteEffects.None, 0f);
    }
}
