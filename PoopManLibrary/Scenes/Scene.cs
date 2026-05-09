using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace PoopManLibrary.Scenes;

public class Scene : IDisposable 
{
    protected ContentManager Content { get; private set; }

    public bool IsDisposed { get; private set; }

    public Scene()
    {
        Content = new ContentManager(Core.ContentManager.ServiceProvider);

        Content.RootDirectory = Core.ContentManager.RootDirectory;
    }

    ~Scene() => Dispose(false);

    public virtual void Initialize()
    {
        LoadContent();
    }

    public virtual void LoadContent() { }

    public virtual void UnloadContent() {}

    public virtual void Update(GameTime gameTime) {}

    public virtual void Draw(GameTime gameTime) { }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed)
            return;

        if (disposing)
        {
            UnloadContent();
            Content.Dispose();
        }
        IsDisposed = true;
    }
}
