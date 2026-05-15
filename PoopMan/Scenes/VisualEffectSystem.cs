using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PoopManLibrary.World;

namespace PoopMan.Scenes;

/// <summary>
/// Centralises all post-processing and atmospheric visual effects for GameScene.
/// Pipeline (per frame):
///   1. Caller draws the world scene into WorldTarget.
///   2. ApplyPostProcess() → applies distortion (heat/frost) → bloom → color-grade
///      and composites the result onto the back buffer.
///   3. Caller draws overlay + HUD on top (sharp, no post-process).
///   4. Update() manages shockwaves, ambient particles, and performance throttling.
/// </summary>
internal sealed class VisualEffectSystem : IDisposable
{
    // ── Render targets ────────────────────────────────────────────────────────
    private RenderTarget2D _worldTarget;    // full-res world scene
    private RenderTarget2D _halfTarget;     // half-res for bloom downscale
    private RenderTarget2D _blurTarget;     // half-res blur ping-pong
    private RenderTarget2D _distortTarget;  // full-res after distortion

    // ── Shaders ───────────────────────────────────────────────────────────────
    private Effect _bloomFx;
    private Effect _colorGradeFx;
    private Effect _heatDistortFx;
    private Effect _shockwaveFx;

    // ── Graphics ──────────────────────────────────────────────────────────────
    private readonly GraphicsDevice _gd;
    private SpriteBatch _sb;
    private Texture2D _pixel;

    // ── Shockwaves ────────────────────────────────────────────────────────────
    private struct ShockwaveEntry
    {
        public Vector2 OriginUV;    // normalised screen UV of explosion centre
        public float Radius;      // current ring radius in UV units
        public float Life;        // remaining life [0..1]
    }
    private readonly List<ShockwaveEntry> _shockwaves = new();
    private const float ShockwaveSpeed = 0.9f;  // UV/s expansion
    private const float ShockwaveThickness = 0.04f;
    private const float ShockwaveStrength = 0.012f;
    private const int MaxShockwaves = 4;      // performance cap

    // ── Ambient particles (lava embers, swamp dust, cave sparks, etc.) ────────
    private struct AmbientParticle
    {
        public Vector2 Pos;         // world-space (tile units * TileSize)
        public Vector2 Vel;
        public float Life;
        public float MaxLife;
        public Color Col;
        public float Size;
    }
    private readonly List<AmbientParticle> _ambientParticles = new();
    private float _ambientTimer;
    private const float AmbientInterval = 0.06f;  // ~16 particles/s
    private const int MaxAmbientParticles = 120;

    // ── Dynamic point lights (glow sources) ──────────────────────────────────
    private struct PointLight
    {
        public Vector2 PosUV;   // screen UV
        public Color Col;
        public float Radius;  // screen UV radius
        public float Life;    // [0..1]
    }
    private readonly List<PointLight> _lights = new();

    // ── State ─────────────────────────────────────────────────────────────────
    private TileMap.MapTheme _theme;
    private float _time;
    private bool _initialized;
    private int _mapW;
    private int _mapH;

    // Performance throttling: reduce quality when many explosions are active
    private int _heavyFrameCount;
    private bool _lowQuality;

    // ── Biome color-grade parameters ──────────────────────────────────────────
    private static readonly Dictionary<TileMap.MapTheme, (Vector3 tint, float tintStr, float contrast, float sat, float bright, float vignR, float vignS)> BiomeGrade = new()
    {
        [TileMap.MapTheme.Forest] = (new Vector3(0.80f, 1.00f, 0.70f), 0.18f, 1.10f, 1.15f, -0.02f, 0.65f, 0.55f),
        [TileMap.MapTheme.Cave] = (new Vector3(0.60f, 0.65f, 0.90f), 0.20f, 1.20f, 0.90f, -0.04f, 0.55f, 0.70f),
        [TileMap.MapTheme.Lava] = (new Vector3(1.00f, 0.70f, 0.45f), 0.22f, 1.15f, 1.25f, 0.02f, 0.60f, 0.65f),
        [TileMap.MapTheme.Ice] = (new Vector3(0.75f, 0.90f, 1.00f), 0.20f, 1.10f, 0.85f, -0.03f, 0.62f, 0.50f),
        [TileMap.MapTheme.Swamp] = (new Vector3(0.65f, 0.90f, 0.55f), 0.20f, 1.08f, 1.10f, -0.02f, 0.60f, 0.60f),
        [TileMap.MapTheme.Ruins] = (new Vector3(0.85f, 0.80f, 0.60f), 0.18f, 1.12f, 0.95f, -0.01f, 0.60f, 0.55f),
    };

    // ── Bloom tuning per bioma ────────────────────────────────────────────────
    private static readonly Dictionary<TileMap.MapTheme, (float threshold, float intensity, float saturation)> BiomeBloom = new()
    {
        [TileMap.MapTheme.Forest] = (0.70f, 0.55f, 1.2f),
        [TileMap.MapTheme.Cave] = (0.65f, 0.70f, 1.1f),
        [TileMap.MapTheme.Lava] = (0.55f, 0.95f, 1.5f),
        [TileMap.MapTheme.Ice] = (0.60f, 0.65f, 1.0f),
        [TileMap.MapTheme.Swamp] = (0.72f, 0.50f, 1.1f),
        [TileMap.MapTheme.Ruins] = (0.68f, 0.58f, 1.1f),
    };

    // ─────────────────────────────────────────────────────────────────────────
    public VisualEffectSystem(GraphicsDevice gd) => _gd = gd;

    // ─────────────────────────────────────────────────────────────────────────
    public void LoadContent(ContentManager content, int mapW, int mapH)
    {
        _mapW = mapW;
        _mapH = mapH;
        _sb = new SpriteBatch(_gd);

        _pixel = new Texture2D(_gd, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // Render targets
        RebuildTargets(mapW, mapH);

        // Shaders — loaded via MonoGame content pipeline
        try
        {
            _bloomFx = content.Load<Effect>("fx/bloom");
            _colorGradeFx = content.Load<Effect>("fx/colorgrade");
            _heatDistortFx = content.Load<Effect>("fx/heatdistort");
            _shockwaveFx = content.Load<Effect>("fx/shockwave");
        }
        catch
        {
            // If shader compilation fails at runtime, degrade gracefully
            _bloomFx = _colorGradeFx = _heatDistortFx = _shockwaveFx = null;
        }

        _initialized = true;
    }

    private void RebuildTargets(int w, int h)
    {
        _worldTarget?.Dispose();
        _distortTarget?.Dispose();
        _halfTarget?.Dispose();
        _blurTarget?.Dispose();

        _worldTarget = new RenderTarget2D(_gd, w, h, false, SurfaceFormat.Color, DepthFormat.None);
        _distortTarget = new RenderTarget2D(_gd, w, h, false, SurfaceFormat.Color, DepthFormat.None);
        _halfTarget = new RenderTarget2D(_gd, w / 2, h / 2, false, SurfaceFormat.Color, DepthFormat.None);
        _blurTarget = new RenderTarget2D(_gd, w / 2, h / 2, false, SurfaceFormat.Color, DepthFormat.None);
    }

    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Set the render target so the caller can draw the world into it.</summary>
    public void BeginWorldCapture(Color clearColor)
    {
        if (!_initialized) return;
        _gd.SetRenderTarget(_worldTarget);
        _gd.Clear(clearColor);
    }

    // ─────────────────────────────────────────────────────────────────────────
    public void Update(GameTime gameTime, TileMap.MapTheme theme, int worldW, int worldH,
                       Func<IEnumerable<(Vector2 worldPos, Color col, float radius)>> lightSources)
    {
        if (!_initialized) return;
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _time += dt;
        _theme = theme;

        // Rebuild targets if resolution changed
        if (_worldTarget.Width != worldW || _worldTarget.Height != worldH)
            RebuildTargets(worldW, worldH);

        // ── Shockwaves ────────────────────────────────────────────────────────
        for (int i = _shockwaves.Count - 1; i >= 0; i--)
        {
            var s = _shockwaves[i];
            s.Radius += ShockwaveSpeed * dt;
            s.Life -= dt * 2.2f;            // fades in ~0.45 s
            if (s.Life <= 0f) { _shockwaves.RemoveAt(i); continue; }
            _shockwaves[i] = s;
        }

        // ── Ambient particles ─────────────────────────────────────────────────
        if (_ambientParticles.Count < MaxAmbientParticles)
        {
            _ambientTimer += dt;
            while (_ambientTimer >= AmbientInterval && _ambientParticles.Count < MaxAmbientParticles)
            {
                _ambientTimer -= AmbientInterval;
                SpawnAmbientParticle(theme, worldW, worldH);
            }
        }

        for (int i = _ambientParticles.Count - 1; i >= 0; i--)
        {
            var p = _ambientParticles[i];
            p.Pos += p.Vel * dt;
            p.Life -= dt;
            if (p.Life <= 0) { _ambientParticles.RemoveAt(i); continue; }
            _ambientParticles[i] = p;
        }

        // ── Performance throttling ────────────────────────────────────────────
        int particleLoad = _shockwaves.Count * 20 + _ambientParticles.Count;
        _heavyFrameCount = particleLoad > 150 ? _heavyFrameCount + 1 : Math.Max(0, _heavyFrameCount - 1);
        _lowQuality = _heavyFrameCount > 10;
    }

    // ─────────────────────────────────────────────────────────────────────────
    private static readonly Random _rng = new();

    private void SpawnAmbientParticle(TileMap.MapTheme theme, int worldW, int worldH)
    {
        AmbientParticle p;
        switch (theme)
        {
            case TileMap.MapTheme.Lava:
                // Embers floating upward from lava pools
                p = new AmbientParticle
                {
                    Pos = new Vector2(_rng.Next(0, worldW), _rng.Next(worldH / 2, worldH)),
                    Vel = new Vector2((float)(_rng.NextDouble() - 0.5) * 14f, -(float)_rng.NextDouble() * 38f - 12f),
                    Life = (float)(_rng.NextDouble() * 1.2 + 0.6f),
                    MaxLife = 1.8f,
                    Col = _rng.Next(3) == 0 ? new Color(255, 240, 80) : _rng.Next(2) == 0 ? new Color(255, 120, 0) : new Color(220, 50, 0),
                    Size = (float)(_rng.NextDouble() * 2.5f + 0.5f),
                };
                break;

            case TileMap.MapTheme.Ice:
                // Frost motes drifting downward
                p = new AmbientParticle
                {
                    Pos = new Vector2(_rng.Next(0, worldW), _rng.Next(-8, worldH / 2)),
                    Vel = new Vector2((float)(_rng.NextDouble() - 0.5) * 8f, (float)_rng.NextDouble() * 15f + 4f),
                    Life = (float)(_rng.NextDouble() * 2.0 + 1.0f),
                    MaxLife = 3.0f,
                    Col = new Color(190, 220, 255),
                    Size = (float)(_rng.NextDouble() * 2.0f + 0.5f),
                };
                break;

            case TileMap.MapTheme.Swamp:
                // Poisonous bubbles floating up
                p = new AmbientParticle
                {
                    Pos = new Vector2(_rng.Next(0, worldW), _rng.Next(worldH / 3, worldH)),
                    Vel = new Vector2((float)(_rng.NextDouble() - 0.5) * 6f, -(float)_rng.NextDouble() * 20f - 5f),
                    Life = (float)(_rng.NextDouble() * 1.5 + 0.8f),
                    MaxLife = 2.3f,
                    Col = _rng.Next(2) == 0 ? new Color(80, 200, 60) : new Color(100, 160, 40),
                    Size = (float)(_rng.NextDouble() * 3.0f + 0.8f),
                };
                break;

            case TileMap.MapTheme.Cave:
                // Cave sparks / dust
                p = new AmbientParticle
                {
                    Pos = new Vector2(_rng.Next(0, worldW), _rng.Next(0, worldH)),
                    Vel = new Vector2((float)(_rng.NextDouble() - 0.5) * 10f, (float)_rng.NextDouble() * 5f - 6f),
                    Life = (float)(_rng.NextDouble() * 1.8 + 0.5f),
                    MaxLife = 2.3f,
                    Col = _rng.Next(3) == 0 ? new Color(140, 130, 180) : new Color(80, 80, 120),
                    Size = (float)(_rng.NextDouble() * 1.5f + 0.5f),
                };
                break;

            case TileMap.MapTheme.Ruins:
                // Dust motes
                p = new AmbientParticle
                {
                    Pos = new Vector2(_rng.Next(0, worldW), _rng.Next(0, worldH)),
                    Vel = new Vector2((float)(_rng.NextDouble() - 0.5) * 6f, -(float)_rng.NextDouble() * 8f - 1f),
                    Life = (float)(_rng.NextDouble() * 2.5 + 1.0f),
                    MaxLife = 3.5f,
                    Col = new Color(180, 165, 120),
                    Size = (float)(_rng.NextDouble() * 1.8f + 0.5f),
                };
                break;

            default: // Forest
                // Firefly-like pollen
                p = new AmbientParticle
                {
                    Pos = new Vector2(_rng.Next(0, worldW), _rng.Next(0, worldH)),
                    Vel = new Vector2((float)(_rng.NextDouble() - 0.5) * 8f, -(float)_rng.NextDouble() * 12f - 2f),
                    Life = (float)(_rng.NextDouble() * 2.0 + 0.8f),
                    MaxLife = 2.8f,
                    Col = _rng.Next(2) == 0 ? new Color(200, 255, 140) : new Color(160, 230, 100),
                    Size = (float)(_rng.NextDouble() * 2.0f + 0.5f),
                };
                break;
        }
        _ambientParticles.Add(p);
    }

    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Registers a shockwave originating from a world-space point.
    /// Called by GameScene whenever a bomb or bat explodes.
    /// </summary>
    public void AddShockwave(Vector2 worldPos, Matrix worldTransform, bool big)
    {
        if (_shockwaves.Count >= MaxShockwaves) return;

        // Transform world position to screen UV
        Vector2 screen = Vector2.Transform(worldPos, worldTransform);
        int vw = _gd.Viewport.Width;
        int vh = _gd.Viewport.Height;
        Vector2 uv = new Vector2(screen.X / vw, screen.Y / vh);

        _shockwaves.Add(new ShockwaveEntry
        {
            OriginUV = uv,
            Radius = 0f,
            Life = 1f,
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Composites the captured world target onto the back buffer with all effects.
    /// Call after EndWorldCapture (SpriteBatch.End()) and before UI draws.
    ///
    /// Safe additive pipeline:
    ///   1. Base scene  : _worldTarget → back buffer  (color grade or plain)
    ///   2. Bloom       : bright-pass → blur → additive blit on top
    ///   3. Shockwaves  : additive ring overlay
    ///   4. Ambient     : additive particles
    ///
    /// Each step is independent: if a shader fails the base scene is still visible.
    /// </summary>
    public void ApplyPostProcess(Matrix worldTransform)
    {
        if (!_initialized) return;

        int tw = _worldTarget.Width;
        int th = _worldTarget.Height;
        int vw = _gd.Viewport.Width;
        int vh = _gd.Viewport.Height;

        // ── 1: Base scene → back buffer ────────────────────────────────────
        // Color grade is applied here; if shader unavailable, plain blit.
        _gd.SetRenderTarget(null);

        if (_colorGradeFx != null)
        {
            var g = BiomeGrade[_theme];
            _colorGradeFx.Parameters["Tint"].SetValue(g.tint);
            _colorGradeFx.Parameters["TintStrength"].SetValue(g.tintStr);
            _colorGradeFx.Parameters["Contrast"].SetValue(g.contrast);
            _colorGradeFx.Parameters["Saturation"].SetValue(g.sat);
            _colorGradeFx.Parameters["Brightness"].SetValue(g.bright);
            _colorGradeFx.Parameters["VignetteRadius"].SetValue(g.vignR);
            _colorGradeFx.Parameters["VignetteStrength"].SetValue(g.vignS);
            _colorGradeFx.Parameters["TexelSize"].SetValue(new Vector2(1f / tw, 1f / th));
            _colorGradeFx.CurrentTechnique = _colorGradeFx.Techniques["ColorGrade"];
            _sb.Begin(effect: _colorGradeFx, samplerState: SamplerState.LinearClamp,
                      transformMatrix: worldTransform);
        }
        else
        {
            _sb.Begin(samplerState: SamplerState.PointClamp, transformMatrix: worldTransform);
        }
        _sb.Draw(_worldTarget, new Rectangle(0, 0, tw, th), Color.White);
        _sb.End();

        // ── 2: Bloom (additive on top of base scene) ───────────────────────
        if (_bloomFx != null && !_lowQuality)
        {
            var bloom = BiomeBloom[_theme];
            float threshold = bloom.threshold;

            // Bright-pass: _worldTarget → _halfTarget
            _gd.SetRenderTarget(_halfTarget);
            _gd.Clear(Color.Transparent);
            _bloomFx.CurrentTechnique = _bloomFx.Techniques["BrightPass"];
            _bloomFx.Parameters["TexelSize"].SetValue(new Vector2(1f / tw, 1f / th));
            _bloomFx.Parameters["Threshold"].SetValue(threshold);
            _sb.Begin(effect: _bloomFx, samplerState: SamplerState.LinearClamp);
            _sb.Draw(_worldTarget, new Rectangle(0, 0, _halfTarget.Width, _halfTarget.Height), Color.White);
            _sb.End();

            // Blur H: _halfTarget → _blurTarget
            _gd.SetRenderTarget(_blurTarget);
            _gd.Clear(Color.Transparent);
            _bloomFx.CurrentTechnique = _bloomFx.Techniques["BlurH"];
            _bloomFx.Parameters["TexelSize"].SetValue(new Vector2(1f / _halfTarget.Width, 1f / _halfTarget.Height));
            _sb.Begin(effect: _bloomFx, samplerState: SamplerState.LinearClamp);
            _sb.Draw(_halfTarget, new Rectangle(0, 0, _blurTarget.Width, _blurTarget.Height), Color.White);
            _sb.End();

            // Blur V: _blurTarget → _halfTarget
            _gd.SetRenderTarget(_halfTarget);
            _gd.Clear(Color.Transparent);
            _bloomFx.CurrentTechnique = _bloomFx.Techniques["BlurV"];
            _bloomFx.Parameters["TexelSize"].SetValue(new Vector2(1f / _blurTarget.Width, 1f / _blurTarget.Height));
            _sb.Begin(effect: _bloomFx, samplerState: SamplerState.LinearClamp);
            _sb.Draw(_blurTarget, new Rectangle(0, 0, _halfTarget.Width, _halfTarget.Height), Color.White);
            _sb.End();

            // Add blurred bloom on top of back buffer (additive)
            _gd.SetRenderTarget(null);
            _sb.Begin(samplerState: SamplerState.LinearClamp,
                      blendState: BlendState.Additive,
                      transformMatrix: worldTransform);
            _sb.Draw(_halfTarget, new Rectangle(0, 0, tw, th),
                     Color.White * bloom.intensity * 0.6f);
            _sb.End();
        }

        // ── 3: Shockwave overlay (additive flash ring) ─────────────────────
        if (_shockwaveFx != null && _shockwaves.Count > 0 && !_lowQuality)
        {
            foreach (var sw in _shockwaves)
            {
                _shockwaveFx.Parameters["WaveOrigin"].SetValue(sw.OriginUV);
                _shockwaveFx.Parameters["WaveRadius"].SetValue(sw.Radius);
                _shockwaveFx.Parameters["WaveThickness"].SetValue(ShockwaveThickness);
                _shockwaveFx.Parameters["WaveStrength"].SetValue(ShockwaveStrength);
                _shockwaveFx.Parameters["WaveLife"].SetValue(sw.Life);
                _shockwaveFx.Parameters["TexelSize"].SetValue(new Vector2(1f / vw, 1f / vh));
                _sb.Begin(effect: _shockwaveFx, samplerState: SamplerState.LinearClamp,
                          blendState: BlendState.Additive);
                _sb.Draw(_pixel, new Rectangle(0, 0, vw, vh), Color.White * sw.Life * 0.15f);
                _sb.End();
            }
        }

        // ── 4: Ambient particles (world-space → screen) ────────────────────
        DrawAmbientParticles(worldTransform);
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void DrawAmbientParticles(Matrix worldTransform)
    {
        if (_ambientParticles.Count == 0) return;
        _sb.Begin(samplerState: SamplerState.PointClamp,
                  blendState: BlendState.Additive,
                  transformMatrix: worldTransform);
        foreach (var p in _ambientParticles)
        {
            float alpha = MathF.Sin(p.Life / p.MaxLife * MathF.PI) * 0.65f;
            Color c = p.Col * alpha;
            int s = Math.Max(1, (int)(p.Size + 0.5f));
            _sb.Draw(_pixel,
                new Rectangle((int)(p.Pos.X - s / 2f), (int)(p.Pos.Y - s / 2f), s, s), c);
        }
        _sb.End();
    }

    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Draws additive glow halos for point-light sources (bombs, upgrades, etc.).</summary>
    public void DrawGlowOverlay(SpriteBatch sb, IEnumerable<(Vector2 worldPos, Color col, float radius)> lights,
                                Matrix worldTransform)
    {
        bool any = false;
        foreach (var _ in lights) { any = true; break; }
        if (!any) return;

        sb.Begin(samplerState: SamplerState.LinearClamp,
                 blendState: BlendState.Additive,
                 transformMatrix: worldTransform);
        foreach (var (pos, col, radius) in lights)
        {
            // Draw a soft square glow (radial gradient approximated with multiple layers)
            for (int layer = 3; layer >= 1; layer--)
            {
                float r = radius * layer / 3f;
                float a = 0.07f / layer;
                int size = (int)(r * 2);
                sb.Draw(_pixel,
                    new Rectangle((int)(pos.X - r), (int)(pos.Y - r), size, size),
                    col * a);
            }
        }
        sb.End();
    }

    // ─────────────────────────────────────────────────────────────────────────
    public void Dispose()
    {
        _worldTarget?.Dispose();
        _distortTarget?.Dispose();
        _halfTarget?.Dispose();
        _blurTarget?.Dispose();
        _bloomFx?.Dispose();
        _colorGradeFx?.Dispose();
        _heatDistortFx?.Dispose();
        _shockwaveFx?.Dispose();
        _pixel?.Dispose();
        _sb?.Dispose();
    }
}
