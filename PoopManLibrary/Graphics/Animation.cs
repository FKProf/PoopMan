using System;
using System.Collections.Generic;

namespace PoopManLibrary.Graphics;

public class Animation
{
    public List<TextureRegion> Frames { get; set; }

    public TimeSpan Delay { get; set; }

    public Animation()
    {
        Frames = new List<TextureRegion>();
        Delay = TimeSpan.FromMicroseconds(100);
    }

    public Animation(List<TextureRegion> frames, TimeSpan delay)
    {
        Frames = frames;
        Delay = delay;
    }
}