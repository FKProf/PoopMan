using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PoopManLibrary.World;
using System;
using System.Collections.Generic;

namespace PoopMan.Scenes;

/// <summary>
///     Atmosfera del bioma: strati di luce, nebbia e particelle specifici per ogni tema.
///     <list type="bullet">
///         <item>Foresta: raggi di sole, foglie che cadono, lucciole, polline, foschia leggera.</item>
///         <item>Caverna: gocce che cadono dai blocchi e schizzano, cristalli che brillano, polvere, nebbia fredda.</item>
///         <item>Lava: braci che salgono, cenere che cade, bolle sulla lava, bagliore di calore dal basso, fumo.</item>
///         <item>Ghiaccio: neve a più profondità con raffiche di vento, aurora boreale, scintillii, bruma.</item>
///         <item>Palude: banchi di nebbia, fuochi fatui, lucciole verdi, bolle sull'acqua, spore.</item>
///         <item>Rovine: sabbia trasportata dal vento, polvere dorata, granelli che cadono, raggi caldi.</item>
///     </list>
///     Tutto in coordinate mondo, disegnato sopra l'illuminazione.
/// </summary>
internal sealed class BiomeAtmosphere : IDisposable
{
    private const int MaxParticles = 340;
    private const int SpawnSlots = 4;

    private readonly List<FogBank> _fog = new();
    private readonly int _h;
    private readonly List<(Vector2 pos, bool lava)> _liquids = new();
    private readonly List<P> _particles = new();
    private readonly Texture2D _pixel;
    private readonly Texture2D _ray;
    private readonly Random _rng = new();
    private readonly Texture2D _soft;
    private readonly float[] _spawnAcc = new float[SpawnSlots];
    private readonly List<Vector2> _dripSources = new(); // bordo inferiore dei blocchi sopra il pavimento
    private readonly int _w;
    private float _gust;
    private float _gustDir = 1f;
    private float _gustLeft;
    private float _gustTimer = 4f;

    private TileMap _map;
    private TileMap.MapTheme _theme;
    private float _time;
    private float _wind;

    public BiomeAtmosphere(GraphicsDevice gd, int worldW, int worldH)
    {
        _w = worldW;
        _h = worldH;
        _pixel = new Texture2D(gd, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _soft = FxTextures.CreateRadial(gd, 32, 1.5f);
        _ray = CreateRay(gd, 32, 128);
    }

    public void Dispose()
    {
        _pixel.Dispose();
        _soft.Dispose();
        _ray.Dispose();
    }

    private float R(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

    // ═══════════════════════════════════════════════════════════════════
    // UPDATE
    // ═══════════════════════════════════════════════════════════════════

    public void Update(float dt, TileMap map)
    {
        if (!ReferenceEquals(map, _map))
        {
            SetMap(map);
            // Pre-riscaldamento: il livello parte già con l'atmosfera "piena"
            for (var i = 0; i < 80; i++) Step(0.1f);
        }

        Step(dt);
    }

    private void SetMap(TileMap map)
    {
        _map = map;
        _theme = map.Theme;
        _particles.Clear();
        Array.Clear(_spawnAcc);

        _liquids.Clear();
        _dripSources.Clear();
        for (var y = 0; y < TileMap.Rows; y++)
            for (var x = 0; x < TileMap.Cols; x++)
            {
                var t = new Point(x, y);
                var center = new Vector2(x * TileMap.TileSize + 16f, y * TileMap.TileSize + 16f);
                if (map.IsLiquid(t)) _liquids.Add((center, map.IsLava(t)));
                if (map.IsRaised(t) && map.IsWalkable(new Point(x, y + 1)))
                    _dripSources.Add(new Vector2(x * TileMap.TileSize, (y + 1) * TileMap.TileSize - 2f));
            }

        InitFog();
    }

    private void Step(float dt)
    {
        _time += dt;
        UpdateWind(dt);
        SpawnForTheme(dt);

        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life -= dt;
            if (p.Life <= 0f)
            {
                OnExpire(p);
                _particles.RemoveAt(i);
                continue;
            }

            Integrate(ref p, dt);
            if (p.Kind == Kind.Drip && p.Pos.Y >= p.FloorY)
            {
                Splash(p.Pos, p.Col);
                _particles.RemoveAt(i);
                continue;
            }

            _particles[i] = p;
        }

        for (var i = 0; i < _fog.Count; i++)
        {
            var f = _fog[i];
            f.Pos.X += (f.Speed + _wind * 18f) * dt;
            if (f.Pos.X - f.Size > _w) f.Pos.X = -f.Size;
            if (f.Pos.X + f.Size < 0) f.Pos.X = _w + f.Size;
            _fog[i] = f;
        }
    }

    /// <summary>Vento di fondo + raffiche periodiche (neve e sabbia ne sono trascinate).</summary>
    private void UpdateWind(float dt)
    {
        _gustTimer -= dt;
        if (_gustTimer <= 0f)
        {
            _gustTimer = R(6f, 12f);
            _gustLeft = R(1.6f, 2.8f);
            _gustDir = _rng.Next(2) == 0 ? -1f : 1f;
        }

        _gustLeft -= dt;
        var target = _gustLeft > 0f ? _gustDir * 1.3f : 0f;
        _gust = MathHelper.Lerp(_gust, target, 1f - MathF.Exp(-dt * 1.4f));
        _wind = MathF.Sin(_time * 0.35f) * 0.3f + _gust;
    }

    private void Integrate(ref P p, float dt)
    {
        switch (p.Kind)
        {
            case Kind.Glow:
                p.Pos += p.Vel * dt;
                p.Pos.X += (MathF.Sin(_time * 1.5f + p.Phase) * 6f + _wind * 10f) * dt;
                break;

            case Kind.Firefly:
            case Kind.Wisp:
            {
                // Vagano lentamente cambiando direzione in modo morbido
                var a = p.Phase + MathF.Sin(_time * 0.6f + p.Phase * 3f) * 2.2f;
                var speed = p.Kind == Kind.Wisp ? 9f : 14f;
                p.Pos += new Vector2(MathF.Cos(a), MathF.Sin(a)) * speed * dt;
                break;
            }

            case Kind.Leaf:
                p.Pos.Y += p.Vel.Y * dt;
                p.Pos.X += (MathF.Sin(_time * 2f + p.Phase) * 26f + _wind * 30f) * dt;
                p.Rot += p.RotSpeed * dt;
                break;

            case Kind.Snow:
            case Kind.Ash:
                p.Pos.Y += p.Vel.Y * dt;
                p.Pos.X += (MathF.Sin(_time * 1.2f + p.Phase) * 10f + _wind * 55f) * p.Depth * dt;
                p.Rot += p.RotSpeed * dt;
                break;

            case Kind.Drip:
            case Kind.Splash:
                p.Vel.Y += (p.Kind == Kind.Drip ? 520f : 320f) * dt;
                p.Pos += p.Vel * dt;
                break;

            case Kind.Bubble:
                p.Pos.Y -= 3f * dt;
                break;

            case Kind.Streak:
                p.Pos += p.Vel * dt;
                p.Pos.Y += MathF.Sin(_time * 3f + p.Phase) * 8f * dt;
                break;
        }
    }

    private void OnExpire(P p)
    {
        // Le bolle scoppiano in qualche goccia luminosa
        if (p.Kind != Kind.Bubble) return;
        for (var i = 0; i < 4; i++)
        {
            var a = R(MathF.PI * 1.1f, MathF.PI * 1.9f);
            Add(new P
            {
                Kind = Kind.Splash, Pos = p.Pos, Vel = new Vector2(MathF.Cos(a), MathF.Sin(a)) * R(25f, 55f),
                Life = R(0.25f, 0.4f), Col = p.Col, Size = 1.5f, Additive = p.Additive
            });
        }
    }

    private void Splash(Vector2 pos, Color col)
    {
        for (var i = 0; i < 3; i++)
            Add(new P
            {
                Kind = Kind.Splash, Pos = pos, Vel = new Vector2(R(-35f, 35f), R(-70f, -35f)),
                Life = R(0.2f, 0.35f), Col = col, Size = 1.4f
            });
    }

    private void Add(P p)
    {
        if (_particles.Count >= MaxParticles) return;
        p.MaxLife = p.Life;
        if (p.Phase == 0f) p.Phase = R(0f, MathF.Tau);
        if (p.Depth == 0f) p.Depth = 1f;
        _particles.Add(p);
    }

    /// <summary>Genera <paramref name="perSecond" /> particelle al secondo nello slot indicato.</summary>
    private void Rate(int slot, float perSecond, float dt, Action spawn)
    {
        _spawnAcc[slot] += perSecond * dt;
        while (_spawnAcc[slot] >= 1f)
        {
            _spawnAcc[slot] -= 1f;
            spawn();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // SPAWN PER BIOMA
    // ═══════════════════════════════════════════════════════════════════

    private void SpawnForTheme(float dt)
    {
        switch (_theme)
        {
            case TileMap.MapTheme.Forest:
                Rate(0, 2.2f, dt, SpawnLeaf);
                Rate(1, 1.4f, dt, () => SpawnFirefly(new Color(230, 255, 120)));
                Rate(2, 6f, dt, () => SpawnMote(_rng.Next(2) == 0 ? new Color(220, 255, 160) : new Color(255, 245, 190)));
                break;

            case TileMap.MapTheme.Cave:
                Rate(0, 1.6f, dt, () => SpawnDrip(new Color(150, 190, 255)));
                Rate(1, 4f, dt, () => SpawnTwinkle(_rng.Next(2) == 0 ? new Color(140, 170, 255) : new Color(200, 140, 255)));
                Rate(2, 5f, dt, () => SpawnMote(new Color(150, 145, 200)));
                break;

            case TileMap.MapTheme.Lava:
                Rate(0, 16f, dt, SpawnEmber);
                Rate(1, 5f, dt, () => SpawnFlake(Kind.Ash, new Color(70, 60, 60), 8f, 18f));
                Rate(2, _liquids.Count > 0 ? 3f : 0f, dt, () => SpawnBubble(true));
                break;

            case TileMap.MapTheme.Ice:
                Rate(0, 24f, dt, () => SpawnFlake(Kind.Snow, new Color(235, 245, 255), 14f, 42f));
                Rate(1, 3f, dt, () => SpawnTwinkle(_rng.Next(2) == 0 ? Color.White : new Color(170, 230, 255)));
                break;

            case TileMap.MapTheme.Swamp:
                Rate(0, 0.7f, dt, SpawnWisp);
                Rate(1, 1.2f, dt, () => SpawnFirefly(new Color(140, 255, 110)));
                Rate(2, _liquids.Count > 0 ? 3.5f : 0f, dt, () => SpawnBubble(false));
                Rate(3, 4f, dt, () => SpawnMote(new Color(150, 200, 90)));
                break;

            case TileMap.MapTheme.Ruins:
                Rate(0, 7f + MathF.Abs(_wind) * 14f, dt, SpawnSandStreak);
                Rate(1, 5f, dt, () => SpawnMote(new Color(255, 215, 130)));
                Rate(2, 0.9f, dt, () => SpawnDrip(new Color(200, 175, 120)));
                break;
        }
    }

    private void SpawnMote(Color col)
    {
        Add(new P
        {
            Kind = Kind.Glow, Pos = new Vector2(R(0, _w), R(0, _h)),
            Vel = new Vector2(R(-4f, 4f), R(-10f, -2f)), Life = R(1.6f, 3.2f), Col = col,
            Size = R(1f, 2.4f), Additive = true
        });
    }

    private void SpawnEmber()
    {
        // Metà delle braci nasce dalla lava, il resto dal fondo della mappa
        var fromLava = _liquids.Count > 0 && _rng.Next(2) == 0;
        var pos = fromLava
            ? _liquids[_rng.Next(_liquids.Count)].pos + new Vector2(R(-12f, 12f), R(-8f, 8f))
            : new Vector2(R(0, _w), R(_h * 0.5f, _h));
        var hot = _rng.Next(3);
        Add(new P
        {
            Kind = Kind.Glow, Pos = pos, Vel = new Vector2(R(-8f, 8f), R(-55f, -18f)), Life = R(0.9f, 2.2f),
            Col = hot == 0 ? new Color(255, 240, 120) : hot == 1 ? new Color(255, 140, 30) : new Color(230, 60, 10),
            Size = R(1.2f, 3f), Additive = true
        });
    }

    private void SpawnFlake(Kind kind, Color col, float minSpeed, float maxSpeed)
    {
        // Profondità: fiocchi vicini più grandi, veloci e luminosi
        var depth = R(0.35f, 1f);
        var vy = MathHelper.Lerp(minSpeed, maxSpeed, depth);
        Add(new P
        {
            Kind = kind, Pos = new Vector2(R(-40, _w + 40), -6f), Vel = new Vector2(0f, vy),
            Life = (_h + 12f) / vy, Col = col, Size = MathHelper.Lerp(1f, 3.6f, depth * depth), Depth = depth,
            RotSpeed = R(-2f, 2f)
        });
    }

    private void SpawnLeaf()
    {
        var palette = new[] { new Color(120, 170, 60), new Color(170, 190, 70), new Color(210, 150, 50), new Color(90, 140, 50) };
        var vy = R(16f, 30f);
        Add(new P
        {
            Kind = Kind.Leaf, Pos = new Vector2(R(-30, _w + 30), -6f), Vel = new Vector2(0f, vy),
            Life = (_h + 12f) / vy, Col = palette[_rng.Next(palette.Length)], Size = R(2.5f, 4f),
            Rot = R(0, MathF.Tau), RotSpeed = R(-3f, 3f)
        });
    }

    private void SpawnFirefly(Color col)
    {
        Add(new P
        {
            Kind = Kind.Firefly, Pos = new Vector2(R(0, _w), R(0, _h)), Life = R(4f, 8f), Col = col,
            Size = 1f, Additive = true
        });
    }

    private void SpawnWisp()
    {
        Add(new P
        {
            Kind = Kind.Wisp, Pos = new Vector2(R(0, _w), R(_h * 0.2f, _h)), Life = R(5f, 9f),
            Col = _rng.Next(2) == 0 ? new Color(120, 255, 210) : new Color(170, 255, 140), Size = 1f, Additive = true
        });
    }

    private void SpawnTwinkle(Color col)
    {
        Add(new P
        {
            Kind = Kind.Twinkle, Pos = new Vector2(R(0, _w), R(0, _h)), Life = R(0.5f, 1.1f), Col = col,
            Size = R(3f, 6f), Additive = true
        });
    }

    private void SpawnDrip(Color col)
    {
        if (_dripSources.Count == 0) return;
        var src = _dripSources[_rng.Next(_dripSources.Count)] + new Vector2(R(6f, 26f), 0f);
        Add(new P
        {
            Kind = Kind.Drip, Pos = src, Vel = Vector2.Zero, Life = 2f, Col = col, Size = 1.6f,
            FloorY = src.Y + R(16f, 28f)
        });
    }

    private void SpawnBubble(bool lava)
    {
        var candidates = _liquids.FindAll(l => l.lava == lava);
        if (candidates.Count == 0) return;
        var pos = candidates[_rng.Next(candidates.Count)].pos + new Vector2(R(-10f, 10f), R(-10f, 10f));
        Add(new P
        {
            Kind = Kind.Bubble, Pos = pos, Life = R(0.7f, 1.4f),
            Col = lava ? new Color(255, 170, 60) : new Color(150, 210, 110), Size = R(3f, 5.5f), Additive = lava
        });
    }

    private void SpawnSandStreak()
    {
        var dir = _wind >= 0f ? 1f : -1f;
        var speed = R(140f, 260f) * (0.6f + MathF.Abs(_wind) * 0.6f);
        Add(new P
        {
            Kind = Kind.Streak, Pos = new Vector2(dir > 0 ? R(-60f, _w * 0.7f) : R(_w * 0.3f, _w + 60f), R(0, _h)),
            Vel = new Vector2(dir * speed, R(-6f, 6f)), Life = R(0.5f, 1f),
            Col = _rng.Next(2) == 0 ? new Color(225, 200, 140) : new Color(190, 160, 100), Size = R(8f, 18f)
        });
    }

    private void InitFog()
    {
        _fog.Clear();
        var (count, minY, maxY) = _theme switch
        {
            TileMap.MapTheme.Swamp => (9, 0f, 1f),
            TileMap.MapTheme.Ice => (6, 0.55f, 1f),
            TileMap.MapTheme.Cave => (6, 0f, 1f),
            TileMap.MapTheme.Lava => (5, 0f, 0.45f),
            TileMap.MapTheme.Ruins => (5, 0.4f, 1f),
            _ => (4, 0.5f, 1f)
        };

        for (var i = 0; i < count; i++)
            _fog.Add(new FogBank
            {
                Pos = new Vector2(R(0, _w), R(_h * minY, _h * maxY)), Size = R(180f, 340f),
                Speed = R(5f, 14f) * (_rng.Next(2) == 0 ? -1f : 1f), Phase = R(0, MathF.Tau)
            });
    }

    // ═══════════════════════════════════════════════════════════════════
    // DRAW
    // ═══════════════════════════════════════════════════════════════════

    public void Draw(SpriteBatch sb, Matrix transform)
    {
        if (_map == null) return;

        // 1. Luce di fondo del bioma (raggi, aurora, calore) — additiva
        sb.Begin(samplerState: SamplerState.LinearClamp, blendState: BlendState.Additive, transformMatrix: transform);
        DrawBackLight(sb);
        sb.End();

        // 2. Nebbia e particelle "materiali" (foglie, neve, cenere, gocce, bolle)
        sb.Begin(samplerState: SamplerState.LinearClamp, blendState: BlendState.AlphaBlend, transformMatrix: transform);
        DrawFog(sb);
        foreach (var p in _particles)
            if (!p.Additive)
                DrawParticle(sb, p);
        sb.End();

        // 3. Particelle luminose (braci, lucciole, fuochi fatui, scintillii)
        sb.Begin(samplerState: SamplerState.LinearClamp, blendState: BlendState.Additive, transformMatrix: transform);
        foreach (var p in _particles)
            if (p.Additive)
                DrawParticle(sb, p);
        sb.End();
    }

    private void DrawBackLight(SpriteBatch sb)
    {
        switch (_theme)
        {
            case TileMap.MapTheme.Forest:
                DrawRays(sb, 4, new Color(255, 240, 180), 0.075f, 0.38f);
                break;

            case TileMap.MapTheme.Ruins:
                DrawRays(sb, 3, new Color(255, 210, 140), 0.07f, 0.3f);
                break;

            case TileMap.MapTheme.Ice:
                DrawAurora(sb);
                break;

            case TileMap.MapTheme.Lava:
                // Bagliore di calore che pulsa dal basso
                for (var i = 0; i < 7; i++)
                {
                    var pulse = 0.75f + 0.25f * MathF.Sin(_time * 1.8f + i * 1.3f);
                    var cx = _w * i / 6f;
                    sb.Draw(_soft, new Rectangle((int)cx - 230, _h - 150, 460, 320),
                        new Color(255, 70, 15) * (0.13f * pulse));
                }

                // Luce viva delle pozze di lava
                foreach (var (pos, lava) in _liquids)
                    if (lava)
                    {
                        var pulse = 0.7f + 0.3f * MathF.Sin(_time * 2.4f + pos.X * 0.05f + pos.Y * 0.03f);
                        sb.Draw(_soft, new Rectangle((int)pos.X - 30, (int)pos.Y - 30, 60, 60),
                            new Color(255, 110, 30) * (0.16f * pulse));
                    }

                break;

            case TileMap.MapTheme.Swamp:
                // Riflessi verdastri sull'acqua
                foreach (var (pos, lava) in _liquids)
                    if (!lava)
                    {
                        var pulse = 0.5f + 0.5f * MathF.Sin(_time * 1.3f + pos.X * 0.04f);
                        sb.Draw(_soft, new Rectangle((int)pos.X - 22, (int)pos.Y - 22, 44, 44),
                            new Color(90, 200, 120) * (0.07f * pulse));
                    }

                break;
        }
    }

    /// <summary>Raggi di luce obliqui che entrano dall'alto e ondeggiano lentamente.</summary>
    private void DrawRays(SpriteBatch sb, int count, Color color, float strength, float angle)
    {
        var origin = new Vector2(_ray.Width / 2f, 0f);
        for (var i = 0; i < count; i++)
        {
            var x = _w * (0.12f + i * 0.8f / count) + MathF.Sin(_time * 0.18f + i * 2f) * 40f;
            var width = 90f + 40f * MathF.Sin(i * 1.7f);
            var pulse = 0.65f + 0.35f * MathF.Sin(_time * 0.45f + i * 1.9f);
            var scale = new Vector2(width / _ray.Width, _h * 1.25f / _ray.Height);
            sb.Draw(_ray, new Vector2(x, -10f), null, color * (strength * pulse), -angle, origin, scale,
                SpriteEffects.None, 0f);
        }
    }

    /// <summary>Aurora boreale: tende di luce verde-viola che ondeggiano in cima alla mappa.</summary>
    private void DrawAurora(SpriteBatch sb)
    {
        var green = new Color(80, 255, 170);
        var purple = new Color(170, 100, 255);
        for (var x = -10; x < _w + 10; x += 9)
        {
            var y = 18f + MathF.Sin(x * 0.007f + _time * 0.4f) * 16f + MathF.Sin(x * 0.021f - _time * 0.7f) * 7f;
            var mix = (MathF.Sin(x * 0.004f + _time * 0.25f) + 1f) * 0.5f;
            var alpha = 0.07f + 0.05f * MathF.Sin(x * 0.03f + _time * 1.1f);
            var height = 90f + 40f * MathF.Sin(x * 0.012f + _time * 0.5f);
            sb.Draw(_ray, new Rectangle(x, (int)y, 24, (int)height), Color.Lerp(green, purple, mix) * alpha);
        }
    }

    private void DrawFog(SpriteBatch sb)
    {
        var (color, alpha) = _theme switch
        {
            TileMap.MapTheme.Swamp => (new Color(150, 175, 140), 0.20f),
            TileMap.MapTheme.Ice => (new Color(235, 245, 255), 0.13f),
            TileMap.MapTheme.Cave => (new Color(70, 75, 120), 0.16f),
            TileMap.MapTheme.Lava => (new Color(40, 25, 25), 0.20f),
            TileMap.MapTheme.Ruins => (new Color(215, 190, 140), 0.09f),
            _ => (new Color(235, 245, 225), 0.07f)
        };

        foreach (var f in _fog)
        {
            var breathe = 0.75f + 0.25f * MathF.Sin(_time * 0.3f + f.Phase);
            var w = f.Size * 1.6f;
            var h = f.Size * 0.7f;
            sb.Draw(_soft, new Rectangle((int)(f.Pos.X - w / 2), (int)(f.Pos.Y - h / 2), (int)w, (int)h),
                color * (alpha * breathe));
        }
    }

    private void DrawParticle(SpriteBatch sb, P p)
    {
        var k = p.Life / p.MaxLife; // 1 → 0
        var age = p.MaxLife - p.Life;
        var fade = MathF.Min(1f, p.Life / 0.4f) * MathF.Min(1f, age / 0.4f);

        switch (p.Kind)
        {
            case Kind.Glow:
            {
                var s = p.Size * 3f;
                sb.Draw(_soft, p.Pos, null, p.Col * (0.45f * fade), 0f, new Vector2(16f), s / 32f * 2f,
                    SpriteEffects.None, 0f);
                sb.Draw(_pixel, p.Pos, null, p.Col * fade, 0f, new Vector2(0.5f), MathF.Max(1f, p.Size * 0.7f),
                    SpriteEffects.None, 0f);
                break;
            }

            case Kind.Firefly:
            case Kind.Wisp:
            {
                var big = p.Kind == Kind.Wisp;
                var pulse = 0.4f + 0.6f * MathF.Max(0f, MathF.Sin(_time * (big ? 2f : 3.2f) + p.Phase));
                var halo = (big ? 34f : 14f) * (0.7f + 0.3f * pulse);
                sb.Draw(_soft, p.Pos, null, p.Col * (0.55f * pulse * fade), 0f, new Vector2(16f), halo / 32f,
                    SpriteEffects.None, 0f);
                sb.Draw(_soft, p.Pos, null, Color.White * (0.6f * pulse * fade), 0f, new Vector2(16f),
                    (big ? 8f : 4f) / 32f, SpriteEffects.None, 0f);
                break;
            }

            case Kind.Leaf:
                sb.Draw(_pixel, p.Pos, null, p.Col * (0.9f * fade), p.Rot, new Vector2(0.5f),
                    new Vector2(p.Size * 1.7f, p.Size * MathF.Max(0.25f, MathF.Abs(MathF.Cos(p.Rot * 0.7f)))),
                    SpriteEffects.None, 0f);
                break;

            case Kind.Snow:
            case Kind.Ash:
            {
                var a = (0.35f + 0.55f * p.Depth) * fade;
                if (p.Depth > 0.75f)
                    sb.Draw(_soft, p.Pos, null, p.Col * a, 0f, new Vector2(16f), p.Size * 2f / 32f,
                        SpriteEffects.None, 0f);
                else
                    sb.Draw(_pixel, p.Pos, null, p.Col * a, p.Rot, new Vector2(0.5f), p.Size, SpriteEffects.None, 0f);
                break;
            }

            case Kind.Drip:
                sb.Draw(_pixel, p.Pos, null, p.Col * 0.85f, 0f, new Vector2(0.5f),
                    new Vector2(p.Size, p.Size + MathF.Min(4f, p.Vel.Y * 0.02f)), SpriteEffects.None, 0f);
                break;

            case Kind.Splash:
                sb.Draw(_pixel, p.Pos, null, p.Col * (k * 0.9f), 0f, new Vector2(0.5f), p.Size,
                    SpriteEffects.None, 0f);
                break;

            case Kind.Bubble:
            {
                // Cresce fino a scoppiare
                var size = p.Size * (0.3f + 0.7f * (1f - k)) * 2f;
                sb.Draw(_soft, p.Pos, null, p.Col * (0.7f * fade), 0f, new Vector2(16f), size / 32f * 1.6f,
                    SpriteEffects.None, 0f);
                sb.Draw(_pixel, p.Pos + new Vector2(-size * 0.2f, -size * 0.25f), null, Color.White * (0.7f * fade),
                    0f, new Vector2(0.5f), 1f, SpriteEffects.None, 0f);
                break;
            }

            case Kind.Streak:
            {
                var a = MathF.Sin(k * MathF.PI) * 0.45f;
                var rot = MathF.Atan2(p.Vel.Y, p.Vel.X);
                sb.Draw(_pixel, p.Pos, null, p.Col * a, rot, new Vector2(0.5f), new Vector2(p.Size, 1f),
                    SpriteEffects.None, 0f);
                break;
            }

            case Kind.Twinkle:
            {
                // Stella a quattro punte che si accende e si spegne
                var a = MathF.Sin((1f - k) * MathF.PI);
                var s = p.Size * a;
                sb.Draw(_soft, p.Pos, null, p.Col * (0.35f * a), 0f, new Vector2(16f), s * 2f / 32f,
                    SpriteEffects.None, 0f);
                sb.Draw(_pixel, p.Pos, null, p.Col * a, 0f, new Vector2(0.5f), new Vector2(s * 2f, 1f),
                    SpriteEffects.None, 0f);
                sb.Draw(_pixel, p.Pos, null, p.Col * a, 0f, new Vector2(0.5f), new Vector2(1f, s * 2f),
                    SpriteEffects.None, 0f);
                break;
            }
        }
    }

    /// <summary>Fascio di luce: morbido ai lati, sfuma verso il basso.</summary>
    private static Texture2D CreateRay(GraphicsDevice gd, int w, int h)
    {
        var tex = new Texture2D(gd, w, h);
        var data = new Color[w * h];
        var c = (w - 1) / 2f;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var side = 1f - MathF.Abs(x - c) / c;
                var down = 1f - y / (float)(h - 1);
                data[y * w + x] = Color.White * (side * side * MathF.Sqrt(down));
            }

        tex.SetData(data);
        return tex;
    }

    private enum Kind
    {
        Glow, // pulviscolo / braci / spore luminose
        Firefly, // lucciola che vaga e pulsa
        Wisp, // fuoco fatuo (palude)
        Leaf, // foglia che cade ruotando
        Snow, // fiocco di neve con profondità
        Ash, // cenere che cade
        Drip, // goccia che cade da un blocco
        Splash, // schizzo / scoppio
        Bubble, // bolla su un liquido
        Streak, // filo di sabbia nel vento
        Twinkle // scintillio a stella
    }

    private struct P
    {
        public Kind Kind;
        public Vector2 Pos, Vel;
        public float Life, MaxLife, Size, Rot, RotSpeed, Phase, Depth, FloorY;
        public Color Col;
        public bool Additive;
    }

    private struct FogBank
    {
        public Vector2 Pos;
        public float Size, Speed, Phase;
    }
}
