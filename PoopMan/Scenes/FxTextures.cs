using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace PoopMan.Scenes;

/// <summary>
///     Texture procedurali condivise dagli effetti grafici (luci, ombre, particelle).
///     Generate a runtime: nessun asset esterno da caricare.
/// </summary>
internal static class FxTextures
{
    /// <summary>Cerchio con sfumatura radiale morbida (luce / alone).</summary>
    public static Texture2D CreateRadial(GraphicsDevice gd, int size, float power = 2f)
    {
        var tex = new Texture2D(gd, size, size);
        var data = new Color[size * size];
        var c = (size - 1) / 2f;
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = MathF.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                var a = Math.Clamp(1f - d, 0f, 1f);
                a = MathF.Pow(a, power);
                data[y * size + x] = Color.White * a;
            }

        tex.SetData(data);
        return tex;
    }

    /// <summary>Ellisse piena con bordo sfumato (ombra sotto le entità).</summary>
    public static Texture2D CreateEllipse(GraphicsDevice gd, int w, int h)
    {
        var tex = new Texture2D(gd, w, h);
        var data = new Color[w * h];
        var cx = (w - 1) / 2f;
        var cy = (h - 1) / 2f;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var dx = (x - cx) / (w / 2f);
                var dy = (y - cy) / (h / 2f);
                var d = MathF.Sqrt(dx * dx + dy * dy);
                var a = Math.Clamp((1f - d) * 2.5f, 0f, 1f);
                data[y * w + x] = Color.White * a;
            }

        tex.SetData(data);
        return tex;
    }

    /// <summary>Gradiente verticale: opaco in alto, trasparente in basso (ombra proiettata).</summary>
    public static Texture2D CreateVerticalFade(GraphicsDevice gd, int h)
    {
        var tex = new Texture2D(gd, 1, h);
        var data = new Color[h];
        for (var y = 0; y < h; y++)
        {
            var a = 1f - y / (float)(h - 1);
            data[y] = Color.White * (a * a);
        }

        tex.SetData(data);
        return tex;
    }

    /// <summary>Gradiente orizzontale: opaco a sinistra, trasparente a destra.</summary>
    public static Texture2D CreateHorizontalFade(GraphicsDevice gd, int w)
    {
        var tex = new Texture2D(gd, w, 1);
        var data = new Color[w];
        for (var x = 0; x < w; x++)
        {
            var a = 1f - x / (float)(w - 1);
            data[x] = Color.White * (a * a);
        }

        tex.SetData(data);
        return tex;
    }
}
