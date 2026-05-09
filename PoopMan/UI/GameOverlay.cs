using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PoopManLibrary.World;

namespace PoopMan.UI;

/// <summary>
/// Overlay semi-trasparente per: pausa, game over, flash livello completato.
/// </summary>
public class GameOverlay
{
    public const float LevelFlashDuration = 1.8f;

    private readonly SpriteFont _font;
    private readonly Texture2D  _pixel;

    public GameOverlay(SpriteFont font, Texture2D pixel)
    {
        _font  = font;
        _pixel = pixel;
    }

    // ── Game Over ─────────────────────────────────────────────────────────
    public void DrawGameOver(SpriteBatch sb, int score)
    {
        int vw = sb.GraphicsDevice.Viewport.Width;
        int vh = sb.GraphicsDevice.Viewport.Height;
        int cx = vw / 2;
        int cy = vh / 2;

        // Sfondo scuro totale
        DrawRect(sb, new Rectangle(0, 0, vw, vh), Color.Black * 0.80f);

        // Riquadro centrale
        int boxW = (int)(vw * 0.55f);
        int boxH = (int)(vh * 0.45f);
        int boxX = cx - boxW / 2;
        int boxY = cy - boxH / 2;
        DrawRect(sb, new Rectangle(boxX,     boxY,     boxW, boxH), new Color(30, 10, 10) * 0.95f);
        DrawRect(sb, new Rectangle(boxX,     boxY,     boxW, 3),    new Color(180, 20, 20));
        DrawRect(sb, new Rectangle(boxX,     boxY + boxH - 3, boxW, 3), new Color(180, 20, 20));
        DrawRect(sb, new Rectangle(boxX,     boxY,     3, boxH),    new Color(180, 20, 20));
        DrawRect(sb, new Rectangle(boxX + boxW - 3, boxY, 3, boxH), new Color(180, 20, 20));

        // Testi
        DrawTextCentered(sb, "** GAME OVER **",       cx, boxY + boxH / 5,       Color.Red,   2.2f);
        DrawTextCentered(sb, $"PUNTEGGIO FINALE",      cx, boxY + boxH / 5 + 62,  Color.White, 1.1f);
        DrawTextCentered(sb, $"{score}",               cx, boxY + boxH / 5 + 92,  Color.Gold,  2.0f);
        DrawTextCentered(sb, "---------------------", cx, cy + 20,               Color.DarkRed * 1.5f, 1f);
        DrawTextCentered(sb, "R  /  ENTER  per riavviare", cx, cy + 50,          Color.LightGray, 1f);
        DrawTextCentered(sb, "ESC  per uscire",            cx, cy + 76,          Color.Gray * 0.9f, 0.85f);
    }

    // ── Pausa ─────────────────────────────────────────────────────────────
    public void DrawPause(SpriteBatch sb)
    {
        int vw = sb.GraphicsDevice.Viewport.Width;
        int vh = sb.GraphicsDevice.Viewport.Height;

        DrawRect(sb, new Rectangle(0, 0, vw, vh), Color.Black * 0.55f);
        DrawTextCentered(sb, "PAUSA",              vw / 2, vh / 2 - 20, Color.Yellow, 2f);
        DrawTextCentered(sb, "ESC per continuare",  vw / 2, vh / 2 + 28, Color.Gray,   1f);
    }

    // ── Flash livello ─────────────────────────────────────────────────────
    public void DrawLevelFlash(SpriteBatch sb, int level, float elapsed, TileMap.MapTheme theme)
    {
        int vw = sb.GraphicsDevice.Viewport.Width;
        int vh = sb.GraphicsDevice.Viewport.Height;

        float alpha = 1f - (elapsed / LevelFlashDuration);

        // Colore accent per tema
        Color accent = theme switch
        {
            TileMap.MapTheme.Forest  => new Color( 60, 180,  60),
            TileMap.MapTheme.Cave    => new Color(160, 100, 220),
            TileMap.MapTheme.Stone   => new Color(160, 160, 180),
            TileMap.MapTheme.Desert  => new Color(220, 180,  60),
            _                        => Color.Cyan
        };

        // Tema cambiato? (ogni 3 livelli)
        bool themeChanged = level % 3 == 0 && level > 0;
        string themeName = theme switch
        {
            TileMap.MapTheme.Forest  => "FOREST",
            TileMap.MapTheme.Cave    => "CAVE",
            TileMap.MapTheme.Stone   => "STONE",
            TileMap.MapTheme.Desert  => "DESERT",
            _                        => ""
        };

        DrawRect(sb, new Rectangle(0, 0, vw, vh), Color.Black * (alpha * 0.55f));
        DrawTextCentered(sb, $"LIVELLO {level}", vw / 2, vh / 2 - (themeChanged ? 28 : 0),
            Color.Cyan * alpha, 2f);

        if (themeChanged)
        {
            DrawTextCentered(sb, $"TEMA: {themeName}", vw / 2, vh / 2 + 30,
                accent * alpha, 1.3f);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private void DrawRect(SpriteBatch sb, Rectangle r, Color c)
        => sb.Draw(_pixel, r, c);

    private void DrawTextCentered(SpriteBatch sb, string text, int cx, int cy, Color color, float scale)
    {
        Vector2 origin = _font.MeasureString(text) * 0.5f;
        Vector2 pos    = new Vector2(cx, cy);
        sb.DrawString(_font, text, pos + new Vector2(2, 2) * scale, Color.Black * 0.6f, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos,                              color,               0f, origin, scale, SpriteEffects.None, 0f);
    }
}
