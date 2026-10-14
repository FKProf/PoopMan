using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PoopMan.GameObjects;
using PoopManLibrary.World;
using System;
using System.Collections.Generic;

namespace PoopMan.Scenes;

/// <summary>
///     Proiettili dei golem: palle di roccia (Booler) e raggi laser (Laser).
///     Gestisce movimento, impatti e disegno (inclusi i segnali di preavviso del
///     laser e della carica del rhino). I danni al miner li applica GameScene.
/// </summary>
internal sealed class GolemAttackSystem
{
    private const float RockSpeed = 250f;
    private const float RockMaxTravel = 10 * TileMap.TileSize;
    private const float BeamLife = 0.45f;
    private const float BeamDamageWindow = 0.3f;
    private const int LaserMaxLength = 14;

    private readonly List<Rock> _rocks = new();
    private readonly List<Beam> _beams = new();

    /// <summary>Una roccia si è frantumata (posizione in pixel): per le particelle.</summary>
    public event Action<Vector2>? RockShattered;

    public void Clear()
    {
        _rocks.Clear();
        _beams.Clear();
    }

    public void ThrowRock(Point origin, Point dir)
    {
        _rocks.Add(new Rock { Position = TileCenter(origin), Direction = new Vector2(dir.X, dir.Y) });
    }

    /// <summary>
    ///     Tile attraversate dal laser: prosegue sopra pavimento, liquidi e blocchi
    ///     distruttibili; si ferma ai muri indistruttibili e al confine.
    /// </summary>
    public static List<Point> TraceLaser(TileMap map, Point origin, Point dir)
    {
        var tiles = new List<Point>();
        var t = new Point(origin.X + dir.X, origin.Y + dir.Y);
        while (tiles.Count < LaserMaxLength && map.IsInside(t))
        {
            if (map.GetTile(t) == TileType.Wall && !map.IsLiquid(t)) break;
            tiles.Add(t);
            t = new Point(t.X + dir.X, t.Y + dir.Y);
        }

        return tiles;
    }

    /// <summary>Spara il laser: distrugge i blocchi lungo il raggio e ne restituisce le tile.</summary>
    public List<Point> FireLaser(TileMap map, Point origin, Point dir)
    {
        var tiles = TraceLaser(map, origin, dir);
        foreach (var t in tiles)
            if (map.GetTile(t) == TileType.Breakable)
                map.BreakTile(t);

        _beams.Add(new Beam { Origin = origin, Tiles = tiles, Life = BeamLife });
        return tiles;
    }

    /// <summary>
    ///     Aggiorna rocce e raggi. Restituisce true se una roccia o un laser
    ///     ha colpito la tile del miner in questo frame.
    /// </summary>
    public bool Update(float dt, TileMap map, Point minerTile)
    {
        var minerHit = false;

        for (var i = _rocks.Count - 1; i >= 0; i--)
        {
            var r = _rocks[i];
            var step = RockSpeed * dt;
            r.Position += r.Direction * step;
            r.Traveled += step;
            r.Spin += dt * 14f;

            var tile = new Point((int)(r.Position.X / TileMap.TileSize), (int)(r.Position.Y / TileMap.TileSize));
            if (tile == minerTile)
            {
                minerHit = true;
                Shatter(i);
                continue;
            }

            // Vola sopra pavimento e liquidi, si frantuma contro muri e blocchi
            if ((!map.IsWalkable(tile) && !map.IsLiquid(tile)) || r.Traveled >= RockMaxTravel)
                Shatter(i);
        }

        for (var i = _beams.Count - 1; i >= 0; i--)
        {
            var b = _beams[i];
            b.Life -= dt;
            if (b.Life <= 0f)
            {
                _beams.RemoveAt(i);
                continue;
            }

            if (!b.HitApplied && BeamLife - b.Life <= BeamDamageWindow && b.Tiles.Contains(minerTile))
            {
                b.HitApplied = true;
                minerHit = true;
            }
        }

        return minerHit;
    }

    private void Shatter(int index)
    {
        RockShattered?.Invoke(_rocks[index].Position);
        _rocks.RemoveAt(index);
    }

    /// <summary>Luci dinamiche dei raggi laser (posizione, raggio, colore, intensità).</summary>
    public IEnumerable<(Vector2 pos, float radius, Color color, float intensity)> Lights()
    {
        foreach (var b in _beams)
        {
            var k = b.Life / BeamLife;
            for (var i = 0; i < b.Tiles.Count; i += 2)
                yield return (TileCenter(b.Tiles[i]), 70f, new Color(255, 60, 80), k);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // DRAW
    // ═══════════════════════════════════════════════════════════════════

    public void Draw(SpriteBatch sb, Texture2D pixel, TileMap map, IEnumerable<Bat> enemies, float time)
    {
        DrawTelegraphs(sb, pixel, map, enemies, time);

        foreach (var r in _rocks)
        {
            // Palla di roccia: sasso scuro con riflesso che ruota
            var c = r.Position;
            var wobble = new Vector2(MathF.Cos(r.Spin), MathF.Sin(r.Spin)) * 1.5f;
            sb.Draw(pixel, new Rectangle((int)c.X - 6, (int)c.Y + 6, 12, 3), Color.Black * 0.3f);
            sb.Draw(pixel, new Rectangle((int)c.X - 6, (int)c.Y - 6, 12, 12), new Color(90, 80, 72));
            sb.Draw(pixel, new Rectangle((int)c.X - 4, (int)c.Y - 7, 8, 14), new Color(110, 100, 90));
            sb.Draw(pixel, new Rectangle((int)(c.X - 3 + wobble.X), (int)(c.Y - 4 + wobble.Y), 4, 3),
                new Color(170, 160, 150));
        }

        foreach (var b in _beams)
        {
            if (b.Tiles.Count == 0) continue;
            var k = b.Life / BeamLife;
            var width = (int)(6 + 10 * k);
            var flicker = 0.85f + 0.15f * MathF.Sin(time * 60f);
            var start = TileCenter(b.Origin);
            var end = TileCenter(b.Tiles[^1]);
            DrawLine(sb, pixel, start, end, width + 8, new Color(255, 40, 70) * (0.35f * k));
            DrawLine(sb, pixel, start, end, width, new Color(255, 70, 90) * (0.85f * k * flicker));
            DrawLine(sb, pixel, start, end, Math.Max(2, width / 3), Color.White * k);
        }
    }

    /// <summary>Preavvisi: linea lampeggiante del laser in carica e traiettoria del rhino.</summary>
    private static void DrawTelegraphs(SpriteBatch sb, Texture2D pixel, TileMap map, IEnumerable<Bat> enemies,
        float time)
    {
        foreach (var e in enemies)
        {
            if (e.IsDead || !e.IsGolem) continue;

            if (e.IsChargingLaser)
            {
                var tiles = TraceLaser(map, e.TilePosition, e.AttackDirection);
                if (tiles.Count == 0) continue;
                var k = e.LaserChargeProgress;
                var blink = MathF.Sin(time * (12f + 30f * k)) > 0f ? 1f : 0.45f;
                DrawLine(sb, pixel, TileCenter(e.TilePosition), TileCenter(tiles[^1]), 2 + (int)(2 * k),
                    new Color(255, 50, 70) * ((0.25f + 0.5f * k) * blink));
            }
            else if (e.IsRhinoWindup)
            {
                // Puntini lampeggianti nella direzione della carica
                var d = e.AttackDirection;
                for (var i = 1; i <= 4; i++)
                {
                    var t = new Point(e.TilePosition.X + d.X * i, e.TilePosition.Y + d.Y * i);
                    if (!map.IsWalkable(t)) break;
                    var c = TileCenter(t);
                    var a = 0.75f - i * 0.15f + 0.2f * MathF.Sin(time * 18f - i);
                    sb.Draw(pixel, new Rectangle((int)c.X - 3, (int)c.Y - 3, 6, 6), new Color(255, 160, 40) * a);
                }
            }
        }
    }

    private static void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 a, Vector2 b, int width, Color color)
    {
        var delta = b - a;
        var length = delta.Length();
        if (length < 1f) return;
        var angle = MathF.Atan2(delta.Y, delta.X);
        sb.Draw(pixel, a, null, color, angle, new Vector2(0f, 0.5f), new Vector2(length, width),
            SpriteEffects.None, 0f);
    }

    private static Vector2 TileCenter(Point t)
    {
        var half = TileMap.TileSize * 0.5f;
        return new Vector2(t.X * TileMap.TileSize + half, t.Y * TileMap.TileSize + half);
    }

    private sealed class Rock
    {
        public Vector2 Direction;
        public Vector2 Position; // centro in pixel
        public float Spin;
        public float Traveled;
    }

    private sealed class Beam
    {
        public bool HitApplied;
        public float Life;
        public Point Origin;
        public List<Point> Tiles = new();
    }
}
