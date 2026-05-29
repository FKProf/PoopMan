using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PoopMan.GameObjects;
using PoopManLibrary.World;

namespace PoopMan.UI;

/// <summary>
///     Overlay semi-trasparente per: pausa, game over, flash livello completato.
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
        var vw = sb.GraphicsDevice.Viewport.Width;
        var vh = sb.GraphicsDevice.Viewport.Height;
        var cx = vw / 2;
        var cy = vh / 2;

        // Sfondo scuro totale
        DrawRect(sb, new Rectangle(0, 0, vw, vh), Color.Black * 0.80f);

        // Riquadro centrale
        var boxW = (int)(vw * 0.55f);
        var boxH = (int)(vh * 0.45f);
        var boxX = cx - boxW / 2;
        var boxY = cy - boxH / 2;
        DrawRect(sb, new Rectangle(boxX, boxY, boxW, boxH), new Color(30, 10, 10) * 0.95f);
        DrawRect(sb, new Rectangle(boxX, boxY, boxW, 3), new Color(180, 20, 20));
        DrawRect(sb, new Rectangle(boxX, boxY + boxH - 3, boxW, 3), new Color(180, 20, 20));
        DrawRect(sb, new Rectangle(boxX, boxY, 3, boxH), new Color(180, 20, 20));
        DrawRect(sb, new Rectangle(boxX + boxW - 3, boxY, 3, boxH), new Color(180, 20, 20));

        // Testi
        DrawTextCentered(sb, "** GAME OVER **", cx, boxY + boxH / 5, Color.Red, 2.2f);
        DrawTextCentered(sb, "PUNTEGGIO FINALE", cx, boxY + boxH / 5 + 62, Color.White, 1.1f);
        DrawTextCentered(sb, $"{score}", cx, boxY + boxH / 5 + 92, Color.Gold, 2.0f);
        DrawTextCentered(sb, "---------------------", cx, cy + 20, Color.DarkRed * 1.5f, 1f);
        DrawTextCentered(sb, "R  /  ENTER  /  Click  per riavviare", cx, cy + 50, Color.LightGray, 1f);
        DrawTextCentered(sb, "ESC  per uscire", cx, cy + 76, Color.Gray * 0.9f, 0.85f);
    }

    // ── Flash livello ─────────────────────────────────────────────────────
    public void DrawLevelFlash(SpriteBatch sb, int level, float elapsed, TileMap.MapTheme theme, int doorBonus = 500)
    {
        var vw = sb.GraphicsDevice.Viewport.Width;
        var vh = sb.GraphicsDevice.Viewport.Height;

        var alpha = 1f - elapsed / LevelFlashDuration;

        // Colore accent per bioma
        var accent = theme switch
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
        var themeChanged = level % 4 == 0 && level > 0;
        var themeName = theme switch
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

        var baseY = vh / 2 - (themeChanged ? 38 : 14);
        DrawTextCentered(sb, $"LIVELLO {level}", vw / 2, baseY,
            Color.Cyan * alpha, 2f);

        if (themeChanged)
            DrawTextCentered(sb, $"TEMA: {themeName}", vw / 2, baseY + 44,
                accent * alpha, 1.8f);

        // Bonus porta — più grande ogni 5 livelli
        var bigMilestone = level > 0 && level % 5 == 0;
        var bonusText = bigMilestone
            ? $"+{doorBonus} PTS  * BONUS PORTA"
            : $"+{doorBonus} PTS";
        var bonusColor = bigMilestone ? Color.Gold : Color.LightGreen;
        var bonusScale = bigMilestone ? 1.5f : 1.15f;
        var bonusY = baseY + (themeChanged ? 88 : 44);
        DrawTextCentered(sb, bonusText, vw / 2, bonusY, bonusColor * alpha, bonusScale);
    }

    // ── Flash vita extra ──────────────────────────────────────────────────
    public void DrawExtraLife(SpriteBatch sb, float elapsed, float duration)
    {
        var vw = sb.GraphicsDevice.Viewport.Width;
        var vh = sb.GraphicsDevice.Viewport.Height;

        var t = elapsed / duration;
        var alpha = 1f - t;

        // Sfondo più opaco per risaltare
        DrawRect(sb, new Rectangle(0, 0, vw, vh), Color.Black * (alpha * 0.65f));

        // Riquadro verde centrato
        var boxW = (int)(vw * 0.55f);
        var boxH = 110;
        var boxX = vw / 2 - boxW / 2;
        var boxY = vh / 2 - boxH / 2;
        DrawRect(sb, new Rectangle(boxX, boxY, boxW, boxH), new Color(10, 60, 10) * alpha * 0.95f);
        DrawRect(sb, new Rectangle(boxX, boxY, boxW, 3), Color.LimeGreen * alpha);
        DrawRect(sb, new Rectangle(boxX, boxY + boxH - 3, boxW, 3), Color.LimeGreen * alpha);
        DrawRect(sb, new Rectangle(boxX, boxY, 3, boxH), Color.LimeGreen * alpha);
        DrawRect(sb, new Rectangle(boxX + boxW - 3, boxY, 3, boxH), Color.LimeGreen * alpha);

        // Testo con effetto pulsante nella prima metà
        var pulse = t < 0.5f ? 1.0f + 0.15f * (float)Math.Sin(elapsed * 20f) : 1f;
        DrawTextCentered(sb, "+1 VITA!", vw / 2, vh / 2 - 20, Color.LimeGreen * alpha, 2.8f * pulse);
        DrawTextCentered(sb, "VITA BONUS GUADAGNATA", vw / 2, vh / 2 + 30, Color.White * alpha, 1.05f);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private void DrawRect(SpriteBatch sb, Rectangle r, Color c)
    {
        sb.Draw(_pixel, r, c);
    }

    private void DrawTextCentered(SpriteBatch sb, string text, int cx, int cy, Color color, float scale)
    {
        var origin = _font.MeasureString(text) * 0.5f;
        var pos = new Vector2(cx, cy);
        // 4-direction outline per massima leggibilità
        var outline = Color.Black * 0.92f;
        var d = Math.Max(1f, scale * 1.5f);
        sb.DrawString(_font, text, pos + new Vector2(-d, -d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(d, -d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(-d, d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos + new Vector2(d, d), outline, 0f, origin, scale, SpriteEffects.None, 0f);
        // Ombra diagonale più scura
        sb.DrawString(_font, text, pos + new Vector2(d + 1f, d + 1f), Color.Black * 0.5f, 0f, origin, scale,
            SpriteEffects.None, 0f);
        sb.DrawString(_font, text, pos, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    // ── Menu Upgrade ──────────────────────────────────────────────────────
    /// <summary>
    ///     Disegna il pannello di scelta upgrade.
    ///     <paramref name="options" />      = lista di upgrade disponibili (3 di solito).
    ///     <paramref name="selected" />     = indice selezionato (0-based).
    ///     <paramref name="pulse" />        = valore pulsante (da aggiornare esternamente con sin).
    ///     <paramref name="getLevelInfo" /> = callback che restituisce (livelloCorrente, livelloMassimo) per tipo.
    /// </summary>
    public void DrawUpgradeMenu(SpriteBatch sb, IReadOnlyList<UpgradeDef> options,
        int selected, float pulse,
        Func<UpgradeType, (int cur, int max)> getLevelInfo = null)
    {
        var vw = sb.GraphicsDevice.Viewport.Width;
        var vh = sb.GraphicsDevice.Viewport.Height;
        var cx = vw / 2;

        // Sfondo
        DrawRect(sb, new Rectangle(0, 0, vw, vh), Color.Black * 0.82f);

        // ── Titolo ────────────────────────────────────────────────────────
        // Abbassato: almeno 12% dall'alto (min 28px) così non tocca il bordo
        var titleY = Math.Max(28, (int)(vh * 0.12f));
        DrawTextCentered(sb, "SCEGLI UN POTENZIAMENTO", cx, titleY, Color.Gold, 2.0f);
        var subtitleScale = vw < 900 ? 0.85f : 1.0f;
        DrawTextCentered(sb, "Frecce / A-D  |  ENTER  |  Click",
            cx, titleY + 46, new Color(170, 170, 170), subtitleScale);

        // ── Layout card adattivo ──────────────────────────────────────────
        var padding = 14;
        var gap = Math.Max(10, vw / 80);
        var cardW = Math.Min(340, (vw - gap * (options.Count + 1)) / options.Count);
        cardW = Math.Max(cardW, 200);

        var nameScale = cardW < 260 ? 1.3f : 1.6f;
        var descScale = cardW < 260 ? 0.85f : 1.0f;
        var lineH = (int)(_font.MeasureString("A").Y * descScale) + 6;

        var maxDescLines = 0;
        foreach (var opt in options)
        {
            var lc = opt.Description.Split('\n').Length;
            if (opt.Type == UpgradeType.ExplosionDamage && getLevelInfo != null) lc++;
            if (lc > maxDescLines) maxDescLines = lc;
        }

        var headerH = 70;
        var sepH = 10;
        var descH = maxDescLines * lineH + 8;
        var footerH = 36;
        var badgeH = 30;
        var cardH = badgeH + headerH + sepH + descH + footerH + padding * 2;
        cardH = Math.Max(cardH, 220);

        var totalW = options.Count * cardW + (options.Count - 1) * gap;
        var startX = cx - totalW / 2;
        var topReserved = titleY + 66;
        var availableH = vh - topReserved - 20;
        var cardY = topReserved + Math.Max(0, (availableH - cardH) / 2);

        for (var i = 0; i < options.Count; i++)
        {
            var opt = options[i];
            var sel = i == selected;
            var x = startX + i * (cardW + gap);
            var nameCx = x + cardW / 2;
            var pulseMul = sel ? 0.88f + 0.12f * pulse : 1f;

            // ── Sfondo e bordo card ───────────────────────────────────────
            var bg = sel ? new Color(40, 30, 90, 240) : new Color(20, 18, 40, 200);
            var border = sel ? opt.Color : new Color(70, 70, 100);

            if (sel)
            {
                var glow = opt.Color * (0.30f * pulseMul);
                DrawRect(sb, new Rectangle(x - 4, cardY - 4, cardW + 8, cardH + 8), glow);
            }

            DrawRect(sb, new Rectangle(x, cardY, cardW, cardH), bg);
            var bw = sel ? 3 : 2;
            DrawRect(sb, new Rectangle(x, cardY, cardW, bw), border);
            DrawRect(sb, new Rectangle(x, cardY + cardH - bw, cardW, bw), border);
            DrawRect(sb, new Rectangle(x, cardY, bw, cardH), border);
            DrawRect(sb, new Rectangle(x + cardW - bw, cardY, bw, cardH), border);

            if (sel)
                DrawRect(sb, new Rectangle(x + 4, cardY + 4, cardW - 8, 4), opt.Color * 0.7f);

            var cursorY = cardY + padding;

            // ── Badge livello ─────────────────────────────────────────────
            if (getLevelInfo != null)
            {
                var (cur, max) = getLevelInfo(opt.Type);
                string lvText;
                Color badgeBg;
                Color badgeFg;
                if (max > 1 && max != int.MaxValue)
                {
                    lvText = $"Lv {cur}/{max}";
                    badgeBg = cur > 0 ? new Color(60, 20, 90) : new Color(30, 30, 30);
                    badgeFg = cur > 0 ? opt.Color : new Color(130, 130, 130);
                }
                else if (cur > 0 && max == 1)
                {
                    lvText = "ATTIVO";
                    badgeBg = new Color(20, 70, 20);
                    badgeFg = Color.LightGreen;
                }
                else
                {
                    lvText = null;
                    badgeBg = Color.Transparent;
                    badgeFg = Color.White;
                }

                if (lvText != null)
                {
                    var bW = Math.Min(80, cardW - 16);
                    var bH = 22;
                    var bx = x + cardW - bW - 8;
                    var by = cursorY;
                    DrawRect(sb, new Rectangle(bx, by, bW, bH), badgeBg);
                    DrawTextCentered(sb, lvText, bx + bW / 2, by + 2, badgeFg, 0.9f);
                }
            }

            cursorY += badgeH;

            // ── Nome upgrade (scala automatica se troppo largo) ───────────
            var actualNameScale = nameScale * (sel ? pulseMul : 1f);
            {
                var nameW = _font.MeasureString(opt.Name).X * actualNameScale;
                if (nameW > cardW - padding * 2)
                    actualNameScale *= (cardW - padding * 2) / nameW;
            }
            DrawTextCentered(sb, opt.Name, nameCx, cursorY,
                sel ? opt.Color * pulseMul : Color.White, actualNameScale);
            cursorY += headerH;

            // ── Separatore ────────────────────────────────────────────────
            DrawRect(sb, new Rectangle(x + padding, cursorY, cardW - padding * 2, 2),
                new Color(80, 80, 120));
            cursorY += sepH;

            // ── Descrizione con word-wrap ─────────────────────────────────
            var descColor = sel ? Color.White : new Color(185, 185, 210);
            var maxTextW = cardW - padding * 2;
            var rawLines = opt.Description.Split('\n');
            foreach (var raw in rawLines)
            foreach (var wline in WrapText(raw, descScale, maxTextW))
            {
                DrawTextCentered(sb, wline, nameCx, cursorY, descColor, descScale);
                cursorY += lineH;
            }

            // ── Riga bonus danno dinamica ─────────────────────────────────
            if (opt.Type == UpgradeType.ExplosionDamage && getLevelInfo != null)
            {
                var (cur, max) = getLevelInfo(opt.Type);
                var bonusDmg = cur / 2;
                var bonusLine = bonusDmg > 0
                    ? $"+{bonusDmg} danno  (Lv {cur}/{max})"
                    : $"Nessun bonus  (Lv {cur}/{max})";
                var bonusColor = bonusDmg > 0 ? new Color(255, 160, 60) : new Color(150, 150, 150);
                DrawTextCentered(sb, bonusLine, nameCx, cursorY, bonusColor, descScale * 0.95f);
            }

            // ── Footer selezione ──────────────────────────────────────────
            if (sel)
                DrawTextCentered(sb, "[ SELEZIONATO ]", nameCx, cardY + cardH - footerH / 2 - bw,
                    opt.Color * pulseMul, 1.0f);
        }
    }

    // ── Word-wrap helper ──────────────────────────────────────────────────
    /// <summary>
    ///     Spezza <paramref name="text" /> in righe che non superano <paramref name="maxPixelWidth" />
    ///     alla scala <paramref name="scale" /> data.
    /// </summary>
    private IEnumerable<string> WrapText(string text, float scale, int maxPixelWidth)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield return "";
            yield break;
        }

        if (_font.MeasureString(text).X * scale <= maxPixelWidth)
        {
            yield return text;
            yield break;
        }

        var words = text.Split(' ');
        var current = new StringBuilder();

        foreach (var word in words)
        {
            var test = current.Length == 0 ? word : current + " " + word;
            if (_font.MeasureString(test).X * scale > maxPixelWidth && current.Length > 0)
            {
                yield return current.ToString();
                current.Clear();
                current.Append(word);
            }
            else
            {
                if (current.Length > 0) current.Append(' ');
                current.Append(word);
            }
        }

        if (current.Length > 0) yield return current.ToString();
    }
}