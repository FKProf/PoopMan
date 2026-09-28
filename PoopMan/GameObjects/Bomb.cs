using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PoopManLibrary.World;
using System;
using System.Collections.Generic;

namespace PoopMan.GameObjects;

internal class Bomb
{
    private const float AnimSpeed = 0.15f;
    private readonly Dictionary<string, List<Rectangle>> _bombAnimations;
    private readonly Texture2D _bombTexture;
    private readonly Dictionary<string, List<Rectangle>> _explosionAnimations;
    private readonly Texture2D _explosionTexture;
    private readonly int _extraRange;
    private readonly float _fuseDuration = 2f;

    private readonly bool _multiHit;
    // ═══════════════════════════════════════════════════════════════════
    // CAMPI – GRAFICA E ANIMAZIONE
    // ═══════════════════════════════════════════════════════════════════

    private readonly Vector2 _position;
    private float _animTimer;
    private string _currentAnimation;
    private int _currentFrame;
    private List<Rectangle> _currentFrames;
    private float _fuseTimer;
    private float _pulseTimer;

    // ═══════════════════════════════════════════════════════════════════
    // CAMPI – STATO
    // ═══════════════════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════════════════
    // COSTRUTTORE
    // ═══════════════════════════════════════════════════════════════════

    public Bomb(Vector2 pos,
        Texture2D bombTex,
        Dictionary<string, List<Rectangle>> bombAnim,
        Texture2D explTex,
        Dictionary<string, List<Rectangle>> explAnim,
        bool big,
        int extraRange = 0,
        float fuseReduce = 0f,
        bool multiHit = false)
    {
        _position = pos;
        _bombTexture = bombTex;
        _bombAnimations = bombAnim;
        _explosionTexture = explTex;
        _explosionAnimations = explAnim;
        BigBomb = big;
        _extraRange = extraRange;
        _multiHit = multiHit;
        _fuseDuration = Math.Max(0.5f, _fuseDuration - fuseReduce);

        _currentAnimation = BigBomb ? "big_tnt" : "small_tnt";
        _currentFrames = _bombAnimations[_currentAnimation];
    }

    // ═══════════════════════════════════════════════════════════════════
    // PROPRIETÀ E EVENTI
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Tile colpiti dall'esplosione (usato per collisioni e drop).</summary>
    public List<Point> ExplosionTiles { get; } = new();

    public bool IsFinished { get; private set; }

    public bool IsExploding { get; private set; }

    /// <summary>True dopo che il danno ai bat/miner è stato già applicato per questa esplosione.</summary>
    public bool DamageApplied { get; set; } = false;

    public Vector2 Position => _position;
    public bool BigBomb { get; }

    /// <summary>
    ///     Bomba a detonazione remota (upgrade DETONATORE): la miccia non si consuma,
    ///     esplode solo col comando di detonazione o se raggiunta da un'altra esplosione.
    /// </summary>
    public bool IsRemote { get; set; }

    /// <summary>Scattato nel momento in cui la bomba esplode. Arg: true = bomba grande.</summary>
    public event EventHandler<bool>? Exploded;

    // ═══════════════════════════════════════════════════════════════════
    // UPDATE
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Aggiorna la miccia e, una volta scaduta, avvia l'esplosione.</summary>
    public void Update(GameTime gameTime, TileMap map)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (!IsExploding)
        {
            if (!IsRemote) _fuseTimer += dt;
            _pulseTimer += dt;
            _animTimer += dt;

            if (_animTimer >= AnimSpeed)
            {
                _animTimer = 0f;
                _currentFrame = (_currentFrame + 1) % _currentFrames.Count;
            }

            if (!IsRemote && _fuseTimer >= _fuseDuration)
                Explode(map);
        }
        else
        {
            _animTimer += dt;
            if (_animTimer >= AnimSpeed)
            {
                _animTimer = 0f;
                _currentFrame++;
                if (_currentFrame >= _currentFrames.Count)
                    IsFinished = true;
            }
        }
    }

    /// <summary>Punto (mondo) della miccia accesa: sorgente di scintille e luce.</summary>
    public Vector2 FusePosition => _position + (BigBomb ? new Vector2(16f, 5f) : new Vector2(25f, 6f));

    /// <summary>Avanzamento dell'animazione di esplosione (0 → 1), 0 se non ancora esplosa.</summary>
    public float ExplosionProgress => IsExploding && _currentFrames.Count > 0
        ? Math.Clamp((_currentFrame + _animTimer / AnimSpeed) / _currentFrames.Count, 0f, 1f)
        : 0f;

    /// <summary>Tile su cui si trova la bomba.</summary>
    public Point Tile => new((int)(_position.X / TileMap.TileSize), (int)(_position.Y / TileMap.TileSize));

    // ═══════════════════════════════════════════════════════════════════
    // ESPLOSIONE
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Fa esplodere subito la bomba (reazione a catena o detonatore remoto).
    ///     Restituisce true se la bomba non era ancora esplosa.
    /// </summary>
    public bool Detonate(TileMap map)
    {
        if (IsExploding || IsFinished) return false;
        Explode(map);
        return true;
    }

    /// <summary>
    ///     Calcola i tile colpiti dall'esplosione e rompe i breakable.
    ///     I tile breakable bloccano la propagazione ma non vengono aggiunti
    ///     agli ExplosionTiles (il bat nascosto lì è al sicuro).
    /// </summary>
    private void Explode(TileMap map)
    {
        IsExploding = true;
        _currentAnimation = "explosion";
        _currentFrames = _explosionAnimations[_currentAnimation];
        _currentFrame = 0;
        _animTimer = 0f;
        ExplosionTiles.Clear();
        Exploded?.Invoke(this, BigBomb);

        Point center = new((int)(_position.X / TileMap.TileSize),
            (int)(_position.Y / TileMap.TileSize));

        if (map.GetTile(center) != TileType.Wall)
        {
            ExplosionTiles.Add(center);
            if (map.GetTile(center) == TileType.Breakable)
                map.BreakTile(center);
        }

        var range = (BigBomb ? 2 : 1) + _extraRange;
        int[] dx = { 0, 0, -1, 1 };
        int[] dy = { -1, 1, 0, 0 };

        for (var dir = 0; dir < 4; dir++)
            for (var step = 1; step <= range; step++)
            {
                Point t = new(center.X + dx[dir] * step,
                    center.Y + dy[dir] * step);

                if (!map.IsInside(t)) break;
                if (map.GetTile(t) == TileType.Wall) break;

                if (map.GetTile(t) == TileType.Breakable)
                {
                    map.BreakTile(t);
                    if (!_multiHit) break; // MultiHit: continua oltre i breakable
                    continue;
                }

                ExplosionTiles.Add(t);
            }
    }

    // ═══════════════════════════════════════════════════════════════════
    // DRAW
    // ═══════════════════════════════════════════════════════════════════

    // Strati della fiamma dall'esterno verso il nucleo (larghezza relativa, colore)
    private static readonly (float w, Color c)[] FlameLayers =
    {
        (1.00f, new Color(200, 40, 20)),
        (0.74f, new Color(255, 120, 30)),
        (0.48f, new Color(255, 214, 80)),
        (0.22f, new Color(255, 250, 225))
    };

    /// <summary>
    ///     Fiamme "a croce" in stile Bomberman: raggi luminosi che collegano le tile
    ///     dell'esplosione. Sono emissive: vanno disegnate DOPO l'illuminazione.
    /// </summary>
    public void DrawFlame(SpriteBatch spriteBatch, Texture2D pixel, float time)
    {
        if (!IsExploding || IsFinished || ExplosionTiles.Count == 0) return;

        var k = ExplosionProgress;
        // Inviluppo: esplode in fretta, resta piena, poi si assottiglia e sparisce
        var env = k < 0.12f ? k / 0.12f : k > 0.5f ? 1f - (k - 0.5f) / 0.4f : 1f;
        if (env <= 0f) return;

        var ts = TileMap.TileSize;
        var set = new HashSet<Point>(ExplosionTiles);
        var center = Tile;
        var baseW = (BigBomb ? 28f : 24f) * env;
        var alpha = k > 0.75f ? Math.Clamp(1f - (k - 0.75f) / 0.15f, 0f, 1f) : 1f;

        foreach (var (lw, col) in FlameLayers)
            foreach (var t in set)
            {
                var flick = 1f + 0.10f * MathF.Sin(time * 38f + t.X * 1.7f + t.Y * 2.3f);
                var w = baseW * lw * flick * (t == center ? 1.2f : 1f);
                if (w < 1f) continue;
                var cx = t.X * ts + ts / 2;
                var cy = t.Y * ts + ts / 2;
                var hw = (int)MathF.Round(w / 2f);

                // Nucleo della tile
                spriteBatch.Draw(pixel, new Rectangle(cx - hw, cy - hw, hw * 2, hw * 2), col * alpha);

                // Raggi verso le tile collegate (fino al bordo condiviso)
                if (set.Contains(new Point(t.X + 1, t.Y)))
                    spriteBatch.Draw(pixel, new Rectangle(cx, cy - hw, ts / 2, hw * 2), col * alpha);
                if (set.Contains(new Point(t.X - 1, t.Y)))
                    spriteBatch.Draw(pixel, new Rectangle(cx - ts / 2, cy - hw, ts / 2, hw * 2), col * alpha);
                if (set.Contains(new Point(t.X, t.Y + 1)))
                    spriteBatch.Draw(pixel, new Rectangle(cx - hw, cy, hw * 2, ts / 2), col * alpha);
                if (set.Contains(new Point(t.X, t.Y - 1)))
                    spriteBatch.Draw(pixel, new Rectangle(cx - hw, cy - ts / 2, hw * 2, ts / 2), col * alpha);
            }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (IsFinished) return;

        var safeFrame = Math.Min(_currentFrame, _currentFrames.Count - 1);

        if (IsExploding)
            foreach (var tile in ExplosionTiles)
            {
                Vector2 drawPos = new(tile.X * TileMap.TileSize, tile.Y * TileMap.TileSize);
                spriteBatch.Draw(_explosionTexture, drawPos, _currentFrames[safeFrame], Color.White);
            }
        else
        {
            // Bomba remota: lampeggio rosso lento per distinguerla da quelle a miccia.
            // Bomba a miccia: il lampeggio accelera negli ultimi istanti prima dello scoppio.
            var tint = Color.White;
            if (IsRemote)
            {
                var p = 0.5f + 0.5f * MathF.Sin(_pulseTimer * 5f);
                tint = Color.Lerp(Color.White, new Color(255, 90, 90), p * 0.7f);
            }
            else
            {
                var remaining = _fuseDuration - _fuseTimer;
                if (remaining < 0.8f && MathF.Sin(_pulseTimer * (remaining < 0.35f ? 45f : 25f)) > 0f)
                    tint = new Color(255, 170, 170);
            }

            // Leggero "respiro" (squash & stretch) appoggiato al pavimento: accelera a fine miccia.
            var speed = IsRemote ? 5f : 8f + 10f * Math.Clamp(_fuseTimer / _fuseDuration, 0f, 1f);
            var s = 0.06f * MathF.Sin(_pulseTimer * speed);
            var scale = new Vector2(1f + s, 1f - s * 0.8f);
            var origin = new Vector2(16f, 30f);
            spriteBatch.Draw(_bombTexture, _position + origin, _currentFrames[safeFrame], tint, 0f, origin, scale,
                SpriteEffects.None, 0f);
        }
    }
}