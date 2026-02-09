using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PoopManLibrary.World; // per TileMap e TileType

namespace PoopManLibrary.Entities
{
    public class Player
    {
        public Point TilePosition; // Posizione del giocatore sulla griglia
        public Texture2D Texture; // Texture del giocatore
        public Vector2 Position; // Posizione del giocatore in pixel
        public int Speed = 2; // Velocità di movimento del giocatore 
        
        //costruttore
        public Player(Texture2D texture,Point startTile)
        {
            texture = Texture;
            TilePosition = startTile;
            Position = new Vector2(TilePosition.X * TileMap.TileSize, TilePosition.Y * TileMap.TileSize);
            //setto la pos iniziale del player dimensionandolo alla tile
        }
        public void Update(TileMap map,KeyboardState keyboardstate)
        {
            Point targetTile = TilePosition; // tile di destinazione inizialmente uguale alla tile attuale

            // Controllo input per il movimento
            if (keyboardstate.IsKeyDown(Keys.W)) targetTile.Y -= 1; // su
            else if (keyboardstate.IsKeyDown(Keys.S)) targetTile.Y += 1; // giù
            else if (keyboardstate.IsKeyDown(Keys.A)) targetTile.X -= 1; // sinistra
            else if (keyboardstate.IsKeyDown(Keys.D)) targetTile.X += 1; // destra

            if(map.IsWalkable(targetTile)) // se la tile di destinazione è camminabile
            {
                TilePosition = targetTile; // aggiorno la posizione del giocatore
                Position = new Vector2(TilePosition.X * TileMap.TileSize, TilePosition.Y * TileMap.TileSize); // aggiorno la posizione in pixel
            }
        }
    }
}
