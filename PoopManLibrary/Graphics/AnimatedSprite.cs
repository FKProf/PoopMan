using Microsoft.Xna.Framework;
using System;

namespace PoopManLibrary.Graphics;

public class AnimatedSprite : Sprite
{
    private Animation _animation;
    private int _currentFrame;
    private TimeSpan _elapsed;

    public AnimatedSprite()
    {
    }

    public AnimatedSprite(Animation animation)
    {
        Animation = animation;
    }

    public Animation Animation
    {
        get => _animation;
        set
        {
            _animation = value;
            Region = _animation.Frames[0];
        }
    }

    public void Update(GameTime gameTime)
    {
        _elapsed += gameTime.ElapsedGameTime;

        if (_elapsed >= _animation.Delay)
        {
            _elapsed -= _animation.Delay;
            _currentFrame++;

            if (_currentFrame >= _animation.Frames.Count) _currentFrame = 0;

            Region = _animation.Frames[_currentFrame];
        }
    }
}