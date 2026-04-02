using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace PoopManLibrary.Graphics;

public class TextureCharacter
{
    private Dictionary<string, TextureRegion> _regions;

    private Dictionary<string, Animation> _animations;

    public Texture2D Texture { get; set; }

    public TextureCharacter()
    {
        _regions = new Dictionary<string, TextureRegion>();
        _animations = new Dictionary<string, Animation>();
    }

    /// <summary>
    /// Crea un personaggio a partire da una texture, senza definizioni di regioni o animazioni
    /// </summary>
    /// <param name="texture"></param>
    public TextureCharacter(Texture2D texture)
    {
        Texture = texture;
        _regions = new Dictionary<string, TextureRegion>();
        _animations = new Dictionary<string, Animation>();
    }

    /// <summary>
    /// Crea una nuova regione per il personaggio,
    /// definendo un'area specifica della texture da utilizzare come parte del personaggio
    /// </summary>
    /// <param name="name"></param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="width"></param>
    /// <param name="height"></param>
    public void AddRegion(string name, int x , int y, int width, int height)
    {
        TextureRegion region = new TextureRegion(Texture, x, y, width, height);
        _regions.Add(name, region);
    }

    /// <summary>
    /// Ottiene una regione specifica del personaggio, identificata dal nome.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public TextureRegion GetRegion(string name)
    {
        return _regions[name];
    }

    public bool RemoveRegion(string name)
    {
        return _regions.Remove(name);
    }

    public void Clear()
    {
        _regions.Clear();
    }

    public static TextureCharacter FromFile(ContentManager content, string fileName)
    {
        TextureCharacter character = new TextureCharacter();

        string filePath = Path.Combine(content.RootDirectory, fileName);

        try
        {
            using (Stream stream = TitleContainer.OpenStream(filePath))
            {
                using (XmlReader reader = XmlReader.Create(stream))
                {
                    XDocument doc = XDocument.Load(reader);
                    XElement root = doc.Root;

                    var texturePath = root.Element("Texture").Value;
                    character.Texture = content.Load<Texture2D>(texturePath);

                    var regions = root.Element("Regions")?.Elements("Region");

                    if (regions != null)
                    {
                        foreach (var region in regions)
                        {
                            var name = region.Attribute("name")?.Value;
                            var x = int.Parse(region.Attribute("x")?.Value ?? "0");
                            var y = int.Parse(region.Attribute("y")?.Value ?? "0");
                            var width = int.Parse(region.Attribute("width")?.Value ?? "0");
                            var height = int.Parse(region.Attribute("height")?.Value ?? "0");

                            if (!string.IsNullOrEmpty(name)) character.AddRegion(name, x, y, width, height);
                        }
                    }

                    var animationElements = root.Element("Animations").Elements("Animation");

                    if (animationElements != null)
                    {
                        foreach (var animationElement in animationElements)
                        {
                            var name = animationElement.Attribute("name")?.Value;
                            var delayInMilliseconds = float.Parse(animationElement.Attribute("delay")?.Value ?? "0");
                            var delay = TimeSpan.FromMilliseconds(delayInMilliseconds);

                            var frames = new List<TextureRegion>();

                            var frameElements = animationElement.Elements("Frame");

                            if (frameElements != null)
                            {
                                foreach (var frameElement in frameElements)
                                {
                                    var regionName = frameElement.Attribute("region")?.Value;
                                    var region = character.GetRegion(regionName);
                                    frames.Add(region);
                                }
                            }

                            var animation = new Animation(frames, delay);
                            character.AddAnimation(name, animation);
                        }
                    }

                    return character;
                }
            }
        } catch (Exception ex)
        {
            Console.WriteLine($"Error loading TextureCharacter from file: {ex.Message}");
            return null;
        }
    }

    public Sprite CreaSprite(string regionName)
    {
        TextureRegion region = GetRegion(regionName);
        return new Sprite(region);
    }

    public void AddAnimation(string animationName, Animation animation)
    {
        _animations.Add(animationName, animation);
    }

    public Animation GetAnimation(string animationName)
    {
        return _animations[animationName];
    }

    public bool RemoveAnimation(string animationName)
    {
        return _animations.Remove(animationName);
    }

    public AnimatedSprite CreaAnimatedSprite(string animationName)
    {
        Animation animation = GetAnimation(animationName);
        return new AnimatedSprite(animation);
    }
}