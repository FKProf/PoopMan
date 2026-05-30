using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PoopManLibrary;
using PoopManLibrary.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace PoopMan.GameObjects;

public class Miner
{
    // Input buffer tile-per-tile
    private const int MAX_BUFFER_SIZE = 2;
    private const float AnimSpeed = 0.15f;

    private const int ExtraLifeEvery = 1000;
    private const float BlinkInterval = 0.1f;
    private const int ShieldRechargePeriod = 3;
    private readonly Dictionary<string, List<Rectangle>> _animations = new();

    // Legacy body segments (non attivi)
    private readonly List<(Vector2 from, Vector2 to)> _bodySegments = new();
    private readonly Dictionary<string, List<Rectangle>> _bombAnimations;

    private readonly List<Bomb> _bombs = new();
    private readonly Texture2D _bombTexture;
    private readonly Dictionary<string, List<Rectangle>> _explosionAnimations = new();
    private readonly Queue<Vector2> _inputBuffer = new(MAX_BUFFER_SIZE);
    private readonly Dictionary<string, List<Rectangle>> _itemAnimations = new();
    // ═══════════════════════════════════════════════════════════════════
    // ANIMAZIONI
    // ═══════════════════════════════════════════════════════════════════

    private float _animTimer;
    private string _currentAnimation = "idle_front";
    private int _currentFrame;
    private List<Rectangle> _currentFrames;
    private Texture2D _texture;
    private Texture2D _explosionTexture;
    private Texture2D _itemTexture;

    // ═══════════════════════════════════════════════════════════════════
    // POSIZIONE E MOVIMENTO
    // ═══════════════════════════════════════════════════════════════════

    public Vector2 Position;
    public Point TilePosition;

    // Tile occupate dai bat (aggiornate da GameScene prima di Update)
    private HashSet<Point> _batBlockedTiles = new();
    private Vector2 _currentDirection = Vector2.UnitX;
    private float _movementProgress;
    private float _moveSpeed = 160f;
    private Vector2 _targetPosition;
    private float _dashSpeedBonus;
    private float _dashTimer;

    // ═══════════════════════════════════════════════════════════════════
    // BOMBE
    // ═══════════════════════════════════════════════════════════════════

    private float _bombTimerBonus;
    private int _extraBombSteps;
    private int _fasterBombSteps;

    // ═══════════════════════════════════════════════════════════════════
    // VITE E MORTE
    // ═══════════════════════════════════════════════════════════════════

    private int _maxLifeSteps;
    private int _extraLifeThreshold = ExtraLifeEvery;
    private float _blinkTimer;
    private bool _blinkVisible = true;

    // ═══════════════════════════════════════════════════════════════════
    // INVINCIBILITÀ (attiva solo dopo aver perso una vita)
    // ═══════════════════════════════════════════════════════════════════

    private float _invincibilityDuration = 3f;
    private float _invincibilityTimer;

    // ═══════════════════════════════════════════════════════════════════
    // UPGRADE – OFFENSIVI
    // ═══════════════════════════════════════════════════════════════════

    private int _chainSteps;
    private int _doubleDropSteps;
    private int _explosionDamageSteps;
    private int _explosionRangeSteps;

    // ═══════════════════════════════════════════════════════════════════
    // UPGRADE – DIFENSIVI
    // ═══════════════════════════════════════════════════════════════════

    private int _damageReductionSteps;
    private int _explosionResistanceSteps;
    private int _shieldRechargeLevel;

    // ═══════════════════════════════════════════════════════════════════
    // UPGRADE – MOVIMENTO
    // ═══════════════════════════════════════════════════════════════════

    private int _moveSteps;

    // ═══════════════════════════════════════════════════════════════════
    // UPGRADE – SPECIALI
    // ═══════════════════════════════════════════════════════════════════

    private int _regenLevelsAccum;

    // ═══════════════════════════════════════════════════════════════════
    // STATO
    // ═══════════════════════════════════════════════════════════════════

    private MinerState _state = MinerState.IdleFront;

    // ═══════════════════════════════════════════════════════════════════
    // COSTRUTTORE
    // ═══════════════════════════════════════════════════════════════════

    public Miner(Point startTile, string xmlPath, ContentManager content)
    {
        LoadAnimationsFromXml(xmlPath, content);

        TilePosition = startTile;
        Position = new Vector2(startTile.X * TileMap.TileSize, startTile.Y * TileMap.TileSize);
        _targetPosition = Position;

        var itemXml = Path.Combine(content.RootDirectory, "image", "items", "items.xml");
        var explosionXml = Path.Combine(content.RootDirectory, "image", "fxs", "fsx.xml");
        LoadItemAnimations(itemXml, content);
        LoadExplosionAnimations(explosionXml, content);

        _bombAnimations = _itemAnimations;
        _bombTexture = _itemTexture;
    }

    internal bool IsMoving { get; private set; }

    internal bool IsDead { get; private set; }

    internal bool IsDeathAnimationFinished { get; private set; }

    public int Lives { get; private set; } = 3;

    public int MaxLives { get; private set; } = 5;

    public bool IsInvincible { get; private set; }

    public float InvincibilityRatio =>
        IsInvincible ? Math.Clamp(_invincibilityTimer / _invincibilityDuration, 0f, 1f) : 0f;

    public int BigBombCount { get; private set; }

    private int MaxActiveBombs { get; set; } = 3;

    /// <summary>Tile colpiti da esplosioni attive.</summary>
    public IEnumerable<Point> ActiveExplosionTiles =>
        _bombs.Where(b => !b.IsFinished).SelectMany(b => b.ExplosionTiles);

    /// <summary>Bombe che sono appena esplose ma il cui danno non è ancora stato applicato.</summary>
    internal IEnumerable<Bomb> FreshExplosions =>
        _bombs.Where(b => b.IsExploding && !b.IsFinished && !b.DamageApplied);

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

    public int BonusExplosionRange { get; private set; }

    public float ChainExplosionChance { get; private set; }
    public bool UpgradeMultiHit { get; private set; }
    public bool UpgradeCritical { get; private set; }

    /// <summary>Danno aggiuntivo inflitto dalle bombe normali (1 extra per ogni livello upgrade).</summary>
    public int ExplosionDamageBonus => _explosionDamageSteps;

    public bool UpgradeDashAfterHit { get; private set; }

    public bool UpgradeShield { get; private set; }
    public bool ShieldActive { get; private set; }

    public bool UpgradeMagnet { get; private set; }
    public bool UpgradeStunOnHit { get; private set; }
    public bool UpgradeSlowOnHit { get; private set; }
    public float DoubleDropChance { get; private set; }
    public float BonusLootChance { get; private set; }
    public bool SlowRegenActive { get; private set; }

    // ── Mythic ──────────────────────────────────────────────────
    /// <summary>Se attivo, il Miner non subisce danni dalle proprie esplosioni.</summary>
    public bool UpgradeMythicImmortality { get; private set; }

    /// <summary>Se attivo, le bombe piccole del Miner uccidono istantaneamente qualsiasi bat.</summary>
    public bool UpgradeInstantKill { get; private set; }

    // ═══════════════════════════════════════════════════════════════════
    // PROPRIETÀ DERIVATE
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Tile visivo del miner basato sulla posizione pixel interpolata.</summary>
    public Point VisualTilePosition =>
        new((int)Math.Round(Position.X / TileMap.TileSize),
            (int)Math.Round(Position.Y / TileMap.TileSize));

    public event EventHandler? DeathAnimationFinished;

    public event EventHandler? NeedsRespawn;
    public event EventHandler? ExtraLifeEarned;

    /// <summary>
    ///     Controlla se il punteggio ha raggiunto la soglia per una vita extra.
    ///     Chiamare ogni frame da GameScene dopo aver aggiornato il punteggio.
    /// </summary>
    public void CheckExtraLife(int score)
    {
        if (Lives >= MaxLives)
        {
            _extraLifeThreshold = score + ExtraLifeEvery;
            return;
        }

        if (score >= _extraLifeThreshold)
        {
            Lives++;
            _extraLifeThreshold += ExtraLifeEvery;
            ExtraLifeEarned?.Invoke(this, EventArgs.Empty);
        }
    }

    public void AddBigBomb()
    {
        BigBombCount++;
    }

    public event EventHandler? BombPlaced;
    public event EventHandler<bool>? BombExploded;

    public bool IsSolidBombTile(Point tile)
    {
        return SolidBombTiles.Contains(tile);
    }

    public void SetBatTiles(IEnumerable<Point> batTiles)
    {
        _batBlockedTiles = new HashSet<Point>(batTiles);
    }

    /// <summary>
    ///     Restituisce il livello attuale di un upgrade (0 = mai preso).
    ///     Usato da GameScene per mostrare il livello nella UI e per filtrare il menu upgrade.
    /// </summary>
    public int GetUpgradeLevel(UpgradeType type)
    {
        return type switch
        {
            UpgradeType.IncreasedDamage => _explosionRangeSteps,
            UpgradeType.ExplosionDamage => _explosionDamageSteps,
            UpgradeType.FasterBomb => _fasterBombSteps,
            UpgradeType.ExtraBomb => _extraBombSteps,
            UpgradeType.ChainExplosion => _chainSteps,
            UpgradeType.FasterMovement => _moveSteps,
            UpgradeType.MaxLifeUp => _maxLifeSteps,
            UpgradeType.DoubleDrop => _doubleDropSteps,
            UpgradeType.BonusLoot => (int)Math.Round(BonusLootChance / 0.15f),
            UpgradeType.ExplosionResistance => _explosionResistanceSteps,
            UpgradeType.DamageReduction => _damageReductionSteps,
            UpgradeType.DashAfterHit => UpgradeDashAfterHit ? 1 : 0,
            UpgradeType.Shield => UpgradeShield ? 1 : 0,
            UpgradeType.MultiHit => UpgradeMultiHit ? 1 : 0,
            UpgradeType.CriticalChance => UpgradeCritical ? 1 : 0,
            UpgradeType.Magnet => UpgradeMagnet ? 1 : 0,
            UpgradeType.StunOnHit => UpgradeStunOnHit ? 1 : 0,
            UpgradeType.SlowOnHit => UpgradeSlowOnHit ? 1 : 0,
            UpgradeType.SlowRegen => SlowRegenActive ? 1 : 0,
            UpgradeType.MythicImmortality => UpgradeMythicImmortality ? 1 : 0,
            UpgradeType.InstantKill => UpgradeInstantKill ? 1 : 0,
            _ => 0
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // UPDATE
    // ═══════════════════════════════════════════════════════════════════

    public void Update(TileMap map, GameTime gameTime)
    {
        if (IsDead)
        {
            UpdateAnimation(gameTime);
            return;
        }

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
        if (!IsInvincible) return;

        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _invincibilityTimer -= dt;
        _blinkTimer -= dt;

        if (_blinkTimer <= 0f)
        {
            _blinkVisible = !_blinkVisible;
            _blinkTimer = BlinkInterval;
        }

        if (_invincibilityTimer <= 0f)
        {
            IsInvincible = false;
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
            TryPlaceBomb(VisualTilePosition, false);
        else if (GameController.BigBomb() && BigBombCount > 0)
            if (TryPlaceBomb(VisualTilePosition, true))
                BigBombCount--;
    }

    private bool TryPlaceBomb(Point placeTile, bool big)
    {
        var activeBombs = _bombs.Count(b => !b.IsFinished);
        var tileOccupied = _bombs.Any(b => !b.IsFinished &&
                                           new Point((int)(b.Position.X / TileMap.TileSize),
                                               (int)(b.Position.Y / TileMap.TileSize)) == placeTile);

        if (tileOccupied || activeBombs >= MaxActiveBombs) return false;

        var bomb = new Bomb(
            new Vector2(placeTile.X * TileMap.TileSize, placeTile.Y * TileMap.TileSize),
            _bombTexture, _bombAnimations, _explosionTexture, _explosionAnimations,
            big, BonusExplosionRange, _bombTimerBonus, UpgradeMultiHit);
        bomb.Exploded += (s, isBig) => BombExploded?.Invoke(this, isBig);
        _bombs.Add(bomb);
        BombPlaced?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void UpdateBombs(GameTime gameTime, TileMap map)
    {
        for (var i = _bombs.Count - 1; i >= 0; i--)
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
        if (!IsMoving && _inputBuffer.Count > 0)
        {
            _currentDirection = _inputBuffer.Dequeue();
            Point nextTile = new(TilePosition.X + (int)_currentDirection.X,
                TilePosition.Y + (int)_currentDirection.Y);

            if (map.IsWalkable(nextTile) && !IsSolidBombTile(nextTile))
            {
                // Aggiorna segmenti corpo (legacy)
                for (var i = _bodySegments.Count - 1; i > 0; i--)
                    _bodySegments[i] = (_bodySegments[i].to, _bodySegments[i - 1].to);
                if (_bodySegments.Count > 0)
                    _bodySegments[0] = (_bodySegments[0].to, _targetPosition);

                TilePosition = nextTile;
                _targetPosition = new Vector2(nextTile.X * TileMap.TileSize, nextTile.Y * TileMap.TileSize);
                IsMoving = true;
                _currentFrame = 0;
                _animTimer = 0f;
                _movementProgress = 0f;

                _state = _currentDirection switch
                {
                    var d when d == Vector2.UnitX => MinerState.WalkRight,
                    var d when d == -Vector2.UnitX => MinerState.WalkLeft,
                    var d when d == -Vector2.UnitY => MinerState.WalkBack,
                    _ => MinerState.WalkFront
                };
            }
            else
            {
                _state = _state switch
                {
                    MinerState.WalkFront => MinerState.IdleFront,
                    MinerState.WalkBack => MinerState.IdleBack,
                    MinerState.WalkLeft => MinerState.IdleLeft,
                    MinerState.WalkRight => MinerState.IdleRight,
                    _ => _state
                };
            }
        }

        if (IsMoving)
        {
            var effectiveSpeed = _moveSpeed + (_dashTimer > 0f ? _dashSpeedBonus : 0f);
            var distance = effectiveSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
            var dir = _targetPosition - Position;

            if (dir.Length() <= distance)
            {
                Position = _targetPosition;
                IsMoving = false;
                _movementProgress = 1f;
                _state = _state switch
                {
                    MinerState.WalkFront => MinerState.IdleFront,
                    MinerState.WalkBack => MinerState.IdleBack,
                    MinerState.WalkLeft => MinerState.IdleLeft,
                    MinerState.WalkRight => MinerState.IdleRight,
                    _ => _state
                };
            }
            else
            {
                Position += Vector2.Normalize(dir) * distance;
                _movementProgress = MathHelper.Clamp(_movementProgress + distance / TileMap.TileSize, 0f, 1f);
            }
        }
    }

    private void HandleInput()
    {
        var dir = Vector2.Zero;

        if (GameController.HoldUp()) dir = -Vector2.UnitY;
        if (GameController.HoldDown()) dir = Vector2.UnitY;
        if (GameController.HoldLeft()) dir = -Vector2.UnitX;
        if (GameController.HoldRight()) dir = Vector2.UnitX;

        if (dir == Vector2.Zero)
        {
            _inputBuffer.Clear();
            return;
        }

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
        if (IsDead)
        {
            if (_animations.ContainsKey("dead") && _currentAnimation != "dead")
            {
                _currentAnimation = "dead";
                _currentFrames = _animations["dead"];
                _currentFrame = 0;
                _animTimer = 0f;
            }

            if (_currentFrames == null || _currentFrames.Count == 0 || IsDeathAnimationFinished) return;

            _animTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_animTimer >= AnimSpeed)
            {
                _animTimer = 0f;
                _currentFrame++;
                if (_currentFrame >= _currentFrames.Count)
                {
                    _currentFrame = _currentFrames.Count - 1;
                    IsDeathAnimationFinished = true;
                    DeathAnimationFinished?.Invoke(this, EventArgs.Empty);
                }
            }

            return;
        }

        var newAnim = AnimNameOf(_state);
        if (newAnim != _currentAnimation)
        {
            _currentAnimation = newAnim;
            _currentFrames = _animations.GetValueOrDefault(newAnim);
            _currentFrame = 0;
            _animTimer = 0f;
        }

        if (_currentFrames?.Count > 1)
        {
            _animTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_animTimer >= AnimSpeed)
            {
                _animTimer = 0f;
                _currentFrame = (_currentFrame + 1) % _currentFrames.Count;
            }
        }
        else
        {
            _currentFrame = 0;
        }
    }

    private static string AnimNameOf(MinerState s)
    {
        return s switch
        {
            MinerState.IdleFront => "idle_front",
            MinerState.IdleBack => "idle_back",
            MinerState.IdleLeft => "idle_left",
            MinerState.IdleRight => "idle_right",
            MinerState.WalkFront => "walk_front",
            MinerState.WalkBack => "walk_back",
            MinerState.WalkLeft => "walk_left",
            MinerState.WalkRight => "walk_right",
            _ => "idle_front"
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // DRAW
    // ═══════════════════════════════════════════════════════════════════

    public void Draw(SpriteBatch spriteBatch)
    {
        if (_currentFrames == null || _currentFrames.Count == 0) return;
        if (_currentFrame >= _currentFrames.Count) _currentFrame = 0;

        foreach (var bomb in _bombs) bomb.Draw(spriteBatch);

        if (IsInvincible && !_blinkVisible) return;

        spriteBatch.Draw(_texture, Position, _currentFrames[_currentFrame], Color.White);
    }

    // ═══════════════════════════════════════════════════════════════════
    // VITA / MORTE / RESPAWN
    // ═══════════════════════════════════════════════════════════════════

    public void Kill()
    {
        if (IsDead) return;

        Lives--;

        if (Lives > 0)
        {
            IsMoving = false;
            _movementProgress = 0f;
            _inputBuffer.Clear();
            NeedsRespawn?.Invoke(this, EventArgs.Empty);
            return;
        }

        IsDead = true;
        IsMoving = false;
        _movementProgress = 0f;
        IsDeathAnimationFinished = false;

        if (_animations.ContainsKey("dead"))
        {
            _currentAnimation = "dead";
            _currentFrames = _animations["dead"];
            if (_currentFrames?.Count <= 1)
            {
                IsDeathAnimationFinished = true;
                DeathAnimationFinished?.Invoke(this, EventArgs.Empty);
            }
        }
        else
        {
            var fallback = new[] { "idle_front", "idle_back", "idle_left", "idle_right", "idle" }
                               .FirstOrDefault(k => _animations.ContainsKey(k))
                           ?? _animations.Keys.FirstOrDefault();

            if (fallback != null)
            {
                _currentAnimation = fallback;
                _currentFrames = _animations[fallback];
            }

            IsDeathAnimationFinished = true;
            DeathAnimationFinished?.Invoke(this, EventArgs.Empty);
        }

        _currentFrame = 0;
        _animTimer = 0f;
    }

    /// <summary>Riposiziona il miner dopo la perdita di una vita e attiva l'invincibilità.</summary>
    public void Respawn(Point spawnTile)
    {
        ResetPosition(spawnTile);
        IsInvincible = true;
        _invincibilityTimer = _invincibilityDuration;
        _blinkTimer = BlinkInterval;
        _blinkVisible = true;
    }

    /// <summary>Reset per cambio livello: azzera posizione e bombe, nessuna invincibilità.</summary>
    public void ResetForNewLevel(Point spawnTile)
    {
        ResetPosition(spawnTile);
        _bombs.Clear();
        IsInvincible = false;
        _blinkVisible = true;
        // Non resettare _bigBombCount: i pack bomba raccolti persistono tra i livelli.
        // Resetta solo le bombe attive sul campo.
    }

    private void ResetPosition(Point spawnTile)
    {
        TilePosition = spawnTile;
        Position = new Vector2(spawnTile.X * TileMap.TileSize, spawnTile.Y * TileMap.TileSize);
        _targetPosition = Position;
        IsMoving = false;
        _movementProgress = 0f;
        _inputBuffer.Clear();
        _state = MinerState.IdleFront;
        _currentAnimation = "idle_front";
        _currentFrames = _animations["idle_front"];
        _currentFrame = 0;
        _animTimer = 0f;
    }

    public Collision GetBounds()
    {
        if (!_animations.TryGetValue(_currentAnimation, out var frames) || frames.Count == 0)
            return Collision.Empty;

        var frame = frames[Math.Min(_currentFrame, frames.Count - 1)];
        var radius = (int)(frame.Width * 0.20f);
        return new Collision(
            (int)(Position.X + frame.Width * 0.5f),
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
                if (Lives < MaxLives)
                {
                    Lives++;
                    ExtraLifeEarned?.Invoke(this, EventArgs.Empty);
                }

                break;

            case UpgradeType.MaxLifeUp:
                if (_maxLifeSteps < UpgradeRegistry.MaxLifeSteps)
                {
                    _maxLifeSteps++;
                    MaxLives++;
                    Lives = Math.Min(Lives + 1, MaxLives);
                    ExtraLifeEarned?.Invoke(this, EventArgs.Empty);
                }

                break;

            case UpgradeType.SlowRegen:
                _regenLevelsAccum = 0;
                SlowRegenActive = true;
                break;

            // ── Offensivi ─────────────────────────────────────────────
            case UpgradeType.IncreasedDamage:
                if (_explosionRangeSteps < UpgradeRegistry.MaxExplosionRange)
                {
                    _explosionRangeSteps++;
                    BonusExplosionRange++;
                }

                break;

            case UpgradeType.ExplosionDamage:
                if (_explosionDamageSteps < UpgradeRegistry.MaxExplosionDamageSteps)
                    _explosionDamageSteps++;
                break;

            case UpgradeType.FasterBomb:
                if (_fasterBombSteps < UpgradeRegistry.MaxFasterBombSteps)
                {
                    _fasterBombSteps++;
                    _bombTimerBonus += 0.4f;
                }

                break;

            case UpgradeType.ExtraBomb:
                if (_extraBombSteps < UpgradeRegistry.MaxExtraBombs)
                {
                    _extraBombSteps++;
                    MaxActiveBombs++;
                }

                break;

            case UpgradeType.ChainExplosion:
                if (_chainSteps < UpgradeRegistry.MaxChainSteps)
                {
                    _chainSteps++;
                    ChainExplosionChance = Math.Min(ChainExplosionChance + 0.15f, 0.60f);
                }

                break;

            // ── Movimento ─────────────────────────────────────────────
            case UpgradeType.FasterMovement:
                if (_moveSteps < UpgradeRegistry.MaxMoveSteps)
                {
                    _moveSteps++;
                    _moveSpeed = Math.Min(_moveSpeed + 20f, 280f);
                }

                break;

            case UpgradeType.DashAfterHit:
                UpgradeDashAfterHit = true;
                break;

            // ── Difensivi ─────────────────────────────────────────────
            case UpgradeType.ExplosionResistance:
                if (_explosionResistanceSteps < UpgradeRegistry.MaxInvincibility)
                {
                    _explosionResistanceSteps++;
                    _invincibilityDuration = Math.Min(_invincibilityDuration + 1f, UpgradeRegistry.MaxInvincibility);
                }

                break;

            case UpgradeType.DamageReduction:
                if (_damageReductionSteps < UpgradeRegistry.MaxInvincibility)
                {
                    _damageReductionSteps++;
                    _invincibilityDuration = Math.Min(_invincibilityDuration + 0.5f, UpgradeRegistry.MaxInvincibility);
                }

                break;

            case UpgradeType.Shield:
                UpgradeShield = true;
                ShieldActive = true;
                break;

            // ── Speciali ──────────────────────────────────────────────
            case UpgradeType.MultiHit: UpgradeMultiHit = true; break;
            case UpgradeType.CriticalChance: UpgradeCritical = true; break;
            case UpgradeType.Magnet: UpgradeMagnet = true; break;
            case UpgradeType.StunOnHit: UpgradeStunOnHit = true; break;
            case UpgradeType.SlowOnHit: UpgradeSlowOnHit = true; break;

            case UpgradeType.BonusLoot:
                BonusLootChance = Math.Min(BonusLootChance + 0.15f, 0.60f);
                break;

            case UpgradeType.DoubleDrop:
                if (_doubleDropSteps < UpgradeRegistry.MaxDoubleDropSteps)
                {
                    _doubleDropSteps++;
                    DoubleDropChance = Math.Min(DoubleDropChance + 0.15f, 0.60f);
                }

                break;

            // ── Mythic ───────────────────────────────────────────────
            case UpgradeType.MythicImmortality:
                UpgradeMythicImmortality = true;
                break;

            case UpgradeType.InstantKill:
                UpgradeInstantKill = true;
                break;
        }
    }

    /// <summary>Chiamato da GameScene a ogni avanzamento di livello.</summary>
    public void NotifyLevelUp()
    {
        // Rigenerazione lenta: +1 vita ogni 10 livelli
        if (SlowRegenActive)
        {
            _regenLevelsAccum++;
            if (_regenLevelsAccum >= 5)
            {
                _regenLevelsAccum = 0;
                if (Lives < MaxLives)
                {
                    Lives++;
                    ExtraLifeEarned?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        // Ricarica scudo ogni ShieldRechargePeriod livelli
        if (UpgradeShield && !ShieldActive)
        {
            _shieldRechargeLevel++;
            if (_shieldRechargeLevel >= ShieldRechargePeriod)
            {
                _shieldRechargeLevel = 0;
                ShieldActive = true;
            }
        }
    }

    /// <summary>Tenta di assorbire un colpo con lo scudo. Restituisce true se assorbito.</summary>
    public bool TryAbsorbWithShield()
    {
        if (!ShieldActive) return false;
        ShieldActive = false;
        _shieldRechargeLevel = 0;
        // Breve invincibilità dopo assorbimento scudo
        StartInvincibility(_invincibilityDuration);
        return true;
    }

    /// <summary>Avvia il periodo di invincibilità per la durata corrente configurata.</summary>
    public void StartInvincibility(float duration)
    {
        IsInvincible = true;
        _invincibilityTimer = duration;
        _blinkTimer = BlinkInterval;
        _blinkVisible = true;
    }

    /// <summary>Attiva il boost di velocità post-danno se l'upgrade DashAfterHit è presente.</summary>
    public void TriggerDashAfterHit()
    {
        if (!UpgradeDashAfterHit) return;
        _dashTimer = 3f;
        _dashSpeedBonus = _moveSpeed * 0.4f;
    }

    // ═══════════════════════════════════════════════════════════════════
    // CARICAMENTO RISORSE
    // ═══════════════════════════════════════════════════════════════════

    private void LoadAnimationsFromXml(string xmlPath, ContentManager content)
    {
        if (!File.Exists(xmlPath))
            throw new FileNotFoundException($"File XML animazioni non trovato: {xmlPath}");

        var doc = XDocument.Load(xmlPath);
        _animations.Clear();
        foreach (var region in doc.Descendants("Region"))
            AddFrameToDict(_animations, region);

        var texturePath = doc.Descendants("Texture").FirstOrDefault()?.Value ?? "image/character/miner";
        _texture = content.Load<Texture2D>(texturePath);
        _currentFrames = _animations["idle_front"];
    }

    private void LoadItemAnimations(string xmlPath, ContentManager content)
    {
        if (!File.Exists(xmlPath))
            throw new FileNotFoundException($"File XML item non trovato: {xmlPath}");

        var doc = XDocument.Load(xmlPath);
        _itemAnimations.Clear();
        foreach (var region in doc.Descendants("Region"))
            AddFrameToDict(_itemAnimations, region, true);

        var texturePath = doc.Descendants("Texture").FirstOrDefault()?.Value ?? "image/items/items";
        _itemTexture = content.Load<Texture2D>(texturePath);
    }

    private void LoadExplosionAnimations(string xmlPath, ContentManager content)
    {
        if (!File.Exists(xmlPath))
            throw new FileNotFoundException($"File XML esplosioni non trovato: {xmlPath}");

        var doc = XDocument.Load(xmlPath);
        _explosionAnimations.Clear();
        foreach (var region in doc.Descendants("Region"))
            AddFrameToDict(_explosionAnimations, region, true);

        var texturePath = doc.Descendants("Texture").FirstOrDefault()?.Value ?? "image/fxs/fsx";
        _explosionTexture = content.Load<Texture2D>(texturePath);
    }

    /// <summary>Aggiunge un frame XML a un dizionario di animazioni raggruppando per nome base.</summary>
    private static void AddFrameToDict(
        Dictionary<string, List<Rectangle>> dict,
        XElement region,
        bool allowEmpty = false)
    {
        var fullName = region.Attribute("Name")?.Value ?? "";
        var x = int.Parse(region.Attribute("X")?.Value ?? "0");
        var y = int.Parse(region.Attribute("Y")?.Value ?? "0");
        var width = int.Parse(region.Attribute("Width")?.Value ?? "32");
        var height = int.Parse(region.Attribute("Height")?.Value ?? "32");

        var i = fullName.Length;
        while (i > 0 && char.IsDigit(fullName[i - 1])) i--;
        var animName = fullName[..i].TrimEnd('_', '-', ' ');

        if (string.IsNullOrEmpty(animName))
        {
            if (!allowEmpty) return;
            animName = fullName;
        }

        if (!dict.ContainsKey(animName)) dict[animName] = new List<Rectangle>();
        var rect = new Rectangle(x, y, width, height);
        if (!dict[animName].Contains(rect)) dict[animName].Add(rect);
    }

    private enum MinerState
    {
        IdleFront,
        IdleBack,
        IdleLeft,
        IdleRight,
        WalkFront,
        WalkBack,
        WalkLeft,
        WalkRight
    }
}