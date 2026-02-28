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

        public Game1() : base("PoopMan", 1247, 735, false)
        {
        }

        protected override void Initialize()
        {
            base.Initialize();
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
            map = new TileMap(atlas, 23, 39);

            // Player
            string playerXml = Path.Combine(Content.RootDirectory, "image", "character", "miner_animation.xml");
            player = new Player(new Point(1,1), playerXml, Content);
        }

        protected override void Update(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            player.Update(map, Keyboard.GetState(), gameTime);

            
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            // Usa PointClamp per evitare bleeding tra i frame
            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            map.Draw(_spriteBatch);
            // Disegna il personaggio
            player.Draw(_spriteBatch);
            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}