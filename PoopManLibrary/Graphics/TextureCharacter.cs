using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace PoopManLibrary.Graphics;

public class TextureCharacter
{
    private Dictionary<string, TextureRegion> _regions;

    public Texture2D Texture { get; set; }

    public TextureCharacter()
    {
        _regions = new Dictionary<string, TextureRegion>();
    }

    /// <summary>
    /// Carica il personaggio da un file XML di definizione
    /// </summary>
    public static TextureCharacter LoadFromXml(ContentManager content, string xmlPath)
    {
        var character = new TextureCharacter();

        XDocument doc = XDocument.Load(xmlPath);
        var root = doc.Root;

        // Carica la texture
        string textureName = root.Element("Texture")?.Value;
        if (!string.IsNullOrEmpty(textureName))
        {
            character.Texture = content.Load<Texture2D>(textureName);
        }

        // Carica le regioni definite nell'XML
        var regionsElement = root.Element("Regions");
        if (regionsElement != null)
        {
            foreach (var regionElement in regionsElement.Elements("Region"))
            {
                string name = regionElement.Attribute("Name")?.Value ?? "unnamed";
                int x = int.Parse(regionElement.Attribute("X")?.Value ?? "0");
                int y = int.Parse(regionElement.Attribute("Y")?.Value ?? "0");
                int width = int.Parse(regionElement.Attribute("Width")?.Value ?? "16");
                int height = int.Parse(regionElement.Attribute("Height")?.Value ?? "16");

                character.AddRegion(new TextureRegion(name, x, y, width, height));
            }
        }

        return character;
    }

    /// <summary>
    /// Aggiunge una regione al dizionario
    /// </summary>
    public void AddRegion(TextureRegion region)
    {
        _regions[region.Name] = region;
    }

    /// <summary>
    /// Ottiene una regione per nome
    /// </summary>
    public TextureRegion GetRegion(string name)
    {
        return _regions.TryGetValue(name, out var region) ? region : null;
    }

    /// <summary>
    /// Disegna una specifica regione del personaggio
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, string regionName, Vector2 position, Color color, float scale = 1f)
    {
        var region = GetRegion(regionName);
        if (region != null && Texture != null)
        {
            spriteBatch.Draw(
                Texture, 
                position, 
                region.Bounds, 
                color, 
                0f, 
                Vector2.Zero, 
                scale, 
                SpriteEffects.None, 
                0f);
        }
    }
}