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
        private float _chaseChance = 0.55f;

        /// <summary>Aumenta velocità e aggressione in base al livello.</summary>
        public void SetAggressionLevel(int level)
        {
            _chaseChance = Math.Min(0.55f + level * 0.05f, 0.90f);
            moveSpeed = Math.Min(130f + level * 5f, 220f);
            waitDuration = Math.Max(0.35f - level * 0.02f, 0.10f);
        }

        private float waitTimer = -1f;   // -1 = muoviti subito al primo frame
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
                UpdateAnimation(gameTime);
                return;
            }

            if (!isMoving)
            {
                waitTimer -= dt;
                if (waitTimer <= 0f)
                {
                    var dirs = new List<Vector2> { -Vector2.UnitY, Vector2.UnitY, -Vector2.UnitX, Vector2.UnitX };

                    if (_rand.NextDouble() < _chaseChance)
                    {
                        int dx = _playerTile.X - TilePosition.X;
                        int dy = _playerTile.Y - TilePosition.Y;

                        dirs.Sort((a, b) =>
                        {
                            int scoreA = (int)(a.X * dx + a.Y * dy);
                            int scoreB = (int)(b.X * dx + b.Y * dy);
                            return scoreB.CompareTo(scoreA);
                        });
                    }
                    else
                    {
                        dirs = dirs.OrderBy(_ => _rand.Next()).ToList();
                    }

                    foreach (var d in dirs)
                    {
                        Point nextTile = new Point(
                            TilePosition.X + (int)d.X,
                            TilePosition.Y + (int)d.Y);

                        if (map.IsWalkable(nextTile))
                        {
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

                    waitDuration = (float)(_rand.NextDouble() * (waitDuration * 0.8f) + waitDuration * 0.2f);
                    waitTimer = waitDuration;
                }
            }

            if (isMoving)
            {
                Vector2 direction = targetPosition - Position;
                float distance = direction.Length();

                if (distance <= moveSpeed * dt)
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
                    Position += Vector2.Normalize(direction) * moveSpeed * dt;
                }
            }

            UpdateAnimation(gameTime);
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