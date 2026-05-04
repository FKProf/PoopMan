using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using PoopManLibrary;
using PoopManLibrary.Input;
using PoopManLibrary.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.Xml;
using System.Windows.Forms;
using System.Xml.Linq;

namespace PoopMan.GameObjects
{
    public class Miner
    {
        // ── Posizione e movimento ────────────────────────────────────────────
        public Point TilePosition;              // Tile di destinazione (aggiornato subito)
        public Vector2 Position;               // Posizione pixel interpolata
        private Vector2 targetPosition;        // Pixel target del tile di destinazione
        private float moveSpeed = 100f;        // Pixel al secondo
        private bool isMoving = false;
        internal bool IsMoving => isMoving;

        // ── Animazioni ───────────────────────────────────────────────────────
        private Texture2D texture;
        private float animationTimer = 0f;
        private float animationSpeed = 0.15f;  // Secondi per frame
        private int currentFrame = 0;
        private Dictionary<string, List<Rectangle>> animations = new();
        private string currentAnimation = "idle_front";
        private List<Rectangle> currentAnimationFrames;

        // ── Vite ─────────────────────────────────────────────────────────────
        private int lives = 3;
        public int Lives => lives;

        // ── Evento respawn ───────────────────────────────────────────────────
        // Lanciato quando il miner perde una vita ma ne ha ancora
        public event EventHandler? NeedsRespawn;

        // ── Invincibilità (dopo respawn) ─────────────────────────────────────
        private bool isInvincible = false;
        private float invincibilityTimer = 0f;
        private float invincibilityDuration = 2f;   // Secondi di protezione
        private float blinkTimer = 0f;
        private float blinkInterval = 0.1f;         // Secondi tra un blink e l'altro
        private bool blinkVisible = true;
        public bool IsInvincible => isInvincible;

        // ── Bombe grandi ─────────────────────────────────────────────────────
        private int bigBombCount = 0;
        public int BigBombCount => bigBombCount;
        public void AddBigBomb() => bigBombCount++;

        // ── Stato del miner ──────────────────────────────────────────────────
        private enum MinerState
        {
            IdleFront, IdleBack, IdleLeft, IdleRight,
            WalkFront, WalkBack, WalkLeft, WalkRight
        }
        private MinerState state = MinerState.IdleFront;

        // ── Morte ────────────────────────────────────────────────────────────
        private bool isDead = false;
        internal bool IsDead => isDead;
        public event EventHandler? DeathAnimationFinished;
        private bool _deathAnimationFinished = false;
        internal bool IsDeathAnimationFinished => _deathAnimationFinished;

        // ── Segmenti corpo (non usati attivamente, eredità da snake) ─────────
        private List<(Vector2 from, Vector2 to)> _bodySegments = new();
        private float _movementProgress = 0f;
        private Vector2 _currentDirection = Vector2.UnitX;

        // ── Buffer input ─────────────────────────────────────────────────────
        private const int MAX_BUFFER_SIZE = 2;
        private Queue<Vector2> _inputBuffer = new(MAX_BUFFER_SIZE);

        // ── Risorse bombe ────────────────────────────────────────────────────
        private Texture2D itemTexture;
        private Dictionary<string, List<Rectangle>> itemAnimations = new();
        private List<Bomb> bombs = new();
        private Texture2D bombTexture;
        private Dictionary<string, List<Rectangle>> bombAnimations;
        private Texture2D explosionTexture;
        private Dictionary<string, List<Rectangle>> explosionAnimations = new();

        // ── Input mouse ──────────────────────────────────────────────────────

        /// <summary>
        /// Espone i tile attualmente colpiti da esplosioni attive.
        /// Usato da Game1 per controllare collisioni bat/casse.
        /// </summary>
        public IEnumerable<Point> ActiveExplosionTiles =>
            bombs.Where(b => !b.IsFinished)
                 .SelectMany(b => b.ExplosionTiles);

        // ────────────────────────────────────────────────────────────────────
        // Costruttore: carica animazioni, posiziona il miner e carica le risorse
        // per bombe ed esplosioni.
        // ────────────────────────────────────────────────────────────────────
        public Miner(Point startTile, string xmlPath, ContentManager content)
        {
            LoadAnimationsFromXml(xmlPath, content);

            TilePosition = startTile;
            Position = new Vector2(TilePosition.X * TileMap.TileSize,
                                   TilePosition.Y * TileMap.TileSize);
            targetPosition = Position;

            string itemXml = Path.Combine(content.RootDirectory, "image", "items", "items.xml");
            LoadItemAnimations(itemXml, content);

            string explosionXml = Path.Combine(content.RootDirectory, "image", "fxs", "fsx.xml");
            LoadExplosionAnimations(explosionXml, content);

            // Le bombe usano la stessa texture degli item
            bombAnimations = itemAnimations;
            bombTexture = itemTexture;
        }

        // ════════════════════════════════════════════════════════════════════
        // CLASSE INTERNA: Bomb
        // Gestisce il ciclo di vita di una singola bomba: miccia → esplosione
        // ════════════════════════════════════════════════════════════════════
        public class Bomb
        {
            private Vector2 position;
            private Texture2D bombTexture;
            private Dictionary<string, List<Rectangle>> bombAnimations;
            private Texture2D explosionTexture;
            private Dictionary<string, List<Rectangle>> explosionAnimations = new();
            private string currentAnimation;
            private List<Rectangle> currentFrames;
            private int currentFrame = 0;
            private float animationTimer = 0f;
            private float animationSpeed = 0.15f;
            private bool isExploding = false;
            private bool isFinished = false;    // true quando l'animazione di esplosione è finita
            private float fuseTimer = 0f;
            private float fuseDuration = 2f;    // Secondi prima dell'esplosione
            private bool bigBomb;               // true = raggio 2, false = raggio 1

            /// <summary>Tile colpiti dall'esplosione (usato per collisioni e drop).</summary>
            public List<Point> ExplosionTiles { get; private set; } = new();
            public bool IsFinished => isFinished;
            public Vector2 Position => position;
            public bool BigBomb => bigBomb;

            public Bomb(Vector2 pos,
                        Texture2D bombTex,
                        Dictionary<string, List<Rectangle>> bombAnim,
                        Texture2D explTex,
                        Dictionary<string, List<Rectangle>> explAnim,
                        bool big)
            {
                position = pos;
                bombTexture = bombTex;
                bombAnimations = bombAnim;
                explosionTexture = explTex;
                explosionAnimations = explAnim;
                bigBomb = big;

                currentAnimation = bigBomb ? "big_tnt" : "small_tnt";
                currentFrames = bombAnimations[currentAnimation];
                currentFrame = 0;
                animationTimer = 0f;
            }

            // ────────────────────────────────────────────────────────────────
            // Aggiorna la miccia e, una volta scaduta, avvia l'esplosione.
            // ────────────────────────────────────────────────────────────────
            public void Update(GameTime gameTime, TileMap map)
            {
                float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (!isExploding)
                {
                    fuseTimer += delta;

                    // Anima la bomba durante la miccia
                    animationTimer += delta;
                    if (animationTimer >= animationSpeed)
                    {
                        animationTimer = 0f;
                        currentFrame = (currentFrame + 1) % currentFrames.Count;
                    }

                    if (fuseTimer >= fuseDuration)
                        Explode(map);
                }
                else
                {
                    // Anima l'esplosione fino all'ultimo frame
                    animationTimer += delta;
                    if (animationTimer >= animationSpeed)
                    {
                        animationTimer = 0f;
                        currentFrame++;
                        if (currentFrame >= currentFrames.Count)
                            isFinished = true;
                    }
                }
            }

            // ────────────────────────────────────────────────────────────────
            // Calcola i tile colpiti dall'esplosione e rompe i breakable.
            // I tile breakable bloccano la propagazione ma non vengono aggiunti
            // agli ExplosionTiles (il bat nascosto lì è al sicuro).
            // ────────────────────────────────────────────────────────────────
            private void Explode(TileMap map)
            {
                isExploding = true;
                currentAnimation = "explosion";
                currentFrames = explosionAnimations[currentAnimation];
                currentFrame = 0;
                animationTimer = 0f;
                ExplosionTiles.Clear();

                Point center = new((int)(position.X / TileMap.TileSize),
                                   (int)(position.Y / TileMap.TileSize));

                // Aggiunge il centro se non è un muro
                if (map.GetTile(center) != TileType.Wall)
                {
                    ExplosionTiles.Add(center);
                    if (map.GetTile(center) == TileType.Breakable)
                        map.BreakTile(center);
                }

                int range = bigBomb ? 2 : 1;
                int[] dx = { 0, 0, -1, 1 };
                int[] dy = { -1, 1, 0, 0 };

                for (int dir = 0; dir < 4; dir++)
                {
                    for (int step = 1; step <= range; step++)
                    {
                        Point t = new(center.X + dx[dir] * step,
                                      center.Y + dy[dir] * step);

                        if (!map.IsInside(t)) break;

                        if (map.GetTile(t) == TileType.Wall)
                            break; // Muro indistruttibile: blocca propagazione

                        if (map.GetTile(t) == TileType.Breakable)
                        {
                            map.BreakTile(t); // Rompe il breakable ma NON lo aggiunge
                            break;
                        }

                        ExplosionTiles.Add(t); // Tile libero: aggiunge e continua
                    }
                }
            }

            public void Draw(SpriteBatch spriteBatch)
            {
                if (isFinished) return;

                if (isExploding)
                {
                    // Disegna il frame dell'esplosione su ogni tile colpito
                    foreach (var tile in ExplosionTiles)
                    {
                        Vector2 drawPos = new(tile.X * TileMap.TileSize,
                                             tile.Y * TileMap.TileSize);
                        spriteBatch.Draw(explosionTexture, drawPos,
                            currentFrames[Math.Min(currentFrame, currentFrames.Count - 1)],
                            Color.White);
                    }
                }
                else
                {
                    spriteBatch.Draw(bombTexture, position,
                        currentFrames[Math.Min(currentFrame, currentFrames.Count - 1)],
                        Color.White);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // AGGIORNAMENTO PRINCIPALE
        // ════════════════════════════════════════════════════════════════════
        public void Update(TileMap map, GameTime gameTime)
        {
            // Se morto, aggiorna solo l'animazione di morte
            if (isDead)
            {
                UpdateAnimation(gameTime);
                return;
            }

            // ── Invincibilità e blink ────────────────────────────────────────
            if (isInvincible)
            {
                invincibilityTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                blinkTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (blinkTimer <= 0f)
                {
                    blinkVisible = !blinkVisible;
                    blinkTimer = blinkInterval;
                }

                if (invincibilityTimer <= 0f)
                {
                    isInvincible = false;
                    blinkVisible = true; // Assicura visibilità al termine
                }
            }

            HandleInput();
            
            // ── Piazzamento bomba piccola (click sinistro) ───────────────────
            if (GameController.MiniBomb())
            {
                bool alreadyBombHere = bombs.Any(b =>
                {
                    Point bombTile = new((int)(b.Position.X / TileMap.TileSize),
                                          (int)(b.Position.Y / TileMap.TileSize));
                    return bombTile == TilePosition && !b.IsFinished;
                });

                if (!alreadyBombHere)
                    bombs.Add(new Bomb(
                        new Vector2(TilePosition.X * TileMap.TileSize, TilePosition.Y * TileMap.TileSize),
                        bombTexture, bombAnimations, explosionTexture, explosionAnimations, false));
            }
            // ── Piazzamento bomba grande (click destro, se disponibile) ──────
            else if (GameController.BigBomb() && bigBombCount > 0)
            {
                bool alreadyBombHere = bombs.Any(b =>
                {
                    Point bombTile = new((int)(b.Position.X / TileMap.TileSize),
                                          (int)(b.Position.Y / TileMap.TileSize));
                    return bombTile == TilePosition && !b.IsFinished;
                });

                if (!alreadyBombHere)
                {
                    bigBombCount--;
                    bombs.Add(new Bomb(
                        new Vector2(TilePosition.X * TileMap.TileSize, TilePosition.Y * TileMap.TileSize),
                        bombTexture, bombAnimations, explosionTexture, explosionAnimations, true));
                }
            }

            // ── Aggiornamento bombe attive ───────────────────────────────────
            for (int i = bombs.Count - 1; i >= 0; i--)
            {
                Bomb bomb = bombs[i];
                bomb.Update(gameTime, map);

                // Quando l'esplosione è finita, prova a rompere i tile rimanenti e rimuovi
                if (bomb.IsFinished)
                {
                    foreach (var t in bomb.ExplosionTiles)
                        map.BreakTile(t);
                    bombs.RemoveAt(i);
                }
            }

            UpdateMovement(map, gameTime);
            UpdateAnimation(gameTime);
        }

        // ────────────────────────────────────────────────────────────────────
        // Gestisce il movimento tile-per-tile del miner consumando il buffer
        // di input e interpolando la posizione pixel.
        // ───────────────────────────────────────────────────────────────────

        private void UpdateMovement(TileMap map, GameTime gameTime)
        {
            if (!isMoving)
            {
                if (_inputBuffer.Count > 0)
                {
                    _currentDirection = _inputBuffer.Dequeue();

                    Point nextTile = new(TilePosition.X + (int)_currentDirection.X,
                                         TilePosition.Y + (int)_currentDirection.Y);

                    if (map.IsWalkable(nextTile))
                    {
                        // Aggiorna segmenti corpo (legacy snake)
                        for (int i = _bodySegments.Count - 1; i > 0; i--)
                            _bodySegments[i] = (_bodySegments[i].to, _bodySegments[i - 1].to);
                        if (_bodySegments.Count > 0)
                            _bodySegments[0] = (_bodySegments[0].to, targetPosition);

                        TilePosition = nextTile;
                        targetPosition = new Vector2(nextTile.X * TileMap.TileSize,
                                                     nextTile.Y * TileMap.TileSize);
                        isMoving = true;
                        currentFrame = 0;
                        animationTimer = 0f;
                        _movementProgress = 0f;

                        // Imposta animazione di camminata nella direzione corretta
                        state = _currentDirection switch
                        {
                            var d when d == Vector2.UnitX => MinerState.WalkRight,
                            var d when d == -Vector2.UnitX => MinerState.WalkLeft,
                            var d when d == -Vector2.UnitY => MinerState.WalkBack,
                            _ => MinerState.WalkFront
                        };
                    }
                    else
                    {
                        // Tile non percorribile: torna in idle nella direzione attuale
                        state = state switch
                        {
                            MinerState.WalkFront => MinerState.IdleFront,
                            MinerState.WalkBack => MinerState.IdleBack,
                            MinerState.WalkLeft => MinerState.IdleLeft,
                            MinerState.WalkRight => MinerState.IdleRight,
                            _ => state
                        };
                    }
                }
            }

            if (isMoving)
            {
                float distance = moveSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
                Vector2 direction = targetPosition - Position;

                if (direction.Length() <= distance)
                {
                    // Arrivato al tile di destinazione
                    Position = targetPosition;
                    isMoving = false;
                    _movementProgress = 1f;

                    state = state switch
                    {
                        MinerState.WalkFront => MinerState.IdleFront,
                        MinerState.WalkBack => MinerState.IdleBack,
                        MinerState.WalkLeft => MinerState.IdleLeft,
                        MinerState.WalkRight => MinerState.IdleRight,
                        _ => state
                    };
                }
                else
                {
                    // Interpola verso il tile di destinazione
                    Position += Vector2.Normalize(direction) * distance;
                    _movementProgress = MathHelper.Clamp(
                        _movementProgress + distance / TileMap.TileSize, 0f, 1f);
                }
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Legge l'input e lo aggiunge al buffer, evitando duplicati consecutivi.
        // ────────────────────────────────────────────────────────────────────
        private void HandleInput()
        {
            var potentialNextDirection = Vector2.Zero;

            if (GameController.HoldUp()) potentialNextDirection = -Vector2.UnitY;
            if (GameController.HoldDown()) potentialNextDirection = Vector2.UnitY;
            if (GameController.HoldLeft()) potentialNextDirection = -Vector2.UnitX;
            if (GameController.HoldRight()) potentialNextDirection = Vector2.UnitX;
            
            if (potentialNextDirection == Vector2.Zero)
            {
                _inputBuffer.Clear();
                return;
            }

            var last = _inputBuffer.Count > 0 ? _inputBuffer.Last() : Vector2.Zero;
            if (last == potentialNextDirection) return; // Evita duplicati

            if (_inputBuffer.Count < MAX_BUFFER_SIZE)
                _inputBuffer.Enqueue(potentialNextDirection);
        }

        // ────────────────────────────────────────────────────────────────────
        // Sceglie l'animazione corretta in base allo stato e la fa avanzare.
        // Gestisce separatamente il caso di morte (animazione non ciclica).
        // ────────────────────────────────────────────────────────────────────
        private void UpdateAnimation(GameTime gameTime)
        {
            if (isDead)
            {
                // Forza animazione "dead" se disponibile
                if (animations.ContainsKey("dead") && currentAnimation != "dead")
                {
                    currentAnimation = "dead";
                    currentAnimationFrames = animations[currentAnimation];
                    currentFrame = 0;
                    animationTimer = 0f;
                }

                if (currentAnimationFrames == null || currentAnimationFrames.Count == 0)
                    return;

                if (_deathAnimationFinished) return; // Rimane sull'ultimo frame

                animationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (animationTimer >= animationSpeed)
                {
                    animationTimer = 0f;
                    currentFrame++;
                    if (currentFrame >= currentAnimationFrames.Count)
                    {
                        currentFrame = currentAnimationFrames.Count - 1;
                        _deathAnimationFinished = true;
                    }
                }
                return;
            }

            // Animazione normale: cambia se lo stato è cambiato
            string newAnimation = GetAnimationName(state);
            if (newAnimation != currentAnimation)
            {
                currentAnimation = newAnimation;
                currentAnimationFrames = animations.GetValueOrDefault(newAnimation);
                currentFrame = 0;
                animationTimer = 0f;
            }

            // Avanza il frame se l'animazione ha più di un frame
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

        private string GetAnimationName(MinerState state) => state switch
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

        // ────────────────────────────────────────────────────────────────────
        // Carica le animazioni del miner dall'XML.
        // Raggruppa i frame per nome base (es. idle_front0..N → "idle_front").
        // ────────────────────────────────────────────────────────────────────
        private void LoadAnimationsFromXml(string xmlPath, ContentManager content)
        {
            if (!File.Exists(xmlPath))
                throw new FileNotFoundException($"File XML animazioni non trovato: {xmlPath}");

            XDocument doc = XDocument.Load(xmlPath);
            animations.Clear();

            foreach (var region in doc.Descendants("Region"))
            {
                string fullName = region.Attribute("Name")?.Value ?? "";
                int x = int.Parse(region.Attribute("X")?.Value ?? "0");
                int y = int.Parse(region.Attribute("Y")?.Value ?? "0");
                int width = int.Parse(region.Attribute("Width")?.Value ?? "32");
                int height = int.Parse(region.Attribute("Height")?.Value ?? "32");

                int i = fullName.Length;
                while (i > 0 && char.IsDigit(fullName[i - 1]))
                    i--;
                string animName = fullName.Substring(0, i).TrimEnd('_', '-', ' ');
                if (string.IsNullOrEmpty(animName)) continue;

                if (!animations.ContainsKey(animName))
                    animations[animName] = new List<Rectangle>();
                animations[animName].Add(new Rectangle(x, y, width, height));
            }

            var texturePath = doc.Descendants("Texture").FirstOrDefault()?.Value
                              ?? "image/character/miner";
            texture = content.Load<Texture2D>(texturePath);
            currentAnimationFrames = animations["idle_front"];
        }

        // ────────────────────────────────────────────────────────────────────
        // Carica le animazioni degli item/bombe dall'XML.
        // ────────────────────────────────────────────────────────────────────
        private void LoadItemAnimations(string xmlPath, ContentManager content)
        {
            if (!File.Exists(xmlPath))
                throw new FileNotFoundException($"File XML item non trovato: {xmlPath}");

            XDocument doc = XDocument.Load(xmlPath);
            itemAnimations.Clear();

            foreach (var region in doc.Descendants("Region"))
            {
                string fullName = region.Attribute("Name")?.Value ?? "";
                int x = int.Parse(region.Attribute("X")?.Value ?? "0");
                int y = int.Parse(region.Attribute("Y")?.Value ?? "0");
                int width = int.Parse(region.Attribute("Width")?.Value ?? "32");
                int height = int.Parse(region.Attribute("Height")?.Value ?? "32");

                int i = fullName.Length;
                while (i > 0 && char.IsDigit(fullName[i - 1]))
                    i--;
                string animName = fullName.Substring(0, i).TrimEnd('_', '-', ' ');
                if (string.IsNullOrEmpty(animName))
                    animName = fullName;

                if (!itemAnimations.ContainsKey(animName))
                    itemAnimations[animName] = new List<Rectangle>();
                itemAnimations[animName].Add(new Rectangle(x, y, width, height));
            }

            var texturePath = doc.Descendants("Texture").FirstOrDefault()?.Value
                              ?? "image/items/items";
            itemTexture = content.Load<Texture2D>(texturePath);
        }

        // ────────────────────────────────────────────────────────────────────
        // Carica le animazioni delle esplosioni dall'XML.
        // ────────────────────────────────────────────────────────────────────
        private void LoadExplosionAnimations(string xmlPath, ContentManager content)
        {
            if (!File.Exists(xmlPath))
                throw new FileNotFoundException($"File XML esplosioni non trovato: {xmlPath}");

            XDocument doc = XDocument.Load(xmlPath);
            explosionAnimations.Clear();

            foreach (var region in doc.Descendants("Region"))
            {
                string fullName = region.Attribute("Name")?.Value ?? "";
                int x = int.Parse(region.Attribute("X")?.Value ?? "0");
                int y = int.Parse(region.Attribute("Y")?.Value ?? "0");
                int width = int.Parse(region.Attribute("Width")?.Value ?? "32");
                int height = int.Parse(region.Attribute("Height")?.Value ?? "32");

                int i = fullName.Length;
                while (i > 0 && char.IsDigit(fullName[i - 1]))
                    i--;
                string animName = fullName.Substring(0, i).TrimEnd('_', '-', ' ');
                if (string.IsNullOrEmpty(animName))
                    animName = fullName;

                if (!explosionAnimations.ContainsKey(animName))
                    explosionAnimations[animName] = new List<Rectangle>();
                explosionAnimations[animName].Add(new Rectangle(x, y, width, height));
            }

            var texturePath = doc.Descendants("Texture").FirstOrDefault()?.Value
                              ?? "image/fxs/fsx";
            explosionTexture = content.Load<Texture2D>(texturePath);
        }

        // ────────────────────────────────────────────────────────────────────
        // Disegna il miner (con eventuale blink durante invincibilità)
        // e tutte le bombe/esplosioni attive.
        // ────────────────────────────────────────────────────────────────────
        public void Draw(SpriteBatch spriteBatch)
        {
            if (currentAnimationFrames == null || currentAnimationFrames.Count == 0) return;
            if (currentFrame >= currentAnimationFrames.Count) currentFrame = 0;

            // Non disegnare durante i frame "blink off"
            if (!blinkVisible) return;

            foreach (var bomb in bombs)
                bomb.Draw(spriteBatch);

            spriteBatch.Draw(texture, Position,
                currentAnimationFrames[currentFrame], Color.White);
        }

        // ────────────────────────────────────────────────────────────────────
        // Gestisce la perdita di una vita:
        // - Se ha ancora vite → respawn con invincibilità
        // - Se non ha più vite → animazione di morte definitiva
        // ────────────────────────────────────────────────────────────────────
        public void Kill()
        {
            if (isDead) return;

            lives--;

            if (lives > 0)
            {
                // Ancora vite: ferma il movimento e notifica Game1 per il respawn
                isMoving = false;
                _movementProgress = 0f;
                _inputBuffer.Clear();
                NeedsRespawn?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Nessuna vita rimasta: avvia animazione di morte
            isDead = true;
            isMoving = false;
            _movementProgress = 0f;
            _deathAnimationFinished = false;

            if (animations.ContainsKey("dead"))
            {
                currentAnimation = "dead";
                currentAnimationFrames = animations[currentAnimation];

                if (currentAnimationFrames?.Count <= 1)
                {
                    _deathAnimationFinished = true;
                    DeathAnimationFinished?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                // Fallback se "dead" non esiste: usa idle_front
                string preferred = new[] { "idle_front", "idle_back", "idle_left", "idle_right", "idle" }
                    .FirstOrDefault(k => animations.ContainsKey(k))
                    ?? animations.Keys.FirstOrDefault();

                if (preferred != null)
                {
                    currentAnimation = preferred;
                    currentAnimationFrames = animations[currentAnimation];
                }

                _deathAnimationFinished = true;
                DeathAnimationFinished?.Invoke(this, EventArgs.Empty);
            }

            currentFrame = 0;
            animationTimer = 0f;
        }

        // ────────────────────────────────────────────────────────────────────
        // Riposiziona il miner dopo la perdita di una vita.
        // Attiva l'invincibilità lampeggiante per proteggere il respawn.
        // ────────────────────────────────────────────────────────────────────
        public void Respawn(Point spawnTile)
        {
            TilePosition = spawnTile;
            Position = new Vector2(spawnTile.X * TileMap.TileSize,
                                   spawnTile.Y * TileMap.TileSize);
            targetPosition = Position;
            isMoving = false;
            _inputBuffer.Clear();
            _movementProgress = 0f;
            state = MinerState.IdleFront;
            currentAnimation = "idle_front";
            currentAnimationFrames = animations["idle_front"];
            currentFrame = 0;
            animationTimer = 0f;

            isInvincible = true;
            invincibilityTimer = invincibilityDuration;
            blinkTimer = blinkInterval;
            blinkVisible = true;
        }

        public Collision GetBounds()
        {
            if (!animations.TryGetValue(currentAnimation, out var frames) || frames.Count == 0)
                return Collision.Empty;

            var frame = frames[Math.Min(currentFrame, frames.Count - 1)];
            return new Collision(
                (int)Position.X + frame.Width / 2,
                (int)Position.Y + frame.Height / 2,
                frame.Width / 2);
        }

        // ────────────────────────────────────────────────────────────────────
        // Reset completo per il cambio livello:
        // azzera posizione, bombe e invincibilità senza toccare le vite.
        // ────────────────────────────────────────────────────────────────────
        public void ResetForNewLevel(Point spawnTile)
        {
            TilePosition = spawnTile;
            Position = new Vector2(spawnTile.X * TileMap.TileSize,
                                   spawnTile.Y * TileMap.TileSize);
            targetPosition = Position;
            isMoving = false;
            _inputBuffer.Clear();
            _movementProgress = 0f;
            state = MinerState.IdleFront;
            currentAnimation = "idle_front";
            currentAnimationFrames = animations["idle_front"];
            currentFrame = 0;
            animationTimer = 0f;
            bombs.Clear();      // Rimuovi tutte le bombe piazzate
            bigBombCount = 0;   // Resetta le bombe grandi
            isInvincible = false;
            invincibilityTimer = 0f;
            blinkVisible = true;
        }
    }
}