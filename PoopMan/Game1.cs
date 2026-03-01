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

        public Game1() : base("PoopMan", 1248, 735, false)
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
            Texture2D tilesetTexture = Content.Load<Texture2D>("image/Tile/terrain"); // senza .png

            // Crea l'atlas
            atlas = new TileAtlas(tilesetTexture);

            // Leggi l'XML del Tileset e registra i tile
            string xmlPath = Path.Combine(Content.RootDirectory, "image","Tile","TilesetAtlas.xml");
            XDocument doc = XDocument.Load(xmlPath);
            foreach (var sprite in doc.Descendants("Region"))
            {
                string name = sprite.Attribute("Name").Value;
                int x = (int)sprite.Attribute("X");
                int y = (int)sprite.Attribute("Y");
                int w = (int)sprite.Attribute("Width");
                int h = (int)sprite.Attribute("Height");

                atlas.AddTile(name, x, y, w, h);
            }

            // Crea la mappa per il livello 1
            map = new TileMap(atlas, 23, 39, level: 1);

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