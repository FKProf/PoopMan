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
            _fuseTimer += dt;
            _animTimer += dt;

            if (_animTimer >= AnimSpeed)
            {
                _animTimer = 0f;
                _currentFrame = (_currentFrame + 1) % _currentFrames.Count;
            }

            if (_fuseTimer >= _fuseDuration)
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

    // ═══════════════════════════════════════════════════════════════════
    // ESPLOSIONE
    // ═══════════════════════════════════════════════════════════════════

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
            spriteBatch.Draw(_bombTexture, _position, _currentFrames[safeFrame], Color.White);
    }
}