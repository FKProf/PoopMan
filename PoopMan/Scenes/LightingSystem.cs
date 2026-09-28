using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PoopManLibrary.World;
using System;
using System.Collections.Generic;

namespace PoopMan.Scenes;

/// <summary>
///     Illuminazione dinamica 2D senza shader.
///     Ogni frame:
///     1. <see cref="RenderLightMap" /> disegna in un render target a mezza risoluzione
///        il colore ambientale del bioma + le luci (additive).
///     2. Il mondo viene disegnato normalmente nel back buffer.
///     3. <see cref="ApplyLightMap" /> moltiplica il mondo per la light map
///        (zone buie scure, attorno alle luci colori pieni).
///     4. <see cref="DrawGlow" /> aggiunge un alone additivo sulle sorgenti forti
///        (esplosioni, lava, micce) per un leggero effetto "bloom".
///     La light map va disegnata PRIMA del mondo: cambiare render target a metà
///     frame può scartare il contenuto del back buffer su alcune piattaforme.
/// </summary>
internal sealed class LightingSystem : IDisposable
{
    private const int Downscale = 2;

    // Luce ambientale per bioma: più bassa = più buio (1 = nessun effetto).
    private static readonly Dictionary<TileMap.MapTheme, Color> Ambient = new()
    {
        [TileMap.MapTheme.Forest] = new Color(240, 236, 226),
        [TileMap.MapTheme.Cave] = new Color(104, 100, 140),
        [TileMap.MapTheme.Lava] = new Color(160, 118, 108),
        [TileMap.MapTheme.Ice] = new Color(205, 218, 240),
        [TileMap.MapTheme.Swamp] = new Color(142, 166, 140),
        [TileMap.MapTheme.Ruins] = new Color(188, 172, 146)
    };

    private static readonly BlendState Multiply = new()
    {
        ColorSourceBlend = Blend.DestinationColor,
        ColorDestinationBlend = Blend.Zero,
        ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add
    };

    private readonly GraphicsDevice _gd;
    private readonly List<Light> _glows = new();
    private readonly List<Light> _lights = new();
    private readonly int _mapH;
    private readonly int _mapW;
    private readonly Texture2D _radial;
    private readonly SpriteBatch _sb;
    private RenderTarget2D _lightMap;

    public LightingSystem(GraphicsDevice gd, int mapW, int mapH)
    {
        _gd = gd;
        _mapW = mapW;
        _mapH = mapH;
        _sb = new SpriteBatch(gd);
        _radial = FxTextures.CreateRadial(gd, 128, 1.6f);
    }

    public TileMap.MapTheme Theme { get; set; }

    /// <summary>Luce ambientale del bioma corrente.</summary>
    public Color AmbientColor => Ambient.TryGetValue(Theme, out var c) ? c : Color.White;

    public void Dispose()
    {
        _lightMap?.Dispose();
        _radial?.Dispose();
        _sb?.Dispose();
    }

    /// <summary>Svuota le luci accumulate per il frame precedente.</summary>
    public void Clear()
    {
        _lights.Clear();
        _glows.Clear();
    }

    /// <summary>Aggiunge una luce (coordinate mondo, pixel della mappa).</summary>
    public void AddLight(Vector2 worldPos, float radius, Color color, float intensity = 1f)
    {
        if (intensity <= 0f || radius <= 0f) return;
        _lights.Add(new Light(worldPos, radius, color * Math.Clamp(intensity, 0f, 1f)));
    }

    /// <summary>Aggiunge un alone additivo (bloom leggero) sopra il mondo già illuminato.</summary>
    public void AddGlow(Vector2 worldPos, float radius, Color color, float intensity = 1f)
    {
        if (intensity <= 0f || radius <= 0f) return;
        _glows.Add(new Light(worldPos, radius, color * Math.Clamp(intensity, 0f, 1f)));
    }

    private void EnsureTarget()
    {
        var w = _mapW / Downscale;
        var h = _mapH / Downscale;
        if (_lightMap != null && !_lightMap.IsDisposed) return;
        _lightMap?.Dispose();
        _lightMap = new RenderTarget2D(_gd, w, h, false, SurfaceFormat.Color, DepthFormat.None);
    }

    /// <summary>Disegna la light map nel render target (chiamare prima di disegnare il mondo).</summary>
    public void RenderLightMap()
    {
        EnsureTarget();
        _gd.SetRenderTarget(_lightMap);
        _gd.Clear(AmbientColor);

        if (_lights.Count > 0)
        {
            _sb.Begin(samplerState: SamplerState.LinearClamp, blendState: BlendState.Additive,
                transformMatrix: Matrix.CreateScale(1f / Downscale));
            foreach (var l in _lights) DrawRadial(l);
            _sb.End();
        }

        _gd.SetRenderTarget(null);
    }

    /// <summary>Moltiplica il mondo (già nel back buffer) per la light map.</summary>
    public void ApplyLightMap(Matrix worldTransform)
    {
        if (_lightMap == null) return;
        _sb.Begin(samplerState: SamplerState.LinearClamp, blendState: Multiply, transformMatrix: worldTransform);
        _sb.Draw(_lightMap, new Rectangle(0, 0, _mapW, _mapH), Color.White);
        _sb.End();
    }

    /// <summary>Aloni additivi sopra le sorgenti forti.</summary>
    public void DrawGlow(Matrix worldTransform)
    {
        if (_glows.Count == 0) return;
        _sb.Begin(samplerState: SamplerState.LinearClamp, blendState: BlendState.Additive,
            transformMatrix: worldTransform);
        foreach (var g in _glows) DrawRadial(g);
        _sb.End();
    }

    private void DrawRadial(Light l)
    {
        var size = l.Radius * 2f;
        _sb.Draw(_radial,
            new Rectangle((int)(l.Pos.X - l.Radius), (int)(l.Pos.Y - l.Radius), (int)size, (int)size),
            l.Color);
    }

    private readonly record struct Light(Vector2 Pos, float Radius, Color Color);
}
