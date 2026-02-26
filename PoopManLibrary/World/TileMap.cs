using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace PoopManLibrary.World
{
    public class TileMap
    {
        private TileAtlas atlas;
        public const int TileSize = 32;//Dimensione di ogni tile in pixel
        private TileType[,] map;//Matrice che rappresenta la mappa dei tile
        private string[,] tileVariant; // quale variante usare per ogni tile

        public TileMap(TileAtlas atlas, int rows = 15, int cols = 13)
        {
            this.atlas = atlas;

            map = new TileType[rows, cols];
            tileVariant = new string[rows, cols];

            Random rand = new Random();

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    // DECIDI IL TIPO DI TILE
                    if (y == 0 || y == rows - 1 || x == 0 || x == cols - 1)
                        map[y, x] = TileType.Wall;
                    else if (y % 2 == 0 && x % 2 == 0)
                        map[y, x] = TileType.Wall;
                    else
                        map[y, x] = (rand.Next(2) == 0) ? TileType.Breakable : TileType.Empty;

                    // ASSEGNA LA VARIANTE CORRISPONDENTE (questo è il pezzo di codice)
                    tileVariant[y, x] = map[y, x] switch
                    {
                        TileType.Wall => "wall" + rand.Next(3),
                        TileType.Breakable => "breakable" + rand.Next(3),
                        _ => "empty" + rand.Next(3)   // qui va il nome dei tile camminabili presenti nell'XML
                    };
                }
            }

            // Assicurati che l'angolo in alto a sinistra sia camminabile
            map[1, 1] = TileType.Empty;
            tileVariant[1, 1] = "empty0"; // variante iniziale
        }
        public void Draw(SpriteBatch spriteBatch)
        {
            for (int y = 0; y < map.GetLength(0); y++)
            {
                for (int x = 0; x < map.GetLength(1); x++)
                {
                    string tileName = tileVariant[y, x]; // prendi la variante scelta
                    Rectangle sourceRect = atlas.GetTile(tileName); // rettangolo nel PNG
                    Rectangle destRect = new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize);

                    spriteBatch.Draw(atlas.Texture, destRect, sourceRect, Color.White);
                }
            }
        }

        //controllo se un tile è camminabile (Empty) o no (Wall, Breakable)
        public bool IsWalkable(Point tile)
        {
            // Controlla se il tile è dentro la mappa
            if (tile.X < 0 || tile.X >= map.GetLength(1) || tile.Y < 0 || tile.Y >= map.GetLength(0))
                return false;

            // Permetti di camminare solo su tile Empty
            return map[tile.Y, tile.X] == TileType.Empty;
        }
    }
}
