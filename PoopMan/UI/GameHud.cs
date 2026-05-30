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
        bool mythicImmortality = false, bool instantKill = false)
    {
        var cy = (Height - _font.LineSpacing) / 2f;
        var iconH = Height / 32f; // scala icone all'altezza HUD

        // ── Sfondo sfumato (due rettangoli) ──────────────────────────────
        sb.Draw(_pixel, new Rectangle(0, 0, LogicalWidth, Height / 2), BgTop);
        sb.Draw(_pixel, new Rectangle(0, Height / 2, LogicalWidth, Height / 2), BgBottom);
        sb.Draw(_pixel, new Rectangle(0, Height - 2, LogicalWidth, 2), BorderColor);

        // ── SINISTRA: Score ───────────────────────────────────────────────
        var lx = 10;
        var scoreStr = $"SCORE: {score,6}";
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
        var pillPad = 8;
        sb.Draw(_pixel,
            new Rectangle((int)centerX - pillPad, 4,
                (int)centerSize.X + pillPad * 2, Height - 8),
            themeAccent * 0.18f);
        sb.Draw(_pixel,
            new Rectangle((int)centerX - pillPad, Height - 4,
                (int)centerSize.X + pillPad * 2, 2),
            themeAccent * 0.8f);

        // Testo tema (colorato) + separatore + livello (cyan)
        var themeSize = _font.MeasureString(themeLabel);
        DrawS(sb, themeLabel, new Vector2(centerX, cy), themeAccent);

        var separator = "  |  ";
        var sepX = centerX + themeSize.X;
        DrawS(sb, separator, new Vector2(sepX, cy), BorderColor);

        var lvlX = sepX + _font.MeasureString(separator).X;
        DrawS(sb, $"LVL {level}", new Vector2(lvlX, cy), Color.Cyan);

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
        rx -= 10;
        var abilityY = (Height - 14) / 2f; // centra verticalmente l'etichetta

        // Invincibilità temporanea attiva (bordo luminoso pulsante)
        if (isInvincible)
        {
            var iAlpha = 0.5f + 0.5f * (float)Math.Sin(Environment.TickCount64 * 0.012);
            var iStr = "INV";
            rx -= (int)_font.MeasureString(iStr).X;
            DrawS(sb, iStr, new Vector2(rx, cy), new Color(255, 255, 120) * iAlpha);
            rx -= 8;
        }

        // Scudo
        if (hasShield)
        {
            var shColor = shieldActive ? new Color(180, 220, 255) : new Color(100, 130, 180);
            var shStr = shieldActive ? "[SH]" : "[sh]";
            rx -= (int)_font.MeasureString(shStr).X;
            DrawS(sb, shStr, new Vector2(rx, cy), shColor);
            rx -= 8;
        }

        // Danno esplosione extra
        if (explosionDmgBonus > 0)
        {
            var dmgStr = $"+{explosionDmgBonus}dmg";
            rx -= (int)_font.MeasureString(dmgStr).X;
            DrawS(sb, dmgStr, new Vector2(rx, cy), new Color(255, 160, 40));
            rx -= 8;
        }

        // ── DESTRA: Upgrade Mythic ─────────────────────────────────────────
        if (instantKill)
        {
            var ikStr = "[☠IK]";
            var ikPulse = 0.7f + 0.3f * (float)Math.Sin(Environment.TickCount64 * 0.009);
            rx -= (int)_font.MeasureString(ikStr).X;
            DrawS(sb, ikStr, new Vector2(rx, cy), new Color(255, 80, 80) * ikPulse);
            rx -= 8;
        }

        if (mythicImmortality)
        {
            var miStr = "[✦IMM]";
            var miPulse = 0.7f + 0.3f * (float)Math.Sin(Environment.TickCount64 * 0.008);
            rx -= (int)_font.MeasureString(miStr).X;
            DrawS(sb, miStr, new Vector2(rx, cy), new Color(220, 180, 30) * miPulse);
        }
    }

    /// <summary>DrawString con ombra 1-pixel per garantire leggibilità sull'HUD.</summary>
    private void DrawS(SpriteBatch sb, string text, Vector2 pos, Color color)
    {
        sb.DrawString(_font, text, pos + new Vector2(1, 1), Color.Black * 0.90f);
        sb.DrawString(_font, text, pos + new Vector2(-1, 1), Color.Black * 0.60f);
        sb.DrawString(_font, text, pos, color);
    }
}