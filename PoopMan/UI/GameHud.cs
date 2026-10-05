using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PoopManLibrary.World;
using System;
using System.Collections.Generic;

namespace PoopMan.UI;

/// <summary>
///     Barra HUD in cima alla finestra: score, vite, bombe, tema, livello, chiave.
///     Spazio logico 1248×32; viene scalata alla larghezza viewport tramite GetHudMatrix().
/// </summary>
public class GameHud
{
    public const int Height = 36; // altezza logica HUD (px di gioco)
    private const int LogicalWidth = 1248; // larghezza logica = mappa

    // Rettangoli sorgente dagli spritesheet
    private static readonly Rectangle SrcMinerIcon = new(128, 32, 32, 32);
    private static readonly Rectangle SrcBigBomb = new(96, 0, 32, 32);
    private static readonly Rectangle SrcKey = new(96, 96, 32, 32);
    private static readonly Rectangle SrcSmallBomb = new(0, 96, 32, 32);

    // Colori fissi HUD
    private static readonly Color BgTop = new(12, 10, 28);
    private static readonly Color BgBottom = new(22, 18, 48);
    private static readonly Color BorderColor = new(80, 55, 160);

    // Colori per bioma
    private static readonly Dictionary<TileMap.MapTheme, (Color accent, string label)> ThemeStyle = new()
    {
        [TileMap.MapTheme.Forest] = (new Color(60, 180, 60), "FOREST"),
        [TileMap.MapTheme.Cave] = (new Color(160, 100, 220), "CAVE"),
        [TileMap.MapTheme.Lava] = (new Color(255, 80, 20), "LAVA"),
        [TileMap.MapTheme.Ice] = (new Color(140, 210, 255), "ICE"),
        [TileMap.MapTheme.Swamp] = (new Color(80, 160, 60), "SWAMP"),
        [TileMap.MapTheme.Ruins] = (new Color(200, 170, 100), "RUINS")
    };

    private readonly SpriteFont _font;
    private readonly Texture2D _itemIcon;
    private readonly Texture2D _minerIcon;
    private readonly Texture2D _pixel;

    public GameHud(SpriteFont font, Texture2D minerIcon, Texture2D itemIcon, Texture2D pixel)
    {
        _font = font;
        _minerIcon = minerIcon;
        _itemIcon = itemIcon;
        _pixel = pixel;
    }

    public static Matrix GetHudMatrix(GraphicsDevice gd)
    {
        var scale = gd.Viewport.Width / (float)LogicalWidth;
        return Matrix.CreateScale(scale, scale, 1f);
    }

    public static int ScreenHeight(GraphicsDevice gd)
    {
        var scale = gd.Viewport.Width / (float)LogicalWidth;
        return (int)(Height * scale);
    }

    public void Draw(SpriteBatch sb, int score, int lives, int maxLives, int bigBombs,
        int level, bool hasKey, bool keyActive, TileMap.MapTheme theme,
        bool hasShield = false, bool shieldActive = false,
        int explosionDmgBonus = 0, bool isInvincible = false,
        bool mythicImmortality = false, bool instantKill = false,
        bool hasDetonator = false, int bombsAvailable = -1, int bombCapacity = 0)
    {
        var cy = (Height - _font.LineSpacing) / 2f;
        var iconH = Height / 32f; // scala icone all'altezza HUD

        // ── Sfondo: gradiente + linea d'accento del bioma che sfuma ai lati ──
        var accent = ThemeStyle[theme].accent;
        UiDraw.GradientRect(sb, _pixel, new Rectangle(0, 0, LogicalWidth, Height), BgBottom, BgTop, 0);
        sb.Draw(_pixel, new Rectangle(0, 0, LogicalWidth, 1), Color.White * 0.06f);
        const int segs = 48;
        for (var i = 0; i < segs; i++)
        {
            var a = 1f - MathF.Abs(i - (segs - 1) / 2f) / (segs / 2f);
            var col = Color.Lerp(BorderColor, accent, a);
            sb.Draw(_pixel, new Rectangle(i * LogicalWidth / segs, Height - 2, LogicalWidth / segs + 1, 2),
                col * (0.55f + 0.45f * a));
        }

        // ── SINISTRA: Score ───────────────────────────────────────────────
        var lx = 10;
        var scoreStr = $"SCORE: {score,6}";
        Chip(sb, lx - 5, (int)_font.MeasureString(scoreStr).X + 10);
        DrawS(sb, scoreStr, new Vector2(lx, cy), Color.Yellow);
        lx += (int)_font.MeasureString(scoreStr).X + 16;

        // Separatore verticale
        sb.Draw(_pixel, new Rectangle(lx, 4, 1, Height - 8), BorderColor);
        lx += 10;

        // ── SINISTRA: Vite (slot dinamici basati su maxLives) ────────────
        DrawS(sb, "HP:", new Vector2(lx, cy), new Color(220, 220, 220));
        lx += (int)_font.MeasureString("HP:").X + 6;
        var lifeIconScale = maxLives <= 5 ? iconH * 0.85f : iconH * (4.25f / maxLives);
        var iconStep = Math.Max(2, (int)(32 * lifeIconScale) + 2);

        // Bordo dorato/viola attorno all'area vite se Mythic Immortality attivo
        if (mythicImmortality)
        {
            var borderRect = new Rectangle(lx - 3, 1, maxLives * iconStep + 2, Height - 2);
            var pulse = 0.6f + 0.4f * (float)Math.Sin(Environment.TickCount64 * 0.010);
            sb.Draw(_pixel, new Rectangle(borderRect.X, borderRect.Y, borderRect.Width, 2),
                new Color(220, 180, 30) * pulse);
            sb.Draw(_pixel, new Rectangle(borderRect.X, borderRect.Bottom - 2, borderRect.Width, 2),
                new Color(220, 180, 30) * pulse);
            sb.Draw(_pixel, new Rectangle(borderRect.X, borderRect.Y, 2, borderRect.Height),
                new Color(180, 80, 255) * pulse);
            sb.Draw(_pixel, new Rectangle(borderRect.Right - 2, borderRect.Y, 2, borderRect.Height),
                new Color(180, 80, 255) * pulse);
        }

        for (var i = 0; i < maxLives; i++)
        {
            var lifeColor = i < lives ? Color.White : new Color(60, 20, 20) * 0.6f;
            sb.Draw(_minerIcon, new Vector2(lx + i * iconStep, 2),
                SrcMinerIcon, lifeColor, 0f, Vector2.Zero, lifeIconScale, SpriteEffects.None, 0f);
        }

        lx += maxLives * iconStep + 14;

        // Separatore
        sb.Draw(_pixel, new Rectangle(lx, 4, 1, Height - 8), BorderColor);
        lx += 10;

        // ── SINISTRA: Bombe grandi ────────────────────────────────────────
        sb.Draw(_itemIcon, new Vector2(lx, 2),
            SrcBigBomb, Color.White, 0f, Vector2.Zero, iconH * 0.88f, SpriteEffects.None, 0f);
        lx += (int)(32 * iconH * 0.88f) + 4;
        DrawS(sb, $"x{bigBombs}", new Vector2(lx, cy),
            bigBombs > 0 ? Color.Orange : Color.Gray * 0.5f);

        // ── CENTRO: Tema + Livello ────────────────────────────────────────
        var style = ThemeStyle[theme];
        var themeAccent = style.accent;
        var themeLabel = style.label;
        var centerText = $"{themeLabel}  |  LVL {level}";
        var centerSize = _font.MeasureString(centerText);
        var centerX = LogicalWidth / 2f - centerSize.X / 2f;

        // Sfondo pillola centrata
        var pillPad = 10;
        var pill = new Rectangle((int)centerX - pillPad, 4, (int)centerSize.X + pillPad * 2, Height - 8);
        UiDraw.RoundedRect(sb, _pixel, pill, themeAccent * 0.65f, 8);
        UiDraw.GradientRect(sb, _pixel, new Rectangle(pill.X + 1, pill.Y + 1, pill.Width - 2, pill.Height - 2),
            Color.Lerp(BgTop, themeAccent, 0.30f), Color.Lerp(BgTop, themeAccent, 0.10f), 7);

        // Testo tema (colorato) + separatore + livello (cyan)
        var themeSize = _font.MeasureString(themeLabel);
        DrawS(sb, themeLabel, new Vector2(centerX, cy), themeAccent);

        var separator = "  |  ";
        var sepX = centerX + themeSize.X;
        DrawS(sb, separator, new Vector2(sepX, cy), BorderColor);

        var lvlX = sepX + _font.MeasureString(separator).X;
        DrawS(sb, $"LVL {level}", new Vector2(lvlX, cy), Color.Cyan);

        // ── Bombe piccole disponibili (slot a destra del livello) ─────────
        var middleRight = pill.Right; // limite sinistro per gli indicatori abilità
        if (bombCapacity > 0 && bombsAvailable >= 0)
        {
            var slotScale = iconH * 0.8f;
            var slotStep = (int)(32 * slotScale * 0.55f);
            var sx = pill.Right + 12;
            for (var i = 0; i < bombCapacity; i++)
            {
                var ready = i < bombsAvailable;
                sb.Draw(_itemIcon, new Vector2(sx + i * slotStep, 4), SrcSmallBomb,
                    ready ? Color.White : new Color(40, 40, 60) * 0.8f, 0f, Vector2.Zero, slotScale,
                    SpriteEffects.None, 0f);
            }

            middleRight = sx + (bombCapacity - 1) * slotStep + (int)(32 * slotScale);
        }

        // ── DESTRA: Chiave ────────────────────────────────────────────────
        var rx = LogicalWidth - 10;

        if (keyActive)
        {
            var keyColor = hasKey ? Color.Gold : new Color(80, 80, 80);
            var keyStr = hasKey ? "KEY" : "NO KEY";
            rx -= (int)_font.MeasureString(keyStr).X;
            DrawS(sb, keyStr, new Vector2(rx, cy), keyColor);
            rx -= (int)(32 * iconH * 0.9f) + 4;
            sb.Draw(_itemIcon, new Vector2(rx, 2),
                SrcKey, keyColor, 0f, Vector2.Zero, iconH * 0.9f, SpriteEffects.None, 0f);
        }

        // ── DESTRA: Indicatori abilità permanenti ─────────────────────────
        // Ordine da destra verso sinistra (come prima): INV, scudo, DET, danno, mythic.
        var tags = new List<(string text, Color color)>();
        if (isInvincible)
            tags.Add(("INV", new Color(255, 255, 120) * (0.5f + 0.5f * (float)Math.Sin(Environment.TickCount64 * 0.012))));
        if (hasShield)
            tags.Add(shieldActive ? ("[SH]", new Color(180, 220, 255)) : ("[sh]", new Color(100, 130, 180)));
        if (hasDetonator)
            tags.Add(("[DET]", new Color(255, 99, 71)));
        if (explosionDmgBonus > 0)
            tags.Add(($"+{explosionDmgBonus}dmg", new Color(255, 160, 40)));
        if (instantKill)
            tags.Add(("[IK]", new Color(255, 80, 80) * (0.7f + 0.3f * (float)Math.Sin(Environment.TickCount64 * 0.009))));
        if (mythicImmortality)
            tags.Add(("[IMM]", new Color(220, 180, 30) * (0.7f + 0.3f * (float)Math.Sin(Environment.TickCount64 * 0.008))));

        DrawAbilityTags(sb, tags, middleRight + 12, rx - 10);
    }

    /// <summary>
    ///     Disegna gli indicatori abilità allineati a destra nello spazio [left, right]
    ///     senza mai sovrapporli agli altri elementi dell'HUD: se non entrano su una
    ///     riga vengono rimpiccioliti e, se serve, divisi su due righe.
    /// </summary>
    private void DrawAbilityTags(SpriteBatch sb, List<(string text, Color color)> tags, int left, int right)
    {
        if (tags.Count == 0) return;
        const float gap = 8f;
        var available = Math.Max(1, right - left);

        float RowWidth(int from, int to)
        {
            var w = 0f;
            for (var i = from; i < to; i++) w += _font.MeasureString(tags[i].text).X + (i > from ? gap : 0f);
            return w;
        }

        // Una riga, eventualmente rimpicciolita
        var single = RowWidth(0, tags.Count);
        var singleScale = Math.Min(1f, available / single);
        if (singleScale >= 0.75f || tags.Count == 1)
        {
            DrawTagRow(sb, tags, 0, tags.Count, right, Height / 2f, singleScale, gap);
            return;
        }

        // Due righe impilate (la prima contiene gli indicatori più a destra)
        var split = (tags.Count + 1) / 2;
        var widest = Math.Max(RowWidth(0, split), RowWidth(split, tags.Count));
        var rowScale = Math.Min((Height - 4) / 2f / _font.LineSpacing, available / widest);
        var rowH = _font.LineSpacing * rowScale;
        DrawTagRow(sb, tags, 0, split, right, Height / 2f - rowH / 2f, rowScale, gap);
        DrawTagRow(sb, tags, split, tags.Count, right, Height / 2f + rowH / 2f, rowScale, gap);
    }

    private void DrawTagRow(SpriteBatch sb, List<(string text, Color color)> tags, int from, int to,
        float right, float centerY, float scale, float gap)
    {
        var x = right;
        for (var i = from; i < to; i++)
        {
            var size = _font.MeasureString(tags[i].text) * scale;
            x -= size.X;
            var pos = new Vector2(x, centerY - size.Y / 2f);
            sb.DrawString(_font, tags[i].text, pos + new Vector2(1, 1), Color.Black * 0.90f, 0f, Vector2.Zero,
                scale, SpriteEffects.None, 0f);
            sb.DrawString(_font, tags[i].text, pos, tags[i].color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            x -= gap * scale;
        }
    }

    /// <summary>Sfondo arrotondato leggero dietro un gruppo di informazioni.</summary>
    private void Chip(SpriteBatch sb, int x, int w)
    {
        UiDraw.RoundedRect(sb, _pixel, new Rectangle(x, 5, w, Height - 10), Color.White * 0.06f, 6);
    }

    /// <summary>DrawString con ombra 1-pixel per garantire leggibilità sull'HUD.</summary>
    private void DrawS(SpriteBatch sb, string text, Vector2 pos, Color color)
    {
        sb.DrawString(_font, text, pos + new Vector2(1, 1), Color.Black * 0.90f);
        sb.DrawString(_font, text, pos + new Vector2(-1, 1), Color.Black * 0.60f);
        sb.DrawString(_font, text, pos, color);
    }
}