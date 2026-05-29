using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PoopManLibrary;
using PoopManLibrary.World;

namespace PoopMan.GameObjects;

public class Bat
{
    // ── Variante bat ──────────────────────────────────────────────────────
    /// <summary>
    ///     Ogni bat ha esattamente un'abilità speciale (o nessuna).
    ///     Fino al livello 20 tutti i bat di un'ondata condividono la stessa variante.
    ///     Dal livello 20 in poi le varianti possono mescolarsi.
    /// </summary>
    public enum BatVariant
    {
        Normal, // nessun potere speciale
        Dasher, // scatto rapido               (sblocca lv 5)
        Walid, // esplode alla morte (piccola) (sblocca lv 8)
        Ghost, // attraversa bombe solide      (sblocca lv 10)
        Splitter, // si divide alla morte         (sblocca lv 15)
        Berserk, // velocità furiosa a corto raggio (sblocca lv 16)
        Nuke // esplode alla morte (grande)  (sblocca lv 20)
    }

    private const float PathCacheMax = 0.5f; // ricalcola il percorso ogni 0.5s
    private const float DashCooldownMax = 3f;
    private const float DashChance = 0.25f; // 25% chance per step quando disponibile
    private const float GhostDuration = 2f;
    private const float GhostCooldownMax = 8f;
    private const float GhostChance = 0.10f;
    private const float BerserkRange = 3f;
    private const float BerserkDuration = 2f;
    private const float BerserkSpeedMul = 2.2f;
    private const float KnockbackDuration = 0.22f;

    private static readonly Random _rand = new();

    // ── Separazione tra bat ───────────────────────────────────────────────
    private static readonly List<Point> _allBatPositions = new();

    // ── Evasione bombe ────────────────────────────────────────────────────
    private readonly HashSet<Point> _dangerTiles = new();

    private readonly Texture2D _pixel;
    private readonly float _wanderChangeChance = 0.30f; // probabilità di cambiare direzione in wander
    private readonly float animationSpeed = 0.12f;
    private AiState _aiState = AiState.Wander;
    private float _berserkTimer;
    private bool _canBerserk;
    private bool _canDash;
    private bool _canGhost;
    private bool _canSplit;
    private bool _canWalid; // Walid: esplode al contatto con il giocatore
    private bool _walidDetonating; // sta per esplodere (fase di avvertimento)
    private float _walidDetonationTimer; // tempo rimanente prima dell'esplosione
    private const float WalidDetonationDelay = 0.5f; // mezzo secondo di avvertimento

    // ── Parametri AI ─────────────────────────────────────────────
    private float _chaseChance = 0.60f;

    // Dash
    private float _dashCooldown;
    private float _ghostCooldown;
    private float _ghostTimer;

    // ── Punti vita (bat normali diventano resistenti dal livello 20) ──────
    private int _hitPoints = 1;

    // Berserk
    private bool _isBerserk;

    // Ghost (attraversa bombe solide)
    private bool _isGhosting;
    private bool _isMini; // true = già un mini-bat, non può splittare ancora

    // ── Stordimento e rallentamento (da upgrade miner) ───────────────────
    private float _knockbackTimer;

    // ── Knockback da esplosione ────────────────────────────────────────────
    private Vector2 _knockbackVelocity = Vector2.Zero;
    private Point _lastKnownPlayerTile; // ultima posizione conosciuta del giocatore

    // ── Poteri speciali ───────────────────────────────────────────────────
    private int _level;
    private int _maxHitPoints = 1;

    // ── Cache percorso A* ─────────────────────────────────────────────────
    private List<Point> _path = new();
    private float _pathCacheTime;
    private int _pathStep;
    private Point _pathTarget = new(-1, -1);
    private bool _playerSeen; // ha mai visto il giocatore?

    private Point _playerTile;
    private Point _registeredPos;
    private int _sightRange = 8; // tile di visibilità (line of sight)
    private float _slowFactor = 1f; // 1 = normale, < 1 = più lento

    private float _slowTimer;

    // ── Bombe solide (non attraversabili) ─────────────────────────────────
    private HashSet<Point> _solidBombTiles = new();
    private float _stunTimer;

    // ── Direzione wander corrente ─────────────────────────────────────────
    private Point _wanderDir = new(1, 0);
    private Dictionary<string, List<Rectangle>> animations = new();
    private float animationTimer;
    private string currentAnimation = "idle";
    private List<Rectangle> currentAnimationFrames;
    private int currentFrame;
    private Facing facing = Facing.Front;
    private float invincibilityTimer;

    private bool isMoving;
    private float moveSpeed = 130f;
    public Vector2 Position;
    private BatState state = BatState.Idle;
    private Vector2 targetPosition;

    private Texture2D texture;
    public Point TilePosition;
    private float waitDuration = 0.35f;

    private float waitTimer = -1f;

    internal Bat(Point startTile, string xmlPath, ContentManager content, TileMap map)
    {
        if (!map.IsWalkable(startTile))
            throw new ArgumentException("Start tile must be walkable", nameof(startTile));

        LoadAnimationsFromXml(xmlPath, content);

        // Pixel 1×1 bianco per occhi e aura
        _pixel = new Texture2D(texture.GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        TilePosition = startTile;
        Position = new Vector2(TilePosition.X * TileMap.TileSize,
            TilePosition.Y * TileMap.TileSize);
        targetPosition = Position;
    }

    /// <summary>Tile visiva del bat basata sulla posizione pixel corrente.</summary>
    public Point VisualTilePosition =>
        new((int)Math.Round(Position.X / TileMap.TileSize),
            (int)Math.Round(Position.Y / TileMap.TileSize));

    // Split (spawn mini-bat alla morte)
    public bool CanSplit => _canSplit && !_isMini;

    /// <summary>Nuke: il bat esplode alla morte con bomba grande.</summary>
    public bool ExplodesOnDeath => BigExplosion;

    /// <summary>True quando il Walid sta per esplodere (fase di avvertimento).</summary>
    public bool WalidDetonating => _walidDetonating;

    /// <summary>Nuke: il bat esplode alla morte con bomba grande (livello 16+).</summary>
    public bool BigExplosion { get; private set; }

    /// <summary>Evento: il Walid vuole esplodere sul giocatore (tile di origine).</summary>
    public event Action<Point>? OnWalidDetonation;

    /// <summary>Punti base per uccidere questo bat (aumenta con i poteri).</summary>
    public int KillPoints
    {
        get
        {
            var pts = 100;
            if (_canDash) pts += 50; // Livello  5+
            if (_canGhost) pts += 100; // Livello 10+
            if (_canWalid) pts += 125; // Livello  8+ (Walid)
            if (_canSplit) pts += 150; // Livello 15+
            if (BigExplosion) pts += 200; // Livello 16+ (Nuke)
            if (_canBerserk) pts += 250; // Livello 20+
            if (_isMini) pts = 50; // mini-bat vale meno
            return pts;
        }
    }

    public bool IsDead { get; private set; }

    public bool IsDeathAnimationFinished =>
        IsDead && currentAnimationFrames != null &&
        currentFrame >= currentAnimationFrames.Count - 1;

    public bool IsInvincible { get; private set; }

    public bool IsStunned { get; private set; }

    /// <summary>Colore tint del bat in base al tipo speciale attivo.</summary>
    public Color DrawColor
    {
        get
        {
            if (_isMini) return new Color(180, 220, 255); // azzurro chiaro
            if (BigExplosion) return new Color(255, 60, 60); // rosso fuoco
            if (_canWalid) return new Color(255, 140, 30); // arancio
            if (_canBerserk) return new Color(200, 0, 255); // viola berserk
            if (_canSplit) return new Color(100, 220, 100); // verde split
            if (_canGhost) return new Color(160, 255, 230); // ciano ghost
            if (_canDash) return new Color(255, 200, 100); // ambra dash
            if (_level >= 20 && _maxHitPoints > 1)
                return new Color(255, 100, 100); // rosso resistente
            return Color.White;
        }
    }

    /// <summary>Scala visiva: i bat speciali sono leggermente più grandi.</summary>
    public float DrawScale
    {
        get
        {
            if (BigExplosion) return 1.35f;
            if (_canBerserk) return 1.25f;
            if (_canSplit) return 1.15f;
            if (_canWalid) return 1.10f;
            if (_canGhost) return 1.05f;
            if (_isMini) return 0.75f;
            if (_level >= 20 && _maxHitPoints > 1) return 1.10f;
            return 1.0f;
        }
    }

    /// <summary>True se il bat ha l'aura pulsante.</summary>
    public bool HasAura => _walidDetonating || _isBerserk || _isGhosting || _canWalid || BigExplosion;

    public Color AuraColor
    {
        get
        {
            var pulse = 0.55f + 0.45f * (float)Math.Sin(Environment.TickCount64 * 0.007);
            if (_walidDetonating)
            {
                // Lampeggio rosso-arancione rapido per segnalare l'imminente esplosione
                var fast = 0.5f + 0.5f * (float)Math.Sin(Environment.TickCount64 * 0.030);
                return new Color(255, (int)(60 * fast), 0, (int)(220 * fast));
            }
            if (_isBerserk) return new Color(255, 80, 255, (int)(140 * pulse));
            if (_isGhosting) return new Color(120, 255, 255, (int)(80 * pulse));
            if (BigExplosion) return new Color(255, 30, 30, (int)(160 * pulse)); // rosso vivace
            if (_canWalid) return new Color(255, 120, 0, (int)(120 * pulse)); // arancione
            return Color.Transparent;
        }
    }

    /// <summary>Aggiorna le tile pericolose (bombe + esplosioni previste).</summary>
    public void SetDangerTiles(IEnumerable<Point> bombTiles, IEnumerable<Point> explosionTiles)
    {
        _dangerTiles.Clear();
        foreach (var t in bombTiles) _dangerTiles.Add(t);
        foreach (var t in explosionTiles) _dangerTiles.Add(t);
    }

    /// <summary>Aggiorna le tile occupate da bombe solide: il bat non può attraversarle.</summary>
    public void SetSolidBombTiles(IEnumerable<Point> solidTiles)
    {
        var newSet = new HashSet<Point>(solidTiles);
        // Invalida la cache A* se le bombe solide sono cambiate
        if (!newSet.SetEquals(_solidBombTiles))
        {
            _solidBombTiles = newSet;
            _pathCacheTime = PathCacheMax; // forza ricalcolo immediato
            _path.Clear();
        }
    }

    /// <summary>True se il tile è bloccato da pericolo O da una bomba solida (Ghost bypassa le bombe).</summary>
    private bool IsBlocked(Point tile)
    {
        return _dangerTiles.Contains(tile) || (!_isGhosting && _solidBombTiles.Contains(tile));
    }

    /// <summary>Aumenta velocità e aggressione in base al livello.</summary>
    public void SetAggressionLevel(int level)
    {
        // Curva di difficoltà più graduale:
        //   lv 0-4  → easyFactor 0.30–0.50  (quasi-passivi, lenti)
        //   lv 5-14 → easyFactor 0.50–0.90  (crescita lineare)
        //   lv 15+  → easyFactor 1.0         (piena difficoltà)
        float easyFactor;
        if (level < 5)
            easyFactor = 0.30f + level * 0.04f; // 0.30 → 0.46
        else if (level < 15)
            easyFactor = 0.50f + (level - 5) * 0.04f; // 0.50 → 0.86
        else
            easyFactor = 1.0f;

        _chaseChance = Math.Min((0.20f + level * 0.04f) * easyFactor, 0.92f);
        moveSpeed = Math.Min((65f + level * 7f) * easyFactor, 230f);
        waitDuration = Math.Max((0.85f - level * 0.02f) * (level < 15 ? 1.3f : 1f), 0.08f);
        _sightRange = Math.Min(2 + level, 16);

        _level = level;

        // ── Punti vita scalati sul livello ───────────────────────────────
        // I mini-bat rimangono sempre a 1 HP.
        // Fino al lv 19: 1 HP.  Dal lv 20 in poi: 1 HP extra ogni 5 livelli, cap 6.
        //   lv 20-24 → 2 HP
        //   lv 25-29 → 3 HP
        //   lv 30-34 → 4 HP
        //   lv 35-39 → 5 HP
        //   lv 40+   → 6 HP (massimo)
        if (!_isMini)
        {
            var hp = level >= 20 ? 2 + (level - 20) / 5 : 1;
            _maxHitPoints = Math.Min(hp, 6);
            _hitPoints = _maxHitPoints;
        }
    }

    public void SetMini()
    {
        _isMini = true;
        moveSpeed *= 0.8f;
    }

    /// <summary>
    ///     Restituisce le varianti sbloccate al livello dato.
    ///     Always includes Normal.
    /// </summary>
    public static IReadOnlyList<BatVariant> UnlockedVariants(int level)
    {
        var list = new List<BatVariant> { BatVariant.Normal };
        if (level >= 5) list.Add(BatVariant.Dasher);
        if (level >= 8) list.Add(BatVariant.Walid);
        if (level >= 10) list.Add(BatVariant.Ghost);
        if (level >= 15) list.Add(BatVariant.Splitter);
        if (level >= 16) list.Add(BatVariant.Berserk);
        if (level >= 20) list.Add(BatVariant.Nuke);
        return list;
    }

    /// <summary>
    ///     Applica esattamente una variante speciale a questo bat.
    ///     Chiamare dopo SetAggressionLevel().
    /// </summary>
    public void ApplyVariant(BatVariant variant)
    {
        // reset tutte le abilità
        _canDash = false;
        _canGhost = false;
        _canSplit = false;
        _canBerserk = false;
        _canWalid = false;
        BigExplosion = false;

        switch (variant)
        {
            case BatVariant.Dasher: _canDash = true; break;
            case BatVariant.Walid: _canWalid = true; break;
            case BatVariant.Ghost: _canGhost = true; break;
            case BatVariant.Splitter: _canSplit = true; break;
            case BatVariant.Nuke: BigExplosion = true; break;
            case BatVariant.Berserk: _canBerserk = true; break;
            // Normal: nessun flag
        }
    }

    // Evento split (GameScene ascolta e spawna i mini-bat)
    public event Action<Point>? OnSplit;

    /// <summary>
    ///     Scattato quando il bat Nuke muore e deve esplodere (grande).
    /// </summary>
    public event Action<Point, bool>? OnDeathExplosion;

    public void ApplyStun(float duration)
    {
        if (IsDead) return;
        IsStunned = true;
        _stunTimer = duration;
    }

    public void ApplySlow(float factor, float duration)
    {
        if (IsDead) return;
        _slowFactor = 1f - factor; // es. 0.4 → _slowFactor = 0.6
        _slowTimer = duration;
    }

    /// <summary>
    ///     Avvia la sequenza di detonazione Walid (avvertimento visivo poi esplosione).
    ///     Chiamare quando il Walid raggiunge lo stesso tile del giocatore.
    /// </summary>
    public void TriggerWalidDetonation()
    {
        if (IsDead || _walidDetonating || !_canWalid) return;
        _walidDetonating = true;
        _walidDetonationTimer = WalidDetonationDelay;
    }

    /// <summary>
    ///     Applica un impulso di knockback in direzione <paramref name="direction" /> (normalizzato).
    ///     Il bat si sposta in pixel-space per la breve durata del knockback.
    /// </summary>
    public void ApplyKnockback(Vector2 direction, float speed = 180f)
    {
        if (IsDead) return;
        _knockbackVelocity = direction * speed;
        _knockbackTimer = KnockbackDuration;
    }

    /// <summary>
    ///     Infligge <paramref name="damage" /> punti danno. Restituisce true se il bat è stato ucciso.
    ///     Da chiamare in luogo di Kill() quando si applica danno da esplosione.
    /// </summary>
    public bool TakeDamage(int damage = 1)
    {
        if (IsDead) return false;
        // Ghost: immune ai danni durante la fase di attraversamento
        if (_isGhosting) return false;
        _hitPoints -= damage;
        if (_hitPoints > 0)
        {
            // Breve invincibilità inter-hit per evitare danno multiplo nella stessa frame
            SetInvincible(0.3f);
            return false;
        }

        Kill();
        return true;
    }

    private void LoadAnimationsFromXml(string xmlPath, ContentManager content)
    {
        var doc = XDocument.Load(xmlPath);
        var root = doc.Root ?? throw new InvalidOperationException($"XML root missing in {xmlPath}");

        var textureEl = root.Element("Texture")
                        ?? throw new InvalidOperationException($"Missing <Texture> in {xmlPath}");
        texture = content.Load<Texture2D>(textureEl.Value);

        var regionElements = root.Descendants("Region")
            .Where(r => r.Attribute("Name") != null);

        if (!regionElements.Any())
            throw new InvalidOperationException($"No <Region> elements in {xmlPath}");

        var temp = new Dictionary<string, List<(int frame, Rectangle rect)>>();

        foreach (var region in regionElements)
        {
            var fullName = region.Attribute("Name")!.Value;
            if (!int.TryParse(region.Attribute("X")?.Value, out var x)) continue;
            if (!int.TryParse(region.Attribute("Y")?.Value, out var y)) continue;
            if (!int.TryParse(region.Attribute("Width")?.Value, out var w)) continue;
            if (!int.TryParse(region.Attribute("Height")?.Value, out var h)) continue;

            var frameNumberStart = fullName.Length;
            while (frameNumberStart > 0 && char.IsDigit(fullName[frameNumberStart - 1]))
                frameNumberStart--;

            if (frameNumberStart >= fullName.Length || frameNumberStart == 0) continue;

            var animationName = fullName.Substring(0, frameNumberStart).TrimEnd('_', '-', ' ');
            if (!int.TryParse(fullName.Substring(frameNumberStart), out var frameNumber)) continue;

            if (!temp.ContainsKey(animationName))
                temp[animationName] = new List<(int frame, Rectangle rect)>();
            temp[animationName].Add((frameNumber, new Rectangle(x, y, w, h)));
        }

        animations = temp.ToDictionary(
            p => p.Key,
            p => p.Value.OrderBy(f => f.frame).Select(f => f.rect).ToList()
        );

        if (animations.Count > 0)
        {
            var preferred = new[]
                            {
                                "fly_front", "fly_right", "fly_left", "fly_back",
                                "idle", "walk", "fly"
                            }.FirstOrDefault(k => animations.ContainsKey(k))
                            ?? animations.Keys.FirstOrDefault(k =>
                                !k.Equals("dead", StringComparison.OrdinalIgnoreCase))
                            ?? animations.Keys.First();

            currentAnimation = preferred;
            currentAnimationFrames = animations[currentAnimation];
        }
    }

    public void SetInvincible(float duration)
    {
        IsInvincible = true;
        invincibilityTimer = duration;
    }

    public void SetPlayerTarget(Point playerTile)
    {
        _playerTile = playerTile;
    }

    internal void Kill()
    {
        if (IsDead) return;
        IsDead = true;
        isMoving = false;
        state = BatState.Idle;
        currentFrame = 0;
        animationTimer = 0f;

        // Split: notifica GameScene di spawnare 2 mini-bat
        if (CanSplit)
            OnSplit?.Invoke(TilePosition);

        // Solo Nuke (livello 16+) esplode alla morte (Walid esplode al contatto)
        if (ExplodesOnDeath)
            OnDeathExplosion?.Invoke(TilePosition, true);

        if (animations.ContainsKey("dead"))
        {
            currentAnimation = "dead";
            currentAnimationFrames = animations[currentAnimation];
        }
        else if (animations.Count > 0)
        {
            currentAnimation = animations.Keys.First();
            currentAnimationFrames = animations[currentAnimation];
        }
    }

    internal void Update(TileMap map, GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (IsInvincible)
        {
            invincibilityTimer -= dt;
            if (invincibilityTimer <= 0f) IsInvincible = false;
        }

        // ── Stordimento ───────────────────────────────────────────────────
        if (IsStunned)
        {
            _stunTimer -= dt;
            if (_stunTimer <= 0f)
            {
                IsStunned = false;
                _stunTimer = 0f;
            }
        }

        // ── Rallentamento ─────────────────────────────────────────────────
        if (_slowTimer > 0f)
        {
            _slowTimer -= dt;
            if (_slowTimer <= 0f)
            {
                _slowTimer = 0f;
                _slowFactor = 1f;
            }
        }

        // ── Knockback ─────────────────────────────────────────────────────
        if (_knockbackTimer > 0f)
        {
            _knockbackTimer -= dt;
            Position += _knockbackVelocity * dt;
            _knockbackVelocity *= 0.80f; // attrito rapido
            if (_knockbackTimer <= 0f) _knockbackVelocity = Vector2.Zero;
        }

        // ── Walid: detonazione quando raggiunge il giocatore ──────────────
        if (_canWalid && _walidDetonating)
        {
            _walidDetonationTimer -= dt;
            if (_walidDetonationTimer <= 0f)
            {
                OnWalidDetonation?.Invoke(TilePosition);
                Kill();
                _allBatPositions.Remove(_registeredPos);
                UpdateAnimation(gameTime);
                return;
            }
            // Mentre sta per esplodere: ferma il movimento, aggiorna solo animazione
            isMoving = false;
            UpdateAnimation(gameTime);
            return;
        }

        if (IsDead)
        {
            // Deregistra dalla lista separazione
            _allBatPositions.Remove(_registeredPos);
            UpdateAnimation(gameTime);
            return;
        }

        // ── Aggiorna registrazione separazione ────────────────────────────
        _allBatPositions.Remove(_registeredPos);
        _registeredPos = TilePosition;
        _allBatPositions.Add(_registeredPos);

        _pathCacheTime += dt;

        // ── Cooldown dash ─────────────────────────────────────────────────
        if (_dashCooldown > 0f) _dashCooldown -= dt;

        // ── Ghost: decrementa timer e ripristina solidità ──────────────────
        if (_isGhosting)
        {
            _ghostTimer -= dt;
            if (_ghostTimer <= 0f)
            {
                _isGhosting = false;
                _ghostCooldown = GhostCooldownMax;
            }
        }
        else if (_ghostCooldown > 0f)
        {
            _ghostCooldown -= dt;
        }

        // ── Berserk: attiva se giocatore vicino ───────────────────────────
        if (_canBerserk)
        {
            float distToPlayer = Math.Abs(TilePosition.X - _playerTile.X)
                                 + Math.Abs(TilePosition.Y - _playerTile.Y);
            if (!_isBerserk && distToPlayer <= BerserkRange)
            {
                _isBerserk = true;
                _berserkTimer = BerserkDuration;
            }

            if (_isBerserk)
            {
                _berserkTimer -= dt;
                if (_berserkTimer <= 0f) _isBerserk = false;
            }
        }

        if (!isMoving)
        {
            if (IsStunned) goto skip_movement; // stordito: non si muove
            waitTimer -= dt;
            if (waitTimer <= 0f)
            {
                var nextMove = ChooseNextTile(map, dt);

                if (nextMove != Point.Zero && nextMove != TilePosition)
                {
                    var ddx = nextMove.X - TilePosition.X;
                    var ddy = nextMove.Y - TilePosition.Y;
                    if (ddy < 0) facing = Facing.Back;
                    else if (ddy > 0) facing = Facing.Front;
                    else if (ddx < 0) facing = Facing.Left;
                    else facing = Facing.Right;

                    TilePosition = nextMove;
                    targetPosition = new Vector2(nextMove.X * TileMap.TileSize,
                        nextMove.Y * TileMap.TileSize);
                    isMoving = true;
                    state = BatState.Fly;
                    animationTimer = 0f;
                    currentFrame = 0;
                }

                waitTimer = waitDuration * (float)(0.8 + _rand.NextDouble() * 0.4);
            }
        }

        skip_movement:

        if (isMoving)
        {
            // Se la destinazione è diventata bloccata (bomba piazzata nel frattempo), annulla
            if (_solidBombTiles.Contains(TilePosition))
            {
                // Torna al tile precedente
                TilePosition = VisualTilePosition; // il tile più vicino alla posizione attuale
                targetPosition = new Vector2(TilePosition.X * TileMap.TileSize,
                    TilePosition.Y * TileMap.TileSize);
                // Forza riallineamento: torna indietro scegliendo una tile libera adiacente
                var rollback = new[] { new Point(0, -1), new Point(0, 1), new Point(-1, 0), new Point(1, 0) }
                    .Select(d => new Point(TilePosition.X + d.X, TilePosition.Y + d.Y))
                    .FirstOrDefault(t => map.IsWalkable(t) && !_solidBombTiles.Contains(t));
                if (rollback != Point.Zero)
                {
                    TilePosition = rollback;
                    targetPosition = new Vector2(rollback.X * TileMap.TileSize,
                        rollback.Y * TileMap.TileSize);
                }

                _path.Clear();
            }

            var direction = targetPosition - Position;
            var distance = direction.Length();
            var currentSpeed = moveSpeed * (_isBerserk ? BerserkSpeedMul : 1f) * _slowFactor;

            if (distance <= currentSpeed * dt)
            {
                Position = targetPosition;
                isMoving = false;
                state = BatState.Idle;
                waitTimer = (float)(_rand.NextDouble() * waitDuration * 0.5f + waitDuration * 0.2f);
                currentFrame = 0;
                animationTimer = 0f;
            }
            else
            {
                Position += Vector2.Normalize(direction) * currentSpeed * dt;
            }
        }

        UpdateAnimation(gameTime);
    }

    // ── Macchina a stati: sceglie la prossima tile ────────────────────────
    private Point ChooseNextTile(TileMap map, float dt)
    {
        var inDanger = _dangerTiles.Contains(TilePosition);

        // ── GHOST: attiva se una bomba solida blocca tutti i percorsi ─────
        if (_canGhost && !_isGhosting && _ghostCooldown <= 0f)
        {
            var surrounded = new[] { new Point(0, -1), new Point(0, 1), new Point(-1, 0), new Point(1, 0) }
                .All(d =>
                {
                    var t = new Point(TilePosition.X + d.X, TilePosition.Y + d.Y);
                    return !map.IsWalkable(t) || _solidBombTiles.Contains(t);
                });
            if (surrounded || (_solidBombTiles.Count > 0 && _rand.NextDouble() < GhostChance))
            {
                _isGhosting = true;
                _ghostTimer = GhostDuration;
                _path.Clear(); // ricalcola percorso ignorando bombe solide
            }
        }

        // ── FLEE: priorità assoluta su tutto ──────────────────────────────
        if (inDanger)
        {
            _aiState = AiState.Flee;
            _path.Clear();
            return BfsToSafeTile(map);
        }

        // ── Aggiorna stato AI in base alla visibilità del giocatore ───────
        var canSee = HasLineOfSight(map, TilePosition, _playerTile, _sightRange);
        if (canSee)
        {
            _lastKnownPlayerTile = _playerTile;
            _playerSeen = true;
            _aiState = _rand.NextDouble() < _chaseChance ? AiState.Chase : AiState.Patrol;
        }
        else if (_aiState == AiState.Chase && _playerSeen)
        {
            // Perde la visuale → va verso ultima posizione conosciuta
            _aiState = AiState.Patrol;
        }
        else if (_aiState == AiState.Patrol && TilePosition == _lastKnownPlayerTile)
        {
            // Ha raggiunto l'ultima posizione nota → wander
            _aiState = AiState.Wander;
            _playerSeen = false;
        }

        return _aiState switch
        {
            AiState.Chase => ApplyDash(map, ChaseStep(map)),
            AiState.Patrol => ApplyDash(map, PatrolStep(map)),
            AiState.Flee => BfsToSafeTile(map),
            _ => WanderStep(map)
        };
    }

    // ── DASH: prova a saltare un tile extra nella stessa direzione ────────
    private Point ApplyDash(TileMap map, Point nextStep)
    {
        if (!_canDash || _dashCooldown > 0f || nextStep == Point.Zero)
            return nextStep;
        if (_rand.NextDouble() >= DashChance) return nextStep;

        var ddx = nextStep.X - TilePosition.X;
        var ddy = nextStep.Y - TilePosition.Y;
        Point dashTile = new(nextStep.X + ddx, nextStep.Y + ddy);

        var dashOk = map.IsWalkable(dashTile) &&
                     !_dangerTiles.Contains(dashTile) &&
                     (_isGhosting || !_solidBombTiles.Contains(dashTile));

        if (dashOk)
        {
            _dashCooldown = DashCooldownMax;
            // Aggiorna facing e posizione logica intermedia anche per il tile skippato
            TilePosition = nextStep; // passa dal tile intermedio
            // Avanza _pathStep per il tile saltato, così il percorso non torna indietro
            _pathStep++;
            return dashTile;
        }

        return nextStep;
    }

    // ── CHASE: A* verso il giocatore con cache ────────────────────────────
    private Point ChaseStep(TileMap map)
    {
        var needRepath = _pathStep >= _path.Count
                         || _pathCacheTime >= PathCacheMax
                         || (_path.Count > 0 && _path[^1] != _playerTile);

        if (needRepath)
        {
            _path = AStarPath(map, TilePosition, _playerTile);
            _pathStep = 0;
            _pathCacheTime = 0f;
        }

        if (_path.Count == 0) return WanderStep(map);

        // Avanza sul percorso
        while (_pathStep < _path.Count && _path[_pathStep] == TilePosition)
            _pathStep++;

        if (_pathStep >= _path.Count) return WanderStep(map);

        var next = _path[_pathStep];

        // Separazione: evita tile occupate da altri bat
        if (_allBatPositions.Contains(next) && next != TilePosition)
        {
            // Cerca step successivo libero
            for (var s = _pathStep + 1; s < Math.Min(_pathStep + 3, _path.Count); s++)
                if (!_allBatPositions.Contains(_path[s]) || _path[s] == TilePosition)
                    return _path[s];
            return WanderStep(map); // aspetta o si sposta lateralmente
        }

        return next;
    }

    // ── PATROL: A* verso ultima posizione nota del giocatore ──────────────
    private Point PatrolStep(TileMap map)
    {
        var needRepath = _pathStep >= _path.Count
                         || _pathCacheTime >= PathCacheMax
                         || (_path.Count > 0 && _path[^1] != _lastKnownPlayerTile);

        if (needRepath)
        {
            _path = AStarPath(map, TilePosition, _lastKnownPlayerTile);
            _pathStep = 0;
            _pathCacheTime = 0f;
        }

        if (_path.Count == 0) return WanderStep(map);

        while (_pathStep < _path.Count && _path[_pathStep] == TilePosition)
            _pathStep++;

        if (_pathStep >= _path.Count) return WanderStep(map);
        return _path[_pathStep];
    }

    // ── WANDER: continua nella stessa direzione, cambia se bloccato ───────
    private Point WanderStep(TileMap map)
    {
        // Prova a continuare nella direzione corrente
        Point preferred = new(TilePosition.X + _wanderDir.X, TilePosition.Y + _wanderDir.Y);
        if (map.IsWalkable(preferred) && !IsBlocked(preferred) &&
            !_allBatPositions.Contains(preferred))
            // Piccola chance di cambiare direzione comunque (comportamento naturale)
            if (_rand.NextDouble() >= _wanderChangeChance)
                return preferred;

        // Scegli nuova direzione
        var dirs = new[] { new Point(0, -1), new Point(0, 1), new Point(-1, 0), new Point(1, 0) }
            .OrderBy(_ => _rand.Next()).ToArray();

        foreach (var d in dirs)
        {
            Point cand = new(TilePosition.X + d.X, TilePosition.Y + d.Y);
            if (map.IsWalkable(cand) && !IsBlocked(cand) &&
                !_allBatPositions.Contains(cand))
            {
                _wanderDir = d;
                return cand;
            }
        }

        // Fallback senza separazione
        foreach (var d in dirs)
        {
            Point cand = new(TilePosition.X + d.X, TilePosition.Y + d.Y);
            if (map.IsWalkable(cand) && !IsBlocked(cand))
            {
                _wanderDir = d;
                return cand;
            }
        }

        return Point.Zero;
    }

    // ── Line Of Sight semplice (ray cast tile per tile) ───────────────────
    private static bool HasLineOfSight(TileMap map, Point from, Point to, int maxRange)
    {
        var dist = Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y);
        if (dist > maxRange) return false;

        // Bresenham line
        int x = from.X, y = from.Y;
        int dx = Math.Abs(to.X - x), dy = Math.Abs(to.Y - y);
        int sx = to.X > x ? 1 : -1, sy = to.Y > y ? 1 : -1;
        var err = dx - dy;

        while (x != to.X || y != to.Y)
        {
            if (!map.IsWalkable(new Point(x, y))) return false;
            var e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
        }

        return true;
    }

    // ── A* pathfinding ────────────────────────────────────────────────────
    private List<Point> AStarPath(TileMap map, Point from, Point to)
    {
        if (from == to) return new List<Point>();

        var open = new SortedSet<(float f, int id, Point p)>(
            Comparer<(float f, int id, Point p)>.Create((a, b) =>
                a.f != b.f ? a.f.CompareTo(b.f) : a.id.CompareTo(b.id)));
        var gScore = new Dictionary<Point, float>();
        var parent = new Dictionary<Point, Point>();
        var idSeq = 0;

        gScore[from] = 0f;
        open.Add((Heuristic(from, to), idSeq++, from));

        Point[] dirs = { new(0, -1), new(0, 1), new(-1, 0), new(1, 0) };

        while (open.Count > 0)
        {
            var (_, _, cur) = open.Min;
            open.Remove(open.Min);

            if (cur == to)
            {
                // Ricostruisce percorso
                var path = new List<Point>();
                var c = cur;
                while (c != from)
                {
                    path.Add(c);
                    c = parent[c];
                }

                path.Reverse();
                return path;
            }

            foreach (var d in dirs)
            {
                Point next = new(cur.X + d.X, cur.Y + d.Y);
                if (!map.IsWalkable(next)) continue;
                if (IsBlocked(next)) continue;

                var ng = gScore[cur] + 1f;
                if (!gScore.TryGetValue(next, out var existing) || ng < existing)
                {
                    gScore[next] = ng;
                    parent[next] = cur;
                    open.Add((ng + Heuristic(next, to), idSeq++, next));
                }
            }
        }

        return new List<Point>(); // nessun percorso
    }

    private static float Heuristic(Point a, Point b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

    // ── BFS verso tile sicura (fuga bombe) ───────────────────────────────
    private Point BfsToSafeTile(TileMap map)
    {
        if (!_dangerTiles.Contains(TilePosition)) return Point.Zero;

        var queue = new Queue<Point>();
        var parent = new Dictionary<Point, Point>();
        queue.Enqueue(TilePosition);
        parent[TilePosition] = TilePosition;

        Point[] dirs = { new(0, -1), new(0, 1), new(-1, 0), new(1, 0) };

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (!_dangerTiles.Contains(cur) && cur != TilePosition)
            {
                while (parent[cur] != TilePosition) cur = parent[cur];
                return cur;
            }

            foreach (var d in dirs)
            {
                Point next = new(cur.X + d.X, cur.Y + d.Y);
                if (!parent.ContainsKey(next) && map.IsWalkable(next))
                {
                    parent[next] = cur;
                    queue.Enqueue(next);
                }
            }
        }

        return Point.Zero;
    }

    private void UpdateAnimation(GameTime gameTime)
    {
        if (IsDead)
        {
            if (animations.ContainsKey("dead") && currentAnimation != "dead")
            {
                currentAnimation = "dead";
                currentAnimationFrames = animations[currentAnimation];
                currentFrame = 0;
                animationTimer = 0f;
            }

            // Animazione morte non ciclica: si ferma all'ultimo frame
            if (currentAnimationFrames != null &&
                currentFrame < currentAnimationFrames.Count - 1)
            {
                animationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (animationTimer >= animationSpeed)
                {
                    animationTimer = 0f;
                    currentFrame = Math.Min(currentFrame + 1, currentAnimationFrames.Count - 1);
                }
            }

            return;
        }

        var faceName = facing switch
        {
            Facing.Front => "front",
            Facing.Back => "back",
            Facing.Left => "left",
            Facing.Right => "right",
            _ => "front"
        };

        var candidates = new List<string>();

        if (state == BatState.Fly)
        {
            candidates.Add($"fly_{faceName}");
            candidates.Add("fly");
            candidates.Add("walk");
        }
        else
        {
            candidates.Add("idle");
            candidates.Add($"idle_{faceName}");
            candidates.Add($"fly_{faceName}");
            candidates.Add("fly");
            var nonDead = animations.Keys.FirstOrDefault(k =>
                !k.Equals("dead", StringComparison.OrdinalIgnoreCase));
            if (nonDead != null) candidates.Add(nonDead);
        }

        if (animations.Count > 0) candidates.Add(animations.Keys.First());

        var desired = candidates.FirstOrDefault(c => animations.ContainsKey(c))
                      ?? currentAnimation;

        if (desired != currentAnimation)
        {
            currentAnimation = desired;
            animations.TryGetValue(currentAnimation, out currentAnimationFrames);
            currentFrame = 0;
            animationTimer = 0f;
        }

        if (currentAnimationFrames?.Count > 1)
        {
            animationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (animationTimer >= animationSpeed)
            {
                animationTimer = 0f;
                currentFrame = (currentFrame + 1) % currentAnimationFrames.Count;
            }
        }
        else
        {
            currentFrame = 0;
        }
    }

    internal void Draw(SpriteBatch spriteBatch)
    {
        if (!animations.TryGetValue(currentAnimation, out var frames) ||
            currentFrame >= frames.Count)
            return;

        var srcRect = frames[currentFrame];
        var scale = DrawScale;
        var tint = DrawColor;

        // Blink se danneggiato (HP < max e invincibile inter-hit)
        if (_hitPoints < _maxHitPoints && IsInvincible)
            tint = Color.Lerp(tint, Color.White, 0.6f);

        // Ghost: semi-trasparente
        if (_isGhosting)
            tint *= 0.55f;

        var origin = new Vector2(srcRect.Width * 0.5f, srcRect.Height * 0.5f);
        var center = Position + new Vector2(srcRect.Width * 0.5f, srcRect.Height * 0.5f);

        // Aura pulsante (berserk / ghost)
        if (HasAura)
        {
            var auraScale = scale * (1.35f + 0.10f * (float)Math.Sin(
                Environment.TickCount64 * 0.005));
            spriteBatch.Draw(texture, center, srcRect, AuraColor,
                0f, origin, auraScale, SpriteEffects.None, 0f);
        }

        spriteBatch.Draw(texture, center, srcRect, tint,
            0f, origin, scale, SpriteEffects.None, 0f);

        // Occhi luminosi per bat speciali (piccoli quadrati bianchi sopra gli occhi)
        if (_level >= 5 && !_isMini && !IsDead)
        {
            var eyeColor = _canBerserk ? Color.Red :
                BigExplosion ? new Color(255, 60, 60) :
                _canWalid ? new Color(255, 160, 0) :
                _canGhost ? Color.Cyan :
                _canSplit ? new Color(255, 230, 0) :
                _canDash ? new Color(255, 190, 80) :
                Color.White;
            var eyeSize = (int)(2 * scale);
            var offsetY = (int)(-srcRect.Height * 0.18f * scale);
            // occhio sinistro
            spriteBatch.Draw(_pixel,
                new Rectangle((int)center.X - (int)(4 * scale), (int)center.Y + offsetY, eyeSize, eyeSize),
                eyeColor);
            // occhio destro
            spriteBatch.Draw(_pixel,
                new Rectangle((int)center.X + (int)(3 * scale), (int)center.Y + offsetY, eyeSize, eyeSize),
                eyeColor);
        }

        // ── Badge tipo speciale (sopra la testa) ─────────────────────────
        if (!_isMini && !IsDead && (_canWalid || BigExplosion || _canGhost || _canSplit || _canDash || _canBerserk))
        {
            // posizione base: sopra la testa del bat
            var pulse = 0.6f + 0.4f * (float)Math.Sin(Environment.TickCount64 * 0.008);
            var p = Math.Max(2, (int)(2 * scale)); // dimensione pixel badge
            var bx = (int)center.X;
            var by = (int)(center.Y - srcRect.Height * 0.5f * scale) - p * 4 - 2;

            void Dot(int ox, int oy, Color c)
            {
                spriteBatch.Draw(_pixel, new Rectangle(bx + ox * p - p / 2, by + oy * p, p, p), c);
            }

            if (BigExplosion)
            {
                // X lampeggiante rossa: 4 angoli
                var rc = new Color(255, (int)(40 * pulse), (int)(40 * pulse));
                Dot(-1, -1, rc);
                Dot(1, -1, rc);
                Dot(0, 0, rc);
                Dot(-1, 1, rc);
                Dot(1, 1, rc);
            }
            else if (_canWalid)
            {
                // Croce arancione: bomba stilizzata
                var oc = new Color(255, (int)(120 + 80 * pulse), 0);
                Dot(0, -1, oc);
                Dot(-1, 0, oc);
                Dot(0, 0, oc);
                Dot(1, 0, oc);
                Dot(0, 1, oc);
            }
            else if (_canBerserk)
            {
                // Rombo viola pieno pulsante
                var vc = new Color((int)(180 * pulse), 0, (int)(255 * pulse));
                Dot(0, -1, vc);
                Dot(-1, 0, vc);
                Dot(0, 0, vc);
                Dot(1, 0, vc);
                Dot(0, 1, vc);
            }
            else if (_canSplit)
            {
                // Y gialla: gambo + due rami
                var yc = new Color(255, 230, 0);
                Dot(0, -1, yc);
                Dot(0, 0, yc);
                Dot(-1, 1, yc);
                Dot(1, 1, yc);
            }
            else if (_canGhost)
            {
                // Diamante ciano: 3 punti verticali
                var gc = new Color((int)(100 * pulse), 255, (int)(230 * pulse));
                Dot(0, -1, gc);
                Dot(0, 0, gc);
                Dot(0, 1, gc);
            }
            else if (_canDash)
            {
                // Doppia freccia orizzontale ambra: >> 
                var ac = new Color(255, 180, (int)(60 * pulse));
                Dot(-1, 0, ac);
                Dot(0, 0, ac);
                Dot(0, -1, ac);
                Dot(1, -1, ac);
                Dot(0, 1, ac);
                Dot(1, 1, ac);
            }
        }

        // ── Barra HP (visibile solo se maxHP > 1 e bat vivo) ─────────────
        if (_maxHitPoints > 1 && !IsDead)
        {
            var barW = (int)(srcRect.Width * scale);
            var barH = Math.Max(2, (int)(3 * scale));
            var barX = (int)(center.X - barW * 0.5f);
            var barY = (int)(center.Y - srcRect.Height * 0.5f * scale) - barH - 2;
            // sfondo rosso scuro
            spriteBatch.Draw(_pixel,
                new Rectangle(barX, barY, barW, barH),
                new Color(120, 0, 0));
            // riempimento verde proporzionale agli HP rimasti
            var fillW = (int)(barW * (_hitPoints / (float)_maxHitPoints));
            if (fillW > 0)
                spriteBatch.Draw(_pixel,
                    new Rectangle(barX, barY, fillW, barH),
                    new Color(50, 220, 50));
        }
    }

    public Collision GetBounds()
    {
        if (!animations.TryGetValue(currentAnimation, out var frames) || frames.Count == 0)
            return Collision.Empty;

        var frame = frames[Math.Min(currentFrame, frames.Count - 1)];
        return new Collision(
            (int)(Position.X + frame.Width * 0.5f),
            (int)(Position.Y + frame.Height * 0.5f),
            (int)(frame.Width * 0.175f));
    }

    private enum BatState
    {
        Idle,
        Fly
    }

    private enum Facing
    {
        Front,
        Back,
        Left,
        Right
    }

    // ── Macchina a stati AI ───────────────────────────────────────────────
    private enum AiState
    {
        Wander,
        Chase,
        Flee,
        Patrol
    }
}