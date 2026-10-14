using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PoopManLibrary.World;
using System;
using System.Collections.Generic;

namespace PoopMan.GameObjects;

/// <summary>
///     Golem: nemici del nuovo mondo (dal livello 30). Riusano IA, pathfinding e danni
///     del bat ma camminano (niente volo) e hanno abilità proprie:
///     <list type="bullet">
///         <item>Normal  – nessuna abilità</item>
///         <item>Booler  – lancia una palla di roccia quando il miner è allineato</item>
///         <item>Brute   – robusto, può incassare un colpo senza danno e poi va in furia</item>
///         <item>Laser   – a intervalli carica e spara un laser che ferisce e distrugge i blocchi</item>
///         <item>Rhino   – carica il miner in linea retta: lo stordisce, contro un muro si stordisce lui</item>
///         <item>Godness – boss: tutte le abilità dei golem e dei bat, vede attraverso i muri,
///             attraversa le pareti (tranne il confine), al contatto è game over. Max 2 HP.</item>
///     </list>
/// </summary>
public partial class Bat
{
    public enum GolemKind
    {
        None, // è un bat
        Normal,
        Booler,
        Brute,
        Laser,
        Rhino,
        Godness
    }

    /// <summary>Livello dal quale compaiono i golem (inizio del nuovo mondo).</summary>
    public const int GolemFirstLevel = 30;

    /// <summary>Livello dal quale la Godness può comparire (con probabilità minima).</summary>
    public const int GodnessFirstLevel = 35;

    private enum GolemAction
    {
        None,
        RockWindup, // Booler: carica il lancio
        LaserCharge, // Laser: carica il raggio (telegrafato)
        RhinoWindup, // Rhino: raspa il terreno prima della carica
        RhinoCharge, // Rhino: carica in linea retta
        Recover // breve pausa dopo un attacco
    }

    private const float RockWindupTime = 0.35f;
    private const float RockCooldownMax = 2.8f;
    private const int RockRange = 8;
    private const float LaserChargeTime = 0.9f;
    private const float LaserCooldownMax = 5f;
    private const float LaserRecoverTime = 0.35f;
    private const float RhinoWindupTime = 0.55f;
    private const float RhinoCooldownMax = 3f;
    private const float RhinoCrashStun = 2.5f;
    private const float RhinoHitRecover = 1.8f;
    private const float RhinoSmashRecover = 0.5f;
    private const float RhinoSpeedMul = 3.2f;
    private const int RhinoRange = 10;
    private const int RhinoMaxChargeTiles = 14;
    private const float BruteTankChance = 0.35f;
    private const float RageDuration = 4f;
    private const float RageSpeedMul = 1.7f;
    private const float PhaseDuration = 3f;
    private const float PhaseCooldownMin = 6f;
    private const float PhaseCooldownMax = 10f;
    private const float PhaseChance = 0.25f;
    private const int GodnessSightRange = 20;
    private const float GodnessCooldownMul = 1.3f;

    private GolemAction _golemAction;
    private float _actionTimer;
    private Point _attackDir;
    private float _rockCooldown;
    private float _laserCooldown;
    private float _chargeCooldown;
    private int _chargeTiles;
    private float _rageTimer;
    private bool _isWallPhasing;
    private float _phaseTimer;
    private float _phaseCooldown;

    public GolemKind Golem { get; private set; } = GolemKind.None;
    public bool IsGolem => Golem != GolemKind.None;
    public bool IsGodness => Golem == GolemKind.Godness;

    private bool CanThrowRock => Golem is GolemKind.Booler or GolemKind.Godness;
    private bool CanLaser => Golem is GolemKind.Laser or GolemKind.Godness;
    private bool CanCharge => Golem is GolemKind.Rhino or GolemKind.Godness;
    private bool CanTank => Golem is GolemKind.Brute or GolemKind.Godness;
    private bool CanPhase => Golem == GolemKind.Godness;

    /// <summary>Lancio di una palla di roccia: (golem, tile di partenza, direzione cardinale).</summary>
    public event Action<Bat, Point, Point>? OnRockThrow;

    /// <summary>Sparo del laser: (golem, tile di partenza, direzione cardinale).</summary>
    public event Action<Bat, Point, Point>? OnLaserFire;

    /// <summary>Rhino in carica: al contatto stordisce il miner invece di ferirlo.</summary>
    public bool IsCharging => _golemAction == GolemAction.RhinoCharge;

    public bool IsChargingLaser => _golemAction == GolemAction.LaserCharge;
    public bool IsRhinoWindup => _golemAction == GolemAction.RhinoWindup;
    public float LaserChargeProgress => IsChargingLaser ? 1f - _actionTimer / LaserChargeTime : 0f;

    /// <summary>Direzione dell'attacco in preparazione (laser / carica).</summary>
    public Point AttackDirection => _attackDir;

    public bool IsWallPhasing => _isWallPhasing;
    public bool IsEnraged => _rageTimer > 0f;

    /// <summary>False mentre il rhino si riprende dopo la carica: il contatto non ferisce.</summary>
    public bool DealsContactDamage => !(Golem == GolemKind.Rhino && _golemAction == GolemAction.Recover);

    private float GolemSpeedMul => _rageTimer > 0f ? RageSpeedMul : _isWallPhasing ? 0.8f : 1f;

    private float CooldownMul => IsGodness ? GodnessCooldownMul : 1f;

    /// <summary>Nome del file XML (in image/enemies) con lo sprite sheet del golem.</summary>
    public static string GolemXmlName(GolemKind kind)
    {
        return $"golem_{kind.ToString().ToLowerInvariant()}.xml";
    }

    /// <summary>Golem sbloccati al livello dato (la Godness è gestita a parte, è rara).</summary>
    public static IReadOnlyList<GolemKind> UnlockedGolems(int level)
    {
        var list = new List<GolemKind>();
        if (level < GolemFirstLevel) return list;
        list.Add(GolemKind.Normal);
        list.Add(GolemKind.Booler);
        if (level >= 32) list.Add(GolemKind.Brute);
        if (level >= 34) list.Add(GolemKind.Rhino);
        if (level >= 36) list.Add(GolemKind.Laser);
        return list;
    }

    /// <summary>
    ///     Trasforma questo nemico in un golem. Chiamare dopo SetAggressionLevel():
    ///     i golem sono più lenti dei bat e più resistenti.
    /// </summary>
    public void ApplyGolem(GolemKind kind)
    {
        ApplyVariant(BatVariant.Normal);
        Golem = kind;
        if (kind == GolemKind.None) return;

        moveSpeed *= kind == GolemKind.Godness ? 0.9f : 0.8f;
        waitDuration *= 1.2f;

        var hp = kind switch
        {
            GolemKind.Brute => _maxHitPoints + 3,
            GolemKind.Rhino => _maxHitPoints + 1,
            GolemKind.Godness => 2, // il boss ha al massimo due vite
            _ => _maxHitPoints
        };
        _maxHitPoints = Math.Min(hp, kind == GolemKind.Brute ? 10 : 8);
        _hitPoints = _maxHitPoints;

        if (kind == GolemKind.Godness)
        {
            // Tutte le abilità dei bat (il Walid no: il suo contatto è già game over)
            _canDash = true;
            _canGhost = true;
            _canSplit = true;
            _canBerserk = true;
            BigExplosion = true;
            _sightRange = GodnessSightRange;
            _chaseChance = 1f;
        }

        // Cooldown iniziali sfalsati: i golem non attaccano tutti nello stesso istante
        _rockCooldown = 1f + (float)_rand.NextDouble() * RockCooldownMax;
        _laserCooldown = 1.5f + (float)_rand.NextDouble() * LaserCooldownMax;
        _chargeCooldown = 1f + (float)_rand.NextDouble() * RhinoCooldownMax;
        _phaseCooldown = PhaseCooldownMin;
    }

    private int GolemKillPoints => Golem switch
    {
        GolemKind.Normal => 250,
        GolemKind.Booler => 350,
        GolemKind.Brute => 450,
        GolemKind.Rhino => 450,
        GolemKind.Laser => 500,
        GolemKind.Godness => 5000,
        _ => 100
    };

    // ═══════════════════════════════════════════════════════════════════
    // DANNI
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Brute / Godness: possibilità di incassare il colpo senza danno (e infuriarsi).</summary>
    private bool GolemTanksHit()
    {
        if (!CanTank || _rand.NextDouble() >= BruteTankChance) return false;
        _fxEvents |= FxEvent.Tanked;
        _hitFlashTimer = HitFlashDuration * 0.5f;
        SetInvincible(0.3f);
        Enrage();
        return true;
    }

    /// <summary>Chiamato quando il golem subisce danno: il Brute entra in modalità furia.</summary>
    private void OnGolemDamaged()
    {
        if (CanTank) Enrage();
    }

    private void Enrage()
    {
        if (_rageTimer <= 0f) _fxEvents |= FxEvent.Enraged;
        _rageTimer = RageDuration;
        _path.Clear();
    }

    /// <summary>Il rhino ha travolto il miner: si ferma e si riprende (innocuo per un attimo).</summary>
    public void OnChargeHitPlayer()
    {
        if (_golemAction != GolemAction.RhinoCharge) return;
        StopOnNearestTile();
        _golemAction = GolemAction.Recover;
        _actionTimer = RhinoHitRecover;
        _chargeCooldown = RhinoCooldownMax * CooldownMul;
    }

    // ═══════════════════════════════════════════════════════════════════
    // UPDATE
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Abilità dei golem. Restituisce true se il golem è impegnato in un'azione
    ///     esclusiva (carica, lancio, laser): in quel caso il movimento normale è sospeso.
    /// </summary>
    private bool UpdateGolem(TileMap map, float dt)
    {
        if (_rockCooldown > 0f) _rockCooldown -= dt;
        if (_laserCooldown > 0f) _laserCooldown -= dt;
        if (_chargeCooldown > 0f) _chargeCooldown -= dt;
        if (_rageTimer > 0f) _rageTimer -= dt;
        UpdateWallPhase(map, dt);

        if (_knockbackTimer > 0f || IsStunned)
        {
            if (_golemAction != GolemAction.None) CancelGolemAction();
            return false;
        }

        switch (_golemAction)
        {
            case GolemAction.RockWindup:
                MoveToTarget(dt);
                if ((_actionTimer -= dt) <= 0f)
                {
                    OnRockThrow?.Invoke(this, TilePosition, _attackDir);
                    _rockCooldown = RockCooldownMax * CooldownMul;
                    _golemAction = GolemAction.None;
                }

                return true;

            case GolemAction.LaserCharge:
                MoveToTarget(dt);
                if ((_actionTimer -= dt) <= 0f)
                {
                    OnLaserFire?.Invoke(this, TilePosition, _attackDir);
                    _laserCooldown = (LaserCooldownMax + (float)_rand.NextDouble()) * CooldownMul;
                    _golemAction = GolemAction.Recover;
                    _actionTimer = LaserRecoverTime;
                }

                return true;

            case GolemAction.RhinoWindup:
                MoveToTarget(dt);
                if ((_actionTimer -= dt) <= 0f)
                {
                    _golemAction = GolemAction.RhinoCharge;
                    _chargeTiles = 0;
                    targetPosition = Position;
                }

                return true;

            case GolemAction.RhinoCharge:
                UpdateCharge(map, dt);
                return true;

            case GolemAction.Recover:
                MoveToTarget(dt);
                if ((_actionTimer -= dt) <= 0f) _golemAction = GolemAction.None;
                return true;
        }

        // Le nuove azioni partono solo a golem fermo e allineato alla griglia
        if (isMoving || _isWallPhasing || _isGhosting) return false;
        if (_dangerTiles.Contains(TilePosition)) return false; // prima scappa dalle fiamme

        if (CanCharge && _chargeCooldown <= 0f && TryAlignedDirection(map, RhinoRange, true, out var dir))
            StartGolemAction(GolemAction.RhinoWindup, RhinoWindupTime, dir);
        else if (CanThrowRock && _rockCooldown <= 0f && TryAlignedDirection(map, RockRange, false, out dir))
            StartGolemAction(GolemAction.RockWindup, RockWindupTime, dir);
        else if (CanLaser && _laserCooldown <= 0f)
            // Spara "ogni tot": mira verso il miner lungo l'asse dominante (scava verso di lui)
            StartGolemAction(GolemAction.LaserCharge, LaserChargeTime, DominantDirectionToPlayer());

        return _golemAction != GolemAction.None;
    }

    private void StartGolemAction(GolemAction action, float duration, Point dir)
    {
        _golemAction = action;
        _actionTimer = duration;
        _attackDir = dir;
        FaceDirection(dir);
        state = BatState.Idle;
        if (action != GolemAction.RockWindup) _alertTimer = AlertDuration;
    }

    private void CancelGolemAction()
    {
        if (_golemAction == GolemAction.RhinoCharge) StopOnNearestTile();
        _golemAction = GolemAction.None;
        if (_rockCooldown <= 0f) _rockCooldown = 0.5f;
        if (_laserCooldown <= 0f) _laserCooldown = 1f;
        if (_chargeCooldown <= 0f) _chargeCooldown = 1f;
    }

    /// <summary>Si ferma sulla tile più vicina (sempre calpestabile: è da lì che è passato).</summary>
    private void StopOnNearestTile()
    {
        var tile = VisualTilePosition;
        TilePosition = tile;
        targetPosition = new Vector2(tile.X * TileMap.TileSize, tile.Y * TileMap.TileSize);
        isMoving = false;
        _path.Clear();
    }

    /// <summary>Completa lo spostamento in corso verso la tile di destinazione (allineamento).</summary>
    private void MoveToTarget(float dt)
    {
        var delta = targetPosition - Position;
        var dist = delta.Length();
        var step = moveSpeed * _slowFactor * dt;
        if (dist <= step) Position = targetPosition;
        else Position += delta / dist * step;
        isMoving = false;
    }

    private void FaceDirection(Point dir)
    {
        if (dir.Y < 0) facing = Facing.Back;
        else if (dir.Y > 0) facing = Facing.Front;
        else if (dir.X < 0) facing = Facing.Left;
        else facing = Facing.Right;
    }

    /// <summary>Carica del rhino: tile dopo tile finché non urta qualcosa.</summary>
    private void UpdateCharge(TileMap map, float dt)
    {
        state = BatState.Fly;
        var speed = Math.Min(moveSpeed * RhinoSpeedMul, 560f) * _slowFactor;
        var delta = targetPosition - Position;
        var dist = delta.Length();
        if (dist > speed * dt)
        {
            Position += delta / dist * speed * dt;
            return;
        }

        Position = targetPosition;
        var next = new Point(TilePosition.X + _attackDir.X, TilePosition.Y + _attackDir.Y);

        if (!map.IsInside(next) || (map.GetTile(next) == TileType.Wall && !map.IsLiquid(next)))
        {
            // Blocco indistruttibile (o confine): il rhino si stordisce da solo
            _golemAction = GolemAction.None;
            _chargeCooldown = RhinoCooldownMax * CooldownMul;
            _fxEvents |= FxEvent.Crashed;
            ApplyStun(RhinoCrashStun);
            return;
        }

        if (map.GetTile(next) == TileType.Breakable)
        {
            // Blocco distruttibile: lo sfonda e si ferma
            map.BreakTile(next);
            EndCharge(RhinoSmashRecover);
            return;
        }

        if (map.IsLiquid(next) || _solidBombTiles.Contains(next) || _permanentBlockedTiles.Contains(next) ||
            ++_chargeTiles > RhinoMaxChargeTiles)
        {
            EndCharge(0.3f);
            return;
        }

        _moveFromTile = TilePosition;
        TilePosition = next;
        targetPosition = new Vector2(next.X * TileMap.TileSize, next.Y * TileMap.TileSize);
    }

    private void EndCharge(float recover)
    {
        _golemAction = GolemAction.Recover;
        _actionTimer = recover;
        _chargeCooldown = RhinoCooldownMax * CooldownMul;
        _path.Clear();
    }

    // ═══════════════════════════════════════════════════════════════════
    // VISTA E MIRA
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>La Godness vede il miner attraverso i muri; gli altri serve la linea di vista.</summary>
    private bool CanSeePlayer(TileMap map)
    {
        if (IsGodness)
            return Math.Abs(_playerTile.X - TilePosition.X) + Math.Abs(_playerTile.Y - TilePosition.Y) <= _sightRange;
        return HasLineOfSight(map, TilePosition, _playerTile, _sightRange);
    }

    /// <summary>
    ///     True se il miner è sulla stessa riga/colonna entro <paramref name="range" /> tile
    ///     e la traiettoria è libera (per la carica servono tile calpestabili senza bombe;
    ///     la roccia vola anche sopra acqua e lava).
    /// </summary>
    private bool TryAlignedDirection(TileMap map, int range, bool walkOnly, out Point dir)
    {
        dir = Point.Zero;
        var dx = _playerTile.X - TilePosition.X;
        var dy = _playerTile.Y - TilePosition.Y;
        if ((dx != 0 && dy != 0) || (dx == 0 && dy == 0)) return false;
        if (Math.Abs(dx) + Math.Abs(dy) > range) return false;

        dir = new Point(Math.Sign(dx), Math.Sign(dy));
        for (var t = new Point(TilePosition.X + dir.X, TilePosition.Y + dir.Y);
             t != _playerTile;
             t = new Point(t.X + dir.X, t.Y + dir.Y))
        {
            var free = map.IsWalkable(t) || (!walkOnly && map.IsLiquid(t));
            if (!free || (walkOnly && _solidBombTiles.Contains(t))) return false;
        }

        return true;
    }

    private Point DominantDirectionToPlayer()
    {
        var dx = _playerTile.X - TilePosition.X;
        var dy = _playerTile.Y - TilePosition.Y;
        if (dx == 0 && dy == 0)
            return facing switch
            {
                Facing.Back => new Point(0, -1),
                Facing.Left => new Point(-1, 0),
                Facing.Right => new Point(1, 0),
                _ => new Point(0, 1)
            };
        return Math.Abs(dx) >= Math.Abs(dy) ? new Point(Math.Sign(dx), 0) : new Point(0, Math.Sign(dy));
    }

    // ═══════════════════════════════════════════════════════════════════
    // GODNESS: ATTRAVERSA LE PARETI
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Le tile del confine della mappa non sono mai attraversabili.</summary>
    private static bool IsInnerTile(Point t)
    {
        return t.X >= 1 && t.Y >= 1 && t.X <= TileMap.Cols - 2 && t.Y <= TileMap.Rows - 2;
    }

    private void UpdateWallPhase(TileMap map, float dt)
    {
        if (!CanPhase) return;
        if (!_isWallPhasing)
        {
            if (_phaseCooldown > 0f) _phaseCooldown -= dt;
            return;
        }

        _phaseTimer -= dt;
        // Finita la fase esce solo quando è su una tile libera (mai incastrata in un muro)
        if (_phaseTimer <= 0f && !isMoving && map.IsWalkable(TilePosition))
        {
            _isWallPhasing = false;
            _phaseCooldown = PhaseCooldownMin + (float)_rand.NextDouble() * (PhaseCooldownMax - PhaseCooldownMin);
            _path.Clear();
            _pathCacheTime = PathCacheMax;
        }
    }

    /// <summary>
    ///     Override dell'IA dei golem (dopo la fuga dalle bombe): furia del Brute e
    ///     ingresso in fase della Godness quando il miner è irraggiungibile.
    /// </summary>
    private bool GolemAiOverride(TileMap map, out Point step)
    {
        step = Point.Zero;

        if (CanPhase && !_isWallPhasing && _phaseCooldown <= 0f)
        {
            var blocked = TilePosition != _playerTile && AStarPath(map, TilePosition, _playerTile).Count == 0;
            if (blocked || _rand.NextDouble() < PhaseChance)
            {
                _isWallPhasing = true;
                _phaseTimer = PhaseDuration;
                _fxEvents |= FxEvent.PhaseShift;
                step = PhaseStep(map);
                return true;
            }

            _phaseCooldown = 1f; // riprova tra poco
        }

        if (_rageTimer > 0f)
        {
            _aiState = AiState.Chase;
            step = ChaseStep(map);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Passo in fase: dritto verso il miner attraverso muri e blocchi (non il confine).
    ///     A fase scaduta cerca la tile libera più vicina per uscire dal muro.
    /// </summary>
    private Point PhaseStep(TileMap map)
    {
        if (_phaseTimer <= 0f && !map.IsWalkable(TilePosition))
            return BfsToWalkable(map);

        var dx = _playerTile.X - TilePosition.X;
        var dy = _playerTile.Y - TilePosition.Y;
        if (dx == 0 && dy == 0) return Point.Zero;

        var primary = Math.Abs(dx) >= Math.Abs(dy)
            ? new Point(Math.Sign(dx), 0)
            : new Point(0, Math.Sign(dy));
        var secondary = primary.X != 0
            ? new Point(0, Math.Sign(dy))
            : new Point(Math.Sign(dx), 0);

        foreach (var d in new[] { primary, secondary })
        {
            if (d == Point.Zero) continue;
            var t = new Point(TilePosition.X + d.X, TilePosition.Y + d.Y);
            if (IsInnerTile(t) && !_permanentBlockedTiles.Contains(t)) return t;
        }

        return Point.Zero;
    }

    /// <summary>Primo passo verso la tile calpestabile più vicina (muovendosi tra le tile interne).</summary>
    private Point BfsToWalkable(TileMap map)
    {
        var queue = new Queue<Point>();
        var parent = new Dictionary<Point, Point> { [TilePosition] = TilePosition };
        queue.Enqueue(TilePosition);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur != TilePosition && map.IsWalkable(cur) && !_permanentBlockedTiles.Contains(cur))
            {
                while (parent[cur] != TilePosition) cur = parent[cur];
                return cur;
            }

            foreach (var d in CardinalDirs)
            {
                var next = new Point(cur.X + d.X, cur.Y + d.Y);
                if (!parent.ContainsKey(next) && IsInnerTile(next))
                {
                    parent[next] = cur;
                    queue.Enqueue(next);
                }
            }
        }

        return Point.Zero;
    }

    // ═══════════════════════════════════════════════════════════════════
    // ASPETTO
    // ═══════════════════════════════════════════════════════════════════

    private Color GolemDrawColor
    {
        get
        {
            if (_rageTimer > 0f)
            {
                var pulse = 0.5f + 0.5f * MathF.Sin(Environment.TickCount64 * 0.02f);
                return Color.Lerp(new Color(255, 150, 150), new Color(255, 60, 60), pulse);
            }

            if (_golemAction == GolemAction.Recover && Golem == GolemKind.Rhino) return new Color(200, 200, 200);
            return Color.White;
        }
    }

    private float GolemDrawScale => Golem switch
    {
        GolemKind.Godness => 1.4f,
        GolemKind.Brute => 1.2f,
        GolemKind.Rhino => 1.1f,
        _ => 1.05f
    };

    private bool GolemHasAura => IsGodness || _rageTimer > 0f || _isWallPhasing ||
                                 _golemAction is GolemAction.LaserCharge or GolemAction.RhinoWindup;

    private Color GolemAuraColor
    {
        get
        {
            var pulse = 0.55f + 0.45f * (float)Math.Sin(Environment.TickCount64 * 0.007);
            if (_isWallPhasing) return new Color(170, 90, 255, (int)(110 * pulse));
            if (_golemAction == GolemAction.LaserCharge)
                return new Color(255, 40, 60, (int)(90 + 140 * LaserChargeProgress * pulse));
            if (_golemAction == GolemAction.RhinoWindup) return new Color(255, 160, 40, (int)(150 * pulse));
            if (_rageTimer > 0f) return new Color(255, 40, 30, (int)(150 * pulse));
            return new Color(255, 210, 80, (int)(120 * pulse)); // Godness: aura dorata
        }
    }

    /// <summary>Tremolio dello sprite durante la preparazione degli attacchi.</summary>
    private Vector2 GolemShakeOffset(float time)
    {
        var amp = _golemAction switch
        {
            GolemAction.RhinoWindup => 1.6f,
            GolemAction.LaserCharge => 0.6f + LaserChargeProgress * 1.2f,
            GolemAction.RockWindup => 1f,
            _ => 0f
        };
        if (amp <= 0f) return Vector2.Zero;
        return new Vector2(MathF.Sin(time * 70f) * amp, MathF.Cos(time * 53f) * amp * 0.5f);
    }

    /// <summary>Indicatori dei golem: corona della Godness, vapore della furia, occhio del laser.</summary>
    private void DrawGolemOverlay(SpriteBatch sb, Vector2 center, Rectangle srcRect, float scale, float time)
    {
        var headY = center.Y - srcRect.Height * 0.5f * scale;
        var p = Math.Max(2, (int)(2 * scale));

        void Dot(float x, float y, Color c, int size)
        {
            sb.Draw(_pixel, new Rectangle((int)x - size / 2, (int)y - size / 2, size, size), c);
        }

        if (IsGodness)
        {
            // Corona dorata a tre punte
            var gold = new Color(255, 215, 70);
            var cy = headY - p * 3;
            for (var i = -2; i <= 2; i++) Dot(center.X + i * p, cy, gold, p);
            Dot(center.X - 2 * p, cy - p, gold, p);
            Dot(center.X, cy - p, gold, p);
            Dot(center.X + 2 * p, cy - p, gold, p);
            Dot(center.X, cy - 2 * p, new Color(255, 80, 120), p);
        }

        if (_rageTimer > 0f)
            // Sbuffi di vapore rosso sopra la testa
            for (var i = 0; i < 3; i++)
            {
                var phase = (time * 1.6f + i / 3f) % 1f;
                var x = center.X + (i - 1) * 6f * scale + MathF.Sin(time * 5f + i) * 2f;
                Dot(x, headY + 4f - phase * 14f, new Color(255, 90, 70) * (1f - phase), 3);
            }

        if (_golemAction == GolemAction.LaserCharge)
        {
            // Occhio che si carica: cresce e diventa bianco
            var k = LaserChargeProgress;
            var size = (int)(2 + 5 * k * scale);
            Dot(center.X, center.Y - srcRect.Height * 0.18f * scale,
                Color.Lerp(new Color(255, 40, 60), Color.White, k), size);
        }

        if (_golemAction == GolemAction.RockWindup)
        {
            // Roccia sollevata sopra la testa
            Dot(center.X, headY - 2f, new Color(120, 110, 100), (int)(6 * scale));
            Dot(center.X - 1f, headY - 3f, new Color(170, 160, 150), (int)(3 * scale));
        }
    }
}
