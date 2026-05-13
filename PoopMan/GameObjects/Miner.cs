using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PoopMan.UI;
using PoopManLibrary;
using PoopManLibrary.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace PoopMan.GameObjects
{
    public class Miner
    {
        // ═══════════════════════════════════════════════════════════════════
        // POSIZIONE E MOVIMENTO
        // ═══════════════════════════════════════════════════════════════════

        public Point   TilePosition;
        public Vector2 Position;

        private Vector2       _targetPosition;
        private Vector2       _currentDirection  = Vector2.UnitX;
        private float         _moveSpeed         = 160f;
        private bool          _isMoving          = false;
        private float         _movementProgress  = 0f;

        // Legacy body segments (non attivi)
        private List<(Vector2 from, Vector2 to)> _bodySegments = new();

        internal bool IsMoving => _isMoving;

        // Input buffer tile-per-tile
        private const int      MAX_BUFFER_SIZE = 2;
        private Queue<Vector2> _inputBuffer    = new(MAX_BUFFER_SIZE);

        // ═══════════════════════════════════════════════════════════════════
        // ANIMAZIONI
        // ═══════════════════════════════════════════════════════════════════

        private Texture2D                           _texture;
        private Dictionary<string, List<Rectangle>> _animations       = new();
        private string                              _currentAnimation = "idle_front";
        private List<Rectangle>                     _currentFrames;
        private int                                 _currentFrame     = 0;
        private float                               _animTimer        = 0f;
        private const float                         AnimSpeed         = 0.15f;

        private enum MinerState
        {
            IdleFront, IdleBack, IdleLeft, IdleRight,
            WalkFront, WalkBack, WalkLeft, WalkRight
        }
        private MinerState _state = MinerState.IdleFront;

        // ═══════════════════════════════════════════════════════════════════
        // MORTE
        // ═══════════════════════════════════════════════════════════════════

        private bool _isDead           = false;
        private bool _deathAnimFinished = false;

        internal bool IsDead                   => _isDead;
        internal bool IsDeathAnimationFinished => _deathAnimFinished;

        public event EventHandler? DeathAnimationFinished;

        // ═══════════════════════════════════════════════════════════════════
        // VITE
        // ═══════════════════════════════════════════════════════════════════

        private int _lives    = 3;
        private int _maxLives = 5;

        public int Lives    => _lives;
        public int MaxLives => _maxLives;

        public event EventHandler? NeedsRespawn;
        public event EventHandler? ExtraLifeEarned;

        private const int ExtraLifeEvery      = 1000;
        private int       _extraLifeThreshold = ExtraLifeEvery;

        /// <summary>
        /// Controlla se il punteggio ha raggiunto la soglia per una vita extra.
        /// Chiamare ogni frame da GameScene dopo aver aggiornato il punteggio.
        /// </summary>
        public void CheckExtraLife(int score)
        {
            if (_lives >= _maxLives) { _extraLifeThreshold = score + ExtraLifeEvery; return; }
            if (score >= _extraLifeThreshold)
            {
                _lives++;
                _extraLifeThreshold += ExtraLifeEvery;
                ExtraLifeEarned?.Invoke(this, EventArgs.Empty);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // INVINCIBILITÀ (attiva solo dopo aver perso una vita)
        // ═══════════════════════════════════════════════════════════════════

        private bool        _isInvincible         = false;
        private float       _invincibilityTimer   = 0f;
        private float       _invincibilityDuration = 3f;
        private float       _blinkTimer           = 0f;
        private const float BlinkInterval         = 0.1f;
        private bool        _blinkVisible         = true;

        public bool  IsInvincible      => _isInvincible;
        public float InvincibilityRatio =>
            _isInvincible ? Math.Clamp(_invincibilityTimer / _invincibilityDuration, 0f, 1f) : 0f;

        // ═══════════════════════════════════════════════════════════════════
        // BOMBE
        // ═══════════════════════════════════════════════════════════════════

        private int  _bigBombCount   = 0;
        private int  _maxActiveBombs = 3;

        public int BigBombCount  => _bigBombCount;
        public void AddBigBomb() => _bigBombCount++;
        private int MaxActiveBombs => _maxActiveBombs;

        private List<Bomb>                           _bombs              = new();
        private Texture2D                            _bombTexture;
        private Dictionary<string, List<Rectangle>>  _bombAnimations;
        private Texture2D                            _itemTexture;
        private Dictionary<string, List<Rectangle>>  _itemAnimations     = new();
        private Texture2D                            _explosionTexture;
        private Dictionary<string, List<Rectangle>>  _explosionAnimations = new();

        public event EventHandler?       BombPlaced;
        public event EventHandler<bool>? BombExploded;

        /// <summary>Tile colpiti da esplosioni attive.</summary>
        public IEnumerable<Point> ActiveExplosionTiles =>
            _bombs.Where(b => !b.IsFinished).SelectMany(b => b.ExplosionTiles);

        /// <summary>Tile dove si trova una bomba non ancora esplosa.</summary>
        public IEnumerable<Point> ActiveBombTiles =>
            _bombs.Where(b => !b.IsFinished)
                  .Select(b => new Point((int)(b.Position.X / TileMap.TileSize),
                                         (int)(b.Position.Y / TileMap.TileSize)));

        /// <summary>Tile occupate da bombe solide (il miner non può entrarci).</summary>
        public IEnumerable<Point> SolidBombTiles =>
            _bombs.Where(b => !b.IsFinished && !b.IsExploding)
                  .Select(b => new Point((int)(b.Position.X / TileMap.TileSize),
                                         (int)(b.Position.Y / TileMap.TileSize)))
                  .Where(t => t != VisualTilePosition);

        public bool IsSolidBombTile(Point tile) => SolidBombTiles.Contains(tile);

        // ═══════════════════════════════════════════════════════════════════
        // UPGRADE – OFFENSIVI
        // ═══════════════════════════════════════════════════════════════════

        private int   _bonusExplosionRange = 0;
        private float _bombTimerBonus      = 0f;
        private int   _explosionRangeSteps = 0;
        private int   _extraBombSteps      = 0;
        private int   _fasterBombSteps     = 0;
        private int   _chainSteps          = 0;

        public int   BonusExplosionRange  => _bonusExplosionRange;
        public float ChainExplosionChance { get; private set; } = 0f;
        public bool  UpgradeMultiHit      { get; private set; } = false;
        public bool  UpgradeCritical      { get; private set; } = false;

        // ═══════════════════════════════════════════════════════════════════
        // UPGRADE – MOVIMENTO
        // ═══════════════════════════════════════════════════════════════════

        private int   _moveSteps      = 0;
        private float _dashTimer      = 0f;
        private float _dashSpeedBonus = 0f;

        public bool UpgradeDashAfterHit { get; private set; } = false;

        // ═══════════════════════════════════════════════════════════════════
        // UPGRADE – DIFENSIVI
        // ═══════════════════════════════════════════════════════════════════

        private bool _shieldActive        = false;
        private int  _shieldRechargeLevel = 0;
        private const int ShieldRechargePeriod = 5;

        public bool UpgradeShield { get; private set; } = false;
        public bool ShieldActive  => _shieldActive;

        // ═══════════════════════════════════════════════════════════════════
        // UPGRADE – SPECIALI
        // ═══════════════════════════════════════════════════════════════════

        private int  _maxLifeSteps      = 0;
        private int  _doubleDropSteps   = 0;
        private bool _slowRegenActive   = false;
        private int  _regenLevelsAccum  = 0;

        public bool  UpgradeMagnet    { get; private set; } = false;
        public bool  UpgradeStunOnHit { get; private set; } = false;
        public bool  UpgradeSlowOnHit { get; private set; } = false;
        public float DoubleDropChance { get; private set; } = 0f;
        public float BonusLootChance  { get; private set; } = 0f;
        public bool  SlowRegenActive  => _slowRegenActive;

        // ═══════════════════════════════════════════════════════════════════
        // PROPRIETÀ DERIVATE
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Tile visivo del miner basato sulla posizione pixel interpolata.</summary>
        public Point VisualTilePosition =>
            new Point((int)Math.Round(Position.X / TileMap.TileSize),
                      (int)Math.Round(Position.Y / TileMap.TileSize));

        // ═══════════════════════════════════════════════════════════════════
        // COSTRUTTORE
        // ═══════════════════════════════════════════════════════════════════

        public Miner(Point startTile, string xmlPath, ContentManager content)
        {
            LoadAnimationsFromXml(xmlPath, content);

            TilePosition    = startTile;
            Position        = new Vector2(startTile.X * TileMap.TileSize, startTile.Y * TileMap.TileSize);
            _targetPosition = Position;

            string itemXml      = Path.Combine(content.RootDirectory, "image", "items", "items.xml");
            string explosionXml = Path.Combine(content.RootDirectory, "image", "fxs", "fsx.xml");
            LoadItemAnimations(itemXml, content);
            LoadExplosionAnimations(explosionXml, content);

            _bombAnimations = _itemAnimations;
            _bombTexture    = _itemTexture;
        }

        // ═══════════════════════════════════════════════════════════════════
        // UPDATE
        // ═══════════════════════════════════════════════════════════════════

        public void Update(TileMap map, GameTime gameTime)
        {
            if (_isDead) { UpdateAnimation(gameTime); return; }

            UpdateInvincibility(gameTime);
            HandleInput();
            UpdateDash(gameTime);
            HandleBombPlacement(map);
            UpdateBombs(gameTime, map);
            UpdateMovement(map, gameTime);
            UpdateAnimation(gameTime);
        }

        private void UpdateInvincibility(GameTime gameTime)
        {
            if (!_isInvincible) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _invincibilityTimer -= dt;
            _blinkTimer         -= dt;

            if (_blinkTimer <= 0f)
            {
                _blinkVisible = !_blinkVisible;
                _blinkTimer   = BlinkInterval;
            }

            if (_invincibilityTimer <= 0f)
            {
                _isInvincible = false;
                _blinkVisible = true;
            }
        }

        private void UpdateDash(GameTime gameTime)
        {
            if (_dashTimer > 0f)
                _dashTimer = Math.Max(0f, _dashTimer - (float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        private void HandleBombPlacement(TileMap map)
        {
            if (GameController.MiniBomb())
            {
                TryPlaceBomb(VisualTilePosition, big: false);
            }
            else if (GameController.BigBomb() && _bigBombCount > 0)
            {
                if (TryPlaceBomb(VisualTilePosition, big: true))
                    _bigBombCount--;
            }
        }

        private bool TryPlaceBomb(Point placeTile, bool big)
        {
            int  activeBombs  = _bombs.Count(b => !b.IsFinished);
            bool tileOccupied = _bombs.Any(b => !b.IsFinished &&
                new Point((int)(b.Position.X / TileMap.TileSize),
                          (int)(b.Position.Y / TileMap.TileSize)) == placeTile);

            if (tileOccupied || activeBombs >= MaxActiveBombs) return false;

            var bomb = new Bomb(
                new Vector2(placeTile.X * TileMap.TileSize, placeTile.Y * TileMap.TileSize),
                _bombTexture, _bombAnimations, _explosionTexture, _explosionAnimations,
                big, _bonusExplosionRange, _bombTimerBonus);
            bomb.Exploded += (s, isBig) => BombExploded?.Invoke(this, isBig);
            _bombs.Add(bomb);
            BombPlaced?.Invoke(this, EventArgs.Empty);
            return true;
        }

        private void UpdateBombs(GameTime gameTime, TileMap map)
        {
            for (int i = _bombs.Count - 1; i >= 0; i--)
            {
                _bombs[i].Update(gameTime, map);
                if (_bombs[i].IsFinished) _bombs.RemoveAt(i);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // MOVIMENTO
        // ═══════════════════════════════════════════════════════════════════

        private void UpdateMovement(TileMap map, GameTime gameTime)
        {
            if (!_isMoving && _inputBuffer.Count > 0)
            {
                _currentDirection = _inputBuffer.Dequeue();
                Point nextTile    = new(TilePosition.X + (int)_currentDirection.X,
                                        TilePosition.Y + (int)_currentDirection.Y);

                if (map.IsWalkable(nextTile) && !IsSolidBombTile(nextTile))
                {
                    // Aggiorna segmenti corpo (legacy)
                    for (int i = _bodySegments.Count - 1; i > 0; i--)
                        _bodySegments[i] = (_bodySegments[i].to, _bodySegments[i - 1].to);
                    if (_bodySegments.Count > 0)
                        _bodySegments[0] = (_bodySegments[0].to, _targetPosition);

                    TilePosition      = nextTile;
                    _targetPosition   = new Vector2(nextTile.X * TileMap.TileSize, nextTile.Y * TileMap.TileSize);
                    _isMoving         = true;
                    _currentFrame     = 0;
                    _animTimer        = 0f;
                    _movementProgress = 0f;

                    _state = _currentDirection switch
                    {
                        var d when d ==  Vector2.UnitX => MinerState.WalkRight,
                        var d when d == -Vector2.UnitX => MinerState.WalkLeft,
                        var d when d == -Vector2.UnitY => MinerState.WalkBack,
                        _                              => MinerState.WalkFront
                    };
                }
                else
                {
                    _state = _state switch
                    {
                        MinerState.WalkFront => MinerState.IdleFront,
                        MinerState.WalkBack  => MinerState.IdleBack,
                        MinerState.WalkLeft  => MinerState.IdleLeft,
                        MinerState.WalkRight => MinerState.IdleRight,
                        _                    => _state
                    };
                }
            }

            if (_isMoving)
            {
                float effectiveSpeed = _moveSpeed + (_dashTimer > 0f ? _dashSpeedBonus : 0f);
                float distance       = effectiveSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
                Vector2 dir          = _targetPosition - Position;

                if (dir.Length() <= distance)
                {
                    Position          = _targetPosition;
                    _isMoving         = false;
                    _movementProgress = 1f;
                    _state = _state switch
                    {
                        MinerState.WalkFront => MinerState.IdleFront,
                        MinerState.WalkBack  => MinerState.IdleBack,
                        MinerState.WalkLeft  => MinerState.IdleLeft,
                        MinerState.WalkRight => MinerState.IdleRight,
                        _                    => _state
                    };
                }
                else
                {
                    Position          += Vector2.Normalize(dir) * distance;
                    _movementProgress  = MathHelper.Clamp(_movementProgress + distance / TileMap.TileSize, 0f, 1f);
                }
            }
        }

        private void HandleInput()
        {
            var dir = Vector2.Zero;

            if (GameController.HoldUp())    dir = -Vector2.UnitY;
            if (GameController.HoldDown())  dir =  Vector2.UnitY;
            if (GameController.HoldLeft())  dir = -Vector2.UnitX;
            if (GameController.HoldRight()) dir =  Vector2.UnitX;

            if (dir == Vector2.Zero) { _inputBuffer.Clear(); return; }

            var last = _inputBuffer.Count > 0 ? _inputBuffer.Last() : _currentDirection;

            if (last != dir)
            {
                _inputBuffer.Clear();
                _inputBuffer.Enqueue(dir);
            }
            else if (_inputBuffer.Count < MAX_BUFFER_SIZE)
            {
                _inputBuffer.Enqueue(dir);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // ANIMAZIONI
        // ═══════════════════════════════════════════════════════════════════

        private void UpdateAnimation(GameTime gameTime)
        {
            if (_isDead)
            {
                if (_animations.ContainsKey("dead") && _currentAnimation != "dead")
                {
                    _currentAnimation = "dead";
                    _currentFrames    = _animations["dead"];
                    _currentFrame     = 0;
                    _animTimer        = 0f;
                }

                if (_currentFrames == null || _currentFrames.Count == 0 || _deathAnimFinished) return;

                _animTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_animTimer >= AnimSpeed)
                {
                    _animTimer = 0f;
                    _currentFrame++;
                    if (_currentFrame >= _currentFrames.Count)
                    {
                        _currentFrame      = _currentFrames.Count - 1;
                        _deathAnimFinished = true;
                        DeathAnimationFinished?.Invoke(this, EventArgs.Empty);
                    }
                }
                return;
            }

            string newAnim = AnimNameOf(_state);
            if (newAnim != _currentAnimation)
            {
                _currentAnimation = newAnim;
                _currentFrames    = _animations.GetValueOrDefault(newAnim);
                _currentFrame     = 0;
                _animTimer        = 0f;
            }

            if (_currentFrames?.Count > 1)
            {
                _animTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_animTimer >= AnimSpeed)
                {
                    _animTimer    = 0f;
                    _currentFrame = (_currentFrame + 1) % _currentFrames.Count;
                }
            }
            else
            {
                _currentFrame = 0;
            }
        }

        private static string AnimNameOf(MinerState s) => s switch
        {
            MinerState.IdleFront => "idle_front",
            MinerState.IdleBack  => "idle_back",
            MinerState.IdleLeft  => "idle_left",
            MinerState.IdleRight => "idle_right",
            MinerState.WalkFront => "walk_front",
            MinerState.WalkBack  => "walk_back",
            MinerState.WalkLeft  => "walk_left",
            MinerState.WalkRight => "walk_right",
            _                    => "idle_front"
        };

        // ═══════════════════════════════════════════════════════════════════
        // DRAW
        // ═══════════════════════════════════════════════════════════════════

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_currentFrames == null || _currentFrames.Count == 0) return;
            if (_currentFrame >= _currentFrames.Count) _currentFrame = 0;

            foreach (var bomb in _bombs) bomb.Draw(spriteBatch);

            if (_isInvincible && !_blinkVisible) return;

            spriteBatch.Draw(_texture, Position, _currentFrames[_currentFrame], Color.White);
        }

        // ═══════════════════════════════════════════════════════════════════
        // VITA / MORTE / RESPAWN
        // ═══════════════════════════════════════════════════════════════════

        public void Kill()
        {
            if (_isDead) return;

            _lives--;

            if (_lives > 0)
            {
                _isMoving         = false;
                _movementProgress = 0f;
                _inputBuffer.Clear();
                NeedsRespawn?.Invoke(this, EventArgs.Empty);
                return;
            }

            _isDead            = true;
            _isMoving          = false;
            _movementProgress  = 0f;
            _deathAnimFinished = false;

            if (_animations.ContainsKey("dead"))
            {
                _currentAnimation = "dead";
                _currentFrames    = _animations["dead"];
                if (_currentFrames?.Count <= 1)
                {
                    _deathAnimFinished = true;
                    DeathAnimationFinished?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                string fallback = new[] { "idle_front", "idle_back", "idle_left", "idle_right", "idle" }
                    .FirstOrDefault(k => _animations.ContainsKey(k))
                    ?? _animations.Keys.FirstOrDefault();

                if (fallback != null)
                {
                    _currentAnimation = fallback;
                    _currentFrames    = _animations[fallback];
                }

                _deathAnimFinished = true;
                DeathAnimationFinished?.Invoke(this, EventArgs.Empty);
            }

            _currentFrame = 0;
            _animTimer    = 0f;
        }

        /// <summary>Riposiziona il miner dopo la perdita di una vita e attiva l'invincibilità.</summary>
        public void Respawn(Point spawnTile)
        {
            ResetPosition(spawnTile);
            _isInvincible       = true;
            _invincibilityTimer = _invincibilityDuration;
            _blinkTimer         = BlinkInterval;
            _blinkVisible       = true;
        }

        /// <summary>Reset per cambio livello: azzera posizione e bombe, nessuna invincibilità.</summary>
        public void ResetForNewLevel(Point spawnTile)
        {
            ResetPosition(spawnTile);
            _bombs.Clear();
            _isInvincible = false;
            _blinkVisible = true;
        }

        private void ResetPosition(Point spawnTile)
        {
            TilePosition      = spawnTile;
            Position          = new Vector2(spawnTile.X * TileMap.TileSize, spawnTile.Y * TileMap.TileSize);
            _targetPosition   = Position;
            _isMoving         = false;
            _movementProgress = 0f;
            _inputBuffer.Clear();
            _state            = MinerState.IdleFront;
            _currentAnimation = "idle_front";
            _currentFrames    = _animations["idle_front"];
            _currentFrame     = 0;
            _animTimer        = 0f;
        }

        public Collision GetBounds()
        {
            if (!_animations.TryGetValue(_currentAnimation, out var frames) || frames.Count == 0)
                return Collision.Empty;

            var frame  = frames[Math.Min(_currentFrame, frames.Count - 1)];
            int radius = (int)(frame.Width * 0.20f);
            return new Collision(
                (int)(Position.X + frame.Width  * 0.5f),
                (int)(Position.Y + frame.Height * 0.5f),
                radius);
        }

        // ═══════════════════════════════════════════════════════════════════
        // UPGRADE
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Applica il potenziamento scelto nel menu upgrade.</summary>
        public void ApplyUpgrade(UpgradeType upgrade)
        {
            switch (upgrade)
            {
                // ── Vita ──────────────────────────────────────────────────
                case UpgradeType.ExtraLife:
                    if (_lives < _maxLives) { _lives++; ExtraLifeEarned?.Invoke(this, EventArgs.Empty); }
                    break;

                case UpgradeType.MaxLifeUp:
                    if (_maxLifeSteps < UpgradeRegistry.MaxLifeSteps)
                    {
                        _maxLifeSteps++;
                        _maxLives++;
                        _lives = Math.Min(_lives + 1, _maxLives);
                        ExtraLifeEarned?.Invoke(this, EventArgs.Empty);
                    }
                    break;

                case UpgradeType.SlowRegen:
                    _regenLevelsAccum = 0;
                    _slowRegenActive  = true;
                    break;

                // ── Offensivi ─────────────────────────────────────────────
                case UpgradeType.IncreasedDamage:
                case UpgradeType.BiggerBlast:
                    if (_explosionRangeSteps < UpgradeRegistry.MaxExplosionRange)
                    { _explosionRangeSteps++; _bonusExplosionRange++; }
                    break;

                case UpgradeType.FasterBomb:
                    if (_fasterBombSteps < UpgradeRegistry.MaxFasterBombSteps)
                    { _fasterBombSteps++; _bombTimerBonus += 0.4f; }
                    break;

                case UpgradeType.ExtraBomb:
                    if (_extraBombSteps < UpgradeRegistry.MaxExtraBombs)
                    { _extraBombSteps++; _maxActiveBombs++; }
                    break;

                case UpgradeType.ChainExplosion:
                    if (_chainSteps < UpgradeRegistry.MaxChainSteps)
                    { _chainSteps++; ChainExplosionChance = Math.Min(ChainExplosionChance + 0.15f, 0.60f); }
                    break;

                // ── Movimento ─────────────────────────────────────────────
                case UpgradeType.FasterMovement:
                    if (_moveSteps < UpgradeRegistry.MaxMoveSteps)
                    { _moveSteps++; _moveSpeed = Math.Min(_moveSpeed + 20f, 280f); }
                    break;

                case UpgradeType.DashAfterHit:
                    UpgradeDashAfterHit = true;
                    break;

                // ── Difensivi ─────────────────────────────────────────────
                case UpgradeType.ExplosionResistance:
                    _invincibilityDuration = Math.Min(_invincibilityDuration + 1f, UpgradeRegistry.MaxInvincibility);
                    break;

                case UpgradeType.DamageReduction:
                    _invincibilityDuration = Math.Min(_invincibilityDuration + 0.5f, UpgradeRegistry.MaxInvincibility);
                    break;

                case UpgradeType.Shield:
                    UpgradeShield = true;
                    _shieldActive = true;
                    break;

                // ── Speciali ──────────────────────────────────────────────
                case UpgradeType.MultiHit:       UpgradeMultiHit  = true; break;
                case UpgradeType.CriticalChance: UpgradeCritical  = true; break;
                case UpgradeType.Magnet:         UpgradeMagnet    = true; break;
                case UpgradeType.StunOnHit:      UpgradeStunOnHit = true; break;
                case UpgradeType.SlowOnHit:      UpgradeSlowOnHit = true; break;

                case UpgradeType.BonusLoot:
                    BonusLootChance = Math.Min(BonusLootChance + 0.15f, 0.60f);
                    break;

                case UpgradeType.DoubleDrop:
                    if (_doubleDropSteps < UpgradeRegistry.MaxDoubleDropSteps)
                    { _doubleDropSteps++; DoubleDropChance = Math.Min(DoubleDropChance + 0.15f, 0.60f); }
                    break;
            }
        }

        /// <summary>Chiamato da GameScene a ogni avanzamento di livello.</summary>
        public void NotifyLevelUp()
        {
            // Rigenerazione lenta: +1 vita ogni 10 livelli
            if (_slowRegenActive)
            {
                _regenLevelsAccum++;
                if (_regenLevelsAccum >= 10)
                {
                    _regenLevelsAccum = 0;
                    if (_lives < _maxLives) { _lives++; ExtraLifeEarned?.Invoke(this, EventArgs.Empty); }
                }
            }

            // Ricarica scudo ogni ShieldRechargePeriod livelli
            if (UpgradeShield && !_shieldActive)
            {
                _shieldRechargeLevel++;
                if (_shieldRechargeLevel >= ShieldRechargePeriod)
                {
                    _shieldRechargeLevel = 0;
                    _shieldActive        = true;
                }
            }
        }

        /// <summary>Tenta di assorbire un colpo con lo scudo. Restituisce true se assorbito.</summary>
        public bool TryAbsorbWithShield()
        {
            if (!_shieldActive) return false;
            _shieldActive        = false;
            _shieldRechargeLevel = 0;
            return true;
        }

        /// <summary>Attiva il boost di velocità post-danno se l'upgrade DashAfterHit è presente.</summary>
        public void TriggerDashAfterHit()
        {
            if (!UpgradeDashAfterHit) return;
            _dashTimer      = 3f;
            _dashSpeedBonus = _moveSpeed * 0.4f;
        }

        // ═══════════════════════════════════════════════════════════════════
        // CARICAMENTO RISORSE
        // ═══════════════════════════════════════════════════════════════════

        private void LoadAnimationsFromXml(string xmlPath, ContentManager content)
        {
            if (!File.Exists(xmlPath))
                throw new FileNotFoundException($"File XML animazioni non trovato: {xmlPath}");

            XDocument doc = XDocument.Load(xmlPath);
            _animations.Clear();
            foreach (var region in doc.Descendants("Region"))
                AddFrameToDict(_animations, region);

            var texturePath = doc.Descendants("Texture").FirstOrDefault()?.Value ?? "image/character/miner";
            _texture        = content.Load<Texture2D>(texturePath);
            _currentFrames  = _animations["idle_front"];
        }

        private void LoadItemAnimations(string xmlPath, ContentManager content)
        {
            if (!File.Exists(xmlPath))
                throw new FileNotFoundException($"File XML item non trovato: {xmlPath}");

            XDocument doc = XDocument.Load(xmlPath);
            _itemAnimations.Clear();
            foreach (var region in doc.Descendants("Region"))
                AddFrameToDict(_itemAnimations, region, allowEmpty: true);

            var texturePath = doc.Descendants("Texture").FirstOrDefault()?.Value ?? "image/items/items";
            _itemTexture    = content.Load<Texture2D>(texturePath);
        }

        private void LoadExplosionAnimations(string xmlPath, ContentManager content)
        {
            if (!File.Exists(xmlPath))
                throw new FileNotFoundException($"File XML esplosioni non trovato: {xmlPath}");

            XDocument doc = XDocument.Load(xmlPath);
            _explosionAnimations.Clear();
            foreach (var region in doc.Descendants("Region"))
                AddFrameToDict(_explosionAnimations, region, allowEmpty: true);

            var texturePath   = doc.Descendants("Texture").FirstOrDefault()?.Value ?? "image/fxs/fsx";
            _explosionTexture = content.Load<Texture2D>(texturePath);
        }

        /// <summary>Aggiunge un frame XML a un dizionario di animazioni raggruppando per nome base.</summary>
        private static void AddFrameToDict(
            Dictionary<string, List<Rectangle>> dict,
            XElement region,
            bool allowEmpty = false)
        {
            string fullName = region.Attribute("Name")?.Value ?? "";
            int x      = int.Parse(region.Attribute("X")?.Value      ?? "0");
            int y      = int.Parse(region.Attribute("Y")?.Value      ?? "0");
            int width  = int.Parse(region.Attribute("Width")?.Value  ?? "32");
            int height = int.Parse(region.Attribute("Height")?.Value ?? "32");

            int i = fullName.Length;
            while (i > 0 && char.IsDigit(fullName[i - 1])) i--;
            string animName = fullName[..i].TrimEnd('_', '-', ' ');

            if (string.IsNullOrEmpty(animName))
            {
                if (!allowEmpty) return;
                animName = fullName;
            }

            if (!dict.ContainsKey(animName)) dict[animName] = new List<Rectangle>();
            var rect = new Rectangle(x, y, width, height);
            if (!dict[animName].Contains(rect)) dict[animName].Add(rect);
        }
    }
}
