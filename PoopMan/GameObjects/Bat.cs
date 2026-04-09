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
    internal class Bat
    {
        // ── Posizione e movimento ────────────────────────────────────────────
        public Point TilePosition;              // Tile attuale (aggiornato alla partenza)
        public Vector2 Position;               // Posizione pixel interpolata
        private Vector2 targetPosition;        // Pixel di destinazione del tile corrente
        private float moveSpeed = 90f;         // Pixel al secondo
        private bool isMoving = false;

        // ── Animazione ───────────────────────────────────────────────────────
        private Texture2D texture;
        private float animationTimer = 0f;
        private float animationSpeed = 0.12f;  // Secondi per frame (più veloce del miner)
        private int currentFrame = 0;
        private Dictionary<string, List<Rectangle>> animations = new();
        private string currentAnimation = "idle";
        private List<Rectangle> currentAnimationFrames;

        // ── Stato e direzione ────────────────────────────────────────────────
        private enum BatState { Idle, Fly }
        private BatState state = BatState.Idle;

        private enum Facing { Front, Back, Left, Right }
        private Facing facing = Facing.Front;

        // ── Randomizzazione condivisa ────────────────────────────────────────
        // Static per evitare seed identici con istanze create nello stesso ms
        private static readonly Random _rand = new();

        // ── Timer attesa tra un movimento e l'altro ──────────────────────────
        private float waitTimer = 0f;
        private float waitDuration = 0.3f;     // Durata iniziale; poi randomizzata

        // ── Morte ────────────────────────────────────────────────────────────
        private bool isDead = false;
        public bool IsDead => isDead;

        /// <summary>
        /// true quando il bat è morto e l'animazione di morte ha raggiunto l'ultimo frame.
        /// Game1 lo usa per rimuovere il bat dalla lista.
        /// </summary>
        public bool IsDeathAnimationFinished =>
            isDead && currentFrame >= currentAnimationFrames.Count - 1;

        // ── Invincibilità temporanea (dopo spawn da cassa) ───────────────────
        private bool isInvincible = false;
        private float invincibilityTimer = 0f;
        public bool IsInvincible => isInvincible;

        // ────────────────────────────────────────────────────────────────────
        // Costruttore: il tile di partenza deve essere walkable.
        // ────────────────────────────────────────────────────────────────────
        internal Bat(Point startTile, string xmlPath, ContentManager content, TileMap map)
        {
            if (!map.IsWalkable(startTile))
                throw new ArgumentException("Start tile must be empty/walkable", nameof(startTile));

            LoadAnimationsFromXml(xmlPath, content);

            TilePosition = startTile;
            Position = new Vector2(TilePosition.X * TileMap.TileSize,
                                   TilePosition.Y * TileMap.TileSize);
            targetPosition = Position;
        }

        // ────────────────────────────────────────────────────────────────────
        // Carica le animazioni dall'XML.
        // Il formato atteso è: Name="animazione_N" dove N è il numero del frame.
        // I frame vengono ordinati per numero e raggruppati per nome base.
        // ────────────────────────────────────────────────────────────────────
        private void LoadAnimationsFromXml(string xmlPath, ContentManager content)
        {
            XDocument doc = XDocument.Load(xmlPath);
            var root = doc.Root ?? throw new InvalidOperationException($"XML root missing in {xmlPath}");

            var textureEl = root.Element("Texture")
                ?? throw new InvalidOperationException($"Missing <Texture> element in {xmlPath}");
            texture = content.Load<Texture2D>(textureEl.Value);

            var regionElements = root.Descendants("Region")
                .Where(r => r.Attribute("Name") != null);

            if (!regionElements.Any())
                throw new InvalidOperationException($"No <Region> elements with Name in {xmlPath}");

            // Accumula frame con numero per ordinarli correttamente
            var temp = new Dictionary<string, List<(int frame, Rectangle rect)>>();

            foreach (var region in regionElements)
            {
                string fullName = region.Attribute("Name")!.Value;
                if (!int.TryParse(region.Attribute("X")?.Value, out int x)) continue;
                if (!int.TryParse(region.Attribute("Y")?.Value, out int y)) continue;
                if (!int.TryParse(region.Attribute("Width")?.Value, out int w)) continue;
                if (!int.TryParse(region.Attribute("Height")?.Value, out int h)) continue;

                // Separa il numero finale dal nome (es. "fly_front3" → "fly_front" + 3)
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

            // Ordina i frame per numero e crea il dizionario finale
            animations = temp.ToDictionary(
                p => p.Key,
                p => p.Value.OrderBy(f => f.frame).Select(f => f.rect).ToList()
            );

            if (animations.Count > 0)
            {
                // Preferisce animazioni di volo/idle, evita "dead" come animazione iniziale
                string preferred = new[]
                {
                    "fly_front", "fly_right", "fly_left", "fly_back",
                    "idle", "walk", "fly", "dead"
                }.FirstOrDefault(k => animations.ContainsKey(k))
                ?? animations.Keys.FirstOrDefault(k =>
                    !k.Equals("dead", StringComparison.OrdinalIgnoreCase))
                ?? animations.Keys.First();

                currentAnimation = preferred;
                currentAnimationFrames = animations[currentAnimation];
            }
        }

        /// <summary>
        /// Attiva l'invincibilità per <paramref name="duration"/> secondi.
        /// Usato quando il bat spawna da una cassa per proteggerlo dall'esplosione corrente.
        /// </summary>
        public void SetInvincible(float duration)
        {
            isInvincible = true;
            invincibilityTimer = duration;
        }

        /// <summary>
        /// Segna il bat come morto e avvia l'animazione di morte.
        /// Idempotente: chiamate successive non hanno effetto.
        /// </summary>
        internal void Kill()
        {
            if (isDead) return;

            isDead = true;
            isMoving = false;
            state = BatState.Idle;
            currentFrame = 0;
            animationTimer = 0f;

            if (animations.ContainsKey("dead"))
            {
                currentAnimation = "dead";
                currentAnimationFrames = animations[currentAnimation];
            }
            else
            {
                // Fallback se l'animazione "dead" non esiste
                if (animations.Count > 0)
                {
                    currentAnimation = animations.Keys.First();
                    currentAnimationFrames = animations[currentAnimation];
                }
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Aggiornamento principale: invincibilità, movimento casuale, animazione.
        // ────────────────────────────────────────────────────────────────────
        internal void Update(TileMap map, GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // ── Decremento invincibilità ─────────────────────────────────────
            if (isInvincible)
            {
                invincibilityTimer -= dt;
                if (invincibilityTimer <= 0f)
                    isInvincible = false;
            }

            // ── Se morto aggiorna solo l'animazione ──────────────────────────
            if (isDead)
            {
                UpdateAnimation(gameTime);
                return;
            }

            // ── Scelta prossima direzione (quando fermo) ─────────────────────
            if (!isMoving)
            {
                waitTimer -= dt;
                if (waitTimer <= 0f)
                {
                    // Prova le 4 direzioni in ordine casuale
                    var dirs = new List<Vector2> { -Vector2.UnitY, Vector2.UnitY, -Vector2.UnitX, Vector2.UnitX }
                        .OrderBy(_ => _rand.Next())
                        .ToList();

                    foreach (var d in dirs)
                    {
                        Point nextTile = new Point(
                            TilePosition.X + (int)d.X,
                            TilePosition.Y + (int)d.Y);

                        if (map.IsWalkable(nextTile))
                        {
                            // Aggiorna facing per l'animazione direzionale
                            if (d == -Vector2.UnitY) facing = Facing.Back;
                            else if (d == Vector2.UnitY) facing = Facing.Front;
                            else if (d == -Vector2.UnitX) facing = Facing.Left;
                            else facing = Facing.Right;

                            TilePosition = nextTile;
                            targetPosition = new Vector2(nextTile.X * TileMap.TileSize,
                                                         nextTile.Y * TileMap.TileSize);
                            isMoving = true;
                            state = BatState.Fly;
                            animationTimer = 0f;
                            currentFrame = 0;
                            break;
                        }
                    }

                    // Attesa casuale tra 0.2 e 1.0 secondi prima del prossimo tentativo
                    waitDuration = (float)(_rand.NextDouble() * 0.8 + 0.2);
                    waitTimer = waitDuration;
                }
            }

            // ── Interpolazione verso il tile di destinazione ─────────────────
            if (isMoving)
            {
                Vector2 direction = targetPosition - Position;
                float distance = direction.Length();

                if (distance <= moveSpeed * dt)
                {
                    Position = targetPosition;
                    isMoving = false;
                    state = BatState.Idle;
                    waitDuration = (float)(_rand.NextDouble() * 0.8 + 0.2);
                    waitTimer = waitDuration;
                    currentFrame = 0;
                    animationTimer = 0f;
                }
                else
                {
                    Position += Vector2.Normalize(direction) * moveSpeed * dt;
                }
            }

            UpdateAnimation(gameTime);
        }

        // ────────────────────────────────────────────────────────────────────
        // Sceglie l'animazione appropriata in base a stato e direzione,
        // con fallback progressivi per animazioni mancanti.
        // ────────────────────────────────────────────────────────────────────
        private void UpdateAnimation(GameTime gameTime)
        {
            if (isDead)
            {
                // Forza "dead" se disponibile
                if (animations.ContainsKey("dead") && currentAnimation != "dead")
                {
                    currentAnimation = "dead";
                    currentAnimationFrames = animations[currentAnimation];
                    currentFrame = 0;
                    animationTimer = 0f;
                }

                // Avanza i frame (ciclica anche da morto, per l'animazione di morte)
                if (currentAnimationFrames?.Count > 1)
                {
                    animationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                    if (animationTimer >= animationSpeed)
                    {
                        animationTimer = 0f;
                        currentFrame++;
                        if (currentFrame >= currentAnimationFrames.Count)
                            currentFrame = 0; // Cicla l'animazione di morte
                    }
                }
                else currentFrame = 0;
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

            // Costruisce lista di candidati con priorità decrescente
            var candidates = new List<string>();

            if (state == BatState.Fly)
            {
                candidates.Add($"fly_{faceName}"); // Animazione direzionale specifica
                candidates.Add("fly");             // Fallback generico
                candidates.Add("walk");
            }
            else // Idle
            {
                candidates.Add("idle");
                candidates.Add($"idle_{faceName}");
                candidates.Add($"fly_{faceName}"); // Usa fly come posa idle se idle non esiste
                candidates.Add("fly");

                // Qualsiasi animazione non-dead come ultimo fallback sicuro
                var nonDead = animations.Keys.FirstOrDefault(k =>
                    !k.Equals("dead", StringComparison.OrdinalIgnoreCase));
                if (nonDead != null) candidates.Add(nonDead);

                candidates.Add("dead"); // Solo come ultima risorsa assoluta
            }

            if (animations.Count > 0)
                candidates.Add(animations.Keys.First()); // Safety net finale

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

        /// <summary>
        /// Restituisce la hitbox circolare del bat per le collisioni.
        /// Compatibile con il formato usato da Miner.GetBounds().
        /// </summary>
        public Collision GetBounds()
        {
            if (!animations.TryGetValue(currentAnimation, out var frames) || frames.Count == 0)
                return Collision.Empty;

            var frame = frames[Math.Min(currentFrame, frames.Count - 1)];
            return new Collision(
                (int)(Position.X + frame.Width * 0.5f),
                (int)(Position.Y + frame.Height * 0.5f),
                (int)(frame.Width * 0.5f));
        }
    }
}