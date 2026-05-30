using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PoopManLibrary;
using PoopManLibrary.Input;
using System;

namespace PoopMan.UI;

/// <summary>
///     Pannello grafico per regolare Music Volume e SFX Volume.
///     Supporta tastiera (Up/Down/Left/Right) e mouse (hover, click/drag sulla barra, scroll).
/// </summary>
public class AudioSettingsPanel
{
    private const float RepeatDelay = 0.35f;
    private const float RepeatInterval = 0.09f;

    // ── Dimensioni barra ─────────────────────────────────────────────────
    private const int BarWidth = 230;
    private const int BarHeight = 14;
    private const int RowSpacing = 52;

    // ── Risorse ──────────────────────────────────────────────────────────
    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;
    private readonly float _step = 0.05f;
    private bool _dragging;

    // ── Posizione ultima draw (per hit-test mouse) ────────────────────────
    private int _lastCx;
    private int _lastCy;
    private bool _repeating;

    private float _repeatTimer;

    // ── Stato ────────────────────────────────────────────────────────────
    private int _selectedRow; // 0 = Music, 1 = SFX, 2 = Mute

    public AudioSettingsPanel(SpriteFont font, Texture2D pixel)
    {
        _font = font;
        _pixel = pixel;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers geometrici (devono coincidere con DrawRow)
    // ─────────────────────────────────────────────────────────────────────
    private int BarX(int cx)
    {
        return cx - (100 + 10 + BarWidth + 10 + 48) / 2 + 100 + 10;
    }

    private int BarY(int cy)
    {
        return cy - BarHeight / 2;
    }

    private Rectangle RowHitRect(int cx, int cy)
    {
        return new Rectangle(cx - (100 + 10 + BarWidth + 10 + 48) / 2, cy - 18,
            100 + 10 + BarWidth + 10 + 48, 36);
    }

    private Rectangle BarRect(int cx, int cy)
    {
        return new Rectangle(BarX(cx), BarY(cy), BarWidth, BarHeight);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Update — tastiera + mouse
    // ─────────────────────────────────────────────────────────────────────
    public void Update(GameTime gameTime)
    {
        var kb = Core.Input.Keyboard;
        var mouse = Core.Input.Mouse;
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var mp = mouse.Position;

        // ── Mouse: hover seleziona riga ───────────────────────────────────
        for (var row = 0; row < 3; row++)
        {
            var rowCy = _lastCy + row * RowSpacing;
            if (RowHitRect(_lastCx, rowCy).Contains(mp))
                _selectedRow = row;
        }

        // ── Mouse: click/drag sulla barra ────────────────────────────────
        var leftDown = mouse.IsButtonDown(MouseButton.Left);
        var leftPressed = mouse.WasButtonJustPressed(MouseButton.Left);

        if (leftPressed)
            for (var row = 0; row < 3; row++)
            {
                var rowCy = _lastCy + row * RowSpacing;
                if (row < 2)
                {
                    if (BarRect(_lastCx, rowCy).Contains(mp))
                    {
                        _dragging = true;
                        _selectedRow = row;
                    }
                }
                else
                {
                    // Riga mute: click su qualsiasi parte della riga
                    if (RowHitRect(_lastCx, rowCy).Contains(mp))
                    {
                        _selectedRow = 2;
                        AudioManager.IsMuted = !AudioManager.IsMuted;
                        AudioManager.SavePreferences();
                    }
                }
            }

        if (!leftDown) _dragging = false;

        if (_dragging && leftDown && _selectedRow < 2)
        {
            var rowCy = _lastCy + _selectedRow * RowSpacing;
            var bx = BarX(_lastCx);
            var vol = Math.Clamp((float)(mp.X - bx) / BarWidth, 0f, 1f);
            if (_selectedRow == 0) AudioManager.BgmVolume = vol;
            else AudioManager.SfxVolume = vol;
            AudioManager.SavePreferences();
        }

        // ── Mouse: scroll wheel regola la riga selezionata ────────────────
        var scroll = mouse.ScrollWheelDelta;
        if (scroll != 0 && _selectedRow < 2)
        {
            var delta = Math.Sign(scroll) * _step;
            if (_selectedRow == 0) AudioManager.BgmVolume = AudioManager.BgmVolume + delta;
            else AudioManager.SfxVolume = AudioManager.SfxVolume + delta;
            AudioManager.SavePreferences();
        }

        // ── Tastiera: navigazione riga ────────────────────────────────────
        if (kb.WasKeyJustPressed(Keys.Up)) _selectedRow = (_selectedRow - 1 + 3) % 3;
        if (kb.WasKeyJustPressed(Keys.Down)) _selectedRow = (_selectedRow + 1) % 3;

        // Toggle mute da tastiera: M ovunque, oppure ENTER/SPACE sulla riga MUTE
        var toggleMute = kb.WasKeyJustPressed(Keys.M) ||
                         (_selectedRow == 2 && (kb.WasKeyJustPressed(Keys.Enter) || kb.WasKeyJustPressed(Keys.Space)));
        if (toggleMute)
        {
            AudioManager.IsMuted = !AudioManager.IsMuted;
            AudioManager.SavePreferences();
        }

        // ── Tastiera: modifica volume con auto-repeat ─────────────────────
        var leftHeld = kb.IsKeyDown(Keys.Left);
        var rightHeld = kb.IsKeyDown(Keys.Right);

        var doStep = false;
        if ((leftHeld || rightHeld) && _selectedRow < 2)
        {
            if (!_repeating)
            {
                doStep = true;
                _repeatTimer = 0f;
                _repeating = true;
            }
            else
            {
                _repeatTimer += dt;
                var threshold = _repeatTimer < RepeatDelay ? RepeatDelay : RepeatInterval;
                if (_repeatTimer >= threshold)
                {
                    doStep = true;
                    _repeatTimer = _repeatTimer >= RepeatDelay ? 0f : _repeatTimer;
                }
            }
        }
        else
        {
            _repeating = false;
            _repeatTimer = 0f;
        }

        if (doStep)
        {
            var delta = rightHeld ? _step : -_step;
            if (_selectedRow == 0) AudioManager.BgmVolume = AudioManager.BgmVolume + delta;
            else AudioManager.SfxVolume = AudioManager.SfxVolume + delta;
            AudioManager.SavePreferences();
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Draw — disegna il pannello centrato all'ascissa cx, partendo da cy
    // ─────────────────────────────────────────────────────────────────────
    public void Draw(SpriteBatch sb, int cx, int cy, bool showHint = true)
    {
        _lastCx = cx;
        _lastCy = cy;

        DrawRow(sb, cx, cy, "MUSICA", AudioManager.BgmVolume, _selectedRow == 0);
        DrawRow(sb, cx, cy + RowSpacing, "SFX", AudioManager.SfxVolume, _selectedRow == 1);
        DrawMuteRow(sb, cx, cy + RowSpacing * 2, _selectedRow == 2);

        if (showHint)
            DrawTextCentered(sb, "< > volume    ^ v seleziona    M = mute    scroll/click barra", cx,
                cy + RowSpacing * 2 + 36, Color.DarkGray, 0.75f);
    }

    // ─────────────────────────────────────────────────────────────────────
    private void DrawRow(SpriteBatch sb, int cx, int cy, string label, float volume, bool selected)
    {
        var labelColor = selected ? Color.Yellow : Color.LightGray;
        var barBg = new Color(40, 40, 60);
        var barFill = selected ? new Color(80, 200, 255) : new Color(60, 130, 180);
        var border = selected ? Color.Yellow : new Color(80, 80, 100);

        var totalW = 100 + 10 + BarWidth + 10 + 48; // label + gap + bar + gap + percent
        var startX = cx - totalW / 2;

        // Label
        var labelText = label + ":";
        var labelSz = _font.MeasureString(labelText);
        sb.DrawString(_font, labelText,
            new Vector2(startX + 100 - (int)labelSz.X, cy - (int)(labelSz.Y * 0.5f)),
            labelColor);

        // Barra sfondo
        var barX = startX + 100 + 10;
        var barY = cy - BarHeight / 2;
        DrawRect(sb, new Rectangle(barX - 1, barY - 1, BarWidth + 2, BarHeight + 2), border);
        DrawRect(sb, new Rectangle(barX, barY, BarWidth, BarHeight), barBg);

        // Barra riempita
        var fillW = (int)(volume * BarWidth);
        if (fillW > 0)
            DrawRect(sb, new Rectangle(barX, barY, fillW, BarHeight), barFill);

        // Percentuale
        var pct = $"{(int)(volume * 100)}%";
        sb.DrawString(_font, pct,
            new Vector2(barX + BarWidth + 10, cy - (int)(_font.MeasureString(pct).Y * 0.5f)),
            labelColor);
    }

    private void DrawMuteRow(SpriteBatch sb, int cx, int cy, bool selected)
    {
        var muted = AudioManager.IsMuted;
        var labelColor = selected ? Color.Yellow : Color.LightGray;
        var toggleBg = muted ? new Color(180, 30, 30) : new Color(30, 120, 30);
        var toggleFg = Color.White;
        var border = selected ? Color.Yellow : new Color(80, 80, 100);

        var totalW = 100 + 10 + BarWidth + 10 + 48;
        var startX = cx - totalW / 2;

        // Label
        var labelText = "MUTE:";
        var labelSz = _font.MeasureString(labelText);
        sb.DrawString(_font, labelText,
            new Vector2(startX + 100 - (int)labelSz.X, cy - (int)(labelSz.Y * 0.5f)),
            labelColor);

        // Toggle pill
        var pillX = startX + 100 + 10;
        var pillW = 88;
        var pillH = BarHeight + 4;
        var pillY = cy - pillH / 2;
        DrawRect(sb, new Rectangle(pillX - 1, pillY - 1, pillW + 2, pillH + 2), border);
        DrawRect(sb, new Rectangle(pillX, pillY, pillW, pillH), toggleBg);
        var status = muted ? "MUTO" : "ON";
        DrawTextCentered(sb, status, pillX + pillW / 2, cy, toggleFg, 1.0f);

        // Hint
        if (muted)
        {
            var muteHint = "[AUDIO DISATTIVATO]";
            var hx = pillX + pillW + 10;
            var hSz = _font.MeasureString(muteHint) * 0.85f;
            sb.DrawString(_font, muteHint,
                new Vector2(hx, cy - (int)(hSz.Y * 0.5f)),
                new Color(255, 80, 80) * 0.9f, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
        }
    }

    private void DrawRect(SpriteBatch sb, Rectangle r, Color c)
    {
        sb.Draw(_pixel, r, c);
    }

    private void DrawTextCentered(SpriteBatch sb, string text, int cx, int cy, Color color, float scale)
    {
        var origin = _font.MeasureString(text) * 0.5f;
        sb.DrawString(_font, text, new Vector2(cx, cy), color, 0f, origin, scale, SpriteEffects.None, 0f);
    }
}