using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace PoopManLibrary.World
{
    public class TileMap
    {
        private TileAtlas atlas;
        private TileType[,] map;
        private string[,] tileVariant;

        public const int TileSize = 32;
        private int currentLevel;

        public TileMap(TileAtlas atlas, int rows, int cols, int level)
        {
            this.atlas = atlas;
            this.currentLevel = level;

            map = new TileType[rows, cols];
            tileVariant = new string[rows, cols];

            Random rand = new Random();

            // Generazione base
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    GenerateTile(y, x, rows, cols, rand);
                }
            }

            // Zona spawn sicura: rimuovi ostacoli distruttibili vicino allo spawn
            ClearSpawnArea(rows, cols, rand);

            // Post-processing: aggiungi zone d'acqua e sabbia vicina
            // (DOPO la zona spawn per non sovrascriverla)
            AddWaterZones(rows, cols, rand);
            AddSandNearWater(rows, cols, rand);
        }

        /// <summary>
        /// Crea una zona di spawn sicura con meno ostacoli
        /// </summary>
        private void ClearSpawnArea(int rows, int cols, Random rand)
        {
            // Zona spawn 3x3: rimuovi log e converti a glass
            for (int y = 1; y < 4 && y < rows; y++)
            {
                for (int x = 1; x < 4 && x < cols; x++)
                {
                    // Se è un ostacolo distruttibile (log), convertilo a floor
                    if (map[y, x] == TileType.Breakable)
                    {
                        map[y, x] = TileType.Empty;
                        tileVariant[y, x] = "glass" + rand.Next(3);
                    }
                    // Se è già empty, lascialo glass
                    else if (map[y, x] == TileType.Empty)
                    {
                        tileVariant[y, x] = "glass" + rand.Next(3);
                    }
                }
            }
        }

        private void GenerateTile(int y, int x, int rows, int cols, Random rand)
        {
            // Bordo esterno
            if (y == 0 || y == rows - 1 || x == 0 || x == cols - 1)
            {
                map[y, x] = TileType.Wall;
                tileVariant[y, x] = "wall0";
                return;
            }

            // Griglia fissa di muri
            if (y % 2 == 0 && x % 2 == 0)
            {
                map[y, x] = TileType.Wall;
                tileVariant[y, x] = "wall0";
                return;
            }

            // Tile casuale: 60% Breakable (log), 40% Empty (glass/sand)
            if (rand.Next(100) < 60)
            {
                map[y, x] = TileType.Breakable;
                tileVariant[y, x] = "log" + rand.Next(2);
            }
            else
            {
                map[y, x] = TileType.Empty;
                tileVariant[y, x] = "glass" + rand.Next(3);
            }
        }

        /// <summary>
        /// Crea zone di acqua sparse nella mappa con forme organiche casuali
        /// </summary>
        private void AddWaterZones(int rows, int cols, Random rand)
        {
            // Genera 2-4 zone di acqua con forme casuali
            int waterZones = rand.Next(2, 5);
            for (int i = 0; i < waterZones; i++)
            {
                // Genera acqua lontano dalla zona spawn
                int startY = rand.Next(4, rows - 3);
                int startX = rand.Next(4, cols - 3);

                // Crea una zona d'acqua con forma casuale usando random walk
                int waterSize = rand.Next(8, 20); // Dimensione della zona d'acqua
                GenerateWaterBlob(startY, startX, waterSize, rows, cols, rand);
            }
        }

        /// <summary>
        /// Genera un blob d'acqua con forma casuale e organica
        /// </summary>
        private void GenerateWaterBlob(int startY, int startX, int size, int rows, int cols, Random rand)
        {
            HashSet<(int, int)> waterTiles = new HashSet<(int, int)>();
            Queue<(int, int)> toProcess = new Queue<(int, int)>();

            toProcess.Enqueue((startY, startX));
            waterTiles.Add((startY, startX));

            // Crescita casuale dell'acqua
            while (toProcess.Count > 0 && waterTiles.Count < size)
            {
                var (y, x) = toProcess.Dequeue();

                // Aggiungi tile vicini in modo casuale
                int[] dy = { -1, 1, 0, 0 };
                int[] dx = { 0, 0, -1, 1 };

                // Mescola gli indici per casualità
                for (int i = 0; i < 4; i++)
                {
                    int swapIdx = rand.Next(4);
                    (dy[i], dy[swapIdx]) = (dy[swapIdx], dy[i]);
                    (dx[i], dx[swapIdx]) = (dx[swapIdx], dx[i]);
                }

                for (int i = 0; i < 4; i++)
                {
                    int ny = y + dy[i];
                    int nx = x + dx[i];

                    // Controlla che sia dentro la mappa e non sia già water
                    if (ny > 0 && ny < rows - 1 && nx > 0 && nx < cols - 1 && !waterTiles.Contains((ny, nx)))
                    {
                        // 65% di probabilità di espandere l'acqua
                        if (rand.Next(100) < 65)
                        {
                            waterTiles.Add((ny, nx));
                            toProcess.Enqueue((ny, nx));
                        }
                    }
                }
            }

            // Applica i tile d'acqua alla mappa
            foreach (var (y, x) in waterTiles)
            {
                // Non sovrascrivere i muri della griglia fissa
                if (!(y % 2 == 0 && x % 2 == 0))
                {
                    map[y, x] = TileType.Wall;
                    tileVariant[y, x] = "water" + rand.Next(2);
                }
            }
        }

        /// <summary>
        /// Aggiungi sabbia vicino alle zone d'acqua
        /// </summary>
        private void AddSandNearWater(int rows, int cols, Random rand)
        {
            for (int y = 1; y < rows - 1; y++)
            {
                for (int x = 1; x < cols - 1; x++)
                {
                    // Se questo tile è empty e accanto c'è water
                    if (map[y, x] == TileType.Empty && HasAdjacentWater(y, x, rows, cols))
                    {
                        // 50% di probabilità di convertire a sabbia
                        if (rand.Next(2) == 0)
                        {
                            tileVariant[y, x] = "sand0";
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Controlla se ci sono tile d'acqua adiacenti
        /// </summary>
        private bool HasAdjacentWater(int y, int x, int rows, int cols)
        {
            int[] dy = { -1, 1, 0, 0 };
            int[] dx = { 0, 0, -1, 1 };

            for (int i = 0; i < 4; i++)
            {
                int ny = y + dy[i];
                int nx = x + dx[i];

                if (ny > 0 && ny < rows - 1 && nx > 0 && nx < cols - 1)
                {
                    if (tileVariant[ny, nx].StartsWith("water"))
                        return true;
                }
            }

            return false;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            for (int y = 0; y < map.GetLength(0); y++)
            {
                for (int x = 0; x < map.GetLength(1); x++)
                {
                    string tileName = tileVariant[y, x];
                    Rectangle sourceRect = atlas.GetTile(tileName);
                    Rectangle destRect = new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize);

                    spriteBatch.Draw(atlas.Texture, destRect, sourceRect, Color.White);
                }
            }
        }

        public bool IsWalkable(Point tile)
        {
            if (tile.X < 0 || tile.X >= map.GetLength(1) || tile.Y < 0 || tile.Y >= map.GetLength(0))
                return false;

            return map[tile.Y, tile.X] == TileType.Empty;
        }
    }
}