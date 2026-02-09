using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PoopManLibrary;
using PoopManLibrary.World;
using PoopManLibrary.Entities;
<<<<<<< HEAD
using PoopManLibrary.Graphics;
using System.IO;
=======
>>>>>>> e47a7ba5ae4e3382f26dbdc38ed4db8995c3bdfe

namespace PoopMan
{
    public class Game1 : Core
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        private TileMap map;
        private Player player;
        private Texture2D pixel;
<<<<<<< HEAD
        private TextureCharacter minerCharacter;

        private float animationTimer;
        private int currentFrame;

        public Game1() : base("PoopMan", 1280, 720, false)
=======

        public Game1() : base("PoopMan", 416, 480, false)
>>>>>>> e47a7ba5ae4e3382f26dbdc38ed4db8995c3bdfe
        {

        }

        protected override void Initialize()
        {
            base.Initialize();
<<<<<<< HEAD
            animationTimer = 0f;
            currentFrame = 1;
=======
            // TODO: Add your initialization logic here
>>>>>>> e47a7ba5ae4e3382f26dbdc38ed4db8995c3bdfe
        }

        protected override void LoadContent()
        {
<<<<<<< HEAD
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            map = new TileMap(pixel);
            player = new Player(pixel, new Point(1, 1));

            string xmlPath = Path.Combine(Content.RootDirectory, "image", "character", "character-definition.xml");
            minerCharacter = TextureCharacter.LoadFromXml(Content, xmlPath);
=======
            // TODO: use this.Content to load your game content here
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            // Pixel per test
            pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            //creo la mappa
            map = new TileMap(pixel);

            //creo il player
            player = new Player(pixel, new Point(1, 1));
>>>>>>> e47a7ba5ae4e3382f26dbdc38ed4db8995c3bdfe
        }

        protected override void Update(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

<<<<<<< HEAD
            player.Update(map, Keyboard.GetState());

            // Anima il personaggio (cambia frame ogni 0.15 secondi)
            animationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (animationTimer >= 0.15f)
            {
                animationTimer = 0f;
                currentFrame++;
                if (currentFrame > 4)
                    currentFrame = 1;
            }
=======
            // TODO: Add your update logic here
            player.Update(map, Keyboard.GetState());
>>>>>>> e47a7ba5ae4e3382f26dbdc38ed4db8995c3bdfe

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

<<<<<<< HEAD
            // Usa PointClamp per evitare bleeding tra i frame
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            //map.Draw(_spriteBatch);   //HO TOLTO IL DISEGNO DELLA MAPPA PER FARE TEST SUL PERSONAGGIO

            // Disegna il personaggio con scala ridotta (2x o 3x invece di 6x)
            string frameName = $"idle_front_{currentFrame}";
            minerCharacter.Draw(_spriteBatch, frameName, new Vector2(100, 100), Color.White, 2f);

=======
            // TODO: Add your drawing code here
            _spriteBatch.Begin();

            map.Draw(_spriteBatch);
            _spriteBatch.Draw(player.Texture, new Rectangle((int)player.Position.X, (int)player.Position.Y, TileMap.TileSize, TileMap.TileSize), Color.White);
            
>>>>>>> e47a7ba5ae4e3382f26dbdc38ed4db8995c3bdfe
            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}