using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PoopManLibrary;
using PoopManLibrary.Input;
using System;

namespace PoopMan.UI;

/// <summary>
///     Pannello per personalizzare i comandi: per ogni azione due tasti della tastiera
///     e un pulsante del gamepad. Si seleziona una cella, si conferma e si preme il nuovo tasto.
///     Supporta tastiera, gamepad e mouse.
/// </summary>
public class ControlsSettingsPanel
{
    private const int Columns = InputBindings.KeySlots + 1; // tasto 1, tasto 2, gamepad
    private const float CaptureTimeout = 5f;

    // ── Layout ────────────────────────────────────────────────────────────
    private const int BoxW = 660;
    private const int HeaderH = 74;
    private const int RowH = 30;
    private const int FooterH = 66;
    private const int CellW = 136;
    private const int CellH = 24;
    private const int CellGap = 10;
    private const int FirstCellOffset = 200;

    private static readonly int ResetRow = InputBindings.Actions.Length;

    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;

    private int _row;
    private int _col;
    private bool _capturing;
    private bool _capturedThisFrame;
    private float _captureTimer;
    private float _pulse;
    private Rectangle _lastBox;

    public ControlsSettingsPanel(SpriteFont font, Texture2D pixel)
    {
        _font = font;
        _pixel = pixel;
    }

    /// <summary>
    ///     True mentre si attende il nuovo tasto (o nel frame in cui è stato assegnato):
    ///     chi ospita il pannello non deve trattare ESC / B come "indietro".
    /// </summary>
    public bool BlocksBack => _capturing || _capturedThisFrame;

    public void Open()
    {
        _row = 0;
        _col = 0;
        _capturing = false;
        _capturedThisFrame = false;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Update
    // ─────────────────────────────────────────────────────────────────────
    public void Update(GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _pulse += dt * 4f;
        _capturedThisFrame = false;

        if (_capturing)
        {
            UpdateCapture(dt);
            return;
        }

        var kb = Core.Input.Keyboard;
        var mouse = Core.Input.Mouse;

        // ── Mouse: hover seleziona, click avvia l'assegnazione ───────────
        if (mouse.WasMoved || mouse.WasButtonJustPressed(MouseButton.Left))
            for (var r = 0; r <= ResetRow; r++)
            for (var c = 0; c < Columns; c++)
            {
                if (!CellRect(r, c).Contains(mouse.Position)) continue;
                _row = r;
                if (r != ResetRow) _col = c;
                if (mouse.WasButtonJustPressed(MouseButton.Left))
                {
                    Activate();
                    return;
                }
            }

        // ── Tastiera / gamepad: navigazione ──────────────────────────────
        if (GameController.MenuUp())
        {
            _row = (_row - 1 + ResetRow + 1) % (ResetRow + 1);
            AudioManager.PlayUIHover();
        }

        if (GameController.MenuDown())
        {
            _row = (_row + 1) % (ResetRow + 1);
            AudioManager.PlayUIHover();
        }

        if (_row != ResetRow)
        {
            if (GameController.MenuLeft())
            {
                _col = (_col - 1 + Columns) % Columns;
                AudioManager.PlayUIHover();
            }

            if (GameController.MenuRight())
            {
                _col = (_col + 1) % Columns;
                AudioManager.PlayUIHover();
            }
        }

        // CANC / BACKSPACE svuota un tasto della tastiera (non il gamepad: serve sempre un pulsante)
        if (_row != ResetRow && _col < InputBindings.KeySlots &&
            (kb.WasKeyJustPressed(Keys.Delete) || kb.WasKeyJustPressed(Keys.Back)))
        {
            InputBindings.SetKey(InputBindings.Actions[_row], _col, Keys.None);
            AudioManager.PlayUIClick();
            return;
        }

        if (GameController.Confirm()) Activate();
    }

    private void Activate()
    {
        AudioManager.PlayUIClick();
        if (_row == ResetRow)
        {
            InputBindings.ResetToDefaults();
            InputBindings.Save();
            return;
        }

        // L'input che ha aperto l'attesa è "appena premuto" solo in questo frame:
        // dal frame successivo si ascolta il nuovo tasto.
        _capturing = true;
        _captureTimer = CaptureTimeout;
    }

    private void UpdateCapture(float dt)
    {
        _captureTimer -= dt;
        var mouse = Core.Input.Mouse;
        if (_captureTimer <= 0f || mouse.WasButtonJustPressed(MouseButton.Right))
        {
            EndCapture();
            return;
        }

        var action = InputBindings.Actions[_row];

        if (_col < InputBindings.KeySlots)
        {
            // Durante l'attesa di un tasto anche ESC è assegnabile: si annulla con
            // il tasto destro del mouse, un pulsante del gamepad o aspettando.
            if (AnyButtonJustPressed(out _))
            {
                EndCapture();
                return;
            }

            if (TryGetJustPressedKey(out var key))
            {
                InputBindings.SetKey(action, _col, key);
                EndCapture();
            }
        }
        else
        {
            if (Core.Input.Keyboard.WasKeyJustPressed(Keys.Escape))
            {
                EndCapture();
                return;
            }

            if (AnyButtonJustPressed(out var button))
            {
                InputBindings.SetButton(action, button);
                EndCapture();
            }
        }
    }

    private void EndCapture()
    {
        _capturing = false;
        _capturedThisFrame = true;
        AudioManager.PlayUIClick();
    }

    private static bool TryGetJustPressedKey(out Keys key)
    {
        var kb = Core.Input.Keyboard;
        foreach (var k in kb.CurrentState.GetPressedKeys())
        {
            // F11 resta riservato allo schermo intero
            if (k == Keys.F11 || k == Keys.LeftWindows || k == Keys.RightWindows) continue;
            if (kb.PreviousState.IsKeyUp(k))
            {
                key = k;
                return true;
            }
        }

        key = Keys.None;
        return false;
    }

    private static bool AnyButtonJustPressed(out Buttons button)
    {
        foreach (var b in InputBindings.AssignableButtons)
            if (Core.Input.GamePad.WasButtonJustPressed(b))
            {
                button = b;
                return true;
            }

        button = default;
        return false;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Layout
    // ─────────────────────────────────────────────────────────────────────
    private static int BoxHeight => HeaderH + (ResetRow + 1) * RowH + FooterH;

    private int RowCenterY(int row)
    {
        return _lastBox.Y + HeaderH + row * RowH + RowH / 2;
    }

    private Rectangle CellRect(int row, int col)
    {
        if (row == ResetRow)
        {
            // Il pulsante "ripristina" occupa tutte le colonne
            var w = CellW * Columns + CellGap * (Columns - 1);
            return new Rectangle(_lastBox.X + FirstCellOffset, RowCenterY(row) - CellH / 2, w, CellH);
        }

        return new Rectangle(_lastBox.X + FirstCellOffset + col * (CellW + CellGap),
            RowCenterY(row) - CellH / 2, CellW, CellH);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Draw — pannello completo centrato in (cx, cy)
    // ─────────────────────────────────────────────────────────────────────
    public void Draw(SpriteBatch sb, int cx, int cy)
    {
        var boxH = BoxHeight;
        _lastBox = new Rectangle(cx - BoxW / 2, cy - boxH / 2, BoxW, boxH);
        var box = _lastBox;

        UiDraw.Panel(sb, _pixel, box,
            new Color(30, 38, 84, 248), new Color(12, 14, 34, 248), new Color(120, 220, 140), 12, true, 2);
        DrawTextCentered(sb, "COMANDI", cx, box.Y + 24, new Color(140, 240, 160), 1.3f);
        sb.Draw(_pixel, new Rectangle(box.X + 16, box.Y + 44, box.Width - 32, 2), new Color(50, 120, 80));

        // Intestazioni colonne
        var headY = box.Y + 60;
        string[] heads = { "TASTO 1", "TASTO 2", "GAMEPAD" };
        for (var c = 0; c < Columns; c++)
        {
            var r = CellRect(0, c);
            DrawTextCentered(sb, heads[c], r.Center.X, headY,
                c < InputBindings.KeySlots ? new Color(255, 220, 120) : new Color(150, 200, 255), 0.75f);
        }

        for (var row = 0; row < ResetRow; row++)
        {
            var action = InputBindings.Actions[row];
            var y = RowCenterY(row);
            var rowSelected = row == _row;
            var label = InputBindings.ActionName(action);
            var labelSize = _font.MeasureString(label) * 0.8f;
            sb.DrawString(_font, label, new Vector2(box.X + 24, y - labelSize.Y / 2),
                rowSelected ? Color.Yellow : Color.LightGray, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);

            for (var c = 0; c < Columns; c++)
            {
                var selected = rowSelected && c == _col;
                var waiting = selected && _capturing;
                var text = waiting
                    ? "PREMI..."
                    : c < InputBindings.KeySlots
                        ? InputBindings.KeyName(InputBindings.GetKey(action, c))
                        : InputBindings.ButtonName(InputBindings.GetButton(action));
                DrawCell(sb, CellRect(row, c), text, selected, waiting);
            }
        }

        var resetRect = CellRect(ResetRow, 0);
        DrawCell(sb, resetRect, "RIPRISTINA PREDEFINITI", _row == ResetRow, false);

        // Suggerimenti
        var hintY = box.Bottom - FooterH + 20;
        if (_capturing)
        {
            var target = _col < InputBindings.KeySlots ? "un tasto" : "un pulsante del gamepad";
            DrawTextCentered(sb, $"Premi {target} per {InputBindings.ActionName(InputBindings.Actions[_row])}",
                cx, hintY, Color.Yellow, 0.85f);
            DrawTextCentered(sb, $"Annulla: tasto destro mouse / attendi {Math.Ceiling(_captureTimer)}s",
                cx, hintY + 22, Color.Gray, 0.75f);
        }
        else
        {
            DrawTextCentered(sb, "^v<>: seleziona   ENTER/(A)/click: modifica   CANC: svuota tasto",
                cx, hintY, new Color(110, 120, 150), 0.75f);
            DrawTextCentered(sb, "ESC/(B): indietro   Tasti gia usati vengono scambiati   Levetta SX muove sempre",
                cx, hintY + 22, Color.DarkGray, 0.7f);
        }
    }

    private void DrawCell(SpriteBatch sb, Rectangle r, string text, bool selected, bool waiting)
    {
        var accent = waiting ? new Color(230, 170, 40) : new Color(80, 170, 110);
        UiDraw.Button(sb, _pixel, r, selected, false, accent, (float)Math.Sin(_pulse));

        var color = waiting
            ? Color.Yellow * (0.6f + 0.4f * (float)Math.Abs(Math.Sin(_pulse * 1.5f)))
            : selected ? Color.White : Color.LightGray;
        var size = _font.MeasureString(text);
        var scale = Math.Min(0.8f, (r.Width - 10) / Math.Max(1f, size.X));
        DrawTextCentered(sb, text, r.Center.X, r.Center.Y, color, scale);
    }

    private void DrawTextCentered(SpriteBatch sb, string text, int cx, int cy, Color color, float scale)
    {
        var origin = _font.MeasureString(text) * 0.5f;
        var pos = new Vector2(cx, cy);
        sb.DrawString(_font, text, pos + new Vector2(1f, 1f), Color.Black * 0.8f, 0f, origin, scale,
            SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }
}
