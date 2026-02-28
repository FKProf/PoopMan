using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PoopManLibrary.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace PoopManLibrary.Entities
{
    public class Player
    {
        public Point TilePosition;
        public Vector2 Position;

        private Vector2 targetPosition;
        private float moveSpeed = 120f;
        private bool isMoving = false;

        private Texture2D texture;

        private float animationTimer = 0f;
        private float animationSpeed = 0.15f;
        private int currentFrame = 0;

        private Dictionary<string, List<Rectangle>> animations = new();
        private string currentAnimation = "idle_front";

        private enum PlayerState
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

        private PlayerState state = PlayerState.IdleFront;

        public Player(Point startTile, string xmlPath, ContentManager content)
        {
            LoadAnimationsFromXml(xmlPath, content);

            TilePosition = startTile;
            Position = new Vector2(TilePosition.X * TileMap.TileSize,
                                   TilePosition.Y * TileMap.TileSize);

            targetPosition = Position;
        }

        private void LoadAnimationsFromXml(string xmlPath, ContentManager content)
        {
            XDocument doc = XDocument.Load(xmlPath);
            string texturePath = doc.Root.Element("Texture").Value;
            texture = content.Load<Texture2D>(texturePath);

            // Carica tutte le animazioni e frame
            var temp = new Dictionary<string, List<(int frame, Rectangle rect)>>();

            foreach (var region in doc.Root.Element("Regions").Elements("Region"))
            {
                string fullName = region.Attribute("Name").Value;
                int x = int.Parse(region.Attribute("X").Value);
                int y = int.Parse(region.Attribute("Y").Value);
                int w = int.Parse(region.Attribute("Width").Value);
                int h = int.Parse(region.Attribute("Height").Value);

                string animationName = fullName.Substring(0, fullName.LastIndexOf('_'));
                int frameNumber = int.Parse(fullName.Substring(fullName.LastIndexOf('_') + 1)) - 1;

                if (!temp.ContainsKey(animationName))
                    temp[animationName] = new List<(int, Rectangle)>();

                temp[animationName].Add((frameNumber, new Rectangle(x, y, w, h)));
            }

            animations = new Dictionary<string, List<Rectangle>>();
            foreach (var pair in temp)
            {
                animations[pair.Key] = pair.Value
                    .OrderBy(f => f.frame)
                    .Select(f => f.rect)
                    .ToList();

                Console.WriteLine($"Animazione: {pair.Key}, Frame count: {animations[pair.Key].Count}");
                for (int i = 0; i < animations[pair.Key].Count; i++)
                {
                    var r = animations[pair.Key][i];
                    Console.WriteLine($"\tFrame {i}: X:{r.X} Y:{r.Y} W:{r.Width} H:{r.Height}");
                }
            }
            Console.WriteLine("=== FINE CARICAMENTO ANIMAZIONI ===");
        }

        public void Update(TileMap map, KeyboardState keyboard, GameTime gameTime)
        {
            bool moved = false;
            Point nextTile = TilePosition;

            if (!isMoving)
            {
                bool up = keyboard.IsKeyDown(Keys.W);
                bool down = keyboard.IsKeyDown(Keys.S);
                bool left = keyboard.IsKeyDown(Keys.A);
                bool right = keyboard.IsKeyDown(Keys.D);

                if (right) { nextTile.X++; state = PlayerState.WalkRight; moved = true; }
                else if (left) { nextTile.X--; state = PlayerState.WalkLeft; moved = true; }
                else if (up) { nextTile.Y--; state = PlayerState.WalkBack; moved = true; }
                else if (down) { nextTile.Y++; state = PlayerState.WalkFront; moved = true; }

                if (moved && map.IsWalkable(nextTile))
                {
                    TilePosition = nextTile;
                    targetPosition = new Vector2(TilePosition.X * TileMap.TileSize,
                                                 TilePosition.Y * TileMap.TileSize);
                    isMoving = true;
                    currentFrame = 0;
                    animationTimer = 0f;
                }
                else if (!moved)
                {
                    state = state switch
                    {
                        PlayerState.WalkFront => PlayerState.IdleFront,
                        PlayerState.WalkBack => PlayerState.IdleBack,
                        PlayerState.WalkLeft => PlayerState.IdleLeft,
                        PlayerState.WalkRight => PlayerState.IdleRight,
                        _ => state
                    };
                }
            }

            if (isMoving)
            {
                Vector2 direction = targetPosition - Position;
                float distance = direction.Length();
                float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (distance <= moveSpeed * dt)
                {
                    Position = targetPosition;
                    isMoving = false;

                    state = state switch
                    {
                        PlayerState.WalkFront => PlayerState.IdleFront,
                        PlayerState.WalkBack => PlayerState.IdleBack,
                        PlayerState.WalkLeft => PlayerState.IdleLeft,
                        PlayerState.WalkRight => PlayerState.IdleRight,
                        _ => state
                    };

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
            string newAnimation = state switch
            {
                PlayerState.WalkFront => "walk_front",
                PlayerState.WalkBack => "walk_back",
                PlayerState.WalkLeft => "walk_left",
                PlayerState.WalkRight => "walk_right",
                PlayerState.IdleFront => "idle_front",
                PlayerState.IdleBack => "idle_back",
                PlayerState.IdleLeft => "idle_left",
                PlayerState.IdleRight => "idle_right",
                _ => "idle_front"
            };

            // Se l'animazione è cambiata, reset frame
            if (newAnimation != currentAnimation)
            {
                currentAnimation = newAnimation;
                currentFrame = 0;
                animationTimer = 0f;
            }
            // Aggiorna frame
            animationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (animations.ContainsKey(currentAnimation) && animations[currentAnimation].Count > 1)
            {
                if (animationTimer >= animationSpeed)
                {
                    animationTimer = 0f;
                    currentFrame++;
                    if (currentFrame >= animations[currentAnimation].Count)
                        currentFrame = 0;
                }
            }
            else
            {
                currentFrame = 0;
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (!animations.ContainsKey(currentAnimation)) return;
            Rectangle source = animations[currentAnimation][currentFrame];
            spriteBatch.Draw(texture, Position, source, Color.White);
        }
    }
}