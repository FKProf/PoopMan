using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PoopMan.UI;
using PoopManLibrary.Input;

namespace PoopMan.UI;

/// <summary>Risultato restituito da <see cref="PauseMenu.Update"/> ogni frame.</summary>
public enum PauseAction { None, Resume, GoToTitle }

/// <summary>
/// Menu di pausa con pannello audio integrato.
/// Gestisce stato interno, input (tastiera + mouse) e rendering.
/// </summary>
public class PauseMenu
{
    // ── Costanti layout ───────────────────────────────────────────────────
    private const int BtnW = 280;
    private const int BtnH = 34;
    private const int BtnGap = 10;
    private static readonly string[] Items = { "RIPRENDI", "ENCICLOPEDIA", "AUDIO", "MENU PRINCIPALE" };

    // ── Stato interno ─────────────────────────────────────────────────────
    private enum Screen { Menu, Audio, Encyclopedia }
    private Screen _screen = Screen.Menu;
    private int _selected = 0;
    private float _pulse = 0f;

    // ── Dipendenze ────────────────────────────────────────────────────────
    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;
    private readonly AudioSettingsPanel _audioPanel;
    private readonly BatEncyclopedia _encyclopedia;

    public PauseMenu(SpriteFont font, Texture2D pixel, ContentManager content)
    {
        _font = font;
        _pixel = pixel;
        _audioPanel = new AudioSettingsPanel(font, pixel);
        _encyclopedia = new BatEncyclopedia(font, pixel, content);
    }

    // ── Reset (chiamato quando si apre la pausa) ──────────────────────────
    public void Open()
    {
        _screen = Screen.Menu;
        _selected = 0;
        _pulse = 0f;
    }

    // ── Update ────────────────────────────────────────────────────────────
    /// <summary>
    /// Aggiorna input e stato. Restituisce l'azione richiesta dal giocatore.
    /// </summary>
    public PauseAction Update(GameTime gameTime, KeyboardInfo kb, MouseInfo mouse,
                               GraphicsDevice gd, bool escPressed)
    {
        _pulse += (float)gameTime.ElapsedGameTime.TotalSeconds * 3.5f;

        if (_screen == Screen.Audio)
        {
            _audioPanel.Update(gameTime);
            if (escPressed)
                _screen = Screen.Menu;
            return PauseAction.None;
        }

        if (_screen == Screen.Encyclopedia)
        {
            if (_encyclopedia.Update(gameTime, kb, mouse, gd, escPressed))
                _screen = Screen.Menu;
            return PauseAction.None;
        }

        // ── Navigazione tastiera ──────────────────────────────────────────
        if (kb.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Up))
        {
            _selected = (_selected - 1 + Items.Length) % Items.Length;
            AudioManager.PlayUIHover();
        }
        if (kb.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Down))
        {
            _selected = (_selected + 1) % Items.Length;
            AudioManager.PlayUIHover();
        }

        // ── Hover + click mouse ───────────────────────────────────────────
        int vw = gd.Viewport.Width;
        int vh = gd.Viewport.Height;
        int cx = vw / 2;
        int totalMenuH = Items.Length * (BtnH + BtnGap) - BtnGap;
        int boxH = 52 + totalMenuH + 28;
        int boxY = vh / 2 - boxH / 2;
        int menuStartY = boxY + 54;
        Point mp = mouse.Position;

        for (int i = 0; i < Items.Length; i++)
        {
            int btnY = menuStartY + i * (BtnH + BtnGap);
            var rect = new Rectangle(cx - BtnW / 2, btnY, BtnW, BtnH);
            if (rect.Contains(mp))
            {
                if (_selected != i)
                {
                    _selected = i;
                    AudioManager.PlayUIHover();
                }
                if (mouse.WasButtonJustPressed(MouseButton.Left))
                {
                    AudioManager.PlayUIClick();
                    return ExecuteItem(i);
                }
            }
        }

        // ── Conferma tastiera ─────────────────────────────────────────────
        if (kb.WasKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Enter))
        {
            AudioManager.PlayUIClick();
            return ExecuteItem(_selected);
        }

        return PauseAction.None;
    }

    private PauseAction ExecuteItem(int index) => index switch
    {
        0 => PauseAction.Resume,
        1 => GoToEncyclopedia(),
        2 => GoToAudio(),
        3 => PauseAction.GoToTitle,
        _ => PauseAction.None
    };

    private PauseAction GoToEncyclopedia() { _screen = Screen.Encyclopedia; _encyclopedia.Open(); return PauseAction.None; }

    private PauseAction GoToAudio() { _screen = Screen.Audio; return PauseAction.None; }

    // ── Draw ──────────────────────────────────────────────────────────────
    public void Draw(SpriteBatch sb)
    {
        int vw = sb.GraphicsDevice.Viewport.Width;
        int vh = sb.GraphicsDevice.Viewport.Height;
        int cx = vw / 2;

        sb.Draw(_pixel, new Rectangle(0, 0, vw, vh), Color.Black * 0.60f);

        if (_screen == Screen.Audio)
        {
            DrawAudioPanel(sb, cx, vh / 2);
            return;
        }

        if (_screen == Screen.Encyclopedia)
        {
            _encyclopedia.Draw(sb);
            return;
        }

        DrawMainMenu(sb, cx, vh);
    }

    private void DrawMainMenu(SpriteBatch sb, int cx, int vh)
    {
        int totalMenuH = Items.Length * (BtnH + BtnGap) - BtnGap;
        int boxW = BtnW + 60;
        int boxH = 52 + totalMenuH + 28;
        int boxX = cx - boxW / 2;
        int boxY = vh / 2 - boxH / 2;

        DrawRect(sb, new Rectangle(boxX, boxY, boxW, boxH), new Color(15, 15, 35, 240));
        DrawBorderRect(sb, boxX, boxY, boxW, boxH, Color.Yellow);
        DrawTextCentered(sb, "PAUSA", cx, boxY + 22, Color.Yellow, 1.8f);
        DrawRect(sb, new Rectangle(boxX + 16, boxY + 44, boxW - 32, 2), new Color(80, 60, 160));

        int menuStartY = boxY + 54;
        float pulse = 0.85f + 0.15f * (float)Math.Sin(_pulse);

        for (int i = 0; i < Items.Length; i++)
        {
            int btnY = menuStartY + i * (BtnH + BtnGap);
            bool sel = i == _selected;
            float p = sel ? pulse : 1f;

            Color bg = sel ? new Color(60, 40, 140, 230) : new Color(25, 25, 55, 180);
            Color border = sel ? Color.Yellow : new Color(70, 70, 110);
            Color textColor = i == 2
                ? (sel ? new Color(255, 120, 120) * p : new Color(200, 100, 100))
                : (sel ? Color.Yellow * p : Color.LightGray);

            DrawRect(sb, new Rectangle(cx - BtnW / 2 - 1, btnY - 1, BtnW + 2, BtnH + 2), border);
            DrawRect(sb, new Rectangle(cx - BtnW / 2, btnY, BtnW, BtnH), bg);
            DrawTextCentered(sb, Items[i], cx, btnY + BtnH / 2, textColor, sel ? 1.3f : 1.15f);

            if (sel)
            {
                string arrow = ">";
                Vector2 arSz = _font.MeasureString(arrow);
                sb.DrawString(_font, arrow,
                    new Vector2(cx - BtnW / 2 - arSz.X - 8, btnY + BtnH / 2 - arSz.Y / 2),
                    Color.Yellow * p);
            }
        }

        DrawTextCentered(sb, "^v: seleziona   ENTER: conferma   ESC: riprendi",
            cx, boxY + boxH - 14, Color.DarkGray, 0.95f);
    }

    private void DrawAudioPanel(SpriteBatch sb, int cx, int midY)
    {
        // 3 righe × 52 px + titolo (54) + separatore + hint controls (28) + hint esc (28) + padding
        const int rowSpacing = 52;
        const int headerH = 58;   // titolo + separatore
        const int footerH = 62;   // hint controls + hint ESC + padding basso
        const int rowsH = 3 * rowSpacing;
        int boxW = 520;
        int boxH = headerH + rowsH + footerH;
        int boxX = cx - boxW / 2;
        int boxY = midY - boxH / 2;

        DrawRect(sb, new Rectangle(boxX, boxY, boxW, boxH), new Color(15, 15, 35, 245));
        DrawBorderRect(sb, boxX, boxY, boxW, boxH, Color.CornflowerBlue);
        DrawTextCentered(sb, "IMPOSTAZIONI AUDIO", cx, boxY + 26, Color.CornflowerBlue, 1.3f);
        DrawRect(sb, new Rectangle(boxX + 16, boxY + 46, boxW - 32, 2), new Color(40, 80, 160));

        // Prima riga centrata verticalmente nell'area righe
        int firstRowY = boxY + headerH + rowSpacing / 2;
        _audioPanel.Draw(sb, cx, firstRowY, showHint: false);

        int hintControlY = boxY + headerH + rowsH + 18;
        int hintEscY     = boxY + headerH + rowsH + 42;
        DrawTextCentered(sb, "< > volume    ^ v seleziona    M = mute    scroll/click barra",
            cx, hintControlY, new Color(100, 100, 130), 0.90f);
        DrawTextCentered(sb, "ESC: indietro", cx, hintEscY, Color.DarkGray, 0.95f);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private void DrawRect(SpriteBatch sb, Rectangle r, Color c) => sb.Draw(_pixel, r, c);

    private void DrawBorderRect(SpriteBatch sb, int x, int y, int w, int h, Color c)
    {
        DrawRect(sb, new Rectangle(x, y, w, 2), c);
        DrawRect(sb, new Rectangle(x, y + h - 2, w, 2), c);
        DrawRect(sb, new Rectangle(x, y, 2, h), c);
        DrawRect(sb, new Rectangle(x + w - 2, y, 2, h), c);
    }

    private void DrawTextCentered(SpriteBatch sb, string text, int cx, int cy, Color color, float scale)
    {
        Vector2 origin = _font.MeasureString(text) * 0.5f;
        Vector2 pos = new Vector2(cx, cy);
        Color outline = Color.Black * 0.92f;
        float d = Math.Max(1f, scale * 1.5f);
        sb.DrawString(_font, text, pos + new Vector2(-d, -d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(d, -d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(-d, d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(d, d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(d + 1f, d + 1f), Color.Black * 0.5f, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }
}
