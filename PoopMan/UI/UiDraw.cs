using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace PoopMan.UI;

/// <summary>
///     Primitive grafiche condivise per un'interfaccia più moderna ma sempre pixel-art:
///     pannelli con angoli arrotondati "a gradini", ombra portata, gradiente verticale
///     e bordo luminoso. Usano solo la texture 1×1 bianca.
/// </summary>
internal static class UiDraw
{
    /// <summary>Rientro orizzontale della riga <paramref name="i" /> di un angolo di raggio <paramref name="r" />.</summary>
    private static int Inset(int i, int r)
    {
        if (i >= r) return 0;
        var dy = r - i - 0.5f;
        return (int)MathF.Round(r - MathF.Sqrt(MathF.Max(0f, r * r - dy * dy)));
    }

    /// <summary>Rettangolo pieno con angoli arrotondati.</summary>
    public static void RoundedRect(SpriteBatch sb, Texture2D px, Rectangle r, Color c, int radius)
    {
        radius = Math.Min(radius, Math.Min(r.Width, r.Height) / 2);
        if (radius <= 0)
        {
            sb.Draw(px, r, c);
            return;
        }

        for (var i = 0; i < radius; i++)
        {
            var ins = Inset(i, radius);
            sb.Draw(px, new Rectangle(r.X + ins, r.Y + i, r.Width - ins * 2, 1), c);
            sb.Draw(px, new Rectangle(r.X + ins, r.Bottom - 1 - i, r.Width - ins * 2, 1), c);
        }

        sb.Draw(px, new Rectangle(r.X, r.Y + radius, r.Width, r.Height - radius * 2), c);
    }

    /// <summary>Rettangolo arrotondato con gradiente verticale (top → bottom).</summary>
    public static void GradientRect(SpriteBatch sb, Texture2D px, Rectangle r, Color top, Color bottom, int radius)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        radius = Math.Min(radius, Math.Min(r.Width, r.Height) / 2);
        for (var y = 0; y < r.Height; y++)
        {
            var t = r.Height <= 1 ? 0f : y / (float)(r.Height - 1);
            var ins = y < radius ? Inset(y, radius) : r.Height - 1 - y < radius ? Inset(r.Height - 1 - y, radius) : 0;
            sb.Draw(px, new Rectangle(r.X + ins, r.Y + y, r.Width - ins * 2, 1), Color.Lerp(top, bottom, t));
        }
    }

    /// <summary>
    ///     Pannello completo: ombra portata, bordo, gradiente interno e
    ///     riflesso chiaro in alto.
    /// </summary>
    public static void Panel(SpriteBatch sb, Texture2D px, Rectangle r, Color top, Color bottom, Color border,
        int radius = 8, bool shadow = true, int borderWidth = 2)
    {
        if (shadow)
        {
            RoundedRect(sb, px, new Rectangle(r.X + 3, r.Y + 5, r.Width, r.Height), Color.Black * 0.35f, radius);
            RoundedRect(sb, px, new Rectangle(r.X + 1, r.Y + 2, r.Width, r.Height), Color.Black * 0.25f, radius);
        }

        RoundedRect(sb, px, r, border, radius);
        var inner = new Rectangle(r.X + borderWidth, r.Y + borderWidth, r.Width - borderWidth * 2,
            r.Height - borderWidth * 2);
        GradientRect(sb, px, inner, top, bottom, Math.Max(0, radius - borderWidth));

        // Riflesso: linea chiara appena sotto il bordo superiore
        var hl = Math.Max(0, radius - borderWidth);
        sb.Draw(px, new Rectangle(inner.X + hl, inner.Y, Math.Max(0, inner.Width - hl * 2), 1), Color.White * 0.12f);
    }

    /// <summary>Pulsante "pillola" con stato selezionato / hover.</summary>
    public static void Button(SpriteBatch sb, Texture2D px, Rectangle r, bool selected, bool hovered,
        Color accent, float pulse = 0f)
    {
        if (selected)
        {
            // Alone esterno pulsante
            var glow = 0.25f + 0.15f * pulse;
            RoundedRect(sb, px, new Rectangle(r.X - 3, r.Y - 3, r.Width + 6, r.Height + 6), accent * glow, 9);
            Panel(sb, px, r, Color.Lerp(accent, Color.White, 0.25f) * 0.95f, Color.Lerp(accent, Color.Black, 0.35f),
                Color.Lerp(accent, Color.White, 0.55f), 7, true);
        }
        else if (hovered)
        {
            Panel(sb, px, r, new Color(70, 58, 130), new Color(42, 32, 88), new Color(140, 120, 220), 7, true);
        }
        else
        {
            Panel(sb, px, r, new Color(46, 38, 92), new Color(26, 20, 58), new Color(78, 64, 150), 7, true);
        }
    }

    /// <summary>Testo con ombra morbida e contorno (titoli).</summary>
    public static void OutlinedText(SpriteBatch sb, SpriteFont font, string text, Vector2 pos, Color color,
        float scale, Color? outline = null, int shadowOffset = 3)
    {
        var o = outline ?? new Color(20, 10, 30);
        sb.DrawString(font, text, pos + new Vector2(shadowOffset, shadowOffset), Color.Black * 0.45f, 0f, Vector2.Zero,
            scale, SpriteEffects.None, 0f);
        for (var dx = -2; dx <= 2; dx += 2)
            for (var dy = -2; dy <= 2; dy += 2)
                if (dx != 0 || dy != 0)
                    sb.DrawString(font, text, pos + new Vector2(dx, dy), o, 0f, Vector2.Zero, scale,
                        SpriteEffects.None, 0f);
        sb.DrawString(font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
}
