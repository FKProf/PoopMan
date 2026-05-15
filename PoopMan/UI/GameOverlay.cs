using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PoopMan.GameObjects;
using PoopManLibrary.World;

namespace PoopMan.UI;

/// <summary>
/// Overlay semi-trasparente per: pausa, game over, flash livello completato.
/// </summary>
public class GameOverlay
{
    public const float LevelFlashDuration = 1.8f;

    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;

    public GameOverlay(SpriteFont font, Texture2D pixel)
    {
        _font = font;
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
        DrawRect(sb, new Rectangle(boxX, boxY, boxW, boxH), new Color(30, 10, 10) * 0.95f);
        DrawRect(sb, new Rectangle(boxX, boxY, boxW, 3), new Color(180, 20, 20));
        DrawRect(sb, new Rectangle(boxX, boxY + boxH - 3, boxW, 3), new Color(180, 20, 20));
        DrawRect(sb, new Rectangle(boxX, boxY, 3, boxH), new Color(180, 20, 20));
        DrawRect(sb, new Rectangle(boxX + boxW - 3, boxY, 3, boxH), new Color(180, 20, 20));

        // Testi
        DrawTextCentered(sb, "** GAME OVER **", cx, boxY + boxH / 5, Color.Red, 2.2f);
        DrawTextCentered(sb, $"PUNTEGGIO FINALE", cx, boxY + boxH / 5 + 62, Color.White, 1.1f);
        DrawTextCentered(sb, $"{score}", cx, boxY + boxH / 5 + 92, Color.Gold, 2.0f);
        DrawTextCentered(sb, "---------------------", cx, cy + 20, Color.DarkRed * 1.5f, 1f);
        DrawTextCentered(sb, "R  /  ENTER  /  Click  per riavviare", cx, cy + 50, Color.LightGray, 1f);
        DrawTextCentered(sb, "ESC  per uscire", cx, cy + 76, Color.Gray * 0.9f, 0.85f);
    }

    // ── Flash livello ─────────────────────────────────────────────────────
    public void DrawLevelFlash(SpriteBatch sb, int level, float elapsed, TileMap.MapTheme theme, int doorBonus = 500)
    {
        int vw = sb.GraphicsDevice.Viewport.Width;
        int vh = sb.GraphicsDevice.Viewport.Height;

        float alpha = 1f - (elapsed / LevelFlashDuration);

        // Colore accent per bioma
        Color accent = theme switch
        {
            TileMap.MapTheme.Forest => new Color(60, 180, 60),
            TileMap.MapTheme.Cave => new Color(160, 100, 220),
            TileMap.MapTheme.Lava => new Color(255, 80, 20),
            TileMap.MapTheme.Ice => new Color(140, 210, 255),
            TileMap.MapTheme.Swamp => new Color(80, 160, 60),
            TileMap.MapTheme.Ruins => new Color(200, 170, 100),
            _ => Color.Cyan
        };

        // Bioma cambiato? (ogni 4 livelli)
        bool themeChanged = level % 4 == 0 && level > 0;
        string themeName = theme switch
        {
            TileMap.MapTheme.Forest => "FOREST",
            TileMap.MapTheme.Cave => "CAVE",
            TileMap.MapTheme.Lava => "LAVA",
            TileMap.MapTheme.Ice => "ICE",
            TileMap.MapTheme.Swamp => "SWAMP",
            TileMap.MapTheme.Ruins => "RUINS",
            _ => ""
        };

        DrawRect(sb, new Rectangle(0, 0, vw, vh), Color.Black * (alpha * 0.55f));

        int baseY = vh / 2 - (themeChanged ? 38 : 14);
        DrawTextCentered(sb, $"LIVELLO {level}", vw / 2, baseY,
            Color.Cyan * alpha, 2f);

        if (themeChanged)
        {
            DrawTextCentered(sb, $"TEMA: {themeName}", vw / 2, baseY + 44,
                accent * alpha, 1.8f);
        }

        // Bonus porta — più grande ogni 5 livelli
        bool bigMilestone = level > 0 && level % 5 == 0;
        string bonusText = bigMilestone
            ? $"+{doorBonus} PTS  ★ BONUS PORTA"
            : $"+{doorBonus} PTS";
        Color bonusColor = bigMilestone ? Color.Gold : Color.LightGreen;
        float bonusScale = bigMilestone ? 1.5f : 1.15f;
        int bonusY = baseY + (themeChanged ? 88 : 44);
        DrawTextCentered(sb, bonusText, vw / 2, bonusY, bonusColor * alpha, bonusScale);
    }

    // ── Flash vita extra ──────────────────────────────────────────────────
    public void DrawExtraLife(SpriteBatch sb, float elapsed, float duration)
    {
        int vw = sb.GraphicsDevice.Viewport.Width;
        int vh = sb.GraphicsDevice.Viewport.Height;

        float t = elapsed / duration;
        float alpha = 1f - t;

        // Sfondo più opaco per risaltare
        DrawRect(sb, new Rectangle(0, 0, vw, vh), Color.Black * (alpha * 0.65f));

        // Riquadro verde centrato
        int boxW = (int)(vw * 0.55f);
        int boxH = 110;
        int boxX = vw / 2 - boxW / 2;
        int boxY = vh / 2 - boxH / 2;
        DrawRect(sb, new Rectangle(boxX, boxY, boxW, boxH), new Color(10, 60, 10) * alpha * 0.95f);
        DrawRect(sb, new Rectangle(boxX, boxY, boxW, 3), Color.LimeGreen * alpha);
        DrawRect(sb, new Rectangle(boxX, boxY + boxH - 3, boxW, 3), Color.LimeGreen * alpha);
        DrawRect(sb, new Rectangle(boxX, boxY, 3, boxH), Color.LimeGreen * alpha);
        DrawRect(sb, new Rectangle(boxX + boxW - 3, boxY, 3, boxH), Color.LimeGreen * alpha);

        // Testo con effetto pulsante nella prima metà
        float pulse = t < 0.5f ? (1.0f + 0.15f * (float)Math.Sin(elapsed * 20f)) : 1f;
        DrawTextCentered(sb, "+1 VITA!", vw / 2, vh / 2 - 20, Color.LimeGreen * alpha, 2.8f * pulse);
        DrawTextCentered(sb, "VITA BONUS GUADAGNATA", vw / 2, vh / 2 + 30, Color.White * alpha, 1.05f);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private void DrawRect(SpriteBatch sb, Rectangle r, Color c)
        => sb.Draw(_pixel, r, c);

    private void DrawTextCentered(SpriteBatch sb, string text, int cx, int cy, Color color, float scale)
    {
        Vector2 origin = _font.MeasureString(text) * 0.5f;
        Vector2 pos = new Vector2(cx, cy);
        // 4-direction outline per massima leggibilità
        Color outline = Color.Black * 0.92f;
        float d = Math.Max(1f, scale * 1.5f);
        sb.DrawString(_font, text, pos + new Vector2(-d, -d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(d, -d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(-d, d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(d, d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        // Ombra diagonale più scura
        sb.DrawString(_font, text, pos + new Vector2(d + 1f, d + 1f), Color.Black * 0.5f, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    // ── Menu Upgrade ──────────────────────────────────────────────────────
    /// <summary>
    /// Disegna il pannello di scelta upgrade.
    /// <paramref name="options"/>      = lista di upgrade disponibili (3 di solito).
    /// <paramref name="selected"/>     = indice selezionato (0-based).
    /// <paramref name="pulse"/>        = valore pulsante (da aggiornare esternamente con sin).
    /// <paramref name="getLevelInfo"/> = callback che restituisce (livelloCorrente, livelloMassimo) per tipo.
    /// </summary>
    public void DrawUpgradeMenu(SpriteBatch sb, IReadOnlyList<UpgradeDef> options,
                                int selected, float pulse,
                                Func<UpgradeType, (int cur, int max)> getLevelInfo = null)
    {
        int vw = sb.GraphicsDevice.Viewport.Width;
        int vh = sb.GraphicsDevice.Viewport.Height;
        int cx = vw / 2;

        // Sfondo
        DrawRect(sb, new Rectangle(0, 0, vw, vh), Color.Black * 0.82f);

        // Titolo
        int titleY = (int)(vh * 0.07f);
        DrawTextCentered(sb, "SCEGLI UN POTENZIAMENTO", cx, titleY, Color.Gold, 2.3f);
        DrawTextCentered(sb, "< > o frecce  |  ENTER per confermare  |  Click per selezionare",
            cx, titleY + 52, new Color(180, 180, 180), 1.15f);

        // Card per ogni opzione
        int cardW = Math.Min(380, vw / options.Count - 24);
        int cardH = 290;
        int gap = 22;
        int totalW = options.Count * cardW + (options.Count - 1) * gap;
        int startX = cx - totalW / 2;
        int cardY = (int)(vh * 0.20f);

        for (int i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            bool sel = i == selected;
            int x = startX + i * (cardW + gap);

            // Sfondo card
            Color bg = sel ? new Color(40, 30, 90, 240) : new Color(20, 18, 40, 200);
            Color border = sel ? opt.Color : new Color(70, 70, 100);
            float pulseMul = sel ? (0.88f + 0.12f * pulse) : 1f;

            // Outer glow per carta selezionata (3px halo)
            if (sel)
            {
                Color glow = opt.Color * (0.35f * pulseMul);
                DrawRect(sb, new Rectangle(x - 3, cardY - 3, cardW + 6, cardH + 6), glow);
            }

            DrawRect(sb, new Rectangle(x, cardY, cardW, cardH), bg);
            // Bordo: 3px se selezionato, 2px altrimenti
            int bw = sel ? 3 : 2;
            DrawRect(sb, new Rectangle(x, cardY, cardW, bw), border);
            DrawRect(sb, new Rectangle(x, cardY + cardH - bw, cardW, bw), border);
            DrawRect(sb, new Rectangle(x, cardY, bw, cardH), border);
            DrawRect(sb, new Rectangle(x + cardW - bw, cardY, bw, cardH), border);

            // Indicatore selezione
            if (sel)
                DrawRect(sb, new Rectangle(x + 4, cardY + 4, cardW - 8, 5), opt.Color * 0.7f);

            // ── Badge livello (in alto a destra della card) ───────────────
            if (getLevelInfo != null)
            {
                var (cur, max) = getLevelInfo(opt.Type);
                if (max > 1 && max != int.MaxValue)
                {
                    string lvText = $"Lv {cur}/{max}";
                    Color badgeBg = cur > 0 ? new Color(60, 20, 90) : new Color(30, 30, 30);
                    Color badgeFg = cur > 0 ? opt.Color : new Color(130, 130, 130);
                    int badgeW = 70, badgeH = 22;
                    int bx = x + cardW - badgeW - 8;
                    int by = cardY + 8;
                    DrawRect(sb, new Rectangle(bx, by, badgeW, badgeH), badgeBg);
                    DrawTextCentered(sb, lvText, bx + badgeW / 2, by + 2, badgeFg, 0.95f);
                }
                else if (cur > 0 && max == 1)
                {
                    int badgeW = 70, badgeH = 22;
                    int bx = x + cardW - badgeW - 8;
                    int by = cardY + 8;
                    DrawRect(sb, new Rectangle(bx, by, badgeW, badgeH), new Color(20, 70, 20));
                    DrawTextCentered(sb, "ATTIVO", bx + badgeW / 2, by + 2, Color.LightGreen, 0.95f);
                }
            }

            // Nome upgrade
            int nameCx = x + cardW / 2;
            DrawTextCentered(sb, opt.Name, nameCx, cardY + 46,
                sel ? opt.Color * pulseMul : Color.White, sel ? 1.75f : 1.5f);

            // Linea separatrice
            DrawRect(sb, new Rectangle(x + 14, cardY + 82, cardW - 28, 2),
                new Color(80, 80, 120));

            // Descrizione (wrap manuale su \n)
            var lines = opt.Description.Split('\n');
            int descY = cardY + 100;
            foreach (var line in lines)
            {
                DrawTextCentered(sb, line, nameCx, descY,
                    sel ? Color.White : new Color(185, 185, 205), 1.15f);
                descY += 34;
            }

            // ── Riga bonus danno dinamica (solo per ExplosionDamage) ──────
            if (opt.Type == UpgradeType.ExplosionDamage && getLevelInfo != null)
            {
                var (cur, max) = getLevelInfo(opt.Type);
                int bonusDmg = cur / 2;
                string bonusLine = bonusDmg > 0
                    ? $"+{bonusDmg} danno attuale  (Lv {cur}/{max})"
                    : $"Nessun bonus ancora  (Lv {cur}/{max})";
                Color bonusColor = bonusDmg > 0 ? new Color(255, 160, 60) : new Color(160, 160, 160);
                DrawTextCentered(sb, bonusLine, nameCx, descY + 4, bonusColor, 1.1f);
            }

            // Freccia in basso se selezionato
            if (sel)
                DrawTextCentered(sb, "[ SELEZIONATO ]", nameCx, cardY + cardH - 28,
                    opt.Color * pulseMul, 1.1f);
        }
    }
}
