using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PoopManLibrary;
using PoopManLibrary.World;

namespace PoopMan.GameObjects
{
    public class Bat
    {
        public Point TilePosition;
        public Vector2 Position;
        private Vector2 targetPosition;

        /// <summary>Tile visiva del bat basata sulla posizione pixel corrente.</summary>
        public Point VisualTilePosition =>
            new Point((int)Math.Round(Position.X / TileMap.TileSize),
                      (int)Math.Round(Position.Y / TileMap.TileSize));
        private float moveSpeed = 130f;
        private bool isMoving = false;

        private Texture2D texture;
        private float animationTimer = 0f;
        private float animationSpeed = 0.12f;
        private int currentFrame = 0;
        private Dictionary<string, List<Rectangle>> animations = new();
        private string currentAnimation = "idle";
        private List<Rectangle> currentAnimationFrames;

        private enum BatState { Idle, Fly }
        private BatState state = BatState.Idle;
        private enum Facing { Front, Back, Left, Right }
        private Facing facing = Facing.Front;

        private static readonly Random _rand = new();

        private Point _playerTile;
        private bool _playerSeen = false;       // ha mai visto il giocatore?
        private Point _lastKnownPlayerTile;     // ultima posizione conosciuta del giocatore

        // ── Macchina a stati AI ───────────────────────────────────────────────
        private enum AiState { Wander, Chase, Flee, Patrol }
        private AiState _aiState = AiState.Wander;

        // ── Parametri AI ──────────────────────────────────────────────────────
        private float _chaseChance        = 0.60f;
        private int   _sightRange         = 8;    // tile di visibilità (line of sight)
        private float _wanderChangeChance = 0.30f;// probabilità di cambiare direzione in wander

        // ── Cache percorso A* ─────────────────────────────────────────────────
        private List<Point> _path       = new();
        private Point       _pathTarget = new(-1, -1);
        private int         _pathStep   = 0;
        private float       _pathCacheTime = 0f;
        private const float PathCacheMax   = 0.5f; // ricalcola il percorso ogni 0.5s

        // ── Separazione tra bat ───────────────────────────────────────────────
        private static readonly List<Point> _allBatPositions = new();
        private Point _registeredPos;

        // ── Evasione bombe ────────────────────────────────────────────────────
        private HashSet<Point> _dangerTiles = new();
        // ── Bombe solide (non attraversabili) ─────────────────────────────────
        private HashSet<Point> _solidBombTiles = new();

        // ── Direzione wander corrente ─────────────────────────────────────────
        private Point _wanderDir = new(1, 0);

        /// <summary>Aggiorna le tile pericolose (bombe + esplosioni previste).</summary>
        public void SetDangerTiles(IEnumerable<Point> bombTiles, IEnumerable<Point> explosionTiles)
        {
            _dangerTiles.Clear();
            foreach (var t in bombTiles)      _dangerTiles.Add(t);
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
        private bool IsBlocked(Point tile) =>
            _dangerTiles.Contains(tile) || (!_isGhosting && _solidBombTiles.Contains(tile));

        /// <summary>Aumenta velocità e aggressione in base al livello.</summary>
        public void SetAggressionLevel(int level)
        {
            _chaseChance  = Math.Min(0.60f + level * 0.05f, 0.95f);
            moveSpeed     = Math.Min(130f   + level * 8f,   240f);
            waitDuration  = Math.Max(0.35f  - level * 0.02f, 0.08f);
            _sightRange   = Math.Min(8 + level, 16);

            // ── Poteri speciali per livello ───────────────────────────────────
            _canDash    = level >= 3;   // Livello 3+: scatta di 2 tile
            _canGhost   = level >= 5;   // Livello 5+: attraversa bombe solide
            _canSplit   = level >= 7;   // Livello 7+: alla morte spawna 2 mini-bat
            _canBerserk = level >= 9;   // Livello 9+: berserk se giocatore vicino
            _level      = level;
        }

        // ── Poteri speciali ───────────────────────────────────────────────────
        private int   _level      = 0;
        private bool  _canDash    = false;
        private bool  _canGhost   = false;
        private bool  _canSplit   = false;
        private bool  _canBerserk = false;

        // Dash
        private float _dashCooldown    = 0f;
        private const float DashCooldownMax = 3f;
        private const float DashChance     = 0.25f; // 25% chance per step quando disponibile

        // Ghost (attraversa bombe solide)
        private bool  _isGhosting      = false;
        private float _ghostTimer      = 0f;
        private float _ghostCooldown   = 0f;
        private const float GhostDuration    = 2f;
        private const float GhostCooldownMax = 8f;
        private const float GhostChance      = 0.10f;

        // Split (spawn mini-bat alla morte)
        public bool CanSplit => _canSplit && !_isMini;
        private bool _isMini = false; // true = già un mini-bat, non può splittare ancora
        public void SetMini() { _isMini = true; moveSpeed *= 0.8f; }

        // Berserk
        private bool  _isBerserk    = false;
        private float _berserkTimer = 0f;
        private const float BerserkRange    = 3f;
        private const float BerserkDuration = 2f;
        private const float BerserkSpeedMul = 2.2f;

        // Evento split (GameScene ascolta e spawna i mini-bat)
        public event Action<Point>? OnSplit;

        /// <summary>Punti base per uccidere questo bat (aumenta con i poteri).</summary>
        public int KillPoints
        {
            get
            {
                int pts = 100;
                if (_canDash)    pts += 50;   // Livello 3+
                if (_canGhost)   pts += 100;  // Livello 5+
                if (_canSplit)   pts += 150;  // Livello 7+
                if (_canBerserk) pts += 200;  // Livello 9+
                if (_isMini)     pts = 50;    // mini-bat vale meno
                return pts;
            }
        }

        private float waitTimer    = -1f;
        private float waitDuration = 0.35f;

        private bool isDead = false;
        public bool IsDead => isDead;

        public bool IsDeathAnimationFinished =>
            isDead && currentAnimationFrames != null &&
            currentFrame >= currentAnimationFrames.Count - 1;

        private bool isInvincible = false;
        private float invincibilityTimer = 0f;
        public bool IsInvincible => isInvincible;

        internal Bat(Point startTile, string xmlPath, ContentManager content, TileMap map)
        {
            if (!map.IsWalkable(startTile))
                throw new ArgumentException("Start tile must be walkable", nameof(startTile));

            LoadAnimationsFromXml(xmlPath, content);

            TilePosition = startTile;
            Position = new Vector2(TilePosition.X * TileMap.TileSize,
                                   TilePosition.Y * TileMap.TileSize);
            targetPosition = Position;
        }

        private void LoadAnimationsFromXml(string xmlPath, ContentManager content)
        {
            XDocument doc = XDocument.Load(xmlPath);
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
                string fullName = region.Attribute("Name")!.Value;
                if (!int.TryParse(region.Attribute("X")?.Value, out int x)) continue;
                if (!int.TryParse(region.Attribute("Y")?.Value, out int y)) continue;
                if (!int.TryParse(region.Attribute("Width")?.Value, out int w)) continue;
                if (!int.TryParse(region.Attribute("Height")?.Value, out int h)) continue;

                int frameNumberStart = fullName.Length;
                while (frameNumberStart > 0 && char.IsDigit(fullName[frameNumberStart - 1]))
                    frameNumberStart--;

                if (frameNumberStart >= fullName.Length || frameNumberStart == 0) continue;

                string animationName = fullName.Substring(0, frameNumberStart).TrimEnd('_', '-', ' ');
                if (!int.TryParse(fullName.Substring(frameNumberStart), out int frameNumber)) continue;

                if (!temp.ContainsKey(animationName))
                    temp[animationName] = new();
                temp[animationName].Add((frameNumber, new Rectangle(x, y, w, h)));
            }

            animations = temp.ToDictionary(
                p => p.Key,
                p => p.Value.OrderBy(f => f.frame).Select(f => f.rect).ToList()
            );

            if (animations.Count > 0)
            {
                string preferred = new[]
                {
                    "fly_front", "fly_right", "fly_left", "fly_back",
                    "idle", "walk", "fly"
                }.FirstOrDefault(k => animations.ContainsKey(k))
                ?? animations.Keys.FirstOrDefault(k => !k.Equals("dead", StringComparison.OrdinalIgnoreCase))
                ?? animations.Keys.First();

                currentAnimation = preferred;
                currentAnimationFrames = animations[currentAnimation];
            }
        }

        public void SetInvincible(float duration)
        {
            isInvincible = true;
            invincibilityTimer = duration;
        }

        public void SetPlayerTarget(Point playerTile) => _playerTile = playerTile;

        internal void Kill()
        {
            if (isDead) return;
            isDead = true;
            isMoving = false;
            state = BatState.Idle;
            currentFrame = 0;
            animationTimer = 0f;

            // Split: notifica GameScene di spawnare 2 mini-bat
            if (CanSplit)
                OnSplit?.Invoke(TilePosition);

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
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (isInvincible)
            {
                invincibilityTimer -= dt;
                if (invincibilityTimer <= 0f) isInvincible = false;
            }

            if (isDead)
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
                if (_ghostTimer <= 0f) { _isGhosting = false; _ghostCooldown = GhostCooldownMax; }
            }
            else if (_ghostCooldown > 0f) _ghostCooldown -= dt;

            // ── Berserk: attiva se giocatore vicino ───────────────────────────
            if (_canBerserk)
            {
                float distToPlayer = Math.Abs(TilePosition.X - _playerTile.X)
                                   + Math.Abs(TilePosition.Y - _playerTile.Y);
                if (!_isBerserk && distToPlayer <= BerserkRange)
                {
                    _isBerserk    = true;
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
                waitTimer -= dt;
                if (waitTimer <= 0f)
                {
                    Point nextMove = ChooseNextTile(map, dt);

                    if (nextMove != Point.Zero && nextMove != TilePosition)
                    {
                        int ddx = nextMove.X - TilePosition.X;
                        int ddy = nextMove.Y - TilePosition.Y;
                        if      (ddy < 0) facing = Facing.Back;
                        else if (ddy > 0) facing = Facing.Front;
                        else if (ddx < 0) facing = Facing.Left;
                        else              facing = Facing.Right;

                        TilePosition    = nextMove;
                        targetPosition  = new Vector2(nextMove.X * TileMap.TileSize,
                                                      nextMove.Y * TileMap.TileSize);
                        isMoving        = true;
                        state           = BatState.Fly;
                        animationTimer  = 0f;
                        currentFrame    = 0;
                    }

                    waitTimer = waitDuration * (float)(0.8 + _rand.NextDouble() * 0.4);
                }
            }

            if (isMoving)
            {
                // Se la destinazione è diventata bloccata (bomba piazzata nel frattempo), annulla
                if (_solidBombTiles.Contains(TilePosition))
                {
                    // Torna al tile precedente
                    TilePosition    = VisualTilePosition; // il tile più vicino alla posizione attuale
                    targetPosition  = new Vector2(TilePosition.X * TileMap.TileSize,
                                                  TilePosition.Y * TileMap.TileSize);
                    // Forza riallineamento: torna indietro scegliendo una tile libera adiacente
                    var rollback = new[] { new Point(0,-1), new Point(0,1), new Point(-1,0), new Point(1,0) }
                        .Select(d => new Point(TilePosition.X + d.X, TilePosition.Y + d.Y))
                        .FirstOrDefault(t => map.IsWalkable(t) && !_solidBombTiles.Contains(t));
                    if (rollback != Point.Zero)
                    {
                        TilePosition   = rollback;
                        targetPosition = new Vector2(rollback.X * TileMap.TileSize,
                                                     rollback.Y * TileMap.TileSize);
                    }
                    _path.Clear();
                }

                Vector2 direction = targetPosition - Position;
                float distance    = direction.Length();
                float currentSpeed = moveSpeed * (_isBerserk ? BerserkSpeedMul : 1f);

                if (distance <= currentSpeed * dt)
                {
                    Position       = targetPosition;
                    isMoving       = false;
                    state          = BatState.Idle;
                    waitTimer      = (float)(_rand.NextDouble() * waitDuration * 0.5f + waitDuration * 0.2f);
                    currentFrame   = 0;
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
            bool inDanger = _dangerTiles.Contains(TilePosition);

            // ── GHOST: attiva se una bomba solida blocca tutti i percorsi ─────
            if (_canGhost && !_isGhosting && _ghostCooldown <= 0f)
            {
                bool surrounded = new[] { new Point(0,-1), new Point(0,1), new Point(-1,0), new Point(1,0) }
                    .All(d => { var t = new Point(TilePosition.X+d.X, TilePosition.Y+d.Y);
                                return !map.IsWalkable(t) || _solidBombTiles.Contains(t); });
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
            bool canSee = HasLineOfSight(map, TilePosition, _playerTile, _sightRange);
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
                _aiState    = AiState.Wander;
                _playerSeen = false;
            }

            return _aiState switch
            {
                AiState.Chase   => ApplyDash(map, ChaseStep(map)),
                AiState.Patrol  => ApplyDash(map, PatrolStep(map)),
                AiState.Flee    => BfsToSafeTile(map),
                _               => WanderStep(map),
            };
        }

        // ── DASH: prova a saltare un tile extra nella stessa direzione ────────
        private Point ApplyDash(TileMap map, Point nextStep)
        {
            if (!_canDash || _dashCooldown > 0f || nextStep == Point.Zero)
                return nextStep;
            if (_rand.NextDouble() >= DashChance) return nextStep;

            int ddx = nextStep.X - TilePosition.X;
            int ddy = nextStep.Y - TilePosition.Y;
            Point dashTile = new(nextStep.X + ddx, nextStep.Y + ddy);

            bool dashOk = map.IsWalkable(dashTile) &&
                          !_dangerTiles.Contains(dashTile) &&
                          (_isGhosting || !_solidBombTiles.Contains(dashTile));

            if (dashOk)
            {
                _dashCooldown = DashCooldownMax;
                // Aggiorna facing e posizione logica intermedia anche per il tile skippato
                TilePosition = nextStep; // passa dal tile intermedio
                return dashTile;
            }
            return nextStep;
        }

        // ── CHASE: A* verso il giocatore con cache ────────────────────────────
        private Point ChaseStep(TileMap map)
        {
            bool needRepath = _pathStep >= _path.Count
                           || _pathCacheTime >= PathCacheMax
                           || (_path.Count > 0 && _path[^1] != _playerTile);

            if (needRepath)
            {
                _path       = AStarPath(map, TilePosition, _playerTile);
                _pathStep   = 0;
                _pathCacheTime = 0f;
            }

            if (_path.Count == 0) return WanderStep(map);

            // Avanza sul percorso
            while (_pathStep < _path.Count && _path[_pathStep] == TilePosition)
                _pathStep++;

            if (_pathStep >= _path.Count) return WanderStep(map);

            Point next = _path[_pathStep];

            // Separazione: evita tile occupate da altri bat
            if (_allBatPositions.Contains(next) && next != TilePosition)
            {
                // Cerca step successivo libero
                for (int s = _pathStep + 1; s < Math.Min(_pathStep + 3, _path.Count); s++)
                {
                    if (!_allBatPositions.Contains(_path[s]) || _path[s] == TilePosition)
                        return _path[s];
                }
                return WanderStep(map); // aspetta o si sposta lateralmente
            }

            return next;
        }

        // ── PATROL: A* verso ultima posizione nota del giocatore ──────────────
        private Point PatrolStep(TileMap map)
        {
            bool needRepath = _pathStep >= _path.Count
                           || _pathCacheTime >= PathCacheMax
                           || (_path.Count > 0 && _path[^1] != _lastKnownPlayerTile);

            if (needRepath)
            {
                _path      = AStarPath(map, TilePosition, _lastKnownPlayerTile);
                _pathStep  = 0;
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
            {
                // Piccola chance di cambiare direzione comunque (comportamento naturale)
                if (_rand.NextDouble() >= _wanderChangeChance)
                    return preferred;
            }

            // Scegli nuova direzione
            var dirs = new[] { new Point(0,-1), new Point(0,1), new Point(-1,0), new Point(1,0) }
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
            int dist = Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y);
            if (dist > maxRange) return false;

            // Bresenham line
            int x = from.X, y = from.Y;
            int dx = Math.Abs(to.X - x), dy = Math.Abs(to.Y - y);
            int sx = to.X > x ? 1 : -1, sy = to.Y > y ? 1 : -1;
            int err = dx - dy;

            while (x != to.X || y != to.Y)
            {
                if (!map.IsWalkable(new Point(x, y))) return false;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x += sx; }
                if (e2 <  dx) { err += dx; y += sy; }
            }
            return true;
        }

        // ── A* pathfinding ────────────────────────────────────────────────────
        private List<Point> AStarPath(TileMap map, Point from, Point to)
        {
            if (from == to) return new List<Point>();

            var open   = new SortedSet<(float f, int id, Point p)>(
                Comparer<(float f, int id, Point p)>.Create((a, b) =>
                    a.f != b.f ? a.f.CompareTo(b.f) : a.id.CompareTo(b.id)));
            var gScore = new Dictionary<Point, float>();
            var parent = new Dictionary<Point, Point>();
            int idSeq  = 0;

            gScore[from] = 0f;
            open.Add((Heuristic(from, to), idSeq++, from));

            Point[] dirs = { new(0,-1), new(0,1), new(-1,0), new(1,0) };

            while (open.Count > 0)
            {
                var (_, _, cur) = open.Min;
                open.Remove(open.Min);

                if (cur == to)
                {
                    // Ricostruisce percorso
                    var path = new List<Point>();
                    var c = cur;
                    while (c != from) { path.Add(c); c = parent[c]; }
                    path.Reverse();
                    return path;
                }

                foreach (var d in dirs)
                {
                    Point next = new(cur.X + d.X, cur.Y + d.Y);
                    if (!map.IsWalkable(next)) continue;
                    if (IsBlocked(next)) continue;

                    float ng = gScore[cur] + 1f;
                    if (!gScore.TryGetValue(next, out float existing) || ng < existing)
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
            => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        // ── BFS verso tile sicura (fuga bombe) ───────────────────────────────
        private Point BfsToSafeTile(TileMap map)
        {
            if (!_dangerTiles.Contains(TilePosition)) return Point.Zero;

            var queue  = new Queue<Point>();
            var parent = new Dictionary<Point, Point>();
            queue.Enqueue(TilePosition);
            parent[TilePosition] = TilePosition;

            Point[] dirs = { new(0,-1), new(0,1), new(-1,0), new(1,0) };

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
            if (isDead)
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

            string faceName = facing switch
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

            string desired = candidates.FirstOrDefault(c => animations.ContainsKey(c))
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
            else currentFrame = 0;
        }

        internal void Draw(SpriteBatch spriteBatch)
        {
            if (!animations.TryGetValue(currentAnimation, out var frames) ||
                currentFrame >= frames.Count)
                return;

            spriteBatch.Draw(texture, Position, frames[currentFrame], Color.White);
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
    }
}