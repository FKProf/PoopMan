using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PoopManLibrary;
using PoopManLibrary.World;
using PoopManLibrary.Entities;

namespace PoopMan
{
    public class Game1 : Core
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        private TileMap map;
        private Player player;
        private Texture2D pixel;

        public Game1() : base("PoopMan", 416, 480, false)
        {

        }

        protected override void Initialize()
        {
            base.Initialize();
            // TODO: Add your initialization logic here
        }

        protected override void LoadContent()
        {
            // TODO: use this.Content to load your game content here
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            // Pixel per test
            pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            //creo la mappa
            map = new TileMap(pixel);

            //creo il player
            player = new Player(pixel, new Point(1, 1));
        }

        protected override void Update(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            // TODO: Add your update logic here
            player.Update(map, Keyboard.GetState());

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            // TODO: Add your drawing code here
            _spriteBatch.Begin();

            map.Draw(_spriteBatch);
            _spriteBatch.Draw(player.Texture, new Rectangle((int)player.Position.X, (int)player.Position.Y, TileMap.TileSize, TileMap.TileSize), Color.White);
            
            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
