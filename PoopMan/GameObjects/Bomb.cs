using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PoopManLibrary.World;
using System;
using System.Collections.Generic;

namespace PoopMan.GameObjects;

internal class Bomb
{
    // ═══════════════════════════════════════════════════════════════════
    // CAMPI – GRAFICA E ANIMAZIONE
    // ═══════════════════════════════════════════════════════════════════

    private readonly Vector2 _position;
    private readonly Texture2D _bombTexture;
    private readonly Dictionary<string, List<Rectangle>> _bombAnimations;
    private readonly Texture2D _explosionTexture;
    private readonly Dictionary<string, List<Rectangle>> _explosionAnimations;
    private string _currentAnimation;
    private List<Rectangle> _currentFrames;
    private int   _currentFrame   = 0;
    private float _animTimer      = 0f;
    private const float AnimSpeed = 0.15f;

    // ═══════════════════════════════════════════════════════════════════
    // CAMPI – STATO
    // ═══════════════════════════════════════════════════════════════════

    private bool  _isExploding  = false;
    private bool  _isFinished   = false;
    private float _fuseTimer    = 0f;
    private float _fuseDuration = 2f;
    private readonly bool _bigBomb;
    private readonly int  _extraRange;

    // ═══════════════════════════════════════════════════════════════════
    // PROPRIETÀ E EVENTI
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Tile colpiti dall'esplosione (usato per collisioni e drop).</summary>
    public List<Point> ExplosionTiles { get; private set; } = new();

    public bool    IsFinished  => _isFinished;
    public bool    IsExploding => _isExploding;
    public Vector2 Position    => _position;
    public bool    BigBomb     => _bigBomb;

    /// <summary>Scattato nel momento in cui la bomba esplode. Arg: true = bomba grande.</summary>
    public event EventHandler<bool>? Exploded;

    // ═══════════════════════════════════════════════════════════════════
    // COSTRUTTORE
    // ═══════════════════════════════════════════════════════════════════

    public Bomb(Vector2 pos,
                Texture2D bombTex,
                Dictionary<string, List<Rectangle>> bombAnim,
                Texture2D explTex,
                Dictionary<string, List<Rectangle>> explAnim,
                bool big,
                int   extraRange  = 0,
                float fuseReduce  = 0f)
    {
        _position            = pos;
        _bombTexture         = bombTex;
        _bombAnimations      = bombAnim;
        _explosionTexture    = explTex;
        _explosionAnimations = explAnim;
        _bigBomb             = big;
        _extraRange          = extraRange;
        _fuseDuration        = Math.Max(0.5f, _fuseDuration - fuseReduce);

        _currentAnimation = _bigBomb ? "big_tnt" : "small_tnt";
        _currentFrames    = _bombAnimations[_currentAnimation];
    }

    // ═══════════════════════════════════════════════════════════════════
    // UPDATE
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Aggiorna la miccia e, una volta scaduta, avvia l'esplosione.</summary>
    public void Update(GameTime gameTime, TileMap map)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (!_isExploding)
        {
            _fuseTimer += dt;
            _animTimer += dt;

            if (_animTimer >= AnimSpeed)
            {
                _animTimer    = 0f;
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
                    _isFinished = true;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // ESPLOSIONE
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Calcola i tile colpiti dall'esplosione e rompe i breakable.
    /// I tile breakable bloccano la propagazione ma non vengono aggiunti
    /// agli ExplosionTiles (il bat nascosto lì è al sicuro).
    /// </summary>
    private void Explode(TileMap map)
    {
        _isExploding      = true;
        _currentAnimation = "explosion";
        _currentFrames    = _explosionAnimations[_currentAnimation];
        _currentFrame     = 0;
        _animTimer        = 0f;
        ExplosionTiles.Clear();
        Exploded?.Invoke(this, _bigBomb);

        Point center = new((int)(_position.X / TileMap.TileSize),
                           (int)(_position.Y / TileMap.TileSize));

        if (map.GetTile(center) != TileType.Wall)
        {
            ExplosionTiles.Add(center);
            if (map.GetTile(center) == TileType.Breakable)
                map.BreakTile(center);
        }

        int   range = (_bigBomb ? 2 : 1) + _extraRange;
        int[] dx    = { 0, 0, -1, 1 };
        int[] dy    = { -1, 1,  0,  0 };

        for (int dir = 0; dir < 4; dir++)
        {
            for (int step = 1; step <= range; step++)
            {
                Point t = new(center.X + dx[dir] * step,
                              center.Y + dy[dir] * step);

                if (!map.IsInside(t)) break;
                if (map.GetTile(t) == TileType.Wall) break;

                if (map.GetTile(t) == TileType.Breakable)
                {
                    map.BreakTile(t);
                    break;
                }

                ExplosionTiles.Add(t);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // DRAW
    // ═══════════════════════════════════════════════════════════════════

    public void Draw(SpriteBatch spriteBatch)
    {
        if (_isFinished) return;

        int safeFrame = Math.Min(_currentFrame, _currentFrames.Count - 1);

        if (_isExploding)
        {
            foreach (var tile in ExplosionTiles)
            {
                Vector2 drawPos = new(tile.X * TileMap.TileSize, tile.Y * TileMap.TileSize);
                spriteBatch.Draw(_explosionTexture, drawPos, _currentFrames[safeFrame], Color.White);
            }
        }
        else
        {
            spriteBatch.Draw(_bombTexture, _position, _currentFrames[safeFrame], Color.White);
        }
    }
}
