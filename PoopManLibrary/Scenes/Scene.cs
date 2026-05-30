using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using System;

namespace PoopManLibrary.Scenes;

public class Scene : IDisposable
{
    public Scene()
    {
        Content = new ContentManager(Core.ContentManager.ServiceProvider);

        Content.RootDirectory = Core.ContentManager.RootDirectory;
    }

    protected ContentManager Content { get; }

    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~Scene()
    {
        Dispose(false);
    }

    public virtual void Initialize()
    {
        LoadContent();
    }

    public virtual void LoadContent()
    {
    }

    public virtual void UnloadContent()
    {
    }

    public virtual void Update(GameTime gameTime)
    {
    }

    public virtual void Draw(GameTime gameTime)
    {
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