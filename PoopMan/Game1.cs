using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PoopManLibrary;
using PoopManLibrary.Entities;
using PoopManLibrary.Graphics;
using PoopManLibrary.World;
using System.IO;
using System.Xml.Linq;

namespace PoopMan
{
    public class Game1 : Core
    {
        private SpriteBatch _spriteBatch;

        private TileAtlas atlas;
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

            // Carica il Tileset PNG
            Texture2D tilesetTexture = Content.Load<Texture2D>("image/Tile/TilesetImage"); // senza .png

            // Crea l’atlas
            atlas = new TileAtlas(tilesetTexture);

            // Leggi l’XML del Tileset e registra i tile
            string xmlPath = Path.Combine(Content.RootDirectory, "image","Tile","TilesetAtlas.xml");
            XDocument doc = XDocument.Load(xmlPath);
            foreach (var sprite in doc.Descendants("sprite"))
            {
                string name = sprite.Attribute("n").Value.Replace(".png", "");
                int x = (int)sprite.Attribute("x");
                int y = (int)sprite.Attribute("y");
                int w = (int)sprite.Attribute("w");
                int h = (int)sprite.Attribute("h");

                atlas.AddTile(name, x, y, w, h);
            }

            // Crea la mappa usando l’atlas con varianti casuali
            map = new TileMap(atlas, 15, 13);

            // Player
            player = new Player(pixel, new Point(1, 1));

            xmlPath = Path.Combine(Content.RootDirectory, "image", "character", "character-definition.xml");
            minerCharacter = TextureCharacter.LoadFromXml(Content, xmlPath);
        }

        protected override void Update(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            player.Update(map, Keyboard.GetState(), gameTime);

            // Anima il personaggio (cambia frame ogni 0.15 secondi)
            animationTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;// calcola il tempo trascorso dall'ultimo aggiornamento
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
            minerCharacter.Draw( _spriteBatch, frameName, player.Position, Color.White, 1f);
            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}