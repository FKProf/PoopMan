using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PoopManLibrary.World;
using System;
using System.Collections.Generic;

namespace PoopMan.Scenes;

/// <summary>
///     Lightweight overlay-only VFX system for GameScene.
///     Draw pipeline (per frame):
///     1. Caller calls BeginWorldCapture() — just clears the back buffer, no render target.
///     2. Caller draws the world normally into the back buffer.
///     3. ApplyPostProcess() draws overlays on top:
///     a. Soft vignette
///     b. Ambient particles
///     c. Light flashes (explosion halos)
///     d. Shockwave rings (CPU-drawn)
///     4. Caller draws HUD + UI on top.
/// </summary>
internal sealed class VisualEffectSystem : IDisposable
{
    // ── Explosion variants ────────────────────────────────────────────────────
    public enum ExplosionType
    {
        Normal,
        Big,
        Walid,
        Nuke
    }

    private const float ShockwaveSpeed = 0.70f;
    private const int MaxShockwaves = 6;
    private const int MaxFlashes = 8;
    private const float AmbientInterval = 0.07f;
    private const int MaxAmbientParticles = 80;

    // ── Vignette strength per biome ───────────────────────────────────────────
    private static readonly Dictionary<TileMap.MapTheme, (Color col, float strength)>
        BiomeVignette = new()
        {
            [TileMap.MapTheme.Forest] = (Color.Black, 0.28f),
            [TileMap.MapTheme.Cave] = (new Color(20, 20, 50), 0.40f),
            [TileMap.MapTheme.Lava] = (new Color(60, 10, 0), 0.35f),
            [TileMap.MapTheme.Ice] = (new Color(30, 50, 80), 0.25f),
            [TileMap.MapTheme.Swamp] = (new Color(10, 30, 10), 0.32f),
            [TileMap.MapTheme.Ruins] = (new Color(30, 25, 10), 0.30f)
        };

    // ── Bloom tuning per biome (kept for future use, not applied to world pass) ─
    private static readonly Dictionary<TileMap.MapTheme, (float threshold, float intensity, float saturation)>
        BiomeBloom = new()
        {
            [TileMap.MapTheme.Forest] = (0.72f, 0.50f, 1.15f),
            [TileMap.MapTheme.Cave] = (0.68f, 0.65f, 1.05f),
            [TileMap.MapTheme.Lava] = (0.58f, 0.90f, 1.40f),
            [TileMap.MapTheme.Ice] = (0.65f, 0.60f, 0.95f),
            [TileMap.MapTheme.Swamp] = (0.74f, 0.45f, 1.05f),
            [TileMap.MapTheme.Ruins] = (0.70f, 0.55f, 1.05f)
        };

    // ── Flash colour per explosion type ──────────────────────────────────────
    private static readonly Dictionary<ExplosionType, (Color col, float radiusUV)> ExplosionFlash = new()
    {
        [ExplosionType.Normal] = (new Color(255, 220, 140, 160), 0.08f),
        [ExplosionType.Big] = (new Color(255, 200, 80, 180), 0.14f),
        [ExplosionType.Walid] = (new Color(255, 80, 20, 190), 0.18f),
        [ExplosionType.Nuke] = (new Color(80, 255, 50, 200), 0.30f)
    };

    // ─────────────────────────────────────────────────────────────────────────
    private static readonly Random _rng = new();
    private readonly List<AmbientParticle> _ambientParticles = new();
    private readonly List<FlashEntry> _flashes = new();

    // ── Graphics ──────────────────────────────────────────────────────────────
    private readonly GraphicsDevice _gd;
    private readonly List<ShockwaveEntry> _shockwaves = new();
    private float _ambientTimer;
    private bool _initialized;
    private int _mapH;
    private int _mapW;
    private Texture2D _pixel;
    private SpriteBatch _sb;

    // ── State ─────────────────────────────────────────────────────────────────
    private TileMap.MapTheme _theme;
    private float _time;

    // ─────────────────────────────────────────────────────────────────────────
    public VisualEffectSystem(GraphicsDevice gd)
    {
        _gd = gd;
    }

    // ─────────────────────────────────────────────────────────────────────────
    public void Dispose()
    {
        _pixel?.Dispose();
        _sb?.Dispose();
    }

    // ─────────────────────────────────────────────────────────────────────────
    public void LoadContent(ContentManager content, int mapW, int mapH)
    {
        _mapW = mapW;
        _mapH = mapH;
        _sb = new SpriteBatch(_gd);
        _pixel = new Texture2D(_gd, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _initialized = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    ///     Clears the back buffer with the map background colour.
    ///     The world is drawn directly into the back buffer — no render target capture.
    /// </summary>
    public void BeginWorldCapture(Color clearColor)
    {
        if (!_initialized) return;
        _gd.SetRenderTarget(null); // back buffer
        _gd.Clear(clearColor);
    }

    // ─────────────────────────────────────────────────────────────────────────
    public void Update(GameTime gameTime, TileMap.MapTheme theme, int worldW, int worldH,
        Func<IEnumerable<(Vector2 worldPos, Color col, float radius)>> lightSources)
    {
        if (!_initialized) return;
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _time += dt;
        _theme = theme;

        // ── Shockwaves ────────────────────────────────────────────────────────
        for (var i = _shockwaves.Count - 1; i >= 0; i--)
        {
            var s = _shockwaves[i];
            s.Radius += ShockwaveSpeed * dt;
            s.Life -= dt * 2.4f;
            if (s.Life <= 0f)
            {
                _shockwaves.RemoveAt(i);
                continue;
            }

            _shockwaves[i] = s;
        }

        // ── Light flashes ─────────────────────────────────────────────────────
        for (var i = _flashes.Count - 1; i >= 0; i--)
        {
            var f = _flashes[i];
            f.Life -= dt * 3.0f;
            if (f.Life <= 0f)
            {
                _flashes.RemoveAt(i);
                continue;
            }

            _flashes[i] = f;
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

        for (var i = _ambientParticles.Count - 1; i >= 0; i--)
        {
            var p = _ambientParticles[i];
            p.Pos += p.Vel * dt;
            p.Life -= dt;
            if (p.Life <= 0)
            {
                _ambientParticles.RemoveAt(i);
                continue;
            }

            _ambientParticles[i] = p;
        }
    }

    private void SpawnAmbientParticle(TileMap.MapTheme theme, int worldW, int worldH)
    {
        AmbientParticle p;
        switch (theme)
        {
            case TileMap.MapTheme.Lava:
                p = new AmbientParticle
                {
                    Pos = new Vector2(_rng.Next(0, worldW), _rng.Next(worldH / 2, worldH)),
                    Vel = new Vector2((float)(_rng.NextDouble() - 0.5) * 14f, -(float)_rng.NextDouble() * 42f - 14f),
                    Life = (float)(_rng.NextDouble() * 1.0 + 0.5f),
                    MaxLife = 1.5f,
                    Col = _rng.Next(3) == 0 ? new Color(255, 240, 80)
                        : _rng.Next(2) == 0 ? new Color(255, 120, 0)
                        : new Color(220, 50, 0),
                    Size = (float)(_rng.NextDouble() * 2.5f + 0.5f)
                };
                break;

            case TileMap.MapTheme.Ice:
                p = new AmbientParticle
                {
                    Pos = new Vector2(_rng.Next(0, worldW), _rng.Next(-8, worldH / 2)),
                    Vel = new Vector2((float)(_rng.NextDouble() - 0.5) * 8f, (float)_rng.NextDouble() * 14f + 4f),
                    Life = (float)(_rng.NextDouble() * 2.0 + 1.0f),
                    MaxLife = 3.0f,
                    Col = new Color(200, 230, 255),
                    Size = (float)(_rng.NextDouble() * 1.8f + 0.4f)
                };
                break;

            case TileMap.MapTheme.Swamp:
                p = new AmbientParticle
                {
                    Pos = new Vector2(_rng.Next(0, worldW), _rng.Next(worldH / 3, worldH)),
                    Vel = new Vector2((float)(_rng.NextDouble() - 0.5) * 5f, -(float)_rng.NextDouble() * 18f - 4f),
                    Life = (float)(_rng.NextDouble() * 1.4 + 0.7f),
                    MaxLife = 2.1f,
                    Col = _rng.Next(2) == 0 ? new Color(80, 200, 60) : new Color(100, 160, 40),
                    Size = (float)(_rng.NextDouble() * 2.5f + 0.8f)
                };
                break;

            case TileMap.MapTheme.Cave:
                p = new AmbientParticle
                {
                    Pos = new Vector2(_rng.Next(0, worldW), _rng.Next(0, worldH)),
                    Vel = new Vector2((float)(_rng.NextDouble() - 0.5) * 8f, (float)_rng.NextDouble() * 4f - 5f),
                    Life = (float)(_rng.NextDouble() * 1.6 + 0.4f),
                    MaxLife = 2.0f,
                    Col = _rng.Next(3) == 0 ? new Color(150, 140, 190) : new Color(85, 85, 130),
                    Size = (float)(_rng.NextDouble() * 1.5f + 0.4f)
                };
                break;

            case TileMap.MapTheme.Ruins:
                p = new AmbientParticle
                {
                    Pos = new Vector2(_rng.Next(0, worldW), _rng.Next(0, worldH)),
                    Vel = new Vector2((float)(_rng.NextDouble() - 0.5) * 5f, -(float)_rng.NextDouble() * 7f - 1f),
                    Life = (float)(_rng.NextDouble() * 2.2 + 0.8f),
                    MaxLife = 3.0f,
                    Col = new Color(185, 170, 125),
                    Size = (float)(_rng.NextDouble() * 1.6f + 0.4f)
                };
                break;

            default: // Forest
                p = new AmbientParticle
                {
                    Pos = new Vector2(_rng.Next(0, worldW), _rng.Next(0, worldH)),
                    Vel = new Vector2((float)(_rng.NextDouble() - 0.5) * 8f, -(float)_rng.NextDouble() * 12f - 2f),
                    Life = (float)(_rng.NextDouble() * 1.8 + 0.6f),
                    MaxLife = 2.4f,
                    Col = _rng.Next(2) == 0 ? new Color(205, 255, 145) : new Color(165, 235, 105),
                    Size = (float)(_rng.NextDouble() * 1.8f + 0.4f)
                };
                break;
        }

        _ambientParticles.Add(p);
    }

    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    ///     Registers a shockwave + flash for an explosion.
    ///     Call this from GameScene whenever a bomb or bat explodes.
    /// </summary>
    public void AddExplosionEffect(Vector2 worldPos, Matrix worldTransform, ExplosionType type)
    {
        var vw = _gd.Viewport.Width;
        var vh = _gd.Viewport.Height;
        var screen = Vector2.Transform(worldPos, worldTransform);
        Vector2 uv = new(screen.X / vw, screen.Y / vh);

        // Shockwave ring (CPU-drawn circles later)
        if (_shockwaves.Count < MaxShockwaves)
        {
            var tint = type switch
            {
                ExplosionType.Big => new Color(255, 200, 80),
                ExplosionType.Walid => new Color(255, 100, 30),
                ExplosionType.Nuke => new Color(80, 255, 60),
                _ => Color.White
            };
            _shockwaves.Add(new ShockwaveEntry { OriginUV = uv, Radius = 0f, Life = 1f, Tint = tint });
        }

        // Flash
        if (_flashes.Count < MaxFlashes)
        {
            var (col, rad) = ExplosionFlash[type];
            _flashes.Add(new FlashEntry { OriginUV = uv, Life = 1f, Col = col, RadiusUV = rad });
        }
    }

    // Legacy overload kept for compatibility
    public void AddShockwave(Vector2 worldPos, Matrix worldTransform, bool big)
    {
        AddExplosionEffect(worldPos, worldTransform, big ? ExplosionType.Big : ExplosionType.Normal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    ///     Draws overlay effects on top of the already-drawn world (back buffer).
    ///     No render-target capture, no full-screen shaders.
    /// </summary>
    public void ApplyPostProcess(Matrix worldTransform)
    {
        if (!_initialized) return;

        var vw = _gd.Viewport.Width;
        var vh = _gd.Viewport.Height;

        // ── 1: Soft vignette ─────────────────────────────────────────────
        DrawVignette(vw, vh);

        // ── 2: Light flashes (explosion halos) ────────────────────────────
        DrawFlashes(vw, vh);

        // ── 3: Shockwave rings (CPU-drawn expanding circles) ──────────────
        DrawShockwaveRings(vw, vh);

        // ── 4: Ambient particles ──────────────────────────────────────────
        DrawAmbientParticles(worldTransform);
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void DrawVignette(int vw, int vh)
    {
        if (!BiomeVignette.TryGetValue(_theme, out var v)) return;
        if (v.strength <= 0f) return;

        var steps = 12;
        var edgeW = (int)(Math.Min(vw, vh) * 0.16f);

        _sb.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
        for (var i = 0; i < steps; i++)
        {
            var t = 1f - (float)i / steps;
            var a = t * t * v.strength * 0.7f;
            var col = v.col * a;
            var band = edgeW * i / steps;
            // top
            _sb.Draw(_pixel, new Rectangle(0, band, vw, edgeW / steps + 1), col);
            // bottom
            _sb.Draw(_pixel, new Rectangle(0, vh - band - edgeW / steps - 1, vw, edgeW / steps + 1), col);
            // left
            _sb.Draw(_pixel, new Rectangle(band, 0, edgeW / steps + 1, vh), col);
            // right
            _sb.Draw(_pixel, new Rectangle(vw - band - edgeW / steps - 1, 0, edgeW / steps + 1, vh), col);
        }

        _sb.End();
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void DrawFlashes(int vw, int vh)
    {
        if (_flashes.Count == 0) return;
        _sb.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.Additive);
        foreach (var f in _flashes)
        {
            var alpha = f.Life * f.Life * 0.50f;
            var col = f.Col * alpha;
            var radius = f.RadiusUV * MathF.Sqrt(vw * vw + vh * vh) * 0.5f;
            for (var layer = 4; layer >= 1; layer--)
            {
                var r = radius * layer / 4f;
                var a = 0.10f / layer;
                var size = (int)(r * 2);
                var x = (int)(f.OriginUV.X * vw - r);
                var y = (int)(f.OriginUV.Y * vh - r);
                _sb.Draw(_pixel, new Rectangle(x, y, size, size), col * a);
            }
        }

        _sb.End();
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void DrawShockwaveRings(int vw, int vh)
    {
        if (_shockwaves.Count == 0) return;
        // Draw thin expanding rings as per-pixel lines (CPU approximation)
        _sb.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.Additive);
        foreach (var sw in _shockwaves)
        {
            var alpha = sw.Life * sw.Life * 0.35f;
            var col = sw.Tint * alpha;
            var radius = sw.Radius * Math.Min(vw, vh) * 0.55f;
            var cx = (int)(sw.OriginUV.X * vw);
            var cy = (int)(sw.OriginUV.Y * vh);
            var r = (int)radius;
            var thickness = Math.Max(2, (int)(sw.Life * 6));
            for (var t = 0; t < thickness; t++)
            {
                float fr = r + t - thickness / 2;
                if (fr <= 0) continue;
                // Approximate circle with 64 points
                for (var seg = 0; seg < 64; seg++)
                {
                    var angle = seg * MathF.PI * 2 / 64f;
                    var px = cx + (int)(MathF.Cos(angle) * fr);
                    var py = cy + (int)(MathF.Sin(angle) * fr);
                    if (px >= 0 && px < vw && py >= 0 && py < vh)
                        _sb.Draw(_pixel, new Rectangle(px, py, 1, 1), col);
                }
            }
        }

        _sb.End();
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
            var alpha = MathF.Sin(p.Life / p.MaxLife * MathF.PI) * 0.55f;
            var c = p.Col * alpha;
            var s = Math.Max(1, (int)(p.Size + 0.5f));
            _sb.Draw(_pixel, new Rectangle((int)(p.Pos.X - s / 2f), (int)(p.Pos.Y - s / 2f), s, s), c);
        }

        _sb.End();
    }

    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Draws additive glow halos for point-light sources.</summary>
    public void DrawGlowOverlay(SpriteBatch sb, IEnumerable<(Vector2 worldPos, Color col, float radius)> lights,
        Matrix worldTransform)
    {
        var any = false;
        foreach (var _ in lights)
        {
            any = true;
            break;
        }

        if (!any) return;

        sb.Begin(samplerState: SamplerState.LinearClamp,
            blendState: BlendState.Additive,
            transformMatrix: worldTransform);
        foreach (var (pos, col, radius) in lights)
            for (var layer = 3; layer >= 1; layer--)
            {
                var r = radius * layer / 3f;
                var a = 0.06f / layer;
                var size = (int)(r * 2);
                sb.Draw(_pixel, new Rectangle((int)(pos.X - r), (int)(pos.Y - r), size, size), col * a);
            }

        sb.End();
    }

    // ── Shockwaves (CPU-drawn rings) ──────────────────────────────────────────
    private struct ShockwaveEntry
    {
        public Vector2 OriginUV;
        public float Radius; // 0..1 in UV space
        public float Life; // 1..0
        public Color Tint;
    }

    // ── Light flashes ─────────────────────────────────────────────────────────
    private struct FlashEntry
    {
        public Vector2 OriginUV;
        public float Life;
        public Color Col;
        public float RadiusUV;
    }

    // ── Ambient particles ─────────────────────────────────────────────────────
    private struct AmbientParticle
    {
        public Vector2 Pos;
        public Vector2 Vel;
        public float Life;
        public float MaxLife;
        public Color Col;
        public float Size;
    }
}