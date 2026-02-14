using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PoopManLibrary;
using PoopManLibrary.World;
using PoopManLibrary.Entities;
using PoopManLibrary.Graphics;
using System.IO;

namespace PoopMan
{
    public class Game1 : Core
    {
        private SpriteBatch _spriteBatch;

        private TileMap map;
        private Player player;
        private Texture2D pixel;
        private TextureCharacter minerCharacter;

        private float animationTimer;
        private int currentFrame;

        public Game1() : base("PoopMan", 1280, 720, false)
        {
        }

        protected override void Initialize()
        {
            base.Initialize();
            animationTimer = 0f;
            currentFrame = 1;
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });

            map = new TileMap(pixel);
            player = new Player(pixel, new Point(1, 1));

            string xmlPath = Path.Combine(Content.RootDirectory, "image", "character", "character-definition.xml");
            minerCharacter = TextureCharacter.LoadFromXml(Content, xmlPath);
        }

        protected override void Update(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

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

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            // Usa PointClamp per evitare bleeding tra i frame
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            map.Draw(_spriteBatch);

            // Disegna il personaggio
            string frameName = $"idle_front_{currentFrame}";
            minerCharacter.Draw(_spriteBatch, frameName, new Vector2(100, 100), Color.White, 2f);

            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}