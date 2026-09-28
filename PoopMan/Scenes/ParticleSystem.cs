using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace PoopMan.Scenes;

/// <summary>
///     Particelle "moderne" del mondo di gioco: detriti dei blocchi, fumo delle
///     esplosioni, scintille delle micce, polvere dei passi e testi fluttuanti
///     (punteggio / raccolta oggetti). Tutto in coordinate mondo.
/// </summary>
internal sealed class ParticleSystem : IDisposable
{
    public enum Kind
    {
        Chunk, // quadratino solido con gravità (detriti)
        Smoke, // puff morbido che si espande e sfuma (alpha blend)
        Spark // puntino luminoso additivo
    }

    private const int MaxParticles = 900;
    private const int MaxTexts = 24;
    private const float TileMapWidth = PoopManLibrary.World.TileMap.Cols * PoopManLibrary.World.TileMap.TileSize;

    private readonly Texture2D _pixel;
    private readonly List<Particle> _particles = new();
    private readonly Random _rng = new();
    private readonly Texture2D _soft;
    private readonly List<FloatingText> _texts = new();

    public ParticleSystem(GraphicsDevice gd)
    {
        _pixel = new Texture2D(gd, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _soft = FxTextures.CreateRadial(gd, 32, 1.2f);
    }

    public int Count => _particles.Count;

    public void Dispose()
    {
        _pixel?.Dispose();
        _soft?.Dispose();
    }

    public void Clear()
    {
        _particles.Clear();
        _texts.Clear();
    }

    private float R(float min, float max) => min + (float)_rng.NextDouble() * (max - min);

    public void Emit(Kind kind, Vector2 pos, Vector2 vel, float life, float size, float sizeEnd, Color color,
        float gravity = 0f, float drag = 0f)
    {
        if (_particles.Count >= MaxParticles) return;
        _particles.Add(new Particle
        {
            Kind = kind, Pos = pos, Vel = vel, Life = life, MaxLife = life, Size = size, SizeEnd = sizeEnd,
            Color = color, Gravity = gravity, Drag = drag, Rot = R(0, MathF.Tau), RotSpeed = R(-6f, 6f)
        });
    }

    // ── Effetti pronti ────────────────────────────────────────────────────

    /// <summary>Blocco distrutto: schegge che saltano e ricadono + nuvola di polvere.</summary>
    public void BlockDebris(Vector2 tileCenter, Color main, Color dark)
    {
        for (var i = 0; i < 10; i++)
        {
            var a = R(0, MathF.Tau);
            var sp = R(40f, 120f);
            var vel = new Vector2(MathF.Cos(a) * sp, MathF.Sin(a) * sp * 0.6f - R(60f, 130f));
            Emit(Kind.Chunk, tileCenter + new Vector2(R(-8, 8), R(-8, 8)), vel, R(0.45f, 0.8f), R(2f, 4.5f),
                1f, _rng.Next(3) == 0 ? dark : main, 420f, 1.5f);
        }

        for (var i = 0; i < 4; i++)
            Emit(Kind.Smoke, tileCenter + new Vector2(R(-6, 6), R(-6, 6)),
                new Vector2(R(-18, 18), R(-22, -4)), R(0.5f, 0.8f), R(10f, 14f), R(22f, 30f),
                Color.Lerp(main, new Color(200, 190, 170), 0.6f) * 0.55f, 0f, 1.2f);
    }

    /// <summary>Fumo + scintille su ogni tile dell'esplosione.</summary>
    public void ExplosionTile(Vector2 tileCenter, bool big)
    {
        var smokeCount = big ? 3 : 2;
        for (var i = 0; i < smokeCount; i++)
            Emit(Kind.Smoke, tileCenter + new Vector2(R(-7, 7), R(-7, 7)),
                new Vector2(R(-14, 14), R(-30, -10)), R(0.7f, 1.2f), R(12f, 16f), R(28f, 40f),
                new Color(70, 64, 60) * 0.5f, 0f, 0.8f);

        var sparkCount = big ? 5 : 3;
        for (var i = 0; i < sparkCount; i++)
        {
            var a = R(0, MathF.Tau);
            var sp = R(60f, big ? 220f : 160f);
            Emit(Kind.Spark, tileCenter, new Vector2(MathF.Cos(a), MathF.Sin(a)) * sp, R(0.25f, 0.55f),
                R(2f, 3.5f), 0.5f, _rng.Next(2) == 0 ? new Color(255, 220, 120) : new Color(255, 140, 40), 60f, 3f);
        }
    }

    /// <summary>Scintilla della miccia di una bomba.</summary>
    public void FuseSpark(Vector2 pos, bool remote)
    {
        var col = remote ? new Color(255, 80, 80) : _rng.Next(2) == 0 ? new Color(255, 230, 120) : new Color(255, 160, 50);
        Emit(Kind.Spark, pos, new Vector2(R(-35, 35), R(-60, -15)), R(0.15f, 0.35f), R(1.5f, 2.5f), 0.5f, col,
            140f, 1f);
    }

    /// <summary>Polverina sotto i piedi quando il miner cammina.</summary>
    public void FootDust(Vector2 feet, Color tint)
    {
        Emit(Kind.Smoke, feet + new Vector2(R(-4, 4), R(-1, 2)), new Vector2(R(-10, 10), R(-10, -2)),
            R(0.3f, 0.45f), R(4f, 6f), R(9f, 12f), tint * 0.35f, 0f, 2f);
    }

    /// <summary>Esplosione di scintille colorate (raccolta oggetti, power-up).</summary>
    public void Burst(Vector2 pos, Color color, int count = 16, float speed = 110f)
    {
        for (var i = 0; i < count; i++)
        {
            var a = i * MathF.Tau / count + R(-0.2f, 0.2f);
            var sp = R(speed * 0.5f, speed);
            Emit(Kind.Spark, pos, new Vector2(MathF.Cos(a), MathF.Sin(a)) * sp, R(0.35f, 0.7f), R(2f, 3.5f), 0.5f,
                color, 0f, 2.5f);
        }
    }

    /// <summary>Testo che sale e sfuma (es. "+100").</summary>
    public void AddText(Vector2 pos, string text, Color color, float scale = 0.8f)
    {
        if (_texts.Count >= MaxTexts) _texts.RemoveAt(0);
        // Resta dentro la mappa (il testo sopra la prima riga finirebbe sotto l'HUD)
        pos.Y = MathF.Max(pos.Y, 14f);
        pos.X = Math.Clamp(pos.X, 60f, TileMapWidth - 60f);
        _texts.Add(new FloatingText { Pos = pos, Text = text, Color = color, Scale = scale, Life = 1.1f, MaxLife = 1.1f });
    }

    // ── Update / Draw ─────────────────────────────────────────────────────

    public void Update(float dt)
    {
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life -= dt;
            if (p.Life <= 0f)
            {
                _particles.RemoveAt(i);
                continue;
            }

            p.Vel.Y += p.Gravity * dt;
            if (p.Drag > 0f) p.Vel *= MathF.Max(0f, 1f - p.Drag * dt);
            p.Pos += p.Vel * dt;
            p.Rot += p.RotSpeed * dt;
            _particles[i] = p;
        }

        for (var i = _texts.Count - 1; i >= 0; i--)
        {
            var t = _texts[i];
            t.Life -= dt;
            if (t.Life <= 0f)
            {
                _texts.RemoveAt(i);
                continue;
            }

            t.Pos.Y -= 26f * dt * (t.Life / t.MaxLife + 0.3f);
            _texts[i] = t;
        }
    }

    /// <summary>Detriti e fumo (alpha blend) — da disegnare nel batch del mondo.</summary>
    public void DrawAlpha(SpriteBatch sb)
    {
        foreach (var p in _particles)
        {
            if (p.Kind == Kind.Spark) continue;
            var k = 1f - p.Life / p.MaxLife; // 0 → 1
            var size = MathHelper.Lerp(p.Size, p.SizeEnd, k);
            if (p.Kind == Kind.Chunk)
            {
                var alpha = p.Life < 0.2f ? p.Life / 0.2f : 1f;
                sb.Draw(_pixel, p.Pos, null, p.Color * alpha, p.Rot, new Vector2(0.5f), size, SpriteEffects.None, 0f);
            }
            else
            {
                var alpha = MathF.Sin((1f - k) * MathF.PI * 0.5f); // sfuma verso la fine
                sb.Draw(_soft, p.Pos, null, p.Color * alpha, p.Rot, new Vector2(16f), size / 32f,
                    SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>Scintille (additive) — da disegnare in un batch con BlendState.Additive.</summary>
    public void DrawAdditive(SpriteBatch sb)
    {
        foreach (var p in _particles)
        {
            if (p.Kind != Kind.Spark) continue;
            var k = 1f - p.Life / p.MaxLife;
            var size = MathHelper.Lerp(p.Size, p.SizeEnd, k);
            var alpha = 1f - k * k;
            sb.Draw(_soft, p.Pos, null, p.Color * (alpha * 0.6f), 0f, new Vector2(16f), size * 2.2f / 32f,
                SpriteEffects.None, 0f);
            sb.Draw(_pixel, p.Pos, null, p.Color * alpha, 0f, new Vector2(0.5f), MathF.Max(1f, size * 0.7f),
                SpriteEffects.None, 0f);
        }
    }

    /// <summary>Testi fluttuanti con contorno scuro.</summary>
    public void DrawTexts(SpriteBatch sb, SpriteFont font)
    {
        foreach (var t in _texts)
        {
            var k = t.Life / t.MaxLife;
            var alpha = k < 0.35f ? k / 0.35f : 1f;
            // "pop" iniziale: il testo nasce un po' più grande e si assesta
            var pop = 1f + MathF.Max(0f, k - 0.8f) * 2.2f;
            var scale = t.Scale * pop;
            var size = font.MeasureString(t.Text) * scale;
            var pos = new Vector2(MathF.Round(t.Pos.X - size.X / 2f), MathF.Round(t.Pos.Y - size.Y / 2f));
            var outline = Color.Black * (alpha * 0.85f);
            sb.DrawString(font, t.Text, pos + new Vector2(-1, 0), outline, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.DrawString(font, t.Text, pos + new Vector2(1, 0), outline, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.DrawString(font, t.Text, pos + new Vector2(0, -1), outline, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.DrawString(font, t.Text, pos + new Vector2(0, 1), outline, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.DrawString(font, t.Text, pos, t.Color * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>Posizioni delle scintille attive (per aggiungere piccole luci dinamiche).</summary>
    public IEnumerable<(Vector2 pos, Color color, float life)> Sparks()
    {
        foreach (var p in _particles)
            if (p.Kind == Kind.Spark)
                yield return (p.Pos, p.Color, p.Life / p.MaxLife);
    }

    private struct Particle
    {
        public Kind Kind;
        public Vector2 Pos, Vel;
        public float Life, MaxLife, Size, SizeEnd, Gravity, Drag, Rot, RotSpeed;
        public Color Color;
    }

    private struct FloatingText
    {
        public Vector2 Pos;
        public string Text;
        public Color Color;
        public float Scale, Life, MaxLife;
    }
}
