using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace PoopManLibrary.World
{
    public class TileMap
    {
        public const int TileSize = 32;//Dimensione di ogni tile in pixel
        private TileType[,] map;//Matrice che rappresenta la mappa dei tile

        private Texture2D pixel; // texture 1x1 per debug

        public TileMap(Texture2D pixel, int rows = 15, int cols = 13)//pixel per debug, piu avanti da cambiare con texture
        {
            this.pixel = pixel;

            map = new TileType[rows, cols];

            // Generazione automatica
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)//tengo y e scorro x
                {
                    // Perimetro = Wall
                    if (y == 0 || y == rows - 1 || x == 0 || x == cols - 1)
                        map[y, x] = TileType.Wall;
                    // Muri interni alternati (griglia)
                    else if (y % 2 == 0 && x % 2 == 0)
                        map[y, x] = TileType.Wall;
                    // Altri tile = Breakable o Empty casuale
                    else
                        map[y, x] = (Random.Shared.Next(2) == 0) ? TileType.Breakable : TileType.Empty;
                }
            }
        }
        public void Draw(SpriteBatch spriteBatch)
        {
            // Disegna la mappa dei tile usando il pixel colorato per debug
            for (int y = 0; y < map.GetLength(0); y++)
            {
                for (int x = 0; x < map.GetLength(1); x++)
                {
                    Color color = map[y, x] switch
                    {
                        TileType.Wall => Color.Gray,
                        TileType.Breakable => Color.SaddleBrown,
                        _ => Color.Black
                    };

                    spriteBatch.Draw(
                        pixel,
                        new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize),
                        color
                    );
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
