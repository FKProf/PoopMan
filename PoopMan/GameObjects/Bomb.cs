using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PoopManLibrary.World;
using System;
using System.Collections.Generic;

namespace PoopMan.GameObjects
{
    internal class Bomb
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
}
